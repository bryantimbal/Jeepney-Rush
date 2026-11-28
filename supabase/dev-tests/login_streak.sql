-- Login streak: once a day, 7-day calendar, one save a week (ad or gems), ad double once.
\set ON_ERROR_STOP on
\pset tuples_only on
\pset format unaligned
\set u 'abcdabcd-0000-0000-0000-00000000000a'
insert into auth.users (id) values (:'u');
create function pg_temp.as_user(u uuid) returns void language sql as $$ select set_config('request.jwt.claim.sub', u::text, false) $$;
create function pg_temp.shift(days int) returns void language sql security definer as $$
  update public.jr_login_streaks set last_day = last_day - days, doubled_day = -1 $$;   -- pretend the last claim was earlier
grant execute on all functions in schema pg_temp to authenticated;
update jr_server_settings set value = 'true' where key = 'allow_test_rewards';
select pg_temp.as_user(:'u') \g /dev/null
set role authenticated;
do $$ declare r jsonb; c0 int; begin
  r := public.jr_streak_status();
  assert not (r->>'claimed')::boolean and (r->>'day')::int = 1 and not (r->>'broken')::boolean, 'first day: ' || r::text;
  c0 := (public.jr_get_state()->>'coins')::int;
  r := public.jr_streak_claim();
  assert (r->>'ok')::boolean and (r->>'day')::int = 1 and (r->'snapshot'->>'coins')::int = c0 + 100, 'day 1 = 100 coins: ' || r::text;
  r := public.jr_streak_claim();
  assert r->>'reason' = 'already_today', 'once a day';
  r := public.jr_claim_ad_reward('test-a1:streakdouble:');
  assert (r->>'ok')::boolean and (r->>'reward')::int = 100, 'ad doubles today: ' || r::text;
  r := public.jr_claim_ad_reward('test-a2:streakdouble:');
  assert r->>'reason' = 'streak_not_claimed', 'only once: ' || r::text;
  -- next day: day 2, then days 3..7, then the cycle restarts
  perform pg_temp.shift(1);
  r := public.jr_streak_claim();
  assert (r->>'day')::int = 2 and (r->>'streak')::int = 2, 'day 2';
  for i in 3..7 loop perform pg_temp.shift(1); r := public.jr_streak_claim(); end loop;
  assert (r->>'day')::int = 7 and (r->>'gems')::int = 15 and (r->>'reward')::int = 500, 'day 7: ' || r::text;
  perform pg_temp.shift(1);
  r := public.jr_streak_claim();
  assert (r->>'day')::int = 1 and (r->>'streak')::int = 8, 'cycle restarts, streak continues';
  -- one missed day: can be saved (ad), then the calendar goes on
  perform pg_temp.shift(2);
  r := public.jr_streak_status();
  assert (r->>'can_save')::boolean and (r->>'day')::int = 2, 'missed one day: ' || r::text;
  r := public.jr_claim_ad_reward('test-a3:streaksave:');
  assert (r->>'ok')::boolean, 'saved with an ad: ' || r::text;
  r := public.jr_streak_claim();
  assert (r->>'day')::int = 2 and (r->>'streak')::int = 9, 'kept: ' || r::text;
  -- second miss in the same week: no save (gems refused too), streak resets
  perform pg_temp.shift(2);
  r := public.jr_streak_status();
  assert not (r->>'can_save')::boolean and (r->>'broken')::boolean, 'one save a week: ' || r::text;
  r := public.jr_streak_save_gems();
  assert r->>'reason' in ('streak_cant_save', 'not_enough_gems'), 'no second save: ' || r::text;
  r := public.jr_streak_claim();
  assert (r->>'day')::int = 1 and (r->>'streak')::int = 1, 'starts over';
  begin perform * from public.jr_login_streaks; assert false, 'no direct reads';
  exception when insufficient_privilege then null; end;
  raise notice 'ok  login streak: daily claim, 7-day cycle, ad double once, one save a week, reset';
end $$;
reset role;
-- gems save (a fresh week)
update public.jr_login_streaks set save_week = 0, last_day = last_day - 2, streak = 4;
update public.jr_profiles set gems = 12 where user_id = :'u';
set role authenticated;
do $$ declare r jsonb; begin
  r := public.jr_streak_save_gems();
  assert (r->>'ok')::boolean and (r->'snapshot'->>'gems')::int = 2, 'save for 10 gems: ' || r::text;
  r := public.jr_streak_claim();
  assert (r->>'streak')::int = 5, 'kept';
  raise notice 'ok  login streak: save with gems';
end $$;
reset role;
update jr_server_settings set value = 'false' where key = 'allow_test_rewards';
