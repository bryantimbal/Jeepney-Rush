-- Daily Challenge ghosts: best daily run per player per day; race the #1, the next one up, or a friend.
\set ON_ERROR_STOP on
\pset tuples_only on
\pset format unaligned
\set a 'dadadada-0000-0000-0000-00000000000a'
\set b 'dadadada-0000-0000-0000-00000000000b'
\set c 'dadadada-0000-0000-0000-00000000000c'
insert into auth.users (id) values (:'a'), (:'b'), (:'c');
create function pg_temp.drun(id text, score int, dist real, daily boolean) returns jsonb language plpgsql as $$
declare ch jsonb := public.jr_daily_challenge(); begin
  return public.jr_submit_run(jsonb_build_object('run_id', id, 'seed', (ch->>'seed')::int, 'env', (ch->>'env')::int, 'duration', 100, 'distance', dist,
    'coins', 10, 'score', score, 'passengers', 2, 'deliveries', 2, 'power_ups', 0, 'smooth_humps', 0, 'wet_distance', 0, 'refuels', 0, 'hits', 0,
    'clean_distance', 0, 'best_streak', 0, 'daily', daily, 'day_key', (ch->>'day_key')::int, 'jeepney', 'pilipinas', 'gems', 0, 'mode', ''));
end $$;
create function pg_temp.path(dist real) returns int[] language sql as $$
  select array_agg(least(i * 60, (dist * 10)::int) order by i) from generate_series(0, ceil(dist * 10 / 60)::int) i $$;
create function pg_temp.save(id text, dist real) returns jsonb language sql as $$
  select public.jr_save_ghost(id, pg_temp.path(dist), (select array_agg(0) from generate_series(1, array_length(pg_temp.path(dist), 1)))) $$;
create function pg_temp.as_user(u uuid) returns void language sql as $$ select set_config('request.jwt.claim.sub', u::text, false) $$;
grant execute on all functions in schema pg_temp to authenticated;
set role authenticated;
-- a: 4000, b: 2500, c: 1000 (c is b's friend)
select pg_temp.as_user(:'a') \g /dev/null
do $$ declare r jsonb; begin
  r := public.jr_daily_ghost_pick('top');
  assert not (r->>'found')::boolean, 'nobody yet';
  perform pg_temp.drun('dg-a1', 4000, 900, true);
  r := pg_temp.save('dg-a1', 900);
  assert (r->>'ok')::boolean and (r->>'daily')::boolean and r->>'kept' = 'new', 'daily ghost saved: ' || r::text;
  perform pg_temp.drun('dg-a2', 3500, 800, true);
  r := pg_temp.save('dg-a2', 800);
  assert r->>'kept' = 'older', 'keeps the best of the day';
  r := pg_temp.save('dg-a1', 500);
  assert r->>'reason' = 'distance', 'path must end where the run ended';
  -- a normal run still makes a route ghost, not a daily one
  perform pg_temp.drun('dg-a3', 9000, 900, false);
  r := pg_temp.save('dg-a3', 900);
  assert (r->>'ok')::boolean and r->>'daily' is null, 'free ride ghost: ' || r::text;
  r := public.jr_daily_ghost_pick('top');
  assert not (r->>'found')::boolean, 'your own ghost is not a rival';
end $$;
select pg_temp.as_user(:'c') \g /dev/null
do $$ begin perform pg_temp.drun('dg-c1', 1000, 300, true); perform pg_temp.save('dg-c1', 300); end $$;
reset role;
insert into public.jr_friends (user_id, friend_id) values (:'b', :'c'), (:'c', :'b');
set role authenticated;
select pg_temp.as_user(:'b') \g /dev/null
do $$ declare r jsonb; begin
  perform pg_temp.drun('dg-b1', 2500, 700, true);
  perform pg_temp.save('dg-b1', 700);
  r := public.jr_daily_ghost_pick('top');
  assert (r->>'found')::boolean and (r->>'score')::int = 4000 and (r->>'rank')::int = 1 and (r->>'mine')::int = 2500, 'top: ' || (r - 'd' - 'x')::text;
  assert jsonb_array_length(r->'d') = jsonb_array_length(r->'x') and jsonb_array_length(r->'d') > 10, 'path';
  assert (r->>'seed')::int = (public.jr_daily_challenge()->>'seed')::int, 'today''s road';
  r := public.jr_daily_ghost_pick('rival');
  assert (r->>'score')::int = 4000, 'next one up';
  r := public.jr_daily_ghost_pick('friend');
  assert (r->>'found')::boolean and (r->>'score')::int = 1000, 'friend c: ' || left(r::text, 200);
end $$;
select pg_temp.as_user(:'c') \g /dev/null
do $$ declare r jsonb; begin
  r := public.jr_daily_ghost_pick('rival');
  assert (r->>'score')::int = 2500 and (r->>'rank')::int = 2, 'c''s rival is b (closest above): ' || left(r::text, 120);
  r := public.jr_daily_ghost_pick('friend');
  assert (r->>'score')::int = 2500, 'c''s friend b';
end $$;
select pg_temp.as_user(:'a') \g /dev/null
do $$ declare r jsonb; begin
  r := public.jr_daily_ghost_pick('friend');
  assert not (r->>'found')::boolean, 'a has no friends';
  r := public.jr_daily_ghost_pick('rival');
  assert (r->>'score')::int = 2500, 'already #1: races the next best';
  begin perform * from public.jr_daily_ghosts; assert false, 'no direct reads';
  exception when insufficient_privilege then null; end;
  raise notice 'ok  daily ghosts: best per day, #1 / rival / friend, free ride ghosts unchanged, no direct access';
end $$;
reset role;
