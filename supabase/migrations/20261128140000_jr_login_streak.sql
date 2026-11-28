-- Login streak "Arawang Pasada" (same rules as C# Logic/LoginStreak.cs): one reward per PH day,
-- growing over 7 days, then the cycle starts again. One missed day can be saved once a week (ad or
-- 10 gems); today's reward can be doubled once with an ad. Ad rewards use the verified AdMob flow
-- (new kinds 'streakdouble' and 'streaksave'; redeploy the admob-ssv function).

create table if not exists public.jr_login_streaks (
  user_id uuid primary key references public.jr_profiles (user_id) on delete cascade,
  streak int not null default 0,
  last_day int not null default -1,      -- days since 2024-01-01 (PH)
  save_week int not null default 0,
  doubled_day int not null default -1,
  updated_at timestamptz not null default now());
alter table public.jr_login_streaks enable row level security;
revoke all on public.jr_login_streaks from anon, authenticated;

create or replace function jr_private.streak_today() returns int language sql stable as $$ select (jr_private.ph_date() - date '2024-01-01')::int $$;
create or replace function jr_private.streak_cal(streak int) returns int language sql immutable as $$ select ((greatest(1, streak) - 1) % 7) + 1 $$;
create or replace function jr_private.streak_reward(cal int, out coins int, out gems int) language sql immutable as $$
  select (array[100, 150, 0, 250, 300, 0, 500])[cal], (array[0, 0, 5, 0, 0, 10, 15])[cal] $$;

create or replace function public.jr_streak_status() returns jsonb
language plpgsql stable security definer set search_path = public, jr_private as $$
declare uid uuid := auth.uid(); t int := jr_private.streak_today(); s public.jr_login_streaks; can_save boolean := false; day int; broken boolean := false;
begin
  select * into s from public.jr_login_streaks where user_id = uid;
  if not found then s.streak := 0; s.last_day := -1; s.save_week := 0; s.doubled_day := -1; end if;
  if s.last_day = t then
    return jsonb_build_object('claimed', true, 'day', jr_private.streak_cal(s.streak), 'streak', s.streak, 'doubled', s.doubled_day = t, 'can_save', false, 'broken', false, 'save_gems', 10);
  elsif s.last_day = t - 1 then day := jr_private.streak_cal(s.streak + 1);
  elsif s.last_day >= 0 and s.last_day = t - 2 and s.streak > 0 then
    can_save := s.save_week <> t / 7 + 1; broken := not can_save;
    day := case when can_save then jr_private.streak_cal(s.streak + 1) else 1 end;
  else broken := s.last_day >= 0 and s.streak > 0; day := 1;
  end if;
  return jsonb_build_object('claimed', false, 'day', day, 'streak', s.streak, 'doubled', false, 'can_save', can_save, 'broken', broken, 'save_gems', 10);
end $$;

create or replace function public.jr_streak_claim() returns jsonb
language plpgsql security definer set search_path = public, jr_private as $$
declare uid uuid := auth.uid(); t int := jr_private.streak_today(); s public.jr_login_streaks; cal int; rw record;
begin
  perform jr_private.ensure_profile(uid);
  insert into public.jr_login_streaks (user_id) values (uid) on conflict do nothing;
  select * into s from public.jr_login_streaks where user_id = uid for update;
  if s.last_day = t then return jsonb_build_object('ok', false, 'reason', 'already_today', 'snapshot', jr_private.snapshot(uid)); end if;
  s.streak := case when s.last_day = t - 1 then s.streak + 1 else 1 end;
  cal := jr_private.streak_cal(s.streak);
  select * into rw from jr_private.streak_reward(cal);
  update public.jr_login_streaks set streak = s.streak, last_day = t, updated_at = now() where user_id = uid;
  update public.jr_profiles set coins = coins + rw.coins, gems = gems + rw.gems, updated_at = now() where user_id = uid;
  if rw.coins > 0 then insert into public.jr_transactions (user_id, kind, amount, ref, currency) values (uid, 'streak', rw.coins, 'day' || cal, 'coins'); end if;
  if rw.gems > 0 then insert into public.jr_transactions (user_id, kind, amount, ref, currency) values (uid, 'streak', rw.gems, 'day' || cal, 'gems'); end if;
  return jsonb_build_object('ok', true, 'day', cal, 'streak', s.streak, 'reward', rw.coins, 'gems', rw.gems, 'snapshot', jr_private.snapshot(uid));
end $$;

-- today's reward again (ad); null coins = not possible
create or replace function jr_private.streak_double(uid uuid, out o_coins int, out o_gems int)
language plpgsql security definer set search_path = public, jr_private as $$
declare t int := jr_private.streak_today(); s public.jr_login_streaks; rw record;
begin
  select * into s from public.jr_login_streaks where user_id = uid for update;
  if not found or s.last_day <> t or s.doubled_day = t then o_coins := null; return; end if;
  select * into rw from jr_private.streak_reward(jr_private.streak_cal(s.streak));
  update public.jr_login_streaks set doubled_day = t, updated_at = now() where user_id = uid;
  update public.jr_profiles set coins = coins + rw.coins, gems = gems + rw.gems, updated_at = now() where user_id = uid;
  if rw.coins > 0 then insert into public.jr_transactions (user_id, kind, amount, ref, currency) values (uid, 'streak_ad', rw.coins, 'x2', 'coins'); end if;
  if rw.gems > 0 then insert into public.jr_transactions (user_id, kind, amount, ref, currency) values (uid, 'streak_ad', rw.gems, 'x2', 'gems'); end if;
  o_coins := rw.coins; o_gems := rw.gems;
end $$;

-- one missed day counts as played (once a week)
create or replace function jr_private.streak_save(uid uuid) returns boolean
language plpgsql security definer set search_path = public, jr_private as $$
declare t int := jr_private.streak_today(); s public.jr_login_streaks;
begin
  select * into s from public.jr_login_streaks where user_id = uid for update;
  if not found or s.streak <= 0 or s.last_day <> t - 2 or s.save_week = t / 7 + 1 then return false; end if;
  update public.jr_login_streaks set last_day = t - 1, save_week = t / 7 + 1, updated_at = now() where user_id = uid;
  return true;
end $$;

create or replace function public.jr_streak_save_gems() returns jsonb
language plpgsql security definer set search_path = public, jr_private as $$
declare uid uuid := auth.uid(); g int;
begin
  select gems into g from public.jr_profiles where user_id = uid for update;
  if coalesce(g, 0) < 10 then return jsonb_build_object('ok', false, 'reason', 'not_enough_gems', 'snapshot', jr_private.snapshot(uid)); end if;
  if not jr_private.streak_save(uid) then return jsonb_build_object('ok', false, 'reason', 'streak_cant_save', 'snapshot', jr_private.snapshot(uid)); end if;
  update public.jr_profiles set gems = gems - 10, updated_at = now() where user_id = uid;
  insert into public.jr_transactions (user_id, kind, amount, ref, currency) values (uid, 'streak_save', -10, 'save', 'gems');
  return jsonb_build_object('ok', true, 'snapshot', jr_private.snapshot(uid));
end $$;

create or replace function public.jr_srv_record_ad_reward(p_user uuid, p_nonce text, p_kind text, p_ref text, p_test boolean default false)
returns jsonb
language plpgsql security definer set search_path = public, jr_private as $$
begin
  if p_user is null or coalesce(p_nonce, '') = '' or p_kind not in ('revive', 'doublecoins', 'dailybonus', 'trial', 'streakdouble', 'streaksave') then
    return jsonb_build_object('ok', false, 'reason', 'bad_request'); end if;
  if exists (select 1 from jr_private.retired_ad_nonces where nonce = p_nonce) then return jsonb_build_object('ok', true); end if;
  perform jr_private.ensure_profile(p_user);
  insert into public.jr_ad_rewards (nonce, user_id, kind, ref, test) values (p_nonce, p_user, p_kind, p_ref, p_test) on conflict (nonce) do nothing;
  return jsonb_build_object('ok', true);
end $$;

create or replace function public.jr_claim_ad_reward(p_nonce text) returns jsonb
language plpgsql security definer set search_path = public, jr_private as $$
declare
  uid uuid := auth.uid(); r public.jr_ad_rewards; amount int := 0; gems_extra int := 0; run public.jr_runs;
  today date := jr_private.ph_date();
begin
  perform jr_private.ensure_profile(uid);
  select * into r from public.jr_ad_rewards where nonce = p_nonce for update;
  if not found then
    -- development: simulated ads (no SSV callback) only when explicitly allowed
    if p_nonce like 'test-%' and coalesce(jr_private.setting('allow_test_rewards'), 'false') = 'true' and split_part(p_nonce, ':', 2) <> '' then
      insert into public.jr_ad_rewards (nonce, user_id, kind, ref, test)
      values (p_nonce, uid, split_part(p_nonce, ':', 2), nullif(split_part(p_nonce, ':', 3), ''), true) returning * into r;
    else
      return jsonb_build_object('ok', false, 'reason', 'not_verified');
    end if;
  end if;
  if r.user_id <> uid then return jsonb_build_object('ok', false, 'reason', 'not_verified'); end if;
  if r.claimed_at is not null then return jsonb_build_object('ok', false, 'reason', 'claimed', 'snapshot', jr_private.snapshot(uid)); end if;
  if r.kind = 'dailybonus' then
    if exists (select 1 from public.jr_ad_rewards x where x.user_id = uid and x.kind = 'dailybonus' and x.claimed_at is not null
               and (x.claimed_at at time zone 'Asia/Manila')::date = today) then
      return jsonb_build_object('ok', false, 'reason', 'already_today', 'snapshot', jr_private.snapshot(uid)); end if;
    amount := jr_private.const('ad_daily_bonus_coins')::int;
  elsif r.kind = 'doublecoins' then
    select * into run from public.jr_runs where run_id = r.ref and user_id = uid and accepted;
    if not found or run.created_at < now() - interval '2 hours' then
      return jsonb_build_object('ok', false, 'reason', 'run_not_found', 'snapshot', jr_private.snapshot(uid)); end if;
    if exists (select 1 from public.jr_ad_rewards x where x.user_id = uid and x.kind = 'doublecoins' and x.ref = r.ref and x.claimed_at is not null) then
      return jsonb_build_object('ok', false, 'reason', 'claimed', 'snapshot', jr_private.snapshot(uid)); end if;
    amount := least(run.credited_coins, jr_private.const('ad_max_double_coins')::int);
  elsif r.kind = 'streakdouble' then
    -- login streak: today's reward once more
    select o_coins, o_gems into amount, gems_extra from jr_private.streak_double(uid);
    if amount is null then return jsonb_build_object('ok', false, 'reason', 'streak_not_claimed', 'snapshot', jr_private.snapshot(uid)); end if;
    update public.jr_ad_rewards set claimed_at = now() where nonce = r.nonce;
    return jsonb_build_object('ok', true, 'reward', amount, 'gems', gems_extra, 'snapshot', jr_private.snapshot(uid));
  elsif r.kind = 'streaksave' then
    if not jr_private.streak_save(uid) then return jsonb_build_object('ok', false, 'reason', 'streak_cant_save', 'snapshot', jr_private.snapshot(uid)); end if;
  end if;
  update public.jr_ad_rewards set claimed_at = now() where nonce = r.nonce;
  if amount > 0 then
    update public.jr_profiles set coins = coins + amount, updated_at = now() where user_id = uid;
    insert into public.jr_transactions (user_id, kind, amount, ref, currency) values (uid, 'ad_' || r.kind, amount, r.ref, 'coins');
  end if;
  return jsonb_build_object('ok', true, 'reward', amount, 'snapshot', jr_private.snapshot(uid));
end $$;

revoke all on function public.jr_streak_status(), public.jr_streak_claim(), public.jr_streak_save_gems() from public, anon;
grant execute on function public.jr_streak_status(), public.jr_streak_claim(), public.jr_streak_save_gems() to authenticated;
revoke all on function jr_private.streak_double(uuid), jr_private.streak_save(uuid) from public, anon, authenticated;
