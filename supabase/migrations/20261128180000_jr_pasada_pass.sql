-- Pasada Pass (same rules as C# Logic/PasadaPass.cs): a monthly season pass (PH calendar month).
-- Pass XP = sum over the month's accepted runs of least(60, floor(distance / 100) + deliveries * 2);
-- tier = least(30, xp / 100). Each tier has a free and a premium reward; premium is unlocked for the
-- month with 300 gems. Premium items (paint_pasada, ws_pasada) are never sold.

create table if not exists public.jr_pass (
  user_id uuid not null references public.jr_profiles (user_id) on delete cascade,
  season int not null,                 -- yyyymm (PH)
  premium boolean not null default false,
  claimed_free bigint not null default 0,
  claimed_premium bigint not null default 0,
  primary key (user_id, season));
alter table public.jr_pass enable row level security;
revoke all on public.jr_pass from anon, authenticated;

create or replace function jr_private.pass_season() returns int language sql stable as $$
  select (extract(year from jr_private.ph_date()) * 100 + extract(month from jr_private.ph_date()))::int $$;
create or replace function jr_private.pass_xp(uid uuid) returns int language sql stable security definer set search_path = public, jr_private as $$
  select coalesce(sum(least(60, floor(greatest(0, coalesce((r.report->>'distance')::real, 0)) / 100)::int + greatest(0, coalesce((r.report->>'deliveries')::int, 0)) * 2)), 0)::int
  from public.jr_runs r
  where r.user_id = uid and r.accepted
    and r.created_at >= (date_trunc('month', jr_private.ph_date()::timestamp) at time zone 'Asia/Manila')
    and r.created_at < ((date_trunc('month', jr_private.ph_date()::timestamp) + interval '1 month') at time zone 'Asia/Manila') $$;
-- reward of a tier: kind ('coins' | 'gems' | 'cosmetic'), amount, item
create or replace function jr_private.pass_reward(tier int, premium boolean, out kind text, out amount int, out item text) language sql immutable as $$
  select case when not premium then (case when tier % 5 = 0 then 'gems' else 'coins' end)
              when tier in (15, 30) then 'cosmetic' when tier % 5 = 0 then 'gems' else 'coins' end,
         case when not premium then (case when tier % 5 = 0 then 5 else 50 + tier * 5 end)
              when tier in (15, 30) then 0 when tier % 5 = 0 then 15 else 150 + tier * 10 end,
         case when premium and tier = 30 then 'paint_pasada' when premium and tier = 15 then 'ws_pasada' end $$;

create or replace function public.jr_pass_status() returns jsonb
language plpgsql stable security definer set search_path = public, jr_private as $$
declare uid uuid := auth.uid(); s int := jr_private.pass_season(); p public.jr_pass; xp int := jr_private.pass_xp(auth.uid());
begin
  select * into p from public.jr_pass where user_id = uid and season = s;
  return jsonb_build_object('season', s, 'xp', xp, 'tier', least(30, xp / 100),
    'days_left', ((date_trunc('month', jr_private.ph_date()::timestamp) + interval '1 month')::date - jr_private.ph_date()),
    'premium', coalesce(p.premium, false), 'claimed_free', coalesce(p.claimed_free, 0), 'claimed_premium', coalesce(p.claimed_premium, 0));
end $$;

create or replace function public.jr_pass_buy_premium() returns jsonb
language plpgsql security definer set search_path = public, jr_private as $$
declare uid uuid := auth.uid(); s int := jr_private.pass_season(); g int;
begin
  perform jr_private.ensure_profile(uid);
  insert into public.jr_pass (user_id, season) values (uid, s) on conflict do nothing;
  if (select premium from public.jr_pass where user_id = uid and season = s) then return jsonb_build_object('ok', false, 'reason', 'owned', 'snapshot', jr_private.snapshot(uid)); end if;
  select gems into g from public.jr_profiles where user_id = uid for update;
  if g < 300 then return jsonb_build_object('ok', false, 'reason', 'gems', 'snapshot', jr_private.snapshot(uid)); end if;
  update public.jr_profiles set gems = gems - 300, updated_at = now() where user_id = uid;
  update public.jr_pass set premium = true where user_id = uid and season = s;
  insert into public.jr_transactions (user_id, kind, amount, ref, currency) values (uid, 'pass', -300, 'premium' || s, 'gems');
  return jsonb_build_object('ok', true, 'snapshot', jr_private.snapshot(uid));
end $$;

create or replace function public.jr_pass_claim(p_tier int, p_premium boolean) returns jsonb
language plpgsql security definer set search_path = public, jr_private as $$
declare uid uuid := auth.uid(); s int := jr_private.pass_season(); p public.jr_pass; rw record; bit bigint;
begin
  if p_tier < 1 or p_tier > 30 then return jsonb_build_object('ok', false, 'reason', 'unknown_item', 'snapshot', jr_private.snapshot(uid)); end if;
  insert into public.jr_pass (user_id, season) values (uid, s) on conflict do nothing;
  select * into p from public.jr_pass where user_id = uid and season = s for update;
  if least(30, jr_private.pass_xp(uid) / 100) < p_tier then return jsonb_build_object('ok', false, 'reason', 'not_done', 'snapshot', jr_private.snapshot(uid)); end if;
  if p_premium and not p.premium then return jsonb_build_object('ok', false, 'reason', 'premium', 'snapshot', jr_private.snapshot(uid)); end if;
  bit := (1::bigint << p_tier);
  if (case when p_premium then p.claimed_premium else p.claimed_free end) & bit <> 0 then return jsonb_build_object('ok', false, 'reason', 'claimed', 'snapshot', jr_private.snapshot(uid)); end if;
  if p_premium then update public.jr_pass set claimed_premium = claimed_premium | bit where user_id = uid and season = s;
  else update public.jr_pass set claimed_free = claimed_free | bit where user_id = uid and season = s; end if;
  select * into rw from jr_private.pass_reward(p_tier, p_premium);
  if rw.kind = 'coins' then
    update public.jr_profiles set coins = coins + rw.amount, updated_at = now() where user_id = uid;
    insert into public.jr_transactions (user_id, kind, amount, ref, currency) values (uid, 'pass', rw.amount, 'tier' || p_tier, 'coins');
  elsif rw.kind = 'gems' then
    update public.jr_profiles set gems = gems + rw.amount, updated_at = now() where user_id = uid;
    insert into public.jr_transactions (user_id, kind, amount, ref, currency) values (uid, 'pass', rw.amount, 'tier' || p_tier, 'gems');
  else
    insert into public.jr_cosmetics (user_id, item_id) values (uid, rw.item) on conflict do nothing;
  end if;
  return jsonb_build_object('ok', true, 'reward', case when rw.kind = 'coins' then rw.amount else 0 end, 'snapshot', jr_private.snapshot(uid));
end $$;

revoke all on function public.jr_pass_status(), public.jr_pass_buy_premium(), public.jr_pass_claim(int, boolean) from public, anon;
grant execute on function public.jr_pass_status(), public.jr_pass_buy_premium(), public.jr_pass_claim(int, boolean) to authenticated;
