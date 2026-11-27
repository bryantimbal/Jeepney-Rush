-- Barangay vs Barangay: create / join by code, one barangay each, 3-day switch cooldown,
-- weekly team score = top 10 members' weekly best, name checks, champion of last week.
\set ON_ERROR_STOP on
\pset tuples_only on
\pset format unaligned
\set a 'b0b0b0b0-0000-0000-0000-00000000000a'
\set b 'b0b0b0b0-0000-0000-0000-00000000000b'
\set c 'b0b0b0b0-0000-0000-0000-00000000000c'
delete from public.jr_scores;
insert into auth.users (id) values (:'a'), (:'b'), (:'c');
create function pg_temp.as_user(u uuid) returns void language sql as $$ select set_config('request.jwt.claim.sub', u::text, false) $$;
grant execute on all functions in schema pg_temp to authenticated;
select jr_private.ensure_profile(:'a'), jr_private.ensure_profile(:'b'), jr_private.ensure_profile(:'c') \g /dev/null
-- weekly scores (this week): a 5000, b 3000, c 4000
insert into public.jr_scores (user_id, board, score, distance) select :'a', 'week:' || jr_private.week_key(jr_private.ph_date()), 5000, 1000;
insert into public.jr_scores (user_id, board, score, distance) select :'b', 'week:' || jr_private.week_key(jr_private.ph_date()), 3000, 900;
insert into public.jr_scores (user_id, board, score, distance) select :'c', 'week:' || jr_private.week_key(jr_private.ph_date()), 4000, 900;
set role authenticated;
select pg_temp.as_user(:'a') \g /dev/null
create temp table codes (who text, code text);
grant all on codes to authenticated;
do $$ declare r jsonb; begin
  r := public.jr_barangay_create('Gago FC', 'Quezon City');
  assert r->>'reason' = 'invalid_name', 'bad words blocked';
  r := public.jr_barangay_create('X', 'Quezon City');
  assert r->>'reason' = 'invalid_name', 'too short';
  r := public.jr_barangay_create('Brgy. San Roque', 'Quezon City');
  assert (r->>'ok')::boolean and r->>'code' like 'BRGY%', 'created: ' || r::text;
  insert into codes values ('sanroque', r->>'code');
  r := public.jr_barangay_mine();
  assert (r->>'found')::boolean and r->>'name' = 'Brgy. San Roque' and jsonb_array_length(r->'members') = 1 and (r->>'score')::int = 5000, 'mine: ' || r::text;
  r := public.jr_barangay_create('Brgy. Maligaya', 'Cebu');
  assert r->>'reason' = 'cooldown', 'no team hopping right after creating';
end $$;
select pg_temp.as_user(:'b') \g /dev/null
do $$ declare r jsonb; begin
  r := public.jr_barangay_join((select code from codes where who = 'sanroque'));
  assert (r->>'ok')::boolean and r->>'name' = 'Brgy. San Roque', 'joined by code';
  r := public.jr_barangay_join('BRGYZZZZ');
  assert r->>'reason' = 'unknown_code';
end $$;
select pg_temp.as_user(:'c') \g /dev/null
do $$ declare r jsonb; begin
  r := public.jr_barangay_create('Barangay Malinis', 'Davao');
  assert (r->>'ok')::boolean;
  r := public.jr_barangay_board(50);
  assert jsonb_array_length(r->'rows') = 2, 'two barangays: ' || r::text;
  assert r->'rows'->0->>'name' = 'Brgy. San Roque' and (r->'rows'->0->>'score')::int = 8000 and (r->'rows'->0->>'members')::int = 2, 'team score = members sum';
  assert (r->'me'->>'rank')::int = 2 and (r->'me'->>'score')::int = 4000, 'my barangay rank';
  begin perform * from public.jr_barangays; assert false, 'no direct reads';
  exception when insufficient_privilege then null; end;
end $$;
reset role;
-- the 3-day cooldown passes: c may switch; when c leaves, the empty barangay disappears
update public.jr_profiles set barangay_changed_at = now() - interval '4 days' where user_id = 'b0b0b0b0-0000-0000-0000-00000000000c';
-- last week's champion
insert into public.jr_scores (user_id, board, score, distance) select 'b0b0b0b0-0000-0000-0000-00000000000c', 'week:' || (jr_private.week_key(jr_private.ph_date()) - 1), 9000, 2000;
set role authenticated;
select pg_temp.as_user(:'c') \g /dev/null
do $$ declare r jsonb; begin
  r := public.jr_barangay_board(50);
  assert r->'champion'->>'name' = 'Barangay Malinis', 'barangay of last week: ' || r::text;
  r := public.jr_barangay_join((select code from codes where who = 'sanroque'));
  assert (r->>'ok')::boolean, 'switch after the cooldown';
  r := public.jr_barangay_board(50);
  assert jsonb_array_length(r->'rows') = 1 and (r->'rows'->0->>'score')::int = 12000, 'empty barangay gone, 3 members now';
  r := public.jr_barangay_leave();
  r := public.jr_barangay_mine();
  assert not (r->>'found')::boolean and r->>'can_switch_at' is not null, 'left; cooldown shown';
  raise notice 'ok  barangays: create / join by code, team = top 10 weekly, cooldown, champion, name checks, no direct access';
end $$;
reset role;
