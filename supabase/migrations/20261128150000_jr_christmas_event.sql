-- "Paskong Pasada" Christmas event (same rules as C# Logic/ChristmasEvent.cs): 1 December – 6 January
-- (PH time). Runs bring parol tokens (picked up + carolers, capped per run by distance and per day);
-- tokens unlock rewards in order; Simbang Gabi (16–24 Dec): a Night Shift run of 1.5 km or more
-- lights that night; all nine win the Golden Parol roof sign. Event rewards can't be bought.
-- To try the event on another date: insert into jr_server_settings values ('event_test_date', '2026-12-18')
-- (delete the row to go back to the real date).

create table if not exists public.jr_event_progress (
  user_id uuid not null references public.jr_profiles (user_id) on delete cascade,
  season int not null,
  tokens int not null default 0,
  day_key int not null default 0,
  day_tokens int not null default 0,
  nights int not null default 0,      -- bit n-1 = Simbang Gabi night n
  claimed int not null default 0,     -- bit i = reward i
  golden boolean not null default false,
  updated_at timestamptz not null default now(),
  primary key (user_id, season));
create table if not exists public.jr_event_runs (
  run_id text primary key,
  user_id uuid not null references public.jr_profiles (user_id) on delete cascade,
  season int not null,
  tokens int not null,
  night int not null default 0,
  created_at timestamptz not null default now());
alter table public.jr_event_progress enable row level security;
alter table public.jr_event_runs enable row level security;
revoke all on public.jr_event_progress, public.jr_event_runs from anon, authenticated;

create or replace function jr_private.event_date() returns date language sql stable security definer set search_path = public, jr_private as $$
  select coalesce((select value::date from public.jr_server_settings where key = 'event_test_date'), jr_private.ph_date()) $$;
create or replace function jr_private.event_active(d date) returns boolean language sql immutable as $$
  select extract(month from d) = 12 or (extract(month from d) = 1 and extract(day from d) <= 6) $$;
create or replace function jr_private.event_season(d date) returns int language sql immutable as $$
  select case when extract(month from d) = 12 then extract(year from d)::int else extract(year from d)::int - 1 end $$;
create or replace function jr_private.event_night(d date) returns int language sql immutable as $$
  select case when extract(month from d) = 12 and extract(day from d) between 16 and 24 then extract(day from d)::int - 15 else 0 end $$;
-- rewards (C# ChristmasEvent.Rewards): need, kind, id, amount
create or replace function jr_private.event_reward(i int, out need int, out kind text, out item text, out amount int) language sql immutable as $$
  select (array[50, 120, 200, 300, 450, 600])[i + 1], (array['coins', 'cosmetic', 'cosmetic', 'cosmetic', 'gems', 'jeepney'])[i + 1],
         (array[null, 'horn_jingle', 'orn_parol', 'rear_belen', null, 'pasko'])[i + 1], (array[500, 0, 0, 0, 50, 0])[i + 1] $$;

create or replace function public.jr_event_status() returns jsonb
language plpgsql stable security definer set search_path = public, jr_private as $$
declare uid uuid := auth.uid(); d date := jr_private.event_date(); s int := jr_private.event_season(d); p public.jr_event_progress;
begin
  select * into p from public.jr_event_progress where user_id = uid and season = s;
  return jsonb_build_object('active', jr_private.event_active(d), 'season', s,
    'days_left', case when jr_private.event_active(d) then (make_date(s + 1, 1, 7) - d) else 0 end,
    'night', jr_private.event_night(d),
    'tokens', coalesce(p.tokens, 0), 'nights', coalesce(p.nights, 0), 'claimed', coalesce(p.claimed, 0), 'golden', coalesce(p.golden, false),
    'today_tokens', case when p.day_key = jr_private.day_key(d) then p.day_tokens else 0 end);
end $$;

-- credit a finished (server-accepted) run once: parols + carolers (capped) and the Simbang Gabi night
create or replace function public.jr_event_claim_run(p_run_id text) returns jsonb
language plpgsql security definer set search_path = public, jr_private as $$
declare
  uid uuid := auth.uid(); d date := jr_private.event_date(); s int := jr_private.event_season(d); dk int := jr_private.day_key(d);
  run public.jr_runs; rep jsonb; dist real; earned int; added int; night int := 0; p public.jr_event_progress;
begin
  if not jr_private.event_active(d) then return jsonb_build_object('ok', false, 'reason', 'no_event'); end if;
  select * into run from public.jr_runs where run_id = p_run_id and user_id = uid;
  if not found or not run.accepted or run.created_at < now() - interval '6 hours' then return jsonb_build_object('ok', false, 'reason', 'run'); end if;
  if exists (select 1 from public.jr_event_runs where run_id = p_run_id) then return jsonb_build_object('ok', true, 'duplicate', true); end if;
  rep := run.report; dist := coalesce((rep->>'distance')::real, 0);
  earned := greatest(0, least(coalesce((rep->>'parols')::int, 0), case when dist < 100 then 0 else least(25, floor(dist / 120)::int + 1) end))
          + greatest(0, least(coalesce((rep->>'carolers')::int, 0), 3)) * 2;
  if jr_private.event_night(d) > 0 and coalesce(rep->>'mode', '') = 'night' and dist >= 1500 then night := jr_private.event_night(d); end if;
  insert into public.jr_event_progress (user_id, season) values (uid, s) on conflict do nothing;
  select * into p from public.jr_event_progress where user_id = uid and season = s for update;
  if p.day_key <> dk then p.day_key := dk; p.day_tokens := 0; end if;
  added := greatest(0, least(earned, 120 - p.day_tokens));
  update public.jr_event_progress set tokens = tokens + added, day_key = dk, day_tokens = p.day_tokens + added,
    nights = case when night > 0 then nights | (1 << (night - 1)) else nights end, updated_at = now()
  where user_id = uid and season = s;
  insert into public.jr_event_runs (run_id, user_id, season, tokens, night) values (p_run_id, uid, s, added, night);
  return jsonb_build_object('ok', true, 'added', added, 'night', night);
end $$;

-- p_index 0..5 = the token rewards in order, 6 = the Golden Parol (all nine Simbang Gabi nights)
create or replace function public.jr_event_claim_reward(p_index int) returns jsonb
language plpgsql security definer set search_path = public, jr_private as $$
declare uid uuid := auth.uid(); d date := jr_private.event_date(); s int := jr_private.event_season(d); p public.jr_event_progress; rw record;
begin
  if not jr_private.event_active(d) then return jsonb_build_object('ok', false, 'reason', 'no_event', 'snapshot', jr_private.snapshot(uid)); end if;
  insert into public.jr_event_progress (user_id, season) values (uid, s) on conflict do nothing;
  select * into p from public.jr_event_progress where user_id = uid and season = s for update;
  if p_index = 6 then
    if p.golden then return jsonb_build_object('ok', false, 'reason', 'claimed', 'snapshot', jr_private.snapshot(uid)); end if;
    if p.nights & 511 <> 511 then return jsonb_build_object('ok', false, 'reason', 'nights', 'snapshot', jr_private.snapshot(uid)); end if;
    update public.jr_event_progress set golden = true, updated_at = now() where user_id = uid and season = s;
    insert into public.jr_cosmetics (user_id, item_id) values (uid, 'sign_goldenparol') on conflict do nothing;
    return jsonb_build_object('ok', true, 'snapshot', jr_private.snapshot(uid));
  end if;
  if p_index < 0 or p_index > 5 then return jsonb_build_object('ok', false, 'reason', 'unknown_item', 'snapshot', jr_private.snapshot(uid)); end if;
  if p.claimed & (1 << p_index) <> 0 then return jsonb_build_object('ok', false, 'reason', 'claimed', 'snapshot', jr_private.snapshot(uid)); end if;
  select * into rw from jr_private.event_reward(p_index);
  if p.tokens < rw.need then return jsonb_build_object('ok', false, 'reason', 'tokens', 'snapshot', jr_private.snapshot(uid)); end if;
  update public.jr_event_progress set claimed = claimed | (1 << p_index), updated_at = now() where user_id = uid and season = s;
  if rw.kind = 'coins' then
    update public.jr_profiles set coins = coins + rw.amount, updated_at = now() where user_id = uid;
    insert into public.jr_transactions (user_id, kind, amount, ref, currency) values (uid, 'event', rw.amount, 'pasko' || p_index, 'coins');
  elsif rw.kind = 'gems' then
    update public.jr_profiles set gems = gems + rw.amount, updated_at = now() where user_id = uid;
    insert into public.jr_transactions (user_id, kind, amount, ref, currency) values (uid, 'event', rw.amount, 'pasko' || p_index, 'gems');
  elsif rw.kind = 'cosmetic' then
    insert into public.jr_cosmetics (user_id, item_id) values (uid, rw.item) on conflict do nothing;
  elsif rw.kind = 'jeepney' then
    insert into public.jr_inventory (user_id, item_kind, item_id, level) values (uid, 'jeepney', rw.item, 1) on conflict do nothing;
  end if;
  return jsonb_build_object('ok', true, 'snapshot', jr_private.snapshot(uid));
end $$;

revoke all on function public.jr_event_status(), public.jr_event_claim_run(text), public.jr_event_claim_reward(int) from public, anon;
grant execute on function public.jr_event_status(), public.jr_event_claim_run(text), public.jr_event_claim_reward(int) to authenticated;
