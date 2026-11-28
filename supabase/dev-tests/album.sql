-- Sticker album: one postcard per run of the run's route after 800 m, duplicates pay, sets once.
\set ON_ERROR_STOP on
\pset tuples_only on
\pset format unaligned
\set u 'a1b0a1b0-0000-0000-0000-00000000000a'
insert into auth.users (id) values (:'u');
create function pg_temp.as_user(u uuid) returns void language sql as $$ select set_config('request.jwt.claim.sub', u::text, false) $$;
create function pg_temp.run(id text, env int, dist real, card int) returns jsonb language sql as $$
  select public.jr_submit_run(jsonb_build_object('run_id', id, 'seed', 5, 'env', env, 'duration', dist / 10, 'distance', dist, 'coins', 5, 'score', 50,
    'passengers', 1, 'deliveries', 1, 'power_ups', 0, 'smooth_humps', 0, 'wet_distance', 0, 'refuels', 0, 'hits', 0, 'clean_distance', 0, 'best_streak', 0,
    'daily', false, 'day_key', 0, 'jeepney', 'pilipinas', 'gems', 0, 'mode', '', 'postcard', card)) $$;
grant execute on all functions in schema pg_temp to authenticated;
select pg_temp.as_user(:'u') \g /dev/null
set role authenticated;
do $$ declare r jsonb; c0 int; begin
  perform pg_temp.run('al-1', 0, 1000, 2);
  r := public.jr_album_claim_run('al-1');
  assert (r->>'ok')::boolean and (r->>'new')::boolean and (r->>'card')::int = 2, 'Rizal Park on Manila: ' || r::text;
  r := public.jr_album_claim_run('al-1');
  assert (r->>'duplicate')::boolean, 'once per run';
  perform pg_temp.run('al-2', 0, 1000, 22);
  r := public.jr_album_claim_run('al-2');
  assert r->>'reason' = 'card', 'a Cebu card from a Manila run is refused';
  perform pg_temp.run('al-3', 0, 500, 1);
  r := public.jr_album_claim_run('al-3');
  assert r->>'reason' = 'card', 'too short';
  c0 := (public.jr_get_state()->>'coins')::int;
  perform pg_temp.run('al-4', 0, 1000, 2);
  r := public.jr_album_claim_run('al-4');
  assert not (r->>'new')::boolean and (public.jr_get_state()->>'coins')::int >= c0 + 20, 'duplicate pays 20';
  r := public.jr_album_claim_set(0);
  assert r->>'reason' = 'not_done', 'set needs all four';
  perform pg_temp.run('al-5', 0, 1000, 0); perform public.jr_album_claim_run('al-5');
  perform pg_temp.run('al-6', 0, 1000, 1); perform public.jr_album_claim_run('al-6');
  perform pg_temp.run('al-7', 0, 1000, 3); perform public.jr_album_claim_run('al-7');
  r := public.jr_album_claim_set(0);
  assert (r->>'ok')::boolean, 'Manila set: ' || r::text;
  r := public.jr_album_claim_set(0);
  assert r->>'reason' = 'claimed', 'once';
  r := public.jr_album_status();
  assert jsonb_array_length(r->'cards') = 4 and (r->'sets') @> '[0]', 'status: ' || r::text;
  r := public.jr_album_claim_set(8);
  assert r->>'reason' = 'not_done', 'whole album';
  begin perform * from public.jr_album; assert false, 'no direct reads';
  exception when insufficient_privilege then null; end;
  raise notice 'ok  album: route-checked postcards, duplicates pay, sets once';
end $$;
reset role;
