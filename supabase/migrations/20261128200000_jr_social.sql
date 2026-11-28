-- Friend gifts and the barangay weekly goal.
--  • Gifts: once a day you can send each friend a free tank of gas (60 coins); a player can claim up
--    to 10 gifts a day (the rest wait for tomorrow).
--  • Barangay goal: all members' passengers this PH week (accepted runs) count toward 10,000; when the
--    goal is reached every member can claim 300 coins + 5 gems once that week.

create table if not exists public.jr_gifts (
  from_user uuid not null references public.jr_profiles (user_id) on delete cascade,
  to_user uuid not null references public.jr_profiles (user_id) on delete cascade,
  day_key int not null,
  claimed_day int,
  created_at timestamptz not null default now(),
  primary key (from_user, to_user, day_key));
create index if not exists jr_gifts_inbox on public.jr_gifts (to_user, claimed_day);
create table if not exists public.jr_barangay_goal_claims (
  user_id uuid not null references public.jr_profiles (user_id) on delete cascade,
  week int not null,
  primary key (user_id, week));
alter table public.jr_gifts enable row level security;
alter table public.jr_barangay_goal_claims enable row level security;
revoke all on public.jr_gifts, public.jr_barangay_goal_claims from anon, authenticated;

create or replace function public.jr_gift_send(p_friend uuid) returns jsonb
language plpgsql security definer set search_path = public, jr_private as $$
declare uid uuid := auth.uid(); dk int := jr_private.day_key(jr_private.ph_date());
begin
  if not exists (select 1 from public.jr_friends where user_id = uid and friend_id = p_friend) then return jsonb_build_object('ok', false, 'reason', 'not_friend'); end if;
  insert into public.jr_gifts (from_user, to_user, day_key) values (uid, p_friend, dk) on conflict do nothing;
  if not found then return jsonb_build_object('ok', false, 'reason', 'already_today'); end if;
  return jsonb_build_object('ok', true);
end $$;

create or replace function public.jr_gift_inbox() returns jsonb
language sql stable security definer set search_path = public, jr_private as $$
  select jsonb_build_object(
    'gifts', coalesce((select jsonb_agg(jsonb_build_object('from', p.display_name, 'day_key', g.day_key) order by g.created_at)
                       from public.jr_gifts g join public.jr_profiles p on p.user_id = g.from_user
                       where g.to_user = auth.uid() and g.claimed_day is null), '[]'::jsonb),
    'claimed_today', (select count(*) from public.jr_gifts where to_user = auth.uid() and claimed_day = jr_private.day_key(jr_private.ph_date())),
    'sent_today', coalesce((select jsonb_agg(to_user) from public.jr_gifts where from_user = auth.uid() and day_key = jr_private.day_key(jr_private.ph_date())), '[]'::jsonb)) $$;

create or replace function public.jr_gift_claim() returns jsonb
language plpgsql security definer set search_path = public, jr_private as $$
declare uid uuid := auth.uid(); dk int := jr_private.day_key(jr_private.ph_date()); left_today int; n int;
begin
  left_today := greatest(0, 10 - (select count(*) from public.jr_gifts where to_user = uid and claimed_day = dk));
  with c as (select from_user, to_user, day_key from public.jr_gifts where to_user = uid and claimed_day is null order by created_at limit left_today for update)
  update public.jr_gifts g set claimed_day = dk from c where g.from_user = c.from_user and g.to_user = c.to_user and g.day_key = c.day_key;
  get diagnostics n = row_count;
  if n = 0 then return jsonb_build_object('ok', false, 'reason', case when left_today = 0 then 'limit_today' else 'nothing' end, 'snapshot', jr_private.snapshot(uid)); end if;
  update public.jr_profiles set coins = coins + n * 60, updated_at = now() where user_id = uid;
  insert into public.jr_transactions (user_id, kind, amount, ref, currency) values (uid, 'gift', n * 60, n || ' gifts', 'coins');
  return jsonb_build_object('ok', true, 'reward', n * 60, 'snapshot', jr_private.snapshot(uid));
end $$;

-- the caller's barangay: passengers this week (members' accepted runs) toward the goal
create or replace function jr_private.barangay_goal(uid uuid, out barangay uuid, out progress int, out week int)
language plpgsql stable security definer set search_path = public, jr_private as $$
declare d date := jr_private.ph_date(); wstart date;
begin
  week := jr_private.week_key(d);
  wstart := date '2024-01-01' + (week - 1) * 7;
  select m.barangay_id into barangay from public.jr_barangay_members m where m.user_id = uid;
  if barangay is null then progress := 0; return; end if;
  select coalesce(sum(least(coalesce((r.report->>'passengers')::int, 0), 500)), 0)::int into progress
  from public.jr_runs r join public.jr_barangay_members m on m.user_id = r.user_id and m.barangay_id = barangay
  where r.accepted and r.created_at >= (wstart::timestamp at time zone 'Asia/Manila') and r.created_at < ((wstart + 7)::timestamp at time zone 'Asia/Manila');
end $$;

create or replace function public.jr_barangay_goal_status() returns jsonb
language plpgsql stable security definer set search_path = public, jr_private as $$
declare uid uuid := auth.uid(); g record;
begin
  select * into g from jr_private.barangay_goal(uid);
  return jsonb_build_object('member', g.barangay is not null, 'target', 10000, 'progress', coalesce(g.progress, 0),
    'claimed', exists (select 1 from public.jr_barangay_goal_claims where user_id = uid and week = g.week), 'coins', 300, 'gems', 5);
end $$;

create or replace function public.jr_barangay_goal_claim() returns jsonb
language plpgsql security definer set search_path = public, jr_private as $$
declare uid uuid := auth.uid(); g record;
begin
  select * into g from jr_private.barangay_goal(uid);
  if g.barangay is null then return jsonb_build_object('ok', false, 'reason', 'no_barangay', 'snapshot', jr_private.snapshot(uid)); end if;
  if g.progress < 10000 then return jsonb_build_object('ok', false, 'reason', 'not_done', 'snapshot', jr_private.snapshot(uid)); end if;
  insert into public.jr_barangay_goal_claims (user_id, week) values (uid, g.week) on conflict do nothing;
  if not found then return jsonb_build_object('ok', false, 'reason', 'claimed', 'snapshot', jr_private.snapshot(uid)); end if;
  update public.jr_profiles set coins = coins + 300, gems = gems + 5, updated_at = now() where user_id = uid;
  insert into public.jr_transactions (user_id, kind, amount, ref, currency) values (uid, 'barangay_goal', 300, 'week' || g.week, 'coins'), (uid, 'barangay_goal', 5, 'week' || g.week, 'gems');
  return jsonb_build_object('ok', true, 'reward', 300, 'snapshot', jr_private.snapshot(uid));
end $$;

revoke all on function public.jr_gift_send(uuid), public.jr_gift_inbox(), public.jr_gift_claim(), public.jr_barangay_goal_status(), public.jr_barangay_goal_claim() from public, anon;
grant execute on function public.jr_gift_send(uuid), public.jr_gift_inbox(), public.jr_gift_claim(), public.jr_barangay_goal_status(), public.jr_barangay_goal_claim() to authenticated;
