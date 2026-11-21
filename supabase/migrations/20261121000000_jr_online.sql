-- =====================================================================================
-- JEEPNEY RUSH – Phase 3 online backend (Supabase / Postgres 15+)
--
-- Design: the database is the authority for currency and progress. Clients can only
--   * read their own rows (Row Level Security), and
--   * call the SECURITY DEFINER functions below (exposed through PostgREST as
--     /rest/v1/rpc/jr_*), which validate every request server-side.
-- No table accepts direct INSERT/UPDATE/DELETE from clients. The anon key is safe to
-- ship in the app; the service-role key never is.
--
-- Game rules mirrored from the C# client are cross-checked by supabase/tests (same
-- PRNG, mission generation, daily challenge, level curve and run bounds).
-- =====================================================================================

create schema if not exists jr_private;
revoke all on schema jr_private from public;

-- ------------------------------------------------------------------ catalog (generated data, see *_jr_catalog.sql)
create table if not exists public.jr_catalog_jeepneys (id text primary key, name text not null, price int not null check (price >= 0));
create table if not exists public.jr_catalog_upgrades (kind int not null, kind_name text not null, from_level int not null, cost int not null, primary key (kind, from_level));
create table if not exists public.jr_catalog_missions (
  period text not null check (period in ('daily', 'weekly')), idx int not null, type text not null,
  min_target int not null, max_target int not null, step int not null, text text not null, in_run boolean not null,
  primary key (period, idx));
create table if not exists public.jr_catalog_achievements (id text primary key, name text not null, target real not null, reward int not null, stat_key text not null);
create table if not exists public.jr_catalog_routes (env int primary key, name text not null, unlock_distance real not null);
create table if not exists public.jr_catalog_constants (key text primary key, value double precision not null);

-- ------------------------------------------------------------------ player data
create table if not exists public.jr_profiles (
  user_id uuid primary key references auth.users (id) on delete cascade,
  display_name text not null,
  friend_code text not null unique,
  coins int not null default 0 check (coins >= 0),
  xp int not null default 0 check (xp >= 0),
  best_score int not null default 0,
  best_distance real not null default 0,
  best_clean real not null default 0,
  runs int not null default 0,
  total_distance double precision not null default 0,
  total_coins bigint not null default 0,
  total_passengers int not null default 0,
  total_deliveries int not null default 0,
  total_power_ups int not null default 0,
  total_smooth_humps int not null default 0,
  total_wet_distance double precision not null default 0,
  total_refuels int not null default 0,
  legacy_imported boolean not null default false,
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now());

create table if not exists public.jr_inventory (
  user_id uuid not null references public.jr_profiles (user_id) on delete cascade,
  item_kind text not null check (item_kind in ('jeepney', 'upgrade')),
  item_id text not null,
  level int not null default 1,
  acquired_at timestamptz not null default now(),
  primary key (user_id, item_kind, item_id));

create table if not exists public.jr_runs (
  run_id text primary key,
  user_id uuid not null references public.jr_profiles (user_id) on delete cascade,
  created_at timestamptz not null default now(),
  accepted boolean not null,
  reason text,
  clamped boolean not null default false,
  daily boolean not null default false,
  duration real not null default 0,
  credited_coins int not null default 0,
  report jsonb not null);
create index if not exists jr_runs_user_time on public.jr_runs (user_id, created_at desc);

create table if not exists public.jr_scores (
  user_id uuid not null references public.jr_profiles (user_id) on delete cascade,
  board text not null,
  score int not null,
  distance real not null default 0,
  updated_at timestamptz not null default now(),
  primary key (user_id, board));
create index if not exists jr_scores_board on public.jr_scores (board, score desc, updated_at);

create table if not exists public.jr_friends (
  user_id uuid not null references public.jr_profiles (user_id) on delete cascade,
  friend_id uuid not null references public.jr_profiles (user_id) on delete cascade,
  created_at timestamptz not null default now(),
  primary key (user_id, friend_id),
  check (user_id <> friend_id));

create table if not exists public.jr_mission_progress (
  user_id uuid not null references public.jr_profiles (user_id) on delete cascade,
  period text not null, period_key int not null, slot int not null,
  progress int not null default 0, claimed boolean not null default false,
  primary key (user_id, period, period_key, slot));

create table if not exists public.jr_achievements_claimed (
  user_id uuid not null references public.jr_profiles (user_id) on delete cascade,
  achievement_id text not null,
  claimed_at timestamptz not null default now(),
  primary key (user_id, achievement_id));

create table if not exists public.jr_transactions (
  id bigserial primary key,
  user_id uuid not null references public.jr_profiles (user_id) on delete cascade,
  kind text not null,
  amount int not null,
  ref text,
  created_at timestamptz not null default now());
create index if not exists jr_tx_user on public.jr_transactions (user_id, created_at desc);

-- ------------------------------------------------------------------ Row Level Security
do $$
declare t text;
begin
  foreach t in array array['jr_catalog_jeepneys','jr_catalog_upgrades','jr_catalog_missions','jr_catalog_achievements','jr_catalog_routes','jr_catalog_constants',
                           'jr_profiles','jr_inventory','jr_runs','jr_scores','jr_friends','jr_mission_progress','jr_achievements_claimed','jr_transactions']
  loop
    execute format('alter table public.%I enable row level security', t);
    execute format('revoke all on public.%I from anon, authenticated', t);
  end loop;
end $$;

-- catalog: readable by everyone (prices, missions, achievements are public game data)
do $$
declare t text;
begin
  foreach t in array array['jr_catalog_jeepneys','jr_catalog_upgrades','jr_catalog_missions','jr_catalog_achievements','jr_catalog_routes','jr_catalog_constants']
  loop
    execute format('grant select on public.%I to anon, authenticated', t);
    execute format('drop policy if exists catalog_read on public.%I', t);
    execute format('create policy catalog_read on public.%I for select to anon, authenticated using (true)', t);
  end loop;
end $$;

-- player tables: a signed-in player may READ only their own rows; all writes go through jr_* functions
do $$
declare t text;
begin
  foreach t in array array['jr_profiles','jr_inventory','jr_runs','jr_mission_progress','jr_achievements_claimed','jr_transactions']
  loop
    execute format('grant select on public.%I to authenticated', t);
    execute format('drop policy if exists own_rows on public.%I', t);
    execute format('create policy own_rows on public.%I for select to authenticated using (user_id = auth.uid())', t);
  end loop;
end $$;
-- jr_scores and jr_friends: no direct access at all (leaderboards/friends are served by functions
-- that only expose display names and scores, never other players' ids-to-wallet data)

-- ------------------------------------------------------------------ helpers (private)
create or replace function jr_private.mul32(a bigint, b bigint) returns bigint
language sql immutable as $$
  -- (a * b) mod 2^32 without 64-bit overflow
  select ((a * (b & 65535)) + (((a * (b >> 16)) & 65535) << 16)) & 4294967295
$$;

-- mulberry32 step: returns the new state and the [0,1) value (identical to C# GameRandom.Next)
create or replace function jr_private.rng_next(inout s bigint, out v double precision)
language plpgsql immutable as $$
declare t bigint;
begin
  s := (s + 1831565813) & 4294967295;           -- 0x6D2B79F5
  t := s;
  t := jr_private.mul32(t # (t >> 15), t | 1);
  t := t # ((t + jr_private.mul32(t # (t >> 7), t | 61)) & 4294967295);
  v := ((t # (t >> 14)) & 4294967295)::double precision / 4294967296.0;
end $$;

-- C# GameRandom(int seed): state = (uint)seed
create or replace function jr_private.rng_seed(seed bigint) returns bigint
language sql immutable as $$ select seed & 4294967295 $$;

-- C# GameRandom.Int(min, maxInclusive)
create or replace function jr_private.rng_int(inout s bigint, lo int, hi int, out v int)
language plpgsql immutable as $$
declare d double precision;
begin
  select n.s, n.v into s, d from jr_private.rng_next(s) n;
  v := least(hi, lo + floor(d * (hi - lo + 1))::int);
end $$;

-- Philippine time (UTC+8) – same as the client's PhilippineTime
create or replace function jr_private.ph_date() returns date language sql stable as $$ select (now() at time zone 'Asia/Manila')::date $$;
create or replace function jr_private.day_key(d date) returns int language sql immutable as $$ select (extract(year from d) * 10000 + extract(month from d) * 100 + extract(day from d))::int $$;
create or replace function jr_private.week_key(d date) returns int language sql immutable as $$ select (floor((d - date '2024-01-01') / 7.0) + 1)::int $$;

create or replace function jr_private.level_for(xp int) returns int language sql immutable as $$ select 1 + floor(sqrt(greatest(0, xp) / 120.0))::int $$;

create or replace function jr_private.const(k text) returns double precision language sql stable as $$ select value from public.jr_catalog_constants where key = k $$;

-- Daily / weekly missions (identical to C# Missions.Generate)
create or replace function jr_private.missions(p_period text, p_key int)
returns table (slot int, type text, target int, reward int, text text, in_run boolean)
language plpgsql stable as $$
declare
  s bigint := jr_private.rng_seed(p_key::bigint * 7919 + 17);
  idx int[];
  cnt int := case when p_period = 'daily' then jr_private.const('daily_count')::int else jr_private.const('weekly_count')::int end;
  base real := case when p_period = 'daily' then jr_private.const('daily_reward_base')::real else jr_private.const('weekly_reward_base')::real end;
  m int; pos int; pick int; steps int; tgt int; diff real; r record;
begin
  select array_agg(c.idx order by c.idx) into idx from public.jr_catalog_missions c where c.period = p_period;
  for m in 0 .. cnt - 1 loop
    select n.s, n.v into s, pos from jr_private.rng_int(s, 0, array_length(idx, 1) - 1) n;
    pick := idx[pos + 1];
    idx := idx[1:pos] || idx[pos + 2:];
    select * into r from public.jr_catalog_missions c where c.period = p_period and c.idx = pick;
    steps := (r.max_target - r.min_target) / r.step;
    select n.s, n.v into s, pos from jr_private.rng_int(s, 0, steps) n;
    tgt := r.min_target + pos * r.step;
    diff := (tgt - r.min_target)::real / greatest(1, r.max_target - r.min_target)::real;
    slot := m; type := r.type; target := tgt;
    reward := trunc(base * (1::real + diff))::int;
    text := replace(r.text, '{0}', to_char(tgt, 'FM999,999,999'));
    in_run := r.in_run;
    return next;
  end loop;
end $$;

-- Daily challenge (identical to C# DailyChallenge.For)
create or replace function jr_private.daily_challenge(p_day int, out seed int, out env int)
language plpgsql stable as $$
declare s bigint := jr_private.rng_seed(p_day::bigint * 31 + 7); d double precision; n int;
begin
  select x.s, x.v into s, d from jr_private.rng_next(s) x;
  seed := 1 + floor(d * 2147483000.0)::int;
  n := (select count(*) from public.jr_catalog_routes);
  select x.s, x.v into s, env from jr_private.rng_int(s, 0, n - 1) x;
end $$;

-- Run plausibility (identical to C# RunBounds). Returns null if OK, else the reason.
create or replace function jr_private.run_violation(r jsonb) returns text
language plpgsql stable as $$
declare
  dur real := (r->>'duration')::real; dist real := (r->>'distance')::real;
begin
  if r is null then return 'empty'; end if;
  if coalesce(r->>'run_id', '') = '' or length(r->>'run_id') > 64 then return 'run_id'; end if;
  if dur is null or not (dur >= 5) or dur > 4 * 3600 then return 'duration'; end if;
  if dist is null or not (dist >= 0) or dist > 50 * dur + 50 then return 'too_fast'; end if;
  if coalesce((r->>'env')::int, -1) < 0 or (r->>'env')::int >= (select count(*) from public.jr_catalog_routes) then return 'env'; end if;
  if coalesce((r->>'coins')::int, 0) < 0 or coalesce((r->>'score')::int, 0) < 0 or coalesce((r->>'passengers')::int, 0) < 0
     or coalesce((r->>'deliveries')::int, 0) < 0 or coalesce((r->>'power_ups')::int, 0) < 0 or coalesce((r->>'smooth_humps')::int, 0) < 0
     or coalesce((r->>'refuels')::int, 0) < 0 or coalesce((r->>'hits')::int, 0) < 0 then return 'negative'; end if;
  if coalesce((r->>'clean_distance')::real, 0) > dist + 1 or coalesce((r->>'wet_distance')::real, 0) > dist + 1
     or coalesce((r->>'clean_distance')::real, 0) < 0 or coalesce((r->>'wet_distance')::real, 0) < 0 then return 'distance_parts'; end if;
  if coalesce((r->>'deliveries')::int, 0) > coalesce((r->>'passengers')::int, 0) then return 'deliveries'; end if;
  return null;
end $$;

-- Clamp soft values (identical to C# RunBounds.Clamp); expects a report that passed run_violation
create or replace function jr_private.run_clamp(r jsonb) returns jsonb
language plpgsql immutable as $$
declare
  d real := (r->>'distance')::real;
  pas int := least(coalesce((r->>'passengers')::int, 0), trunc((d / 200::real + 2::real) * 15::real)::int);
  del int := least(coalesce((r->>'deliveries')::int, 0), pas);
  pu int := least(coalesce((r->>'power_ups')::int, 0), trunc(d / 250::real)::int + 3);
  hu int := least(coalesce((r->>'smooth_humps')::int, 0), trunc(d / 150::real)::int + 2);
  rf int := least(coalesce((r->>'refuels')::int, 0), trunc(d / 700::real)::int + 1);
  co int := least(coalesce((r->>'coins')::int, 0), trunc(d * 1.0::real)::int + del * 42 + 50);
  sc int := least(coalesce((r->>'score')::int, 0),
                  trunc(d * 1.5::real)::int + co * 10 + pas * 50 + del * 600 + hu * 50 + trunc(150::real * (d / 300::real + 1::real))::int + 1000);
begin
  return r || jsonb_build_object('passengers', pas, 'deliveries', del, 'power_ups', pu, 'smooth_humps', hu, 'refuels', rf, 'coins', co, 'score', sc);
end $$;

-- Friend codes: 8 chars, no ambiguous letters
create or replace function jr_private.new_friend_code() returns text
language plpgsql volatile as $$
declare alphabet text := 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789'; code text; i int;
begin
  loop
    code := '';
    for i in 1 .. 8 loop code := code || substr(alphabet, 1 + floor(random() * length(alphabet))::int, 1); end loop;
    exit when not exists (select 1 from public.jr_profiles where friend_code = code);
  end loop;
  return code;
end $$;

create or replace function jr_private.ensure_profile(p_uid uuid) returns void
language plpgsql security definer set search_path = public, jr_private as $$
declare code text;
begin
  if p_uid is null then raise exception 'not_authenticated' using errcode = '28000'; end if;
  if exists (select 1 from public.jr_profiles where user_id = p_uid) then return; end if;
  code := jr_private.new_friend_code();
  insert into public.jr_profiles (user_id, display_name, friend_code) values (p_uid, 'Driver-' || substr(code, 1, 4), code)
  on conflict (user_id) do nothing;
  -- free jeepneys
  insert into public.jr_inventory (user_id, item_kind, item_id, level)
  select p_uid, 'jeepney', id, 1 from public.jr_catalog_jeepneys where price = 0
  on conflict do nothing;
end $$;

-- new auth user (guest or email) -> profile
create or replace function jr_private.on_auth_user_created() returns trigger
language plpgsql security definer set search_path = public, jr_private as $$
begin
  perform jr_private.ensure_profile(new.id);
  return new;
end $$;
drop trigger if exists jr_on_auth_user_created on auth.users;
create trigger jr_on_auth_user_created after insert on auth.users for each row execute function jr_private.on_auth_user_created();

create or replace function jr_private.stat_value(p public.jr_profiles, k text) returns double precision
language plpgsql stable as $$
begin
  return case k
    when 'total_passengers' then p.total_passengers
    when 'best_distance' then p.best_distance
    when 'best_clean' then p.best_clean
    when 'total_coins' then p.total_coins
    when 'total_deliveries' then p.total_deliveries
    when 'total_power_ups' then p.total_power_ups
    when 'total_wet_distance' then p.total_wet_distance
    when 'total_smooth_humps' then p.total_smooth_humps
    when 'owned_jeepneys' then (select count(*) from public.jr_inventory i where i.user_id = p.user_id and i.item_kind = 'jeepney')
    when 'max_upgrade' then coalesce((select max(level) from public.jr_inventory i where i.user_id = p.user_id and i.item_kind = 'upgrade'), 0)
    when 'routes_unlocked' then (select count(*) from public.jr_catalog_routes r where p.best_distance >= r.unlock_distance)
    else null end;
end $$;

-- make sure this period's mission rows exist
create or replace function jr_private.ensure_missions(p_uid uuid, p_period text, p_key int) returns void
language plpgsql security definer set search_path = public, jr_private as $$
begin
  insert into public.jr_mission_progress (user_id, period, period_key, slot)
  select p_uid, p_period, p_key, m.slot from jr_private.missions(p_period, p_key) m
  on conflict do nothing;
end $$;

-- The player's whole state as JSON (field names match C# ServerSnapshot)
create or replace function jr_private.snapshot(p_uid uuid) returns jsonb
language plpgsql security definer set search_path = public, jr_private as $$
declare
  p public.jr_profiles; dk int := jr_private.day_key(jr_private.ph_date()); wk int := jr_private.week_key(jr_private.ph_date());
  anon boolean;
begin
  perform jr_private.ensure_missions(p_uid, 'daily', dk);
  perform jr_private.ensure_missions(p_uid, 'weekly', wk);
  select * into p from public.jr_profiles where user_id = p_uid;
  select coalesce(u.is_anonymous, false) into anon from auth.users u where u.id = p_uid;
  return jsonb_build_object(
    'user_id', p.user_id, 'display_name', p.display_name, 'friend_code', p.friend_code, 'is_anonymous', coalesce(anon, false),
    'coins', p.coins, 'xp', p.xp, 'level', jr_private.level_for(p.xp),
    'best_score', p.best_score, 'best_distance', p.best_distance, 'best_clean', p.best_clean, 'runs', p.runs,
    'total_distance', p.total_distance, 'total_coins', p.total_coins, 'total_passengers', p.total_passengers,
    'total_deliveries', p.total_deliveries, 'total_power_ups', p.total_power_ups, 'total_smooth_humps', p.total_smooth_humps,
    'total_wet_distance', p.total_wet_distance, 'total_refuels', p.total_refuels,
    'owned', coalesce((select jsonb_agg(item_id order by acquired_at, item_id) from public.jr_inventory where user_id = p_uid and item_kind = 'jeepney'), '[]'::jsonb),
    'upgrades', (select jsonb_agg(coalesce(i.level, 0) order by u.kind)
                 from (select distinct kind from public.jr_catalog_upgrades) u
                 left join public.jr_inventory i on i.user_id = p_uid and i.item_kind = 'upgrade' and i.item_id = u.kind::text),
    'day_key', dk,
    'daily_progress', (select jsonb_agg(progress order by slot) from public.jr_mission_progress where user_id = p_uid and period = 'daily' and period_key = dk),
    'daily_claimed', (select jsonb_agg(claimed order by slot) from public.jr_mission_progress where user_id = p_uid and period = 'daily' and period_key = dk),
    'week_key', wk,
    'weekly_progress', (select jsonb_agg(progress order by slot) from public.jr_mission_progress where user_id = p_uid and period = 'weekly' and period_key = wk),
    'weekly_claimed', (select jsonb_agg(claimed order by slot) from public.jr_mission_progress where user_id = p_uid and period = 'weekly' and period_key = wk),
    'achievements_unlocked', coalesce((select jsonb_agg(a.id order by a.id) from public.jr_catalog_achievements a where jr_private.stat_value(p, a.stat_key) >= a.target), '[]'::jsonb),
    'achievements_claimed', coalesce((select jsonb_agg(achievement_id order by achievement_id) from public.jr_achievements_claimed where user_id = p_uid), '[]'::jsonb),
    'missions', (select jsonb_agg(jsonb_build_object('period', x.period, 'slot', x.slot, 'type', x.type, 'target', x.target, 'reward', x.reward, 'text', x.text))
                 from (select 'daily' as period, m.* from jr_private.missions('daily', dk) m
                       union all select 'weekly', m.* from jr_private.missions('weekly', wk) m) x));
end $$;

create or replace function jr_private.rank_on(p_uid uuid, p_board text) returns int
language sql stable security definer set search_path = public as $$
  select case when s.user_id is null then 0 else 1 + (select count(*) from public.jr_scores o where o.board = p_board and (o.score > s.score or (o.score = s.score and o.updated_at < s.updated_at)))::int end
  from (select 1) one left join public.jr_scores s on s.user_id = p_uid and s.board = p_board
$$;

create or replace function jr_private.post_score(p_uid uuid, p_board text, p_score int, p_distance real) returns void
language plpgsql security definer set search_path = public as $$
begin
  insert into public.jr_scores (user_id, board, score, distance) values (p_uid, p_board, p_score, p_distance)
  on conflict (user_id, board) do update set score = excluded.score, distance = excluded.distance, updated_at = now()
  where public.jr_scores.score < excluded.score;
end $$;

-- ------------------------------------------------------------------ public API (PostgREST RPC)
create or replace function public.jr_get_state() returns jsonb
language plpgsql security definer set search_path = public, jr_private as $$
begin
  perform jr_private.ensure_profile(auth.uid());
  return jr_private.snapshot(auth.uid());
end $$;

create or replace function public.jr_daily_challenge() returns jsonb
language plpgsql stable security definer set search_path = public, jr_private as $$
declare dk int := jr_private.day_key(jr_private.ph_date()); c record;
begin
  select * into c from jr_private.daily_challenge(dk);
  return jsonb_build_object('day_key', dk, 'seed', c.seed, 'env', c.env);
end $$;

-- Upload one finished run. Idempotent per run_id (safe to retry from the offline queue).
create or replace function public.jr_submit_run(p_report jsonb) returns jsonb
language plpgsql security definer set search_path = public, jr_private as $$
declare
  uid uuid := auth.uid();
  v text; r jsonb; p public.jr_profiles;
  dk int := jr_private.day_key(jr_private.ph_date()); wk int := jr_private.week_key(jr_private.ph_date());
  is_daily boolean := false; ch record;
  played real; clamped boolean;
  v_coins int; bonus int; xp_gain int; lvl_before int; lvl_after int; lvl_reward int := 0; lv int;
  m record; val int; existing public.jr_runs;
  bonus_level int;
begin
  perform jr_private.ensure_profile(uid);
  select * into existing from public.jr_runs where run_id = p_report->>'run_id';
  if found then
    if existing.user_id <> uid then return jsonb_build_object('accepted', false, 'reason', 'run_id'); end if;
    return jsonb_build_object('accepted', existing.accepted, 'duplicate', true, 'reason', existing.reason, 'snapshot', jr_private.snapshot(uid));
  end if;

  v := jr_private.run_violation(p_report);
  if v is null then
    -- a driver can't have played more than 20 hours in the last 24
    select coalesce(sum(duration), 0) into played from public.jr_runs where user_id = uid and accepted and created_at > now() - interval '24 hours';
    if played + (p_report->>'duration')::real > 72000 then v := 'playtime'; end if;
  end if;
  if v is not null then
    insert into public.jr_runs (run_id, user_id, accepted, reason, report, duration) values (p_report->>'run_id', uid, false, v, p_report, coalesce((p_report->>'duration')::real, 0));
    return jsonb_build_object('accepted', false, 'reason', v, 'snapshot', jr_private.snapshot(uid));
  end if;

  r := jr_private.run_clamp(p_report);
  clamped := (r->>'coins')::int <> coalesce((p_report->>'coins')::int, 0) or (r->>'score')::int <> coalesce((p_report->>'score')::int, 0)
          or (r->>'passengers')::int <> coalesce((p_report->>'passengers')::int, 0) or (r->>'power_ups')::int <> coalesce((p_report->>'power_ups')::int, 0);

  -- daily challenge only counts for today's seed and route
  if coalesce((p_report->>'daily')::boolean, false) then
    select * into ch from jr_private.daily_challenge(dk);
    is_daily := (p_report->>'day_key')::int = dk and (p_report->>'seed')::int = ch.seed and (p_report->>'env')::int = ch.env;
  end if;

  select * into p from public.jr_profiles where user_id = uid for update;
  v_coins := (r->>'coins')::int;
  select coalesce(max(level), 0) into bonus_level from public.jr_inventory where user_id = uid and item_kind = 'upgrade' and item_id = '4';
  bonus := round(v_coins * jr_private.const('coin_bonus_per_level') * bonus_level)::int;
  xp_gain := trunc((r->>'distance')::real / 10::real)::int + (r->>'passengers')::int * 5 + (r->>'deliveries')::int * 10 + (r->>'power_ups')::int * 5;
  lvl_before := jr_private.level_for(p.xp);
  lvl_after := jr_private.level_for(p.xp + xp_gain);
  for lv in lvl_before + 1 .. lvl_after loop lvl_reward := lvl_reward + (jr_private.const('level_reward_per_level')::int * lv); end loop;

  update public.jr_profiles set
    coins = coins + v_coins + bonus + lvl_reward,
    xp = xp + xp_gain,
    best_score = greatest(best_score, (r->>'score')::int),
    best_distance = greatest(best_distance, (r->>'distance')::real),
    best_clean = greatest(best_clean, coalesce((r->>'clean_distance')::real, 0)),
    runs = runs + 1,
    total_distance = total_distance + (r->>'distance')::real,
    total_coins = total_coins + v_coins + bonus,
    total_passengers = total_passengers + (r->>'passengers')::int,
    total_deliveries = total_deliveries + (r->>'deliveries')::int,
    total_power_ups = total_power_ups + (r->>'power_ups')::int,
    total_smooth_humps = total_smooth_humps + (r->>'smooth_humps')::int,
    total_wet_distance = total_wet_distance + coalesce((r->>'wet_distance')::real, 0),
    total_refuels = total_refuels + (r->>'refuels')::int,
    updated_at = now()
  where user_id = uid;

  insert into public.jr_runs (run_id, user_id, accepted, clamped, daily, duration, credited_coins, report)
  values (r->>'run_id', uid, true, clamped, is_daily, (r->>'duration')::real, v_coins + bonus, r);
  insert into public.jr_transactions (user_id, kind, amount, ref) values (uid, 'run', v_coins + bonus, r->>'run_id');
  if lvl_reward > 0 then insert into public.jr_transactions (user_id, kind, amount, ref) values (uid, 'level_up', lvl_reward, lvl_after::text); end if;

  -- missions (same rules as C# Missions.ApplyRun)
  perform jr_private.ensure_missions(uid, 'daily', dk);
  perform jr_private.ensure_missions(uid, 'weekly', wk);
  for m in select 'daily' as period, dk as k, x.* from jr_private.missions('daily', dk) x
           union all select 'weekly', wk, x.* from jr_private.missions('weekly', wk) x loop
    val := case m.type
      when 'DistanceInRun' then trunc((r->>'distance')::real)::int when 'TotalDistance' then trunc((r->>'distance')::real)::int
      when 'CoinsInRun' then (r->>'coins')::int when 'TotalCoins' then (r->>'coins')::int
      when 'PassengersInRun' then (r->>'passengers')::int when 'TotalPassengers' then (r->>'passengers')::int
      when 'DeliveriesInRun' then (r->>'deliveries')::int when 'TotalDeliveries' then (r->>'deliveries')::int
      when 'CleanDistanceInRun' then trunc(coalesce((r->>'clean_distance')::real, 0))::int
      when 'StreakInRun' then coalesce((r->>'best_streak')::int, 0)
      when 'TotalPowerUps' then (r->>'power_ups')::int
      when 'TotalSmoothHumps' then (r->>'smooth_humps')::int
      when 'TotalWetDistance' then trunc(coalesce((r->>'wet_distance')::real, 0))::int
      when 'TotalRefuels' then (r->>'refuels')::int
      when 'Runs' then 1
      else 0 end;
    update public.jr_mission_progress mp set progress = least(m.target, case when m.in_run then greatest(mp.progress, val) else mp.progress + val end)
    where mp.user_id = uid and mp.period = m.period and mp.period_key = m.k and mp.slot = m.slot;
  end loop;

  -- leaderboards
  perform jr_private.post_score(uid, 'all', (r->>'score')::int, (r->>'distance')::real);
  perform jr_private.post_score(uid, 'week:' || wk, (r->>'score')::int, (r->>'distance')::real);
  if is_daily then perform jr_private.post_score(uid, 'daily:' || dk, (r->>'score')::int, (r->>'distance')::real); end if;

  return jsonb_build_object(
    'accepted', true, 'clamped', clamped, 'credited_coins', v_coins, 'coin_bonus', bonus, 'xp_gained', xp_gain, 'level_reward', lvl_reward,
    'daily', is_daily,
    'rank_all', jr_private.rank_on(uid, 'all'), 'rank_week', jr_private.rank_on(uid, 'week:' || wk),
    'rank_daily', case when is_daily then jr_private.rank_on(uid, 'daily:' || dk) else 0 end,
    'snapshot', jr_private.snapshot(uid));
end $$;

create or replace function public.jr_buy_jeepney(p_id text) returns jsonb
language plpgsql security definer set search_path = public, jr_private as $$
declare uid uuid := auth.uid(); price int; c int;
begin
  perform jr_private.ensure_profile(uid);
  select j.price into price from public.jr_catalog_jeepneys j where j.id = p_id;
  if not found then return jsonb_build_object('ok', false, 'reason', 'unknown_item', 'snapshot', jr_private.snapshot(uid)); end if;
  if exists (select 1 from public.jr_inventory where user_id = uid and item_kind = 'jeepney' and item_id = p_id) then
    return jsonb_build_object('ok', false, 'reason', 'owned', 'snapshot', jr_private.snapshot(uid)); end if;
  select coins into c from public.jr_profiles where user_id = uid for update;
  if c < price then return jsonb_build_object('ok', false, 'reason', 'coins', 'snapshot', jr_private.snapshot(uid)); end if;
  update public.jr_profiles set coins = coins - price, updated_at = now() where user_id = uid;
  insert into public.jr_inventory (user_id, item_kind, item_id, level) values (uid, 'jeepney', p_id, 1);
  insert into public.jr_transactions (user_id, kind, amount, ref) values (uid, 'buy_jeepney', -price, p_id);
  return jsonb_build_object('ok', true, 'snapshot', jr_private.snapshot(uid));
end $$;

create or replace function public.jr_buy_upgrade(p_kind int) returns jsonb
language plpgsql security definer set search_path = public, jr_private as $$
declare uid uuid := auth.uid(); cur int; cost int; c int;
begin
  perform jr_private.ensure_profile(uid);
  select coalesce((select level from public.jr_inventory where user_id = uid and item_kind = 'upgrade' and item_id = p_kind::text), 0) into cur;
  select u.cost into cost from public.jr_catalog_upgrades u where u.kind = p_kind and u.from_level = cur;
  if not found then return jsonb_build_object('ok', false, 'reason', 'max_level', 'snapshot', jr_private.snapshot(uid)); end if;
  select coins into c from public.jr_profiles where user_id = uid for update;
  if c < cost then return jsonb_build_object('ok', false, 'reason', 'coins', 'snapshot', jr_private.snapshot(uid)); end if;
  update public.jr_profiles set coins = coins - cost, updated_at = now() where user_id = uid;
  insert into public.jr_inventory (user_id, item_kind, item_id, level) values (uid, 'upgrade', p_kind::text, cur + 1)
  on conflict (user_id, item_kind, item_id) do update set level = excluded.level;
  insert into public.jr_transactions (user_id, kind, amount, ref) values (uid, 'buy_upgrade', -cost, p_kind || ':' || (cur + 1));
  return jsonb_build_object('ok', true, 'snapshot', jr_private.snapshot(uid));
end $$;

create or replace function public.jr_claim_mission(p_weekly boolean, p_slot int) returns jsonb
language plpgsql security definer set search_path = public, jr_private as $$
declare
  uid uuid := auth.uid(); per text := case when p_weekly then 'weekly' else 'daily' end;
  k int := case when p_weekly then jr_private.week_key(jr_private.ph_date()) else jr_private.day_key(jr_private.ph_date()) end;
  m record; mp public.jr_mission_progress;
begin
  perform jr_private.ensure_profile(uid);
  perform jr_private.ensure_missions(uid, per, k);
  select * into m from jr_private.missions(per, k) x where x.slot = p_slot;
  if not found then return jsonb_build_object('ok', false, 'reason', 'unknown_mission', 'snapshot', jr_private.snapshot(uid)); end if;
  select * into mp from public.jr_mission_progress where user_id = uid and period = per and period_key = k and slot = p_slot for update;
  if mp.claimed then return jsonb_build_object('ok', false, 'reason', 'claimed', 'snapshot', jr_private.snapshot(uid)); end if;
  if mp.progress < m.target then return jsonb_build_object('ok', false, 'reason', 'not_done', 'snapshot', jr_private.snapshot(uid)); end if;
  update public.jr_mission_progress set claimed = true where user_id = uid and period = per and period_key = k and slot = p_slot;
  update public.jr_profiles set coins = coins + m.reward, updated_at = now() where user_id = uid;
  insert into public.jr_transactions (user_id, kind, amount, ref) values (uid, 'mission', m.reward, per || ':' || k || ':' || p_slot);
  return jsonb_build_object('ok', true, 'reward', m.reward, 'snapshot', jr_private.snapshot(uid));
end $$;

create or replace function public.jr_claim_achievement(p_id text) returns jsonb
language plpgsql security definer set search_path = public, jr_private as $$
declare uid uuid := auth.uid(); a public.jr_catalog_achievements; p public.jr_profiles;
begin
  perform jr_private.ensure_profile(uid);
  select * into a from public.jr_catalog_achievements where id = p_id;
  if not found then return jsonb_build_object('ok', false, 'reason', 'unknown_achievement', 'snapshot', jr_private.snapshot(uid)); end if;
  select * into p from public.jr_profiles where user_id = uid for update;
  if jr_private.stat_value(p, a.stat_key) < a.target then return jsonb_build_object('ok', false, 'reason', 'not_done', 'snapshot', jr_private.snapshot(uid)); end if;
  insert into public.jr_achievements_claimed (user_id, achievement_id) values (uid, p_id) on conflict do nothing;
  if not found then return jsonb_build_object('ok', false, 'reason', 'claimed', 'snapshot', jr_private.snapshot(uid)); end if;
  update public.jr_profiles set coins = coins + a.reward, updated_at = now() where user_id = uid;
  insert into public.jr_transactions (user_id, kind, amount, ref) values (uid, 'achievement', a.reward, p_id);
  return jsonb_build_object('ok', true, 'reward', a.reward, 'snapshot', jr_private.snapshot(uid));
end $$;

create or replace function public.jr_set_name(p_name text) returns jsonb
language plpgsql security definer set search_path = public, jr_private as $$
declare uid uuid := auth.uid(); n text := btrim(coalesce(p_name, ''));
begin
  perform jr_private.ensure_profile(uid);
  if length(n) < 3 or length(n) > 16 or n !~ '^[A-Za-z0-9 _.\-]+$' then return jsonb_build_object('ok', false, 'reason', 'invalid_name'); end if;
  if lower(n) ~ '(putang|puta|gago|tanga|ulol|tarantado|fuck|shit|bitch|admin|moderator)' then return jsonb_build_object('ok', false, 'reason', 'blocked_name'); end if;
  update public.jr_profiles set display_name = n, updated_at = now() where user_id = uid;
  return jsonb_build_object('ok', true, 'snapshot', jr_private.snapshot(uid));
end $$;

-- One-time import of progress made offline before the first sign-in (capped).
create or replace function public.jr_import_legacy(p_data jsonb) returns jsonb
language plpgsql security definer set search_path = public, jr_private as $$
declare uid uuid := auth.uid(); p public.jr_profiles; j text; k int; lvl int;
begin
  perform jr_private.ensure_profile(uid);
  select * into p from public.jr_profiles where user_id = uid for update;
  if p.legacy_imported or p.runs > 0 then return jsonb_build_object('ok', false, 'reason', 'already_has_progress', 'snapshot', jr_private.snapshot(uid)); end if;
  update public.jr_profiles set
    coins = least(greatest(coalesce((p_data->>'coins')::int, 0), 0), 20000),
    xp = least(greatest(coalesce((p_data->>'xp')::int, 0), 0), 200000),
    best_score = least(greatest(coalesce((p_data->>'best_score')::int, 0), 0), 2000000),
    best_distance = least(greatest(coalesce((p_data->>'best_distance')::real, 0), 0), 200000),
    legacy_imported = true, updated_at = now()
  where user_id = uid;
  for j in select jsonb_array_elements_text(coalesce(p_data->'owned', '[]'::jsonb)) loop
    if exists (select 1 from public.jr_catalog_jeepneys where id = j) then
      insert into public.jr_inventory (user_id, item_kind, item_id, level) values (uid, 'jeepney', j, 1) on conflict do nothing;
    end if;
  end loop;
  k := 0;
  for lvl in select coalesce(x::int, 0) from jsonb_array_elements_text(coalesce(p_data->'upgrades', '[]'::jsonb)) x loop
    if k < (select count(distinct kind) from public.jr_catalog_upgrades) and lvl > 0 then
      insert into public.jr_inventory (user_id, item_kind, item_id, level) values (uid, 'upgrade', k::text, least(lvl, jr_private.const('max_upgrade_level')::int))
      on conflict (user_id, item_kind, item_id) do update set level = excluded.level;
    end if;
    k := k + 1;
  end loop;
  if coalesce((p_data->>'best_score')::int, 0) > 0 then
    perform jr_private.post_score(uid, 'all', least((p_data->>'best_score')::int, 2000000), least(coalesce((p_data->>'best_distance')::real, 0), 200000));
  end if;
  insert into public.jr_transactions (user_id, kind, amount, ref) values (uid, 'legacy_import', least(greatest(coalesce((p_data->>'coins')::int, 0), 0), 20000), null);
  return jsonb_build_object('ok', true, 'snapshot', jr_private.snapshot(uid));
end $$;

create or replace function public.jr_add_friend(p_code text) returns jsonb
language plpgsql security definer set search_path = public, jr_private as $$
declare uid uuid := auth.uid(); other uuid; code text := upper(regexp_replace(coalesce(p_code, ''), '[^A-Za-z0-9]', '', 'g'));
begin
  perform jr_private.ensure_profile(uid);
  select user_id into other from public.jr_profiles where friend_code = code;
  if other is null then return jsonb_build_object('ok', false, 'reason', 'unknown_code'); end if;
  if other = uid then return jsonb_build_object('ok', false, 'reason', 'self'); end if;
  if (select count(*) from public.jr_friends where user_id = uid) >= 100 then return jsonb_build_object('ok', false, 'reason', 'limit'); end if;
  insert into public.jr_friends (user_id, friend_id) values (uid, other), (other, uid) on conflict do nothing;
  return jsonb_build_object('ok', true, 'friend', (select display_name from public.jr_profiles where user_id = other));
end $$;

create or replace function public.jr_remove_friend(p_user uuid) returns jsonb
language plpgsql security definer set search_path = public, jr_private as $$
declare uid uuid := auth.uid();
begin
  perform jr_private.ensure_profile(uid);
  delete from public.jr_friends where (user_id = uid and friend_id = p_user) or (user_id = p_user and friend_id = uid);
  return jsonb_build_object('ok', true);
end $$;

create or replace function public.jr_list_friends() returns jsonb
language plpgsql security definer set search_path = public, jr_private as $$
declare uid uuid := auth.uid();
begin
  perform jr_private.ensure_profile(uid);
  return jsonb_build_object('friends', coalesce((
    select jsonb_agg(jsonb_build_object('user_id', p.user_id, 'display_name', p.display_name, 'level', jr_private.level_for(p.xp), 'best_score', p.best_score) order by p.best_score desc)
    from public.jr_friends f join public.jr_profiles p on p.user_id = f.friend_id where f.user_id = uid), '[]'::jsonb));
end $$;

-- p_board: 'all' | 'week' | 'daily'   p_scope: 'global' | 'friends'
create or replace function public.jr_leaderboard(p_board text, p_scope text default 'global', p_limit int default 50) returns jsonb
language plpgsql security definer set search_path = public, jr_private as $$
declare
  uid uuid := auth.uid();
  b text := case p_board when 'week' then 'week:' || jr_private.week_key(jr_private.ph_date())
                         when 'daily' then 'daily:' || jr_private.day_key(jr_private.ph_date()) else 'all' end;
  lim int := least(greatest(coalesce(p_limit, 50), 1), 100);
  friends_only boolean := p_scope = 'friends';
  rows jsonb; me jsonb;
begin
  perform jr_private.ensure_profile(uid);
  with pool as (
    select s.user_id, s.score, s.distance, s.updated_at from public.jr_scores s
    where s.board = b and (not friends_only or s.user_id = uid or exists (select 1 from public.jr_friends f where f.user_id = uid and f.friend_id = s.user_id))),
  ranked as (
    select p.*, rank() over (order by p.score desc, p.updated_at) as rk from pool p)
  select coalesce(jsonb_agg(jsonb_build_object('rank', r.rk, 'user_id', r.user_id, 'display_name', pr.display_name, 'score', r.score,
           'distance', r.distance, 'level', jr_private.level_for(pr.xp), 'is_me', r.user_id = uid) order by r.rk) filter (where r.rk <= lim), '[]'::jsonb),
         (array_agg(jsonb_build_object('rank', r.rk, 'user_id', r.user_id, 'display_name', pr.display_name, 'score', r.score,
           'distance', r.distance, 'level', jr_private.level_for(pr.xp), 'is_me', true)) filter (where r.user_id = uid))[1]
    into rows, me
  from ranked r join public.jr_profiles pr on pr.user_id = r.user_id;
  return jsonb_build_object('board', b, 'scope', case when friends_only then 'friends' else 'global' end, 'rows', rows, 'me', me);
end $$;

-- ------------------------------------------------------------------ permissions on functions
revoke all on all functions in schema jr_private from public, anon, authenticated;
revoke all on function public.jr_get_state(), public.jr_daily_challenge(), public.jr_submit_run(jsonb), public.jr_buy_jeepney(text),
  public.jr_buy_upgrade(int), public.jr_claim_mission(boolean, int), public.jr_claim_achievement(text), public.jr_set_name(text),
  public.jr_import_legacy(jsonb), public.jr_add_friend(text), public.jr_remove_friend(uuid), public.jr_list_friends(),
  public.jr_leaderboard(text, text, int) from public, anon;
grant execute on function public.jr_get_state(), public.jr_daily_challenge(), public.jr_submit_run(jsonb), public.jr_buy_jeepney(text),
  public.jr_buy_upgrade(int), public.jr_claim_mission(boolean, int), public.jr_claim_achievement(text), public.jr_set_name(text),
  public.jr_import_legacy(jsonb), public.jr_add_friend(text), public.jr_remove_friend(uuid), public.jr_list_friends(),
  public.jr_leaderboard(text, text, int) to authenticated;
