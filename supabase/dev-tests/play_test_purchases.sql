-- Google Play license-tester purchases are granted; simulated TEST- tokens still need the dev switch.
\set ON_ERROR_STOP on
\pset tuples_only on
\pset format unaligned
\set u 'aeaeaeae-0000-0000-0000-00000000000a'
insert into auth.users (id) values (:'u');
update jr_server_settings set value = 'false' where key = 'allow_test_purchases';
set role service_role;
do $$ declare r jsonb; begin
  r := public.jr_srv_grant_purchase('aeaeaeae-0000-0000-0000-00000000000a', 'jr.coins.small', 'TEST-fake-1', null, true);
  assert r->>'reason' = 'test_not_allowed', 'made-up TEST- token refused on a live project: ' || r::text;
  r := public.jr_srv_grant_purchase('aeaeaeae-0000-0000-0000-00000000000a', 'jr.coins.small', 'google-test-token-1', 'GPA.TEST-1', true);
  assert (r->>'ok')::boolean and (r->'snapshot'->>'coins')::int = 5000, 'license-tester purchase granted: ' || r::text;
end $$;
reset role;
update jr_server_settings set value = 'false' where key = 'allow_play_test_purchases';
set role service_role;
do $$ declare r jsonb; begin
  r := public.jr_srv_grant_purchase('aeaeaeae-0000-0000-0000-00000000000a', 'jr.coins.small', 'google-test-token-2', 'GPA.TEST-2', true);
  assert r->>'reason' = 'test_not_allowed', 'license-tester purchases can be switched off';
  raise notice 'ok  play test purchases: license testers granted, made-up TEST- tokens refused';
end $$;
reset role;
update jr_server_settings set value = 'true' where key = 'allow_play_test_purchases';
select (select test from public.jr_purchases where purchase_token = 'google-test-token-1') \g /dev/null
