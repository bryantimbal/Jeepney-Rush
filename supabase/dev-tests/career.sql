-- Career mode on the server: stars from the (clamped) run, new-star coins, unlock order,
-- license rewards, replay safety, and the snapshot fields.
\set ON_ERROR_STOP on
\pset tuples_only on
\pset format unaligned
\set u 'dddddddd-0000-0000-0000-00000000000a'
insert into auth.users (id) values (:'u');
select set_config('request.jwt.claim.sub', :'u', false) \g /dev/null
create function pg_temp.krun(id text, shift text, env int, dist real, coins int, deliveries int, hits int) returns jsonb language sql as $$
  select public.jr_submit_run(jsonb_build_object('run_id', id, 'seed', 1, 'env', env, 'duration', dist / 10, 'distance', dist, 'coins', coins, 'score', 100,
    'passengers', deliveries, 'deliveries', deliveries, 'power_ups', 0, 'smooth_humps', 0, 'wet_distance', 0, 'refuels', 0, 'hits', hits,
    'clean_distance', 0, 'best_streak', 0, 'daily', false, 'day_key', 0, 'jeepney', 'pilipinas', 'gems', 0, 'career', shift,
    'fares', coins, 'gas', 0)) $$;
-- a run with road coins and diesel: coins = road + fares - gas
create function pg_temp.mrun(id text, shift text, env int, dist real, road int, fares int, gas int) returns jsonb language sql as $$
  select public.jr_submit_run(jsonb_build_object('run_id', id, 'seed', 1, 'env', env, 'duration', dist / 10, 'distance', dist, 'coins', road + fares - gas, 'score', 100,
    'passengers', 20, 'deliveries', 20, 'power_ups', 0, 'smooth_humps', 0, 'wet_distance', 0, 'refuels', 1, 'hits', 0,
    'clean_distance', 0, 'best_streak', 0, 'daily', false, 'day_key', 0, 'jeepney', 'pilipinas', 'gems', 0, 'career', shift,
    'fares', fares, 'gas', gas)) $$;
grant execute on all functions in schema pg_temp to authenticated;
do $$ begin
  assert (select count(*) from public.jr_catalog_career) = 20, 'catalog has the 20 shifts';
  assert (select stars_needed from public.jr_catalog_licenses where rank = 1) = 6, 'non-pro license at 6 stars';
  assert (select boundary from public.jr_catalog_career where id = 'c1s1') = 25, 'c1s1 boundary from C#';
end $$;
set role authenticated;
do $$ declare r jsonb; c0 int; begin
  -- c1s1: Manila (0), 800 m, boundary 25, deliver 3. c1s2: Manila, 1000 m, boundary 35, at most 1 hit
  r := pg_temp.krun('k-locked', 'c1s2', 0, 1000, 60, 0, 0);
  assert (r->'career'->>'locked')::boolean and (r->'career'->>'mask')::int = 0, 'c1s2 locked until c1s1 has a star: ' || (r->'career')::text;
  r := pg_temp.krun('k-crash', 'c1s1', 0, 700, 500, 5, 0);
  assert (r->'career'->>'mask')::int = 0, 'no stars before the terminal';
  c0 := (r->'snapshot'->>'coins')::int;
  r := pg_temp.krun('k-1', 'c1s1', 0, 805, 10, 0, 0);
  assert (r->'career'->>'mask')::int = 1 and (r->'career'->>'star_coins')::int = 40, 'finish star pays 40: ' || (r->'career')::text;
  assert (r->'career'->>'boundary_paid')::int = 10, 'short of the boundary: all 10 fares go to the operator';
  assert (r->'snapshot'->>'coins')::int = c0 + 10 - 10 + 40 + (r->>'level_reward')::int, 'coins credited: ' || (r->'snapshot'->>'coins');
  assert (r->'snapshot'->'career'->>0)::int = 1 and jsonb_array_length(r->'snapshot'->'career') = 20, 'snapshot career array';
  r := pg_temp.krun('k-1', 'c1s1', 0, 805, 10, 0, 0);
  assert (r->>'duplicate')::boolean, 'replayed run id';
  r := pg_temp.krun('k-2', 'c1s1', 0, 805, 10, 0, 0);
  assert (r->'career'->>'new_mask')::int = 0 and (r->'career'->>'star_coins')::int = 0, 'same stars again pay nothing';
  r := pg_temp.krun('k-wrong-route', 'c1s1', 1, 900, 99, 9, 0);
  assert (r->'career'->>'mask')::int = 0, 'wrong route earns nothing';
  r := pg_temp.krun('k-3', 'c1s1', 0, 805, 45, 3, 0);
  assert (r->'career'->>'boundary_paid')::int = 25, 'boundary 25 out of 45 fares';
  assert (r->'career'->>'mask')::int = 7 and (r->'career'->>'new_mask')::int = 6 and (r->'career'->>'star_coins')::int = 80, 'boundary + goal: ' || (r->'career')::text;
  r := pg_temp.krun('k-4', 'c1s2', 0, 1000, 60, 0, 2);
  assert (r->'career'->>'mask')::int = 3, 'two hits misses the at-most-1-hit goal';
  r := pg_temp.krun('k-5', 'c1s2', 0, 1000, 60, 0, 1);
  assert (r->'career'->>'new_mask')::int = 4 and (r->'career'->>'new_rank')::int = 1, '6 stars = non-pro license: ' || (r->'career')::text;
  assert (r->'career'->>'rank_coins')::int = 500 and (r->'career'->>'rank_gems')::int = 10, 'license reward';
  assert (r->'snapshot'->>'career_rank')::int = 1 and (r->'snapshot'->>'gems')::int = 10, 'rank + gems in snapshot';
  r := pg_temp.krun('k-6', 'c2s1', 4, 1300, 90, 0, 0);
  assert (r->'career'->>'mask')::int = 7, 'chapter 2 open: ' || (r->'career')::text;
  -- road coins never pay the boundary; diesel comes out of the fares first
  c0 := (r->'snapshot'->>'coins')::int;
  r := pg_temp.mrun('k-road', 'c1s1', 0, 805, 500, 0, 0);
  assert (r->'career'->>'mask')::int & 2 = 0 and (r->'career'->>'boundary_paid')::int = 0, 'road coins are not fares: ' || (r->'career')::text;
  assert (r->'snapshot'->>'coins')::int = c0 + 500 + (r->>'level_reward')::int, 'all road coins kept';
  c0 := (r->'snapshot'->>'coins')::int;
  r := pg_temp.mrun('k-gas', 'c1s1', 0, 805, 100, 40, 30);
  assert (r->'career'->>'mask')::int & 2 = 2, 'boundary star: 40 fares >= 25';
  assert (r->'career'->>'boundary_paid')::int = 10, 'only 10 left after 30 of diesel: ' || (r->'career')::text;
  assert (r->'snapshot'->>'coins')::int = c0 + 100 + 40 - 30 - 10 + (r->>'level_reward')::int, 'road + fares - gas - boundary';
  r := pg_temp.krun('k-7', 'nope', 0, 900, 10, 0, 0);
  assert r->'career'->>'shift' = '', 'unknown shift = free ride';
  r := pg_temp.krun('k-8', '', 0, 900, 10, 0, 0);
  assert (r->>'accepted')::boolean and r->'career'->>'shift' = '', 'free ride';
  begin perform jr_private.career_apply(auth.uid(), '{}'::jsonb); assert false, 'private';
  exception when insufficient_privilege then null; end;
  assert (select count(*) from public.jr_career) = 3, 'player reads own career rows';
  raise notice 'ok  career: terminal required, boundary + goal stars, unlock order, license rewards, replay-safe';
end $$;
reset role;
do $$ begin
  assert (select sum(amount) from public.jr_transactions where user_id = 'dddddddd-0000-0000-0000-00000000000a' and currency = 'gems')
       = (select gems from public.jr_profiles where user_id = 'dddddddd-0000-0000-0000-00000000000a'), 'gems ledger balances';
  assert (select sum(amount) from public.jr_transactions where user_id = 'dddddddd-0000-0000-0000-00000000000a' and currency = 'coins')
       = (select coins from public.jr_profiles where user_id = 'dddddddd-0000-0000-0000-00000000000a'), 'coins ledger balances';
  raise notice 'ok  career: ledger balances';
end $$;
