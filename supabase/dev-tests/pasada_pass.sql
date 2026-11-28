-- Pasada Pass: XP from this month's accepted runs, tiers, free / premium claims, premium for gems.
\set ON_ERROR_STOP on
\pset tuples_only on
\pset format unaligned
\set u 'aa55aa55-0000-0000-0000-00000000000a'
insert into auth.users (id) values (:'u');
create function pg_temp.as_user(u uuid) returns void language sql as $$ select set_config('request.jwt.claim.sub', u::text, false) $$;
grant execute on all functions in schema pg_temp to authenticated;
select jr_private.ensure_profile(:'u') \g /dev/null
-- 20 runs this month of 3 km with 10 deliveries (30 + 20 = 50 xp each) = 1000 xp = tier 10; old and rejected runs don't count
insert into public.jr_runs (run_id, user_id, accepted, report) select 'pp-' || i, :'u', true, jsonb_build_object('distance', 3000, 'deliveries', 10) from generate_series(1, 20) i;
insert into public.jr_runs (run_id, user_id, accepted, report, created_at) values ('pp-old', :'u', true, jsonb_build_object('distance', 9000, 'deliveries', 50), now() - interval '62 days');
insert into public.jr_runs (run_id, user_id, accepted, report) values ('pp-bad', :'u', false, jsonb_build_object('distance', 9000, 'deliveries', 50));
select pg_temp.as_user(:'u') \g /dev/null
set role authenticated;
do $$ declare r jsonb; c0 int; begin
  r := public.jr_pass_status();
  assert (r->>'xp')::int = 1000 and (r->>'tier')::int = 10 and not (r->>'premium')::boolean, 'status: ' || r::text;
  c0 := (public.jr_get_state()->>'coins')::int;
  r := public.jr_pass_claim(1, false);
  assert (r->>'ok')::boolean and (r->'snapshot'->>'coins')::int = c0 + 55, 'tier 1 free = 55 coins: ' || r::text;
  r := public.jr_pass_claim(1, false);
  assert r->>'reason' = 'claimed', 'once';
  r := public.jr_pass_claim(11, false);
  assert r->>'reason' = 'not_done', 'tier 11 not reached';
  r := public.jr_pass_claim(1, true);
  assert r->>'reason' = 'premium', 'premium locked';
  r := public.jr_pass_buy_premium();
  assert r->>'reason' = 'gems', 'needs 300 gems';
end $$;
reset role;
update public.jr_profiles set gems = 310 where user_id = :'u';
update public.jr_runs set report = jsonb_build_object('distance', 99000, 'deliveries', 99) where run_id like 'pp-%' and run_id not in ('pp-old', 'pp-bad');
insert into public.jr_runs (run_id, user_id, accepted, report) select 'pp2-' || i, :'u', true, jsonb_build_object('distance', 3000, 'deliveries', 10) from generate_series(1, 40) i;
set role authenticated;
do $$ declare r jsonb; begin
  r := public.jr_pass_buy_premium();
  assert (r->>'ok')::boolean and (r->'snapshot'->>'gems')::int = 10, 'premium for 300 gems: ' || r::text;
  r := public.jr_pass_status();
  assert (r->>'tier')::int = 30 and (r->>'premium')::boolean, 'tier 30 (xp capped per run): ' || r::text;
  r := public.jr_pass_claim(30, true);
  assert (r->>'ok')::boolean and (r->'snapshot'->'cosmetics') ? 'paint_pasada', 'premium paint: ' || r::text;
  r := public.jr_pass_claim(5, true);
  assert (r->>'ok')::boolean and (r->'snapshot'->>'gems')::int = 25, '15 gems: ' || r::text;
  r := public.jr_buy_cosmetic('ws_pasada');
  assert r->>'reason' = 'not_for_sale', 'pass items are not sold';
  begin perform * from public.jr_pass; assert false, 'no direct reads';
  exception when insufficient_privilege then null; end;
  raise notice 'ok  pasada pass: monthly xp from runs, tiers, free/premium claims, premium for gems';
end $$;
reset role;
