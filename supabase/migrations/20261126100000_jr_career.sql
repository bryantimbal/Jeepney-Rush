-- Career mode (BUHAY TSUPER): shifts to a terminal on a set route, three stars each
-- (★ reach the terminal, ★ bring home the boundary, ★ the shift's extra goal), driver's licenses
-- earned with stars. Same rules as C# Logic/Career.cs; the shifts and licenses come from the
-- generated catalog (*_jr_catalog.sql). Every NEW star pays coins; each new license pays once.

create table if not exists public.jr_catalog_career (
  id text primary key,
  idx int not null unique,
  chapter int not null,
  env int not null,
  length int not null check (length > 0),
  boundary int not null check (boundary >= 0),
  goal text not null,
  target int not null,
  title text not null);
create table if not exists public.jr_catalog_licenses (
  rank int primary key,
  name text not null,
  stars_needed int not null,
  coins int not null default 0,
  gems int not null default 0);
create table if not exists public.jr_career (
  user_id uuid not null references public.jr_profiles (user_id) on delete cascade,
  shift_id text not null,
  mask int not null default 0 check (mask between 0 and 7),
  updated_at timestamptz not null default now(),
  primary key (user_id, shift_id));
alter table public.jr_profiles add column if not exists career_rank int not null default 0;

do $$
declare t text;
begin
  foreach t in array array['jr_catalog_career','jr_catalog_licenses','jr_career'] loop
    execute format('alter table public.%I enable row level security', t);
    execute format('revoke all on public.%I from anon, authenticated', t);
  end loop;
  foreach t in array array['jr_catalog_career','jr_catalog_licenses'] loop
    execute format('grant select on public.%I to anon, authenticated', t);
    execute format('drop policy if exists catalog_read on public.%I', t);
    execute format('create policy catalog_read on public.%I for select to anon, authenticated using (true)', t);
  end loop;
end $$;
grant select on public.jr_career to authenticated;
drop policy if exists own_rows on public.jr_career;
create policy own_rows on public.jr_career for select to authenticated using (user_id = auth.uid());

create or replace function jr_private.career_rank_for(p_stars int) returns int
language sql stable security definer set search_path = public as $$
  select coalesce(max(rank), 0) from public.jr_catalog_licenses where stars_needed <= p_stars
$$;

create or replace function jr_private.career_stars(p_uid uuid) returns int
language sql stable security definer set search_path = public as $$
  select coalesce(sum((mask & 1) + ((mask >> 1) & 1) + ((mask >> 2) & 1)), 0)::int from public.jr_career where user_id = p_uid
$$;

-- Applies a (clamped) run report to the career. Expects the caller to hold the profile row lock.
create or replace function jr_private.career_apply(p_uid uuid, r jsonb) returns jsonb
language plpgsql security definer set search_path = public, jr_private as $$
declare
  s public.jr_catalog_career; prev public.jr_catalog_career;
  old_mask int; m int := 0; new_mask int; val int; met boolean;
  star_coins int := 0; rank_before int; rank_after int; rank_coins int := 0; rank_gems int := 0;
begin
  select * into s from public.jr_catalog_career where id = coalesce(r->>'career', '');
  if not found then return jsonb_build_object('shift', ''); end if;
  -- unlocked? the license for this chapter, and a star on the shift before it in the same chapter
  if jr_private.career_rank_for(jr_private.career_stars(p_uid)) < s.chapter then
    return jsonb_build_object('shift', s.id, 'mask', 0, 'new_mask', 0, 'locked', true);
  end if;
  select * into prev from public.jr_catalog_career where idx = s.idx - 1 and chapter = s.chapter;
  if found and coalesce((select c.mask from public.jr_career c where c.user_id = p_uid and c.shift_id = prev.id), 0) = 0 then
    return jsonb_build_object('shift', s.id, 'mask', 0, 'new_mask', 0, 'locked', true);
  end if;

  if (r->>'env')::int = s.env and (r->>'distance')::real >= s.length - 0.5 then
    m := 1;
    if (r->>'coins')::int >= s.boundary then m := m | 2; end if;
    val := case s.goal
      when 'Deliveries' then (r->>'deliveries')::int
      when 'MaxHits' then coalesce((r->>'hits')::int, 0)
      when 'NearMisses' then coalesce((r->>'near_misses')::int, 0)
      when 'Overtakes' then coalesce((r->>'overtakes')::int, 0)
      when 'Streak' then coalesce((r->>'best_streak')::int, 0)
      when 'CleanDistance' then trunc(coalesce((r->>'clean_distance')::real, 0))::int
      when 'Passengers' then (r->>'passengers')::int
      else (r->>'smooth_humps')::int end;
    met := case when s.goal = 'MaxHits' then val <= s.target else val >= s.target end;
    if met then m := m | 4; end if;
  end if;

  select coalesce(c.mask, 0) into old_mask from public.jr_career c where c.user_id = p_uid and c.shift_id = s.id;
  old_mask := coalesce(old_mask, 0);
  new_mask := m & ~old_mask;
  if m <> 0 then
    insert into public.jr_career (user_id, shift_id, mask) values (p_uid, s.id, m)
    on conflict (user_id, shift_id) do update set mask = public.jr_career.mask | excluded.mask, updated_at = now();
  end if;
  star_coins := ((new_mask & 1) + ((new_mask >> 1) & 1) + ((new_mask >> 2) & 1))
                * (jr_private.const('career_star_base')::int + jr_private.const('career_star_per_chapter')::int * s.chapter);
  if star_coins > 0 then
    update public.jr_profiles set coins = coins + star_coins where user_id = p_uid;
    insert into public.jr_transactions (user_id, kind, amount, ref, currency) values (p_uid, 'career_star', star_coins, s.id, 'coins');
  end if;

  select career_rank into rank_before from public.jr_profiles where user_id = p_uid;
  rank_after := jr_private.career_rank_for(jr_private.career_stars(p_uid));
  if rank_after > rank_before then
    select coalesce(sum(coins), 0), coalesce(sum(gems), 0) into rank_coins, rank_gems
      from public.jr_catalog_licenses where rank > rank_before and rank <= rank_after;
    update public.jr_profiles set career_rank = rank_after, coins = coins + rank_coins, gems = gems + rank_gems where user_id = p_uid;
    if rank_coins > 0 then insert into public.jr_transactions (user_id, kind, amount, ref, currency) values (p_uid, 'career_license', rank_coins, rank_after::text, 'coins'); end if;
    if rank_gems > 0 then insert into public.jr_transactions (user_id, kind, amount, ref, currency) values (p_uid, 'career_license', rank_gems, rank_after::text, 'gems'); end if;
  end if;
  return jsonb_build_object('shift', s.id, 'mask', m, 'new_mask', new_mask, 'star_coins', star_coins,
    'new_rank', case when rank_after > rank_before then rank_after else -1 end, 'rank_coins', rank_coins, 'rank_gems', rank_gems);
end $$;

revoke all on function jr_private.career_rank_for(int), jr_private.career_stars(uuid), jr_private.career_apply(uuid, jsonb) from public, anon, authenticated;

create or replace function jr_private.snapshot(p_uid uuid) returns jsonb
language plpgsql security definer set search_path = public, jr_private as $$
declare
  p public.jr_profiles; dk int := jr_private.day_key(jr_private.ph_date()); wk int := jr_private.week_key(jr_private.ph_date());
  anon boolean;
begin
  perform jr_private.ensure_missions(p_uid, 'daily', dk);
  perform jr_private.ensure_missions(p_uid, 'weekly', wk);
  select * into p from public.jr_profiles where user_id = p_uid;
  select coalesce(u.is_anonymous, false) into anon from auth.users u where u.id = p_uid;
  return jsonb_build_object(
    'user_id', p.user_id, 'display_name', p.display_name, 'friend_code', p.friend_code, 'is_anonymous', coalesce(anon, false),
    'coins', p.coins, 'xp', p.xp, 'level', jr_private.level_for(p.xp),
    'best_score', p.best_score, 'best_distance', p.best_distance, 'best_clean', p.best_clean, 'runs', p.runs,
    'total_distance', p.total_distance, 'total_coins', p.total_coins, 'total_passengers', p.total_passengers,
    'total_deliveries', p.total_deliveries, 'total_power_ups', p.total_power_ups, 'total_smooth_humps', p.total_smooth_humps,
    'total_wet_distance', p.total_wet_distance, 'total_refuels', p.total_refuels,
    'owned', coalesce((select jsonb_agg(item_id order by acquired_at, item_id) from public.jr_inventory where user_id = p_uid and item_kind = 'jeepney'), '[]'::jsonb),
    'upgrades', (select jsonb_agg(coalesce(i.level, 0) order by u.kind)
                 from (select distinct kind from public.jr_catalog_upgrades) u
                 left join public.jr_inventory i on i.user_id = p_uid and i.item_kind = 'upgrade' and i.item_id = u.kind::text),
    'day_key', dk,
    'daily_progress', (select jsonb_agg(progress order by slot) from public.jr_mission_progress where user_id = p_uid and period = 'daily' and period_key = dk),
    'daily_claimed', (select jsonb_agg(claimed order by slot) from public.jr_mission_progress where user_id = p_uid and period = 'daily' and period_key = dk),
    'week_key', wk,
    'weekly_progress', (select jsonb_agg(progress order by slot) from public.jr_mission_progress where user_id = p_uid and period = 'weekly' and period_key = wk),
    'weekly_claimed', (select jsonb_agg(claimed order by slot) from public.jr_mission_progress where user_id = p_uid and period = 'weekly' and period_key = wk),
    'achievements_unlocked', coalesce((select jsonb_agg(a.id order by a.id) from public.jr_catalog_achievements a where jr_private.stat_value(p, a.stat_key) >= a.target), '[]'::jsonb),
    'achievements_claimed', coalesce((select jsonb_agg(achievement_id order by achievement_id) from public.jr_achievements_claimed where user_id = p_uid), '[]'::jsonb),
    'gems', p.gems, 'ad_free', p.ad_free,
    'owned_products', coalesce((select jsonb_agg(distinct product_id) from public.jr_purchases pu join public.jr_catalog_products cp on cp.id = pu.product_id
                                where pu.user_id = p_uid and pu.state = 'granted' and cp.kind = 'non_consumable'), '[]'::jsonb),
    'missions', (select jsonb_agg(jsonb_build_object('period', x.period, 'slot', x.slot, 'type', x.type, 'target', x.target, 'reward', x.reward, 'text', x.text))
                 from (select 'daily' as period, m.* from jr_private.missions('daily', dk) m
                       union all select 'weekly', m.* from jr_private.missions('weekly', wk) m) x),
    'career', coalesce((select jsonb_agg(coalesce(c.mask, 0) order by s.idx) from public.jr_catalog_career s
                        left join public.jr_career c on c.user_id = p_uid and c.shift_id = s.id), '[]'::jsonb),
    'career_rank', p.career_rank);
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
