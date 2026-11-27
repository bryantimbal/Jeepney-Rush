-- Phase 4: gems, verified purchases (service role only), rewarded-ad rewards.
\set ON_ERROR_STOP on
\pset tuples_only on
\pset format unaligned
\set m '55555555-5555-5555-5555-555555555555'
\set n '66666666-6666-6666-6666-666666666666'
insert into auth.users (id) values (:'m'), (:'n');
create function pg_temp.as_user(u uuid) returns void language sql as $$ select set_config('request.jwt.claim.sub', u::text, false) $$;

-- 1. players cannot grant themselves anything -------------------------------------------------------
select pg_temp.as_user(:'m') \g /dev/null
set role authenticated;
do $$ begin
  begin perform public.jr_srv_grant_purchase(auth.uid(), 'jr.gems.large', 'fake-token', null, false); assert false, 'players must not grant purchases';
  exception when insufficient_privilege then null; end;
  begin perform public.jr_srv_record_ad_reward(auth.uid(), 'fake-nonce', 'dailybonus', null, false); assert false, 'players must not record ad rewards';
  exception when insufficient_privilege then null; end;
  begin perform * from jr_server_settings; assert false, 'settings hidden';
  exception when insufficient_privilege then null; end;
  begin update jr_profiles set gems = 99999; assert false, 'no direct gem edits';
  exception when insufficient_privilege then null; end;
  assert (select count(*) from jr_catalog_products) = 9, 'products are public';
  raise notice 'ok  security: grants only via the service role, settings hidden, no direct edits';
end $$;
reset role;

-- 2. verified purchase (as the verify-purchase Edge Function would, with the service role) ------------
set role service_role;
do $$
declare r jsonb; m uuid := '55555555-5555-5555-5555-555555555555';
begin
  r := public.jr_srv_grant_purchase(m, 'jr.gems.medium', 'gp-token-1', 'GPA.1', false);
  assert (r->>'ok')::boolean and (r->'snapshot'->>'gems')::int = 550, 'gems granted: ' || r::text;
  r := public.jr_srv_grant_purchase(m, 'jr.gems.medium', 'gp-token-1', 'GPA.1', false);
  assert (r->>'duplicate')::boolean and (r->'snapshot'->>'gems')::int = 550, 'same token never grants twice';
  r := public.jr_srv_grant_purchase('66666666-6666-6666-6666-666666666666', 'jr.gems.medium', 'gp-token-1', 'GPA.1', false);
  assert r->>'reason' = 'token_used', 'a token belongs to one player';
  r := public.jr_srv_grant_purchase(m, 'jr.pack.fiesta', 'gp-token-2', 'GPA.2', false);
  assert (r->'snapshot'->'owned') ? 'fiesta' and (r->'snapshot'->'owned') ? 'flag' and (r->'snapshot'->>'coins')::int = 5000, 'pack: jeepneys + coins';
  assert (r->'snapshot'->'owned_products') ? 'jr.pack.fiesta', 'non-consumable listed';
  r := public.jr_srv_grant_purchase(m, 'jr.pack.fiesta', 'gp-token-3', 'GPA.3', false);
  assert (r->'snapshot'->>'coins')::int = 5000, 'non-consumable re-purchase grants nothing new';
  r := public.jr_srv_grant_purchase(m, 'jr.adfree', 'gp-token-4', 'GPA.4', false);
  assert (r->'snapshot'->>'ad_free')::boolean, 'ad-free';
  r := public.jr_srv_grant_purchase(m, 'jr.unknown', 'gp-token-5', null, false);
  assert r->>'reason' = 'unknown_product', 'unknown product';
  r := public.jr_srv_grant_purchase(m, 'jr.coins.small', 'TEST-1', null, true);
  assert r->>'reason' = 'test_not_allowed', 'test purchases refused by default';
  raise notice 'ok  purchases: verified grant, idempotent per token, packs, ad-free, test purchases refused';
end $$;
reset role;

-- 3. gems -> premium jeepney --------------------------------------------------------------------------
select pg_temp.as_user(:'m') \g /dev/null
set role authenticated;
do $$
declare r jsonb := public.jr_buy_jeepney_gems('gold');
begin
  assert (r->>'ok')::boolean, 'gold (500 gems) bought with the 550 purchased gems: ' || r::text;
  r := public.jr_buy_jeepney_gems('gold');
  assert r->>'reason' = 'owned', 'only once';
end $$;
do $$
declare s jsonb := public.jr_get_state(); r jsonb;
begin
  assert (s->'owned') ? 'gold' and (s->>'gems')::int = 50, 'gold bought for 500 gems: ' || s::text;
  r := public.jr_buy_jeepney_gems('neon');
  assert r->>'reason' = 'gems', 'not enough gems for neon (320)';
  r := public.jr_buy_jeepney_gems('manila');
  assert r->>'reason' = 'unknown_item', 'manila has no gem price';
  raise notice 'ok  gems: premium jeepneys, balance checks';
end $$;
reset role;

-- 4. rewarded ads ------------------------------------------------------------------------------------
select pg_temp.as_user(:'m') \g /dev/null
set role authenticated;
select (public.jr_submit_run(jsonb_build_object('run_id', 'm-run-1', 'seed', 1, 'env', 0, 'duration', 200, 'distance', 3000,
  'coins', 800, 'score', 20000, 'passengers', 10, 'deliveries', 6, 'power_ups', 2, 'smooth_humps', 2, 'wet_distance', 0,
  'refuels', 0, 'hits', 0, 'clean_distance', 1000, 'best_streak', 2, 'daily', false, 'day_key', 0))->>'accepted')::boolean \g /dev/null
do $$
declare r jsonb;
begin
  r := public.jr_claim_ad_reward('never-verified');
  assert r->>'reason' = 'not_verified', 'no SSV record, no reward';
  r := public.jr_claim_ad_reward('test-x:dailybonus:');
  assert r->>'reason' = 'not_verified', 'simulated ads refused by default';
end $$;
reset role;
set role service_role;
select public.jr_srv_record_ad_reward(:'m', 'ssv-1', 'dailybonus', null, false) \g /dev/null
select public.jr_srv_record_ad_reward(:'m', 'ssv-2', 'dailybonus', null, false) \g /dev/null
select public.jr_srv_record_ad_reward(:'m', 'ssv-3', 'doublecoins', 'm-run-1', false) \g /dev/null
select public.jr_srv_record_ad_reward(:'m', 'ssv-4', 'doublecoins', 'm-run-1', false) \g /dev/null
select public.jr_srv_record_ad_reward(:'n', 'ssv-5', 'dailybonus', null, false) \g /dev/null
reset role;
select pg_temp.as_user(:'m') \g /dev/null
set role authenticated;
do $$
declare r jsonb; c int := (public.jr_get_state()->>'coins')::int;
begin
  r := public.jr_claim_ad_reward('ssv-1');
  assert (r->>'ok')::boolean and (r->>'reward')::int = 150 and (r->'snapshot'->>'coins')::int = c + 150, 'daily bonus: ' || r::text;
  r := public.jr_claim_ad_reward('ssv-1');
  assert r->>'reason' = 'claimed', 'claim once';
  r := public.jr_claim_ad_reward('ssv-2');
  assert r->>'reason' = 'already_today', 'one daily bonus per day';
  r := public.jr_claim_ad_reward('ssv-3');
  assert (r->>'ok')::boolean and (r->>'reward')::int = 800, 'double coins = the run''s credited coins: ' || r::text;
  r := public.jr_claim_ad_reward('ssv-4');
  assert r->>'reason' = 'claimed', 'a run is doubled once';
  r := public.jr_claim_ad_reward('ssv-5');
  assert r->>'reason' = 'not_verified', 'someone else''s reward';
  raise notice 'ok  rewarded ads: SSV required, one claim each, daily bonus once a day, double coins once per run';
end $$;
reset role;

-- 5. dev switches + refunds -------------------------------------------------------------------------------
update jr_server_settings set value = 'true' where key in ('allow_test_purchases', 'allow_test_rewards');
set role service_role;
do $$ declare r jsonb; begin
  r := public.jr_srv_grant_purchase('66666666-6666-6666-6666-666666666666', 'jr.coins.small', 'TEST-n-1', null, true);
  assert (r->>'ok')::boolean and (r->'snapshot'->>'coins')::int = 5000, 'test purchase allowed when switched on';
  r := public.jr_srv_revoke_purchase('TEST-n-1');
  assert (r->>'ok')::boolean, 'refund';
end $$;
reset role;
select pg_temp.as_user(:'n') \g /dev/null
set role authenticated;
do $$ declare r jsonb; begin
  assert (public.jr_get_state()->>'coins')::int = 0, 'refund took the coins back';
  r := public.jr_claim_ad_reward('test-abc:trial:gold');
  assert (r->>'ok')::boolean, 'simulated ad accepted when allowed';
  raise notice 'ok  dev switches (test purchases/ads) and refunds';
end $$;
reset role;
update jr_server_settings set value = 'false' where key in ('allow_test_purchases', 'allow_test_rewards');

do $$ begin
  assert (select bool_and(p.coins = coalesce((select sum(amount) from jr_transactions t where t.user_id = p.user_id and t.currency = 'coins'), 0)) from jr_profiles p),
    'coins match the ledger';
  assert (select bool_and(p.gems = coalesce((select sum(amount) from jr_transactions t where t.user_id = p.user_id and t.currency = 'gems'), 0)) from jr_profiles p),
    'gems match the ledger';
  raise notice 'ok  ledger: coins and gems both equal their transactions';
end $$;
