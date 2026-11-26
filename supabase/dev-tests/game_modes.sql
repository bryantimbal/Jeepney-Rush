-- Game modes: Rush Hour / Night Shift runs go on their own weekly boards, not the normal ones.
\set ON_ERROR_STOP on
\pset tuples_only on
\pset format unaligned
\set u 'ffffffff-0000-0000-0000-00000000000a'
delete from public.jr_scores;
insert into auth.users (id) values (:'u');
select set_config('request.jwt.claim.sub', :'u', false) \g /dev/null
create function pg_temp.mode_run(id text, mode text, dur real, score int) returns jsonb language sql as $$
  select public.jr_submit_run(jsonb_build_object('run_id', id, 'seed', 1, 'env', 0, 'duration', dur, 'distance', 1000, 'coins', 50, 'score', score,
    'passengers', 5, 'deliveries', 5, 'power_ups', 0, 'smooth_humps', 0, 'wet_distance', 0, 'refuels', 0, 'hits', 0,
    'clean_distance', 0, 'best_streak', 0, 'daily', false, 'day_key', 0, 'jeepney', 'pilipinas', 'gems', 0, 'mode', mode)) $$;
grant execute on all functions in schema pg_temp to authenticated;
set role authenticated;
do $$ declare r jsonb; l jsonb; begin
  r := pg_temp.mode_run('m-rush', 'rush', 182, 4000);
  assert r->>'mode' = 'rush' and (r->>'mode_rank')::int = 1 and (r->>'mode_class_rank')::int = 1, 'rush board: ' || r::text;
  assert (r->>'rank_all')::int = 0, 'not on the normal boards';
  l := public.jr_leaderboard('rush', 'global', 50);
  assert jsonb_array_length(l->'rows') = 1 and l->>'board' like 'rush:week:%', 'rush leaderboard: ' || l::text;
  r := pg_temp.mode_run('m-rush-long', 'rush', 600, 9999);
  assert r->>'mode' = '' and (r->>'mode_rank')::int = 0 and (r->>'rank_all')::int = 0, 'a 10-minute "rush hour" counts nowhere';
  assert (r->>'accepted')::boolean, 'the run itself still pays';
  r := pg_temp.mode_run('m-night', 'night', 400, 3000);
  assert r->>'mode' = 'night' and (r->>'mode_rank')::int = 1, 'night board';
  l := public.jr_leaderboard('night', 'global', 50, 0);
  assert jsonb_array_length(l->'rows') = 1, 'night class board';
  r := pg_temp.mode_run('m-free', '', 300, 2000);
  assert r->>'mode' = '' and (r->>'rank_all')::int = 1, 'free ride on the normal board';
  r := pg_temp.mode_run('m-ghost', 'ghost', 300, 2500);
  assert (r->>'rank_all')::int = 1, 'ghost race counts like a free ride';
  l := public.jr_leaderboard('all', 'global', 50);
  assert jsonb_array_length(l->'rows') = 1 and (l->'rows'->0->>'score')::int = 2500, 'normal board has only the free ride / ghost runs';
  raise notice 'ok  game modes: rush / night weekly boards, rush clock checked, normal boards untouched';
end $$;
reset role;
