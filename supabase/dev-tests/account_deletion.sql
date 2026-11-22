-- Phase 5: in-app account deletion.
\set ON_ERROR_STOP on
\pset tuples_only on
\pset format unaligned
\set d '77777777-7777-7777-7777-777777777777'
\set e '88888888-8888-8888-8888-888888888888'
insert into auth.users (id, email) values (:'d', 'del@example.com'), (:'e', null);
create function pg_temp.as_user(u uuid) returns void language sql as $$ select set_config('request.jwt.claim.sub', u::text, false) $$;

-- a player with a verified purchase, an ad reward and a friend
update jr_server_settings set value = 'true' where key = 'allow_test_purchases';
set role service_role;
do $$ declare r jsonb; begin
  r := public.jr_srv_grant_purchase('77777777-7777-7777-7777-777777777777', 'jr.coins.small', 'TOKEN-DEL-1', 'GPA.1', true);
  assert (r->>'ok')::boolean, 'purchase granted';
  r := public.jr_srv_record_ad_reward('77777777-7777-7777-7777-777777777777', 'NONCE-DEL-1', 'dailybonus', null, false);
  assert (r->>'ok')::boolean, 'ad reward recorded';
end $$;
reset role;
select pg_temp.as_user(:'e') \g /dev/null
set role authenticated;
do $$ begin perform public.jr_get_state(); end $$;
reset role;
insert into jr_friends (user_id, friend_id) values (:'e', :'d');

-- anonymous callers can't delete anything
select set_config('request.jwt.claim.sub', '', false) \g /dev/null
set role anon;
do $$ begin
  begin perform public.jr_delete_account(); assert false, 'anon must not call it';
  exception when insufficient_privilege then null; end;
end $$;
reset role;

-- the player deletes their account
select pg_temp.as_user(:'d') \g /dev/null
set role authenticated;
do $$ declare r jsonb; begin
  r := public.jr_delete_account();
  assert (r->>'ok')::boolean, 'deleted';
end $$;
reset role;
do $$ declare t text; n int; begin
  assert not exists (select 1 from auth.users where id = '77777777-7777-7777-7777-777777777777'), 'auth user gone';
  foreach t in array array['jr_profiles','jr_inventory','jr_runs','jr_scores','jr_mission_progress','jr_achievements_claimed','jr_transactions','jr_purchases','jr_ad_rewards'] loop
    execute format('select count(*) from public.%I where user_id = %L', t, '77777777-7777-7777-7777-777777777777') into n;
    assert n = 0, t || ' rows left behind';
  end loop;
  assert not exists (select 1 from jr_friends where friend_id = '77777777-7777-7777-7777-777777777777'), 'removed from friends lists';
  assert exists (select 1 from jr_profiles where user_id = '88888888-8888-8888-8888-888888888888'), 'other players untouched';
  raise notice 'ok  account deletion removes every row of that player only';
end $$;

-- the old purchase token and ad nonce can't be replayed on a fresh account
insert into auth.users (id) values ('99999999-9999-9999-9999-999999999999');
set role service_role;
do $$ declare r jsonb; begin
  r := public.jr_srv_grant_purchase('99999999-9999-9999-9999-999999999999', 'jr.coins.small', 'TOKEN-DEL-1', 'GPA.1', true);
  assert not (r->>'ok')::boolean and r->>'reason' = 'token_used', 'retired token refused';
  r := public.jr_srv_record_ad_reward('99999999-9999-9999-9999-999999999999', 'NONCE-DEL-1', 'dailybonus', null, false);
end $$;
reset role;
do $$ begin
  assert not exists (select 1 from jr_ad_rewards where nonce = 'NONCE-DEL-1'), 'retired nonce not re-recorded';
  assert (select coins from jr_profiles where user_id = '99999999-9999-9999-9999-999999999999') = 0, 'no coins from the replay';
  raise notice 'ok  purchase tokens / ad nonces of deleted accounts cannot be replayed';
end $$;
update jr_server_settings set value = 'false';

-- signed out: polite refusal
select set_config('request.jwt.claim.sub', '', false) \g /dev/null
set role authenticated;
do $$ begin assert (public.jr_delete_account()->>'reason') = 'not_signed_in'; end $$;
reset role;
