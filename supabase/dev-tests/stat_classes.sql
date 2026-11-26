-- Stat-class rankings: runs post to the board of the jeepney's class (from the catalog and the
-- player's inventory, not from the client), and jr_leaderboard filters by class.
\set ON_ERROR_STOP on
\pset tuples_only on
\pset format unaligned
\set a 'cccccccc-0000-0000-0000-00000000000a'
\set b 'cccccccc-0000-0000-0000-00000000000b'
delete from public.jr_scores; -- earlier tests' scores would share these boards
insert into auth.users (id) values (:'a'), (:'b');
create function pg_temp.crun(id text, jeep text, score int) returns jsonb language sql as $$
  select public.jr_submit_run(jsonb_build_object('run_id', id, 'seed', 1, 'env', 0, 'duration', 120, 'distance', 1200, 'coins', 0, 'score', score,
    'passengers', 0, 'deliveries', 0, 'power_ups', 0, 'smooth_humps', 0, 'wet_distance', 0, 'refuels', 0, 'hits', 0,
    'clean_distance', 0, 'best_streak', 0, 'daily', false, 'day_key', 0, 'jeepney', jeep, 'gems', 0)) $$;
create function pg_temp.as_user(u uuid) returns void language sql as $$ select set_config('request.jwt.claim.sub', u::text, false) $$;
grant execute on all functions in schema pg_temp to authenticated;
do $$ begin
  assert (select stat_class from public.jr_catalog_jeepneys where id = 'pilipinas') = 0, 'stock jeepney is Standard';
  assert (select stat_class from public.jr_catalog_jeepneys where id = 'neon') = 1, 'neon is Sport';
  assert (select stat_class from public.jr_catalog_jeepneys where id = 'gold') = 2, 'gold is Elite';
end $$;
-- B owns the Gold VIP (bought earlier)
select pg_temp.as_user(:'b') \g /dev/null
select jr_private.ensure_profile(:'b') \g /dev/null
insert into public.jr_inventory (user_id, item_kind, item_id, level) values (:'b', 'jeepney', 'gold', 1) on conflict do nothing;
set role authenticated;
select pg_temp.as_user(:'a') \g /dev/null
do $$ declare r jsonb; begin
  r := pg_temp.crun('cls-a1', 'pilipinas', 500);
  assert (r->>'class')::int = 0 and (r->>'class_rank_all')::int = 1, 'free jeepney -> Standard board: ' || r::text;
  r := pg_temp.crun('cls-a2', 'gold', 900);
  assert (r->>'class')::int = 0, 'naming a jeepney you do not own does not change your class';
end $$;
select pg_temp.as_user(:'b') \g /dev/null
do $$ declare r jsonb; begin
  r := pg_temp.crun('cls-b1', 'gold', 800);
  assert (r->>'class')::int = 2 and (r->>'class_rank_all')::int = 1, 'gold -> Elite board: ' || r::text;
  assert (r->>'rank_all')::int = 2, 'overall board still ranks everyone';
  r := pg_temp.crun('cls-b2', 'pilipinas', 300);
  assert (r->>'class')::int = 0 and (r->>'class_rank_all')::int = 2, 'driving a stock jeepney counts on the Standard board';
  r := pg_temp.crun('cls-b3', 'neon', 950);
  assert (r->>'class')::int = 2, 'unowned jeepney -> highest class owned (no sandbagging)';
end $$;
do $$ declare l jsonb; begin
  l := public.jr_leaderboard('all', 'global', 50, 2);
  assert jsonb_array_length(l->'rows') = 1 and (l->'rows'->0->>'score')::int = 950 and l->>'board' = 'all:c2', 'Elite board: ' || l::text;
  l := public.jr_leaderboard('all', 'global', 50, 0);
  assert jsonb_array_length(l->'rows') = 2 and (l->'rows'->0->>'score')::int = 900 and (l->'rows'->1->>'score')::int = 300, 'Standard board: ' || l::text;
  l := public.jr_leaderboard('week', 'global', 50, 1);
  assert jsonb_array_length(l->'rows') = 0, 'nobody on the Sport board';
  l := public.jr_leaderboard(p_board => 'all', p_scope => 'global', p_limit => 50);
  assert jsonb_array_length(l->'rows') = 2 and l->>'board' = 'all' and (l->>'class')::int = -1, 'old 3-argument call = everyone';
  l := public.jr_leaderboard('all', 'global', 50, 7);
  assert l->>'board' = 'all', 'unknown class = everyone';
  begin perform jr_private.run_class(auth.uid(), 'gold'); assert false, 'players must not call private functions';
  exception when insufficient_privilege then null; end;
  raise notice 'ok  stat classes: server-side class, class boards, no sandbagging, overall board kept';
end $$;
reset role;
