-- Weekly leagues: join on the first scored run, groups of 30 per tier, settle top 7 up / bottom 5 down, rewards.
\set ON_ERROR_STOP on
\pset tuples_only on
\pset format unaligned
create function pg_temp.as_user(u uuid) returns void language sql as $$ select set_config('request.jwt.claim.sub', u::text, false) $$;
create function pg_temp.uid(i int) returns uuid language sql immutable as $$ select ('1eae0000-0000-0000-0000-' || lpad(i::text, 12, '0'))::uuid $$;
grant execute on all functions in schema pg_temp to authenticated;
insert into auth.users (id) select pg_temp.uid(i) from generate_series(1, 45) i;
select jr_private.ensure_profile(pg_temp.uid(i)) from generate_series(1, 45) i \g /dev/null
-- last week: 12 drivers in one Bronze group with scores 1200, 1100, ... 100
do $$ declare wk int := jr_private.week_key(jr_private.ph_date()) - 1; g bigint; begin
  insert into public.jr_league_groups (week, tier) values (wk, 0) returning id into g;
  for i in 1..12 loop
    insert into public.jr_league_members (user_id, week, tier, group_id) values (pg_temp.uid(i), wk, 0, g);
    insert into public.jr_scores (user_id, board, score, distance) values (pg_temp.uid(i), 'week:' || wk, 1300 - i * 100, 100);
  end loop;
end $$;
set role authenticated;
select pg_temp.as_user(pg_temp.uid(1)) \g /dev/null
do $$ declare r jsonb; c0 int; begin
  r := public.jr_league_status();
  assert not (r->>'joined')::boolean and (r->>'tier')::int = 1, 'winner moves up to Silver, not joined before playing: ' || r::text;
  assert (r->'last'->>'rank')::int = 1 and (r->'last'->>'reward')::int = 500 and (r->'last'->>'result_tier')::int = 1, 'last week: ' || r::text;
  c0 := (public.jr_get_state()->>'coins')::int;
  r := public.jr_league_claim();
  assert (r->>'ok')::boolean and (r->'snapshot'->>'coins')::int = c0 + 500, 'reward: ' || r::text;
  r := public.jr_league_claim();
  assert r->>'reason' = 'claimed', 'once';
end $$;
select pg_temp.as_user(pg_temp.uid(12)) \g /dev/null
do $$ declare r jsonb; begin
  r := public.jr_league_status();
  assert (r->'last'->>'result_tier')::int = 0 and (r->'last'->>'reward')::int = 0, 'Bronze last place stays Bronze: ' || r::text;
end $$;
reset role;
-- this week: 45 drivers play. Last week's top 7 (drivers 1-7) are Silver now; 38 Bronze drivers fill a group of 30 and a second one
do $$ declare wk int := jr_private.week_key(jr_private.ph_date()); begin
  for i in 1..45 loop insert into public.jr_scores (user_id, board, score, distance) values (pg_temp.uid(i), 'week:' || wk, 1000 + i, 100); end loop;
end $$;
set role authenticated;
do $$ declare r jsonb; begin
  for i in 2..45 loop perform pg_temp.as_user(pg_temp.uid(i)); perform public.jr_league_status(); end loop;
  perform pg_temp.as_user(pg_temp.uid(1));
  r := public.jr_league_status();
  assert (r->>'joined')::boolean and (r->>'tier')::int = 1 and jsonb_array_length(r->'rows') = 7 and (r->>'rank')::int = 7, 'Silver group of last week''s top 7: ' || left(r::text, 200);
  perform pg_temp.as_user(pg_temp.uid(37));
  r := public.jr_league_status();
  assert (r->>'tier')::int = 0 and jsonb_array_length(r->'rows') = 30 and (r->>'rank')::int = 1, 'full Bronze group of 30: ' || left(r::text, 200);
  perform pg_temp.as_user(pg_temp.uid(45));
  r := public.jr_league_status();
  assert jsonb_array_length(r->'rows') = 8, 'the rest open a second group: ' || left(r::text, 200);
  begin perform * from public.jr_league_members; assert false, 'no direct reads';
  exception when insufficient_privilege then null; end;
  raise notice 'ok  leagues: join on first run, groups of 30 per tier, top 7 up / bottom 5 down, rewards once';
end $$;
reset role;
