-- Road gems credited by jr_submit_run: per-run cap by distance, 15 per PH day, never negative.
\set ON_ERROR_STOP on
\pset tuples_only on
\pset format unaligned
\set g 'aaaaaaaa-0000-0000-0000-00000000000a'
insert into auth.users (id) values (:'g');
select set_config('request.jwt.claim.sub', :'g', false) \g /dev/null
create function pg_temp.grun(id text, dist real, gems int) returns jsonb language sql as $$
  select public.jr_submit_run(jsonb_build_object('run_id', id, 'seed', 1, 'env', 0, 'duration', dist / 10, 'distance', dist, 'coins', 0, 'score', 100,
    'passengers', 0, 'deliveries', 0, 'power_ups', 0, 'smooth_humps', 0, 'wet_distance', 0, 'refuels', 0, 'hits', 0,
    'clean_distance', 0, 'best_streak', 0, 'daily', false, 'day_key', 0, 'jeepney', 'pilipinas', 'gems', gems)) $$;
grant execute on all functions in schema pg_temp to authenticated;
do $$ begin
  assert jr_private.road_gems_max(250) = 0 and jr_private.road_gems_max(300) = 1 and jr_private.road_gems_max(2000) = 3
     and jr_private.road_gems_max(99999) = 5, 'per-run cap matches C# RoadGems.MaxForDistance';
end $$;
set role authenticated;
do $$ declare r jsonb; total int := 0; i int; begin
  begin perform jr_private.road_gems_max(1); assert false, 'players must not call private functions';
  exception when insufficient_privilege then null; end;
  r := pg_temp.grun('gem-short', 250, 3);
  assert (r->>'credited_gems')::int = 0, 'no gems from a 250 m run';
  r := pg_temp.grun('gem-neg', 2000, -4);
  assert (r->>'credited_gems')::int = 0, 'negative gems ignored';
  r := pg_temp.grun('gem-a', 2000, 99);
  assert (r->>'credited_gems')::int = 3, 'clamped to 3 for 2 km: ' || r::text;
  assert (r->'snapshot'->>'gems')::int = 3, 'wallet shows the gems';
  r := pg_temp.grun('gem-a', 2000, 99);
  assert (r->'snapshot'->>'gems')::int = 3, 'replayed run id pays nothing again';
  total := 3;
  for i in 1 .. 4 loop
    r := pg_temp.grun('gem-long-' || i, 4000, 5);
    total := total + (r->>'credited_gems')::int;
  end loop;
  assert total = 15, 'daily cap of 15 gems: got ' || total;
  assert (r->>'credited_gems')::int = 0, 'nothing once the cap is reached';
  raise notice 'ok  road gems: per-run cap, daily cap of 15, replay-safe, ledger entry per credit';
end $$;
reset role;
do $$ begin
  assert (select gems from jr_profiles where user_id = 'aaaaaaaa-0000-0000-0000-00000000000a')
       = (select sum(amount) from jr_transactions where user_id = 'aaaaaaaa-0000-0000-0000-00000000000a' and currency = 'gems'), 'gems equal the ledger';
end $$;
