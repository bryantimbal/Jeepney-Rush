-- Stat-class rankings: every jeepney keeps its stats, but scores are also ranked per stat class
-- (0 Standard, 1 Sport, 2 Elite – C# JeepneyClasses), so boosted jeepneys compete with jeepneys
-- of similar power. The class comes from the catalog, never from the client: a run counts for
-- the class of the jeepney it was driven with if the player owns it, otherwise for the highest
-- class the player owns (no dropping into an easier class by naming a jeepney you don't have).
-- Class boards: 'all:c<n>', 'week:<wk>:c<n>', 'daily:<dk>:c<n>'. The overall boards stay.

alter table public.jr_catalog_jeepneys add column if not exists stat_class int not null default 0;
do $$ begin
  alter table public.jr_catalog_jeepneys add constraint jr_catalog_jeepneys_stat_class check (stat_class between 0 and 2);
exception when duplicate_object then null; end $$;

create or replace function jr_private.run_class(p_uid uuid, p_jeepney text) returns int
language sql stable security definer set search_path = public as $$
  select coalesce(
    (select j.stat_class from public.jr_catalog_jeepneys j
      where j.id = p_jeepney and exists (select 1 from public.jr_inventory i where i.user_id = p_uid and i.item_kind = 'jeepney' and i.item_id = j.id)),
    (select max(j.stat_class) from public.jr_catalog_jeepneys j join public.jr_inventory i on i.item_kind = 'jeepney' and i.item_id = j.id where i.user_id = p_uid),
    0)
$$;
revoke all on function jr_private.run_class(uuid, text) from public, anon, authenticated;

create or replace function public.jr_submit_run(p_report jsonb) returns jsonb
language plpgsql security definer set search_path = public, jr_private as $$
declare
  uid uuid := auth.uid();
  v text; r jsonb; p public.jr_profiles;
  dk int := jr_private.day_key(jr_private.ph_date()); wk int := jr_private.week_key(jr_private.ph_date());
  is_daily boolean := false; ch record;
  played real; clamped boolean;
  v_coins int; bonus int; xp_gain int; lvl_before int; lvl_after int; lvl_reward int := 0; lv int;
  m record; val int; existing public.jr_runs;
  bonus_level int;
  v_gems int; gems_today int;
  cls int;
begin
  perform jr_private.ensure_profile(uid);
  select * into existing from public.jr_runs where run_id = p_report->>'run_id';
  if found then
    if existing.user_id <> uid then return jsonb_build_object('accepted', false, 'reason', 'run_id'); end if;
    return jsonb_build_object('accepted', existing.accepted, 'duplicate', true, 'reason', existing.reason, 'snapshot', jr_private.snapshot(uid));
  end if;

  v := jr_private.run_violation(p_report);
  if v is null then
    -- a driver can't have played more than 20 hours in the last 24
    select coalesce(sum(duration), 0) into played from public.jr_runs where user_id = uid and accepted and created_at > now() - interval '24 hours';
    if played + (p_report->>'duration')::real > 72000 then v := 'playtime'; end if;
  end if;
  if v is not null then
    insert into public.jr_runs (run_id, user_id, accepted, reason, report, duration) values (p_report->>'run_id', uid, false, v, p_report, coalesce((p_report->>'duration')::real, 0));
    return jsonb_build_object('accepted', false, 'reason', v, 'snapshot', jr_private.snapshot(uid));
  end if;

  r := jr_private.run_clamp(p_report);
  clamped := (r->>'coins')::int <> coalesce((p_report->>'coins')::int, 0) or (r->>'score')::int <> coalesce((p_report->>'score')::int, 0)
          or (r->>'passengers')::int <> coalesce((p_report->>'passengers')::int, 0) or (r->>'power_ups')::int <> coalesce((p_report->>'power_ups')::int, 0);

  -- daily challenge only counts for today's seed and route
  if coalesce((p_report->>'daily')::boolean, false) then
    select * into ch from jr_private.daily_challenge(dk);
    is_daily := (p_report->>'day_key')::int = dk and (p_report->>'seed')::int = ch.seed and (p_report->>'env')::int = ch.env;
  end if;

  select * into p from public.jr_profiles where user_id = uid for update;
  v_coins := (r->>'coins')::int;
  select coalesce(max(level), 0) into bonus_level from public.jr_inventory where user_id = uid and item_kind = 'upgrade' and item_id = '4';
  bonus := round(v_coins * jr_private.const('coin_bonus_per_level') * bonus_level)::int;
  xp_gain := trunc((r->>'distance')::real / 10::real)::int + (r->>'passengers')::int * 5 + (r->>'deliveries')::int * 10 + (r->>'power_ups')::int * 5;
  lvl_before := jr_private.level_for(p.xp);
  lvl_after := jr_private.level_for(p.xp + xp_gain);
  for lv in lvl_before + 1 .. lvl_after loop lvl_reward := lvl_reward + (jr_private.const('level_reward_per_level')::int * lv); end loop;

  update public.jr_profiles set
    coins = coins + v_coins + bonus + lvl_reward,
    xp = xp + xp_gain,
    best_score = greatest(best_score, (r->>'score')::int),
    best_distance = greatest(best_distance, (r->>'distance')::real),
    best_clean = greatest(best_clean, coalesce((r->>'clean_distance')::real, 0)),
    runs = runs + 1,
    total_distance = total_distance + (r->>'distance')::real,
    total_coins = total_coins + v_coins + bonus,
    total_passengers = total_passengers + (r->>'passengers')::int,
    total_deliveries = total_deliveries + (r->>'deliveries')::int,
    total_power_ups = total_power_ups + (r->>'power_ups')::int,
    total_smooth_humps = total_smooth_humps + (r->>'smooth_humps')::int,
    total_wet_distance = total_wet_distance + coalesce((r->>'wet_distance')::real, 0),
    total_refuels = total_refuels + (r->>'refuels')::int,
    updated_at = now()
  where user_id = uid;

  insert into public.jr_runs (run_id, user_id, accepted, clamped, daily, duration, credited_coins, report)
  values (r->>'run_id', uid, true, clamped, is_daily, (r->>'duration')::real, v_coins + bonus, r);
  insert into public.jr_transactions (user_id, kind, amount, ref) values (uid, 'run', v_coins + bonus, r->>'run_id');
  if lvl_reward > 0 then insert into public.jr_transactions (user_id, kind, amount, ref) values (uid, 'level_up', lvl_reward, lvl_after::text); end if;

  -- road gems: capped per run by distance (C# RoadGems.MaxForDistance) and per PH day (RoadGems.DailyCap)
  v_gems := least(greatest(coalesce((p_report->>'gems')::int, 0), 0), jr_private.road_gems_max((r->>'distance')::real));
  select coalesce(sum(amount), 0) into gems_today from public.jr_transactions
   where user_id = uid and kind = 'road_gems' and currency = 'gems' and created_at >= jr_private.ph_day_start();
  v_gems := greatest(0, least(v_gems, 15 - gems_today));
  if v_gems > 0 then
    update public.jr_profiles set gems = gems + v_gems where user_id = uid;
    insert into public.jr_transactions (user_id, kind, amount, ref, currency) values (uid, 'road_gems', v_gems, r->>'run_id', 'gems');
  end if;

  -- missions (same rules as C# Missions.ApplyRun)
  perform jr_private.ensure_missions(uid, 'daily', dk);
  perform jr_private.ensure_missions(uid, 'weekly', wk);
  for m in select 'daily' as period, dk as k, x.* from jr_private.missions('daily', dk) x
           union all select 'weekly', wk, x.* from jr_private.missions('weekly', wk) x loop
    val := case m.type
      when 'DistanceInRun' then trunc((r->>'distance')::real)::int when 'TotalDistance' then trunc((r->>'distance')::real)::int
      when 'CoinsInRun' then (r->>'coins')::int when 'TotalCoins' then (r->>'coins')::int
      when 'PassengersInRun' then (r->>'passengers')::int when 'TotalPassengers' then (r->>'passengers')::int
      when 'DeliveriesInRun' then (r->>'deliveries')::int when 'TotalDeliveries' then (r->>'deliveries')::int
      when 'CleanDistanceInRun' then trunc(coalesce((r->>'clean_distance')::real, 0))::int
      when 'StreakInRun' then coalesce((r->>'best_streak')::int, 0)
      when 'TotalPowerUps' then (r->>'power_ups')::int
      when 'TotalSmoothHumps' then (r->>'smooth_humps')::int
      when 'TotalWetDistance' then trunc(coalesce((r->>'wet_distance')::real, 0))::int
      when 'TotalRefuels' then (r->>'refuels')::int
      when 'Runs' then 1
      else 0 end;
    update public.jr_mission_progress mp set progress = least(m.target, case when m.in_run then greatest(mp.progress, val) else mp.progress + val end)
    where mp.user_id = uid and mp.period = m.period and mp.period_key = m.k and mp.slot = m.slot;
  end loop;

  -- leaderboards: overall, plus the board of the jeepney's stat class (Standard / Sport / Elite)
  cls := jr_private.run_class(uid, p_report->>'jeepney');
  perform jr_private.post_score(uid, 'all', (r->>'score')::int, (r->>'distance')::real);
  perform jr_private.post_score(uid, 'week:' || wk, (r->>'score')::int, (r->>'distance')::real);
  perform jr_private.post_score(uid, 'all:c' || cls, (r->>'score')::int, (r->>'distance')::real);
  perform jr_private.post_score(uid, 'week:' || wk || ':c' || cls, (r->>'score')::int, (r->>'distance')::real);
  if is_daily then
    perform jr_private.post_score(uid, 'daily:' || dk, (r->>'score')::int, (r->>'distance')::real);
    perform jr_private.post_score(uid, 'daily:' || dk || ':c' || cls, (r->>'score')::int, (r->>'distance')::real);
  end if;

  return jsonb_build_object(
    'accepted', true, 'clamped', clamped, 'credited_coins', v_coins, 'credited_gems', v_gems, 'coin_bonus', bonus, 'xp_gained', xp_gain, 'level_reward', lvl_reward,
    'daily', is_daily,
    'rank_all', jr_private.rank_on(uid, 'all'), 'rank_week', jr_private.rank_on(uid, 'week:' || wk),
    'rank_daily', case when is_daily then jr_private.rank_on(uid, 'daily:' || dk) else 0 end,
    'class', cls,
    'class_rank_all', jr_private.rank_on(uid, 'all:c' || cls), 'class_rank_week', jr_private.rank_on(uid, 'week:' || wk || ':c' || cls),
    'class_rank_daily', case when is_daily then jr_private.rank_on(uid, 'daily:' || dk || ':c' || cls) else 0 end,
    'snapshot', jr_private.snapshot(uid));
end $$;


-- p_board: 'all' | 'week' | 'daily'   p_scope: 'global' | 'friends'   p_class: null = everyone, 0..2 = that stat class
drop function if exists public.jr_leaderboard(text, text, int);
create or replace function public.jr_leaderboard(p_board text, p_scope text default 'global', p_limit int default 50, p_class int default null) returns jsonb
language plpgsql security definer set search_path = public, jr_private as $$
declare
  uid uuid := auth.uid();
  b text := case p_board when 'week' then 'week:' || jr_private.week_key(jr_private.ph_date())
                         when 'daily' then 'daily:' || jr_private.day_key(jr_private.ph_date()) else 'all' end
            || case when p_class between 0 and 2 then ':c' || p_class else '' end;
  lim int := least(greatest(coalesce(p_limit, 50), 1), 100);
  friends_only boolean := p_scope = 'friends';
  rows jsonb; me jsonb;
begin
  perform jr_private.ensure_profile(uid);
  with pool as (
    select s.user_id, s.score, s.distance, s.updated_at from public.jr_scores s
    where s.board = b and (not friends_only or s.user_id = uid or exists (select 1 from public.jr_friends f where f.user_id = uid and f.friend_id = s.user_id))),
  ranked as (
    select p.*, rank() over (order by p.score desc, p.updated_at) as rk from pool p)
  select coalesce(jsonb_agg(jsonb_build_object('rank', r.rk, 'user_id', r.user_id, 'display_name', pr.display_name, 'score', r.score,
           'distance', r.distance, 'level', jr_private.level_for(pr.xp), 'is_me', r.user_id = uid) order by r.rk) filter (where r.rk <= lim), '[]'::jsonb),
         (array_agg(jsonb_build_object('rank', r.rk, 'user_id', r.user_id, 'display_name', pr.display_name, 'score', r.score,
           'distance', r.distance, 'level', jr_private.level_for(pr.xp), 'is_me', true)) filter (where r.user_id = uid))[1]
    into rows, me
  from ranked r join public.jr_profiles pr on pr.user_id = r.user_id;
  return jsonb_build_object('board', b, 'scope', case when friends_only then 'friends' else 'global' end,
    'class', case when p_class between 0 and 2 then p_class else -1 end, 'rows', rows, 'me', me);
end $$;
revoke all on function public.jr_leaderboard(text, text, int, int) from public, anon;
grant execute on function public.jr_leaderboard(text, text, int, int) to authenticated;
