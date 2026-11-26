-- Ghost Race: plausible paths only, best per route + class, rivals from the same class.
\set ON_ERROR_STOP on
\pset tuples_only on
\pset format unaligned
\set a 'abababab-0000-0000-0000-00000000000a'
\set b 'abababab-0000-0000-0000-00000000000b'
insert into auth.users (id) values (:'a'), (:'b');
create function pg_temp.grun(id text, score int, dist real, mode text) returns jsonb language sql as $$
  select public.jr_submit_run(jsonb_build_object('run_id', id, 'seed', 4242, 'env', 0, 'duration', 100, 'distance', dist, 'coins', 10, 'score', score,
    'passengers', 2, 'deliveries', 2, 'power_ups', 0, 'smooth_humps', 0, 'wet_distance', 0, 'refuels', 0, 'hits', 0,
    'clean_distance', 0, 'best_streak', 0, 'daily', false, 'day_key', 0, 'jeepney', 'pilipinas', 'gems', 0, 'mode', mode)) $$;
-- a straight path: 0, 50, 100 ... dm up to dist
create function pg_temp.path(dist real) returns int[] language sql as $$
  select array_agg(least(i * 60, (dist * 10)::int) order by i) from generate_series(0, ceil(dist * 10 / 60)::int) i $$;
create function pg_temp.zeros(n int) returns int[] language sql as $$ select array_agg(0) from generate_series(1, n) $$;
create function pg_temp.as_user(u uuid) returns void language sql as $$ select set_config('request.jwt.claim.sub', u::text, false) $$;
grant execute on all functions in schema pg_temp to authenticated;
set role authenticated;
select pg_temp.as_user(:'a') \g /dev/null
do $$ declare r jsonb; p int[]; begin
  perform pg_temp.grun('g-a1', 3000, 900, '');
  p := pg_temp.path(900);
  r := public.jr_save_ghost('g-a1', p, pg_temp.zeros(array_length(p, 1)));
  assert (r->>'ok')::boolean and r->>'kept' = 'new', 'saved: ' || r::text;
  r := public.jr_save_ghost('g-a1', pg_temp.path(500), pg_temp.zeros(array_length(pg_temp.path(500), 1)));
  assert r->>'reason' = 'distance', 'must end where the run ended: ' || r::text;
  p := pg_temp.path(900); p[5] := p[4] + 999;
  r := public.jr_save_ghost('g-a1', p, pg_temp.zeros(array_length(p, 1)));
  assert r->>'reason' = 'step', 'too fast';
  r := public.jr_save_ghost('nope', pg_temp.path(900), pg_temp.zeros(array_length(pg_temp.path(900), 1)));
  assert r->>'reason' = 'run', 'unknown run';
  perform pg_temp.grun('g-a-rush', 9000, 900, 'rush');
  r := public.jr_save_ghost('g-a-rush', pg_temp.path(900), pg_temp.zeros(array_length(pg_temp.path(900), 1)));
  assert r->>'reason' = 'mode', 'no ghosts from rush hour';
  perform pg_temp.grun('g-a2', 2000, 900, '');
  r := public.jr_save_ghost('g-a2', pg_temp.path(900), pg_temp.zeros(array_length(pg_temp.path(900), 1)));
  assert r->>'kept' = 'older', 'keeps the best ghost';
  r := public.jr_ghost_pick(0, 0);
  assert not (r->>'found')::boolean, 'no rival yet (your own ghost is not a rival)';
end $$;
select pg_temp.as_user(:'b') \g /dev/null
do $$ declare r jsonb; begin
  r := public.jr_ghost_pick(0, 0);
  assert (r->>'found')::boolean and (r->>'score')::int = 3000 and (r->>'seed')::int = 4242, 'b races a: ' || left(r::text, 200);
  assert jsonb_array_length(r->'d') = jsonb_array_length(r->'x') and jsonb_array_length(r->'d') > 10, 'path';
  r := public.jr_ghost_pick(0, 2);
  assert not (r->>'found')::boolean, 'other classes are separate';
  begin perform * from public.jr_ghosts; assert false, 'no direct reads';
  exception when insufficient_privilege then null; end;
  raise notice 'ok  ghosts: plausible paths only, best per route + class, same-class rivals, no direct access';
end $$;
reset role;
