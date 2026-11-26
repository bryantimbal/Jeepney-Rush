-- Passenger missions: catch fare dodgers, earn tips, give lolas a smooth ride, get riders in a
-- hurry there on time. New mission pool entries carry from_key (the first day / week key they
-- can appear in), so missions of days and weeks that already started never change. Same rules as
-- C# Missions (FromKey) and RunBounds.Clamp.

alter table public.jr_catalog_missions add column if not exists from_key int not null default 0;

create or replace function jr_private.missions(p_period text, p_key int)
returns table (slot int, type text, target int, reward int, text text, in_run boolean)
language plpgsql stable as $$
declare
  s bigint := jr_private.rng_seed(p_key::bigint * 7919 + 17);
  idx int[];
  cnt int := case when p_period = 'daily' then jr_private.const('daily_count')::int else jr_private.const('weekly_count')::int end;
  base real := case when p_period = 'daily' then jr_private.const('daily_reward_base')::real else jr_private.const('weekly_reward_base')::real end;
  m int; pos int; pick int; steps int; tgt int; diff real; r record;
begin
  select array_agg(c.idx order by c.idx) into idx from public.jr_catalog_missions c where c.period = p_period and c.from_key <= p_key;
  for m in 0 .. cnt - 1 loop
    select n.s, n.v into s, pos from jr_private.rng_int(s, 0, array_length(idx, 1) - 1) n;
    pick := idx[pos + 1];
    idx := idx[1:pos] || idx[pos + 2:];
    select * into r from public.jr_catalog_missions c where c.period = p_period and c.idx = pick;
    steps := (r.max_target - r.min_target) / r.step;
    select n.s, n.v into s, pos from jr_private.rng_int(s, 0, steps) n;
    tgt := r.min_target + pos * r.step;
    diff := (tgt - r.min_target)::real / greatest(1, r.max_target - r.min_target)::real;
    slot := m; type := r.type; target := tgt;
    reward := trunc(base * (1::real + diff))::int;
    text := replace(r.text, '{0}', to_char(tgt, 'FM999,999,999'));
    in_run := r.in_run;
    return next;
  end loop;
end $$;

create or replace function jr_private.run_clamp(r jsonb) returns jsonb
language plpgsql immutable as $$
declare
  d real := (r->>'distance')::real;
  pas int := least(coalesce((r->>'passengers')::int, 0), trunc((d / 200::real + 2::real) * 15::real)::int);
  del int := least(coalesce((r->>'deliveries')::int, 0), pas);
  pu int := least(coalesce((r->>'power_ups')::int, 0), trunc(d / 250::real)::int + 3);
  hu int := least(coalesce((r->>'smooth_humps')::int, 0), trunc(d / 150::real)::int + 2);
  rf int := least(coalesce((r->>'refuels')::int, 0), trunc(d / 700::real)::int + 1);
  co int := least(coalesce((r->>'coins')::int, 0), trunc(d * 1.0::real)::int + del * 42 + 50);
  ot int := least(greatest(coalesce((r->>'overtakes')::int, 0), 0), trunc(d / 6::real)::int + 5);
  nm int := least(greatest(coalesce((r->>'near_misses')::int, 0), 0), trunc(d / 10::real)::int + 5);
  sc int := least(coalesce((r->>'score')::int, 0),
                  least(2147483647::bigint, trunc(d * 1.5::real)::bigint + co * 10 + pas * 50 + del * 600 + hu * 50 + trunc(150::real * (d / 300::real + 1::real))::bigint + 1000
                        + nm::bigint * 150 * 5 * 2 + ot::bigint * 40 * 5 * 2)::int);
begin
  return r || jsonb_build_object('passengers', pas, 'deliveries', del, 'power_ups', pu, 'smooth_humps', hu, 'refuels', rf, 'coins', co, 'score', sc, 'overtakes', ot, 'near_misses', nm,
    -- passenger personalities (C# RunBounds.Clamp): tips ≤ 25 per delivery, the counts ≤ deliveries
    'tips', greatest(0, least(coalesce((r->>'tips')::int, 0), del * 25)),
    'dodgers_caught', greatest(0, least(coalesce((r->>'dodgers_caught')::int, 0), del)),
    'smooth_lolas', greatest(0, least(coalesce((r->>'smooth_lolas')::int, 0), del)),
    'on_time', greatest(0, least(coalesce((r->>'on_time')::int, 0), del)));
end $$;

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
  car jsonb;
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
      when 'TotalDodgersCaught' then (r->>'dodgers_caught')::int
      when 'TotalTips' then (r->>'tips')::int
      when 'TotalSmoothLolas' then (r->>'smooth_lolas')::int
      when 'TotalOnTime' then (r->>'on_time')::int
      when 'Runs' then 1
      else 0 end;
    update public.jr_mission_progress mp set progress = least(m.target, case when m.in_run then greatest(mp.progress, val) else mp.progress + val end)
    where mp.user_id = uid and mp.period = m.period and mp.period_key = m.k and mp.slot = m.slot;
  end loop;

  -- career shift: stars, new-star coins and license rewards (same rules as C# Career.Apply)
  car := jr_private.career_apply(uid, r);

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
    'career', car,
    'class_rank_all', jr_private.rank_on(uid, 'all:c' || cls), 'class_rank_week', jr_private.rank_on(uid, 'week:' || wk || ':c' || cls),
    'class_rank_daily', case when is_daily then jr_private.rank_on(uid, 'daily:' || dk || ':c' || cls) else 0 end,
    'snapshot', jr_private.snapshot(uid));
end $$;
