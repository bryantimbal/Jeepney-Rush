-- Paskong Pasada: parols per run (capped), Simbang Gabi nights, reward track, event-only items.
\set ON_ERROR_STOP on
\pset tuples_only on
\pset format unaligned
\set u 'cececece-0000-0000-0000-00000000000a'
insert into auth.users (id) values (:'u');
create function pg_temp.as_user(u uuid) returns void language sql as $$ select set_config('request.jwt.claim.sub', u::text, false) $$;
create function pg_temp.run(id text, dist real, parols int, carolers int, mode text) returns jsonb language sql as $$
  select public.jr_submit_run(jsonb_build_object('run_id', id, 'seed', 77, 'env', 0, 'duration', dist / 10, 'distance', dist, 'coins', 10, 'score', 100,
    'passengers', 3, 'deliveries', 3, 'power_ups', 0, 'smooth_humps', 0, 'wet_distance', 0, 'refuels', 0, 'hits', 0, 'clean_distance', 0, 'best_streak', 0,
    'daily', false, 'day_key', 0, 'jeepney', 'pilipinas', 'gems', 0, 'mode', mode, 'parols', parols, 'carolers', carolers)) $$;
grant execute on all functions in schema pg_temp to authenticated;
-- before the event: nothing
insert into jr_server_settings (key, value) values ('event_test_date', '2026-11-20') on conflict (key) do update set value = excluded.value;
select pg_temp.as_user(:'u') \g /dev/null
set role authenticated;
do $$ declare r jsonb; begin
  perform pg_temp.run('ev-0', 3000, 20, 0, '');
  r := public.jr_event_claim_run('ev-0');
  assert r->>'reason' = 'no_event', 'no event in November: ' || r::text;
  assert not (public.jr_event_status()->>'active')::boolean;
end $$;
reset role;
update jr_server_settings set value = '2026-12-18' where key = 'event_test_date';
set role authenticated;
do $$ declare r jsonb; c0 int; begin
  r := public.jr_event_status();
  assert (r->>'active')::boolean and (r->>'night')::int = 3 and (r->>'season')::int = 2026 and (r->>'days_left')::int = 20, 'status: ' || r::text;
  -- 1000 m: at most 9 parols; 2 carolers = +4
  perform pg_temp.run('ev-1', 1000, 99, 2, '');
  r := public.jr_event_claim_run('ev-1');
  assert (r->>'added')::int = 13 and (r->>'night')::int = 0, 'capped by distance + carolers: ' || r::text;
  r := public.jr_event_claim_run('ev-1');
  assert (r->>'duplicate')::boolean, 'once per run';
  -- Night Shift 1.6 km on 18 Dec = Simbang Gabi night 3
  perform pg_temp.run('ev-2', 1600, 5, 0, 'night');
  r := public.jr_event_claim_run('ev-2');
  assert (r->>'night')::int = 3 and (r->>'added')::int = 5, 'Simbang Gabi: ' || r::text;
  r := public.jr_event_status();
  assert (r->>'nights')::int = 4 and (r->>'tokens')::int = 18, 'night bit 3: ' || r::text;
  -- daily cap 120
  for i in 1..6 loop perform pg_temp.run('ev-c' || i, 9000, 25, 3, ''); perform public.jr_event_claim_run('ev-c' || i); end loop;
  r := public.jr_event_status();
  assert (r->>'tokens')::int = 120 and (r->>'today_tokens')::int = 120, 'daily cap: ' || r::text;
  -- rewards: in order of tokens; event items get granted, can't be bought
  c0 := (public.jr_get_state()->>'coins')::int;
  r := public.jr_event_claim_reward(0);
  assert (r->>'ok')::boolean and (r->'snapshot'->>'coins')::int = c0 + 500, '500 coins: ' || r::text;
  r := public.jr_event_claim_reward(0);
  assert r->>'reason' = 'claimed', 'once';
  r := public.jr_event_claim_reward(1);
  assert (r->>'ok')::boolean and (r->'snapshot'->'cosmetics') ? 'horn_jingle', 'jingle horn granted: ' || r::text;
  r := public.jr_event_claim_reward(2);
  assert r->>'reason' = 'tokens', 'needs 200 parols';
  r := public.jr_buy_cosmetic('orn_parol');
  assert r->>'reason' = 'not_for_sale', 'event items are not sold: ' || r::text;
  r := public.jr_event_claim_reward(6);
  assert r->>'reason' = 'nights', 'golden parol needs all nine';
end $$;
reset role;
update public.jr_event_progress set tokens = 600, nights = 511 where user_id = :'u';
set role authenticated;
do $$ declare r jsonb; begin
  r := public.jr_event_claim_reward(5);
  assert (r->>'ok')::boolean and (r->'snapshot'->'owned') ? 'pasko', 'Pasko jeepney granted: ' || r::text;
  r := public.jr_buy_jeepney('pasko');
  assert r->>'reason' = 'owned', 'owned';
  r := public.jr_event_claim_reward(6);
  assert (r->>'ok')::boolean and (r->'snapshot'->'cosmetics') ? 'sign_goldenparol', 'Golden Parol: ' || r::text;
  begin perform * from public.jr_event_progress; assert false, 'no direct reads';
  exception when insufficient_privilege then null; end;
  raise notice 'ok  christmas event: window, parol caps, Simbang Gabi, daily cap, reward track, event-only items';
end $$;
reset role;
delete from jr_server_settings where key = 'event_test_date';
