-- Operator mode: license, slots, owned jeepneys only, server-clock earnings capped at 8 h, buy slots.
\set ON_ERROR_STOP on
\pset tuples_only on
\pset format unaligned
\set u 'f1ee7000-0000-0000-0000-00000000000a'
insert into auth.users (id) values (:'u');
create function pg_temp.as_user(u uuid) returns void language sql as $$ select set_config('request.jwt.claim.sub', u::text, false) $$;
grant execute on all functions in schema pg_temp to authenticated;
select pg_temp.as_user(:'u') \g /dev/null
set role authenticated;
do $$ declare r jsonb; begin
  r := public.jr_fleet_status();
  assert not (r->>'unlocked')::boolean and (r->>'slots')::int = 0, 'locked before the operator license: ' || r::text;
  r := public.jr_fleet_assign(0, 'pilipinas');
  assert r->>'reason' = 'slot', 'no slot yet';
end $$;
reset role;
update public.jr_profiles set career_rank = 3, coins = 5000 where user_id = :'u';
set role authenticated;
do $$ declare r jsonb; begin
  r := public.jr_fleet_assign(0, 'gold');
  assert r->>'reason' = 'not_owned', 'only owned jeepneys';
  r := public.jr_fleet_assign(0, 'pilipinas');
  assert (r->>'ok')::boolean, 'hired: ' || r::text;
  r := public.jr_fleet_assign(1, 'sanjuan');
  assert r->>'reason' = 'slot', 'one slot at rank 3';
  r := public.jr_fleet_buy_slot();
  assert (r->>'ok')::boolean and (r->'snapshot'->>'coins')::int = 2000, 'bought slot 2 for 3000';
  r := public.jr_fleet_assign(1, 'pilipinas');
  assert r->>'reason' = 'busy', 'a jeepney drives one route at a time';
  r := public.jr_fleet_assign(1, 'sanjuan');
  assert (r->>'ok')::boolean, 'second slot';
end $$;
reset role;
-- 3 hours ago, and 20 hours ago (capped at 8)
update public.jr_fleet set since = now() - interval '3 hours' where user_id = :'u' and slot = 0;
update public.jr_fleet set since = now() - interval '20 hours' where user_id = :'u' and slot = 1;
set role authenticated;
do $$ declare r jsonb; c0 int; begin
  r := public.jr_fleet_status();
  assert (r->'fleet'->0->>'pending')::int = 90 and (r->'fleet'->1->>'pending')::int = 240, 'pilipinas 30/h x 3 h, sanjuan capped at 8 h: ' || r::text;
  c0 := (public.jr_get_state()->>'coins')::int;
  r := public.jr_fleet_collect();
  assert (r->>'reward')::int = 330 and (r->'snapshot'->>'coins')::int = c0 + 330, 'collected: ' || r::text;
  r := public.jr_fleet_collect();
  assert not (r->>'ok')::boolean and (r->>'reward')::int = 0, 'clock restarted';
  begin perform * from public.jr_fleet; assert false, 'no direct reads';
  exception when insufficient_privilege then null; end;
  raise notice 'ok  operator: license, slots, owned only, 8 h cap, collect, buy slot';
end $$;
reset role;
