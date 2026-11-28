-- Friend gifts (once a day per friend, 10 claims a day) and the barangay weekly goal.
\set ON_ERROR_STOP on
\pset tuples_only on
\pset format unaligned
\set a '50c1a100-0000-0000-0000-00000000000a'
\set b '50c1a100-0000-0000-0000-00000000000b'
\set c '50c1a100-0000-0000-0000-00000000000c'
insert into auth.users (id) values (:'a'), (:'b'), (:'c');
select jr_private.ensure_profile(x) from unnest(array[:'a', :'b', :'c']::uuid[]) x \g /dev/null
insert into public.jr_friends (user_id, friend_id) values (:'a', :'b'), (:'b', :'a');
create function pg_temp.as_user(u uuid) returns void language sql as $$ select set_config('request.jwt.claim.sub', u::text, false) $$;
grant execute on all functions in schema pg_temp to authenticated;
set role authenticated;
select pg_temp.as_user(:'a') \g /dev/null
do $$ declare r jsonb; begin
  r := public.jr_gift_send('50c1a100-0000-0000-0000-00000000000b');
  assert (r->>'ok')::boolean, 'gift sent: ' || r::text;
  r := public.jr_gift_send('50c1a100-0000-0000-0000-00000000000b');
  assert r->>'reason' = 'already_today', 'once a day per friend';
  r := public.jr_gift_send('50c1a100-0000-0000-0000-00000000000c');
  assert r->>'reason' = 'not_friend', 'friends only';
end $$;
select pg_temp.as_user(:'b') \g /dev/null
do $$ declare r jsonb; c0 int; begin
  r := public.jr_gift_inbox();
  assert jsonb_array_length(r->'gifts') = 1, 'inbox: ' || r::text;
  c0 := (public.jr_get_state()->>'coins')::int;
  r := public.jr_gift_claim();
  assert (r->>'ok')::boolean and (r->'snapshot'->>'coins')::int = c0 + 60, 'free gas = 60 coins: ' || r::text;
  r := public.jr_gift_claim();
  assert r->>'reason' = 'nothing', 'claimed';
end $$;
reset role;
-- barangay goal: a and c in one barangay; 9,990 passengers this week, then 20 more
insert into public.jr_barangays (code, name, city, created_by) values ('BRGYGOAL', 'Brgy. Goal', 'Pasig', :'a');
insert into public.jr_barangay_members (user_id, barangay_id) select x, (select id from public.jr_barangays where code = 'BRGYGOAL') from unnest(array[:'a', :'c']::uuid[]) x;
insert into public.jr_runs (run_id, user_id, accepted, report) select 'bg-' || i, :'a', true, jsonb_build_object('passengers', 333) from generate_series(1, 30) i;
insert into public.jr_runs (run_id, user_id, accepted, report) values ('bg-x', :'b', true, jsonb_build_object('passengers', 5000));  -- not a member
set role authenticated;
select pg_temp.as_user(:'c') \g /dev/null
do $$ declare r jsonb; begin
  r := public.jr_barangay_goal_status();
  assert (r->>'member')::boolean and (r->>'progress')::int = 9990, 'members only: ' || r::text;
  r := public.jr_barangay_goal_claim();
  assert r->>'reason' = 'not_done', 'not yet';
end $$;
reset role;
insert into public.jr_runs (run_id, user_id, accepted, report) values ('bg-c', :'c', true, jsonb_build_object('passengers', 20));
set role authenticated;
do $$ declare r jsonb; begin
  r := public.jr_barangay_goal_claim();
  assert (r->>'ok')::boolean, 'goal reached, c claims: ' || r::text;
  r := public.jr_barangay_goal_claim();
  assert r->>'reason' = 'claimed', 'once a week';
  perform pg_temp.as_user('50c1a100-0000-0000-0000-00000000000a');
  r := public.jr_barangay_goal_claim();
  assert (r->>'ok')::boolean, 'every member claims';
  begin perform * from public.jr_gifts; assert false, 'no direct reads';
  exception when insufficient_privilege then null; end;
  raise notice 'ok  social: gifts once a day per friend, barangay weekly goal for every member';
end $$;
reset role;
