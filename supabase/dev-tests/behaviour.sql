-- Behaviour + security tests for the online backend. Each block acts as a signed-in player
-- (role "authenticated" with auth.uid() set) exactly like a request through PostgREST.
\set ON_ERROR_STOP on
\pset tuples_only on
\pset format unaligned
\set a '11111111-1111-1111-1111-111111111111'
\set b '22222222-2222-2222-2222-222222222222'
\set c '33333333-3333-3333-3333-333333333333'

insert into auth.users (id, is_anonymous) values (:'a', true);
insert into auth.users (id, email) values (:'b', 'b@example.com'), (:'c', 'c@example.com');

create function pg_temp.as_user(u uuid) returns void language sql as $$ select set_config('request.jwt.claim.sub', u::text, false) $$;
create function pg_temp.report(id text, dist real, dur real, coins int, score int, extra jsonb default '{}') returns jsonb language sql as $$
  select jsonb_build_object('run_id', id, 'seed', 1, 'env', 0, 'duration', dur, 'distance', dist, 'coins', coins, 'score', score,
    'passengers', 10, 'deliveries', 6, 'power_ups', 2, 'smooth_humps', 3, 'wet_distance', dist / 3, 'refuels', 1, 'hits', 0,
    'clean_distance', dist / 2, 'best_streak', 3, 'daily', false, 'day_key', 0, 'jeepney', 'pilipinas') || extra $$;
grant execute on all functions in schema pg_temp to authenticated;

-- 1. sign-up trigger creates profiles with the free jeepneys ------------------------------------
do $$ begin
  assert (select count(*) from jr_profiles) = 3, 'profiles created';
  assert (select count(*) from jr_inventory where item_kind = 'jeepney' and item_id in ('pilipinas', 'sanjuan')) = 6, 'free jeepneys';
  assert (select bool_and(friend_code ~ '^[A-Z2-9]{8}$') from jr_profiles), 'friend codes';
  raise notice 'ok  profiles + free jeepneys on sign-up';
end $$;

-- 2. security: RLS and grants --------------------------------------------------------------------
select pg_temp.as_user(:'a');
set role authenticated;
do $$
declare s jsonb := public.jr_get_state();
begin
  assert (s->>'coins')::int = 0 and jsonb_array_length(s->'owned') = 2, 'fresh state';
  assert (s->>'is_anonymous')::boolean, 'guest account';
  assert jsonb_array_length(s->'daily_progress') = 3 and jsonb_array_length(s->'weekly_progress') = 2, 'mission rows';
  assert jsonb_array_length(s->'missions') = 5, 'mission defs';
  assert (select count(*) from jr_profiles) = 1, 'RLS: only my profile is visible';
  assert (select count(*) from jr_inventory) = 2, 'RLS: only my inventory';
  begin perform * from jr_scores; assert false, 'jr_scores must not be readable';
  exception when insufficient_privilege then null; end;
  begin update jr_profiles set coins = 999999; assert false, 'direct update must fail';
  exception when insufficient_privilege then null; end;
  begin insert into jr_transactions (user_id, kind, amount) values (auth.uid(), 'hack', 1000); assert false, 'direct insert must fail';
  exception when insufficient_privilege then null; end;
  begin perform jr_private.snapshot(auth.uid()); assert false, 'private functions must not be callable';
  exception when insufficient_privilege then null; end;
  raise notice 'ok  RLS: own rows only, no direct writes, private functions hidden';
end $$;
reset role;
set role anon;
do $$ begin
  begin perform public.jr_get_state(); assert false, 'anon must not call RPCs';
  exception when insufficient_privilege then null; end;
  assert (select count(*) from jr_catalog_jeepneys) = 13, 'catalog is public';
  raise notice 'ok  anon: catalog readable, RPCs denied';
end $$;
reset role;

-- 3. runs: accept, idempotency, reject, clamp ---------------------------------------------------
select pg_temp.as_user(:'a');
set role authenticated;
do $$
declare r jsonb; before int;
begin
  r := public.jr_submit_run(pg_temp.report('run-a1', 5000, 300, 3000, 20000));
  assert (r->>'accepted')::boolean and not (r->>'clamped')::boolean, 'legit run accepted: ' || r::text;
  assert (r->>'credited_coins')::int = 3000, 'coins credited';
  assert (r->>'xp_gained')::int = 500 + 50 + 60 + 10, 'xp = d/10 + pas*5 + del*10 + pu*5';
  assert (r->'snapshot'->>'coins')::int = 3000 + (r->>'level_reward')::int, 'wallet = coins + level rewards';
  assert (r->>'level_reward')::int = 200 + 300, 'levels 2 and 3 pay 100*level';
  assert (r->>'rank_all')::int = 1, 'top of the board';
  before := (r->'snapshot'->>'coins')::int;
  r := public.jr_submit_run(pg_temp.report('run-a1', 5000, 300, 3000, 20000));
  assert (r->>'duplicate')::boolean and (r->'snapshot'->>'coins')::int = before, 'replaying a run id credits nothing';
  r := public.jr_submit_run(pg_temp.report('run-a2', 5000, 20, 3000, 20000));
  assert not (r->>'accepted')::boolean and r->>'reason' = 'too_fast', 'teleporting rejected';
  assert (r->'snapshot'->>'coins')::int = before, 'rejected run pays nothing';
  r := public.jr_submit_run(pg_temp.report('run-a3', 1000, 120, 500000, 99999999));
  assert (r->>'accepted')::boolean and (r->>'clamped')::boolean, 'inflated run clamped';
  assert (r->>'credited_coins')::int = 1000 + 6 * 42 + 50, 'coins clamped to the bound';
  assert (r->'snapshot'->>'best_score')::int < 99999999, 'score clamped';
  raise notice 'ok  runs: accepted, idempotent, impossible ones rejected, inflated ones clamped';
end $$;
reset role;

-- 4. purchases ----------------------------------------------------------------------------------
select pg_temp.as_user(:'a');
set role authenticated;
do $$
declare r jsonb; c int;
begin
  c := (public.jr_get_state()->>'coins')::int;
  r := public.jr_buy_jeepney('manila');
  assert (r->>'ok')::boolean and (r->'snapshot'->>'coins')::int = c - 600, 'bought manila for 600';
  assert (r->'snapshot'->'owned') ? 'manila', 'manila owned';
  r := public.jr_buy_jeepney('manila');
  assert r->>'reason' = 'owned', 'cannot buy twice';
  r := public.jr_buy_jeepney('gold');
  assert r->>'reason' = 'coins', 'not enough coins';
  r := public.jr_buy_jeepney('ferrari');
  assert r->>'reason' = 'unknown_item', 'unknown item';
  c := (r->'snapshot'->>'coins')::int;
  r := public.jr_buy_upgrade(4); -- coin bonus
  assert (r->>'ok')::boolean and (r->'snapshot'->>'coins')::int = c - 150 and (r->'snapshot'->'upgrades'->>4)::int = 1, 'upgrade bought';
  r := public.jr_submit_run(pg_temp.report('run-a4', 2000, 150, 100, 5000));
  assert (r->>'coin_bonus')::int = 10, 'coin bonus upgrade applied by the server';
  r := public.jr_buy_upgrade(9);
  assert r->>'reason' = 'max_level', 'unknown upgrade kind';
  raise notice 'ok  purchases: prices from the catalog, ownership, coin checks, upgrade effects';
end $$;
reset role;

-- 5. missions and achievements ------------------------------------------------------------------
select pg_temp.as_user(:'a');
set role authenticated;
do $$
declare r jsonb; s jsonb; reward int; slot int := 0; target int;
begin
  s := public.jr_get_state();
  -- find a daily mission that is not finished yet
  select (m->>'slot')::int, (m->>'target')::int, (m->>'reward')::int into slot, target, reward
  from jsonb_array_elements(s->'missions') m
  where m->>'period' = 'daily' and (s->'daily_progress'->>((m->>'slot')::int))::int < (m->>'target')::int limit 1;
  if slot is not null then
    r := public.jr_claim_mission(false, slot);
    assert r->>'reason' = 'not_done', 'unfinished mission cannot be claimed';
  end if;
  r := public.jr_claim_achievement('first_passenger');
  assert (r->>'ok')::boolean and (r->>'reward')::int = 50, 'achievement claimed';
  r := public.jr_claim_achievement('first_passenger');
  assert r->>'reason' = 'claimed', 'claimed once only';
  r := public.jr_claim_achievement('highway_legend');
  assert (r->>'ok')::boolean, 'highway legend: 5,000 m was driven';
  r := public.jr_claim_achievement('collector');
  assert r->>'reason' = 'not_done', 'needs 5 jeepneys';
  raise notice 'ok  achievements: checked against server stats, claimable once';
end $$;
reset role;
-- complete a mission (as the server would after enough runs) and claim it
update jr_mission_progress set progress = 1000000 where user_id = :'a' and period = 'daily' and slot = 0;
select pg_temp.as_user(:'a');
set role authenticated;
do $$
declare r jsonb; c int := (public.jr_get_state()->>'coins')::int;
begin
  r := public.jr_claim_mission(false, 0);
  assert (r->>'ok')::boolean and (r->'snapshot'->>'coins')::int = c + (r->>'reward')::int, 'mission reward paid';
  assert (r->'snapshot'->'daily_claimed'->>0)::boolean, 'marked claimed';
  r := public.jr_claim_mission(false, 0);
  assert r->>'reason' = 'claimed', 'no double claim';
  r := public.jr_claim_mission(false, 7);
  assert r->>'reason' = 'unknown_mission', 'bad slot';
  raise notice 'ok  missions: progress tracked by the server, rewards paid once';
end $$;
reset role;

-- 6. daily challenge ------------------------------------------------------------------------------
select pg_temp.as_user(:'b');
set role authenticated;
do $$
declare ch jsonb := public.jr_daily_challenge(); r jsonb;
begin
  r := public.jr_submit_run(pg_temp.report('run-b1', 3000, 200, 600, 30000,
        jsonb_build_object('daily', true, 'seed', (ch->>'seed')::int, 'env', (ch->>'env')::int, 'day_key', (ch->>'day_key')::int)));
  assert (r->>'accepted')::boolean and (r->>'daily')::boolean and (r->>'rank_daily')::int = 1, 'daily challenge run ranked: ' || r::text;
  r := public.jr_submit_run(pg_temp.report('run-b2', 3000, 200, 600, 50000,
        jsonb_build_object('daily', true, 'seed', (ch->>'seed')::int + 1, 'env', (ch->>'env')::int, 'day_key', (ch->>'day_key')::int)));
  assert (r->>'accepted')::boolean and not (r->>'daily')::boolean, 'wrong seed: normal run, not on the daily board';
  raise notice 'ok  daily challenge: only today''s seed/route counts';
end $$;
reset role;

-- 7. friends + leaderboards -----------------------------------------------------------------------
select pg_temp.as_user(:'b');
set role authenticated;
do $$
declare acode text; r jsonb;
begin
  select friend_code into acode from jr_profiles; -- RLS: B can only see B's own row
  assert acode = (public.jr_get_state()->>'friend_code'), 'own code';
end $$;
reset role;
select set_config('jr.acode', friend_code, false) from jr_profiles where user_id = :'a' \g /dev/null
select pg_temp.as_user(:'b');
set role authenticated;
do $$
declare r jsonb;
begin
  r := public.jr_add_friend(lower(current_setting('jr.acode')));
  assert (r->>'ok')::boolean, 'friend added by code (case-insensitive)';
  r := public.jr_add_friend((public.jr_get_state()->>'friend_code'));
  assert r->>'reason' = 'self', 'cannot add yourself';
  r := public.jr_add_friend('ZZZZZZZZ');
  assert r->>'reason' = 'unknown_code', 'unknown code';
  assert jsonb_array_length(public.jr_list_friends()->'friends') = 1, 'one friend';
  raise notice 'ok  friends: add by code, validation';
end $$;
reset role;

select pg_temp.as_user(:'c');
set role authenticated;
select (public.jr_submit_run(pg_temp.report('run-c1', 6000, 400, 500, 40000))->>'accepted')::boolean as c_ok \gset
reset role;

select pg_temp.as_user(:'a');
set role authenticated;
do $$
declare g jsonb := public.jr_leaderboard('all', 'global', 10); f jsonb := public.jr_leaderboard('all', 'friends', 10); w jsonb := public.jr_leaderboard('week');
begin
  assert jsonb_array_length(g->'rows') = 3, 'global has everyone: ' || g::text;
  assert (g->'rows'->0->>'score')::int >= (g->'rows'->1->>'score')::int, 'sorted by score';
  assert jsonb_array_length(f->'rows') = 2, 'friends board = me + my friend B (not C): ' || f::text;
  assert (f->'me'->>'is_me')::boolean, 'my row';
  assert not (g->'rows'->0 ? 'coins'), 'no wallet data on the board';
  assert w->>'board' like 'week:%', 'weekly board key';
  assert jsonb_array_length(public.jr_leaderboard('daily')->'rows') = 1, 'daily board has the challenge run only';
  raise notice 'ok  leaderboards: global/friends/weekly/daily, ranks, no private data';
end $$;
reset role;

-- 8. names, legacy import, play-time cap ----------------------------------------------------------
insert into auth.users (id) values ('44444444-4444-4444-4444-444444444444');
select pg_temp.as_user('44444444-4444-4444-4444-444444444444');
set role authenticated;
do $$
declare r jsonb; i int;
begin
  assert (public.jr_set_name('Kuya Jun')->>'ok')::boolean, 'valid name';
  assert public.jr_set_name('x')->>'reason' = 'invalid_name', 'too short';
  assert public.jr_set_name('<script>')->>'reason' = 'invalid_name', 'bad characters';
  assert public.jr_set_name('Gago Driver')->>'reason' = 'blocked_name', 'blocked word';
  r := public.jr_import_legacy('{"coins": 999999, "xp": 5000, "best_score": 12345, "best_distance": 3100, "owned": ["pilipinas", "neon", "fake"], "upgrades": [2, 0, 9, 0, 0, 0]}');
  assert (r->>'ok')::boolean, 'legacy import';
  assert (r->'snapshot'->>'coins')::int = 20000, 'coins capped';
  assert (r->'snapshot'->'owned') ? 'neon' and not ((r->'snapshot'->'owned') ? 'fake'), 'only real jeepneys';
  assert (r->'snapshot'->'upgrades'->>2)::int = 5, 'upgrade level capped at 5';
  assert public.jr_import_legacy('{"coins": 5}')->>'reason' = 'already_has_progress', 'only once';
  for i in 1 .. 5 loop
    r := public.jr_submit_run(pg_temp.report('long-' || i, 100000, 14400, 10, 100));
    assert (r->>'accepted')::boolean, 'long run ' || i || ': ' || r::text;
  end loop;
  r := public.jr_submit_run(pg_temp.report('long-6', 1000, 600, 10, 100));
  assert r->>'reason' = 'playtime', 'over 20 h of play in 24 h rejected';
  raise notice 'ok  names, one-time capped legacy import, play-time cap';
end $$;
reset role;

-- 9. ledger ---------------------------------------------------------------------------------------
do $$ begin
  -- every coin in a wallet is explained by the transaction ledger (minus nothing else)
  assert (select bool_and(p.coins = coalesce((select sum(amount) from jr_transactions t where t.user_id = p.user_id and t.currency = 'coins'), 0)) from jr_profiles p),
    'wallets match the ledger';
  raise notice 'ok  ledger: every wallet equals the sum of its transactions';
end $$;

-- 10. contract samples: real responses, checked against the C# DTOs by OnlineContractTests ------
select pg_temp.as_user(:'a') \g /dev/null
set role authenticated;
\o contract/get_state.json
select jsonb_pretty(public.jr_get_state());
\o contract/submit_run.json
select jsonb_pretty(public.jr_submit_run(pg_temp.report('contract-run', 1500, 120, 200, 4000)));
\o contract/buy.json
select jsonb_pretty(public.jr_buy_upgrade(0));
\o contract/leaderboard.json
select jsonb_pretty(public.jr_leaderboard('all', 'global', 10));
\o contract/friends.json
select jsonb_pretty(public.jr_list_friends());
\o contract/daily.json
select jsonb_pretty(public.jr_daily_challenge());
\o
reset role;
do $$ begin raise notice 'ok  contract samples written'; end $$;
