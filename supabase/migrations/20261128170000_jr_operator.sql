-- Operator mode (same rules as C# Logic/Operator.cs): with the OPERATOR license (career_rank >= 3)
-- hire drivers for jeepneys you own; each earns least(80, 30 + price / 200) coins an hour, up to
-- 8 hours between collections, computed from the server clock. Slots: 1 (rank 3), 2 (rank 4),
-- plus up to 3 bought for 3000 / 8000 / 15000 coins.

create table if not exists public.jr_fleet (
  user_id uuid not null references public.jr_profiles (user_id) on delete cascade,
  slot int not null,
  jeepney text not null,
  since timestamptz not null default now(),
  primary key (user_id, slot));
alter table public.jr_profiles add column if not exists fleet_slots_bought int not null default 0;
alter table public.jr_fleet enable row level security;
revoke all on public.jr_fleet from anon, authenticated;

create or replace function jr_private.fleet_slots(uid uuid) returns int language sql stable security definer set search_path = public, jr_private as $$
  select case when p.career_rank < 3 then 0 else (case when p.career_rank >= 4 then 2 else 1 end) + least(3, p.fleet_slots_bought) end
  from public.jr_profiles p where p.user_id = uid $$;
create or replace function jr_private.fleet_rate(jeep text) returns int language sql stable security definer set search_path = public, jr_private as $$
  select least(80, 30 + coalesce((select price from public.jr_catalog_jeepneys where id = jeep), 0) / 200) $$;
create or replace function jr_private.fleet_pending(jeep text, since timestamptz) returns int language sql stable security definer set search_path = public, jr_private as $$
  select floor(jr_private.fleet_rate(jeep) * greatest(0, least(8, extract(epoch from now() - since) / 3600.0)))::int $$;

-- pays out a slot's earnings (or all slots) and restarts its clock; returns the coins paid
create or replace function jr_private.fleet_pay(uid uuid, p_slot int) returns int
language plpgsql security definer set search_path = public, jr_private as $$
declare total int := 0; f record;
begin
  for f in select * from public.jr_fleet where user_id = uid and (p_slot is null or slot = p_slot) for update loop
    total := total + jr_private.fleet_pending(f.jeepney, f.since);
    update public.jr_fleet set since = now() where user_id = uid and slot = f.slot;
  end loop;
  if total > 0 then
    update public.jr_profiles set coins = coins + total, updated_at = now() where user_id = uid;
    insert into public.jr_transactions (user_id, kind, amount, ref, currency) values (uid, 'fleet', total, coalesce('slot' || p_slot, 'all'), 'coins');
  end if;
  return total;
end $$;

create or replace function public.jr_fleet_status() returns jsonb
language plpgsql stable security definer set search_path = public, jr_private as $$
declare uid uuid := auth.uid(); p public.jr_profiles;
begin
  select * into p from public.jr_profiles where user_id = uid;
  return jsonb_build_object('unlocked', coalesce(p.career_rank, 0) >= 3, 'slots', coalesce(jr_private.fleet_slots(uid), 0), 'bought', coalesce(p.fleet_slots_bought, 0),
    'next_slot_price', case when coalesce(p.fleet_slots_bought, 0) < 3 then (array[3000, 8000, 15000])[coalesce(p.fleet_slots_bought, 0) + 1] else 0 end,
    'fleet', coalesce((select jsonb_agg(jsonb_build_object('slot', f.slot, 'jeepney', f.jeepney, 'rate', jr_private.fleet_rate(f.jeepney),
                         'pending', jr_private.fleet_pending(f.jeepney, f.since), 'hours', least(8, extract(epoch from now() - f.since) / 3600.0)) order by f.slot)
                       from public.jr_fleet f where f.user_id = uid), '[]'::jsonb));
end $$;

create or replace function public.jr_fleet_assign(p_slot int, p_jeepney text) returns jsonb
language plpgsql security definer set search_path = public, jr_private as $$
declare uid uuid := auth.uid(); paid int;
begin
  perform jr_private.ensure_profile(uid);
  if p_slot < 0 or p_slot >= coalesce(jr_private.fleet_slots(uid), 0) then return jsonb_build_object('ok', false, 'reason', 'slot', 'snapshot', jr_private.snapshot(uid)); end if;
  if not exists (select 1 from public.jr_inventory where user_id = uid and item_kind = 'jeepney' and item_id = p_jeepney) then
    return jsonb_build_object('ok', false, 'reason', 'not_owned', 'snapshot', jr_private.snapshot(uid)); end if;
  if exists (select 1 from public.jr_fleet where user_id = uid and jeepney = p_jeepney and slot <> p_slot) then
    return jsonb_build_object('ok', false, 'reason', 'busy', 'snapshot', jr_private.snapshot(uid)); end if;
  paid := jr_private.fleet_pay(uid, p_slot);   -- the old jeepney's boundary first
  insert into public.jr_fleet (user_id, slot, jeepney) values (uid, p_slot, p_jeepney)
  on conflict (user_id, slot) do update set jeepney = excluded.jeepney, since = now();
  return jsonb_build_object('ok', true, 'reward', paid, 'snapshot', jr_private.snapshot(uid));
end $$;

create or replace function public.jr_fleet_unassign(p_slot int) returns jsonb
language plpgsql security definer set search_path = public, jr_private as $$
declare uid uuid := auth.uid(); paid int;
begin
  paid := jr_private.fleet_pay(uid, p_slot);
  delete from public.jr_fleet where user_id = uid and slot = p_slot;
  return jsonb_build_object('ok', true, 'reward', paid, 'snapshot', jr_private.snapshot(uid));
end $$;

create or replace function public.jr_fleet_collect() returns jsonb
language plpgsql security definer set search_path = public, jr_private as $$
declare uid uuid := auth.uid(); paid int;
begin
  paid := jr_private.fleet_pay(uid, null);
  return jsonb_build_object('ok', paid > 0, 'reason', case when paid > 0 then null else 'nothing' end, 'reward', paid, 'snapshot', jr_private.snapshot(uid));
end $$;

create or replace function public.jr_fleet_buy_slot() returns jsonb
language plpgsql security definer set search_path = public, jr_private as $$
declare uid uuid := auth.uid(); p public.jr_profiles; price int;
begin
  select * into p from public.jr_profiles where user_id = uid for update;
  if p.career_rank < 3 then return jsonb_build_object('ok', false, 'reason', 'locked', 'snapshot', jr_private.snapshot(uid)); end if;
  if p.fleet_slots_bought >= 3 then return jsonb_build_object('ok', false, 'reason', 'max_level', 'snapshot', jr_private.snapshot(uid)); end if;
  price := (array[3000, 8000, 15000])[p.fleet_slots_bought + 1];
  if p.coins < price then return jsonb_build_object('ok', false, 'reason', 'coins', 'snapshot', jr_private.snapshot(uid)); end if;
  update public.jr_profiles set coins = coins - price, fleet_slots_bought = fleet_slots_bought + 1, updated_at = now() where user_id = uid;
  insert into public.jr_transactions (user_id, kind, amount, ref, currency) values (uid, 'fleet_slot', -price, 'slot' || (p.fleet_slots_bought + 1), 'coins');
  return jsonb_build_object('ok', true, 'snapshot', jr_private.snapshot(uid));
end $$;

revoke all on function public.jr_fleet_status(), public.jr_fleet_assign(int, text), public.jr_fleet_unassign(int), public.jr_fleet_collect(), public.jr_fleet_buy_slot() from public, anon;
grant execute on function public.jr_fleet_status(), public.jr_fleet_assign(int, text), public.jr_fleet_unassign(int), public.jr_fleet_collect(), public.jr_fleet_buy_slot() to authenticated;
revoke all on function jr_private.fleet_pay(uuid, int) from public, anon, authenticated;
