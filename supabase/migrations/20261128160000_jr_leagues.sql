-- Weekly leagues (same rules as C# Logic/Leagues.cs). A driver's first scored run of the PH week
-- (board 'week:<wk>') puts them in a group of up to 30 drivers of their tier, ranked by their best
-- score that week. Settled lazily the next time the driver opens the league: top 7 move up a tier,
-- bottom 5 move down (groups of 10+), the top places win coins (claimed once).

create table if not exists public.jr_league_groups (
  id bigserial primary key,
  week int not null,
  tier int not null,
  created_at timestamptz not null default now());
create index if not exists jr_league_groups_open on public.jr_league_groups (week, tier, id);
create table if not exists public.jr_league_members (
  user_id uuid not null references public.jr_profiles (user_id) on delete cascade,
  week int not null,
  tier int not null,
  group_id bigint not null references public.jr_league_groups (id) on delete cascade,
  result_tier int,              -- next week's tier, set when the week is settled
  final_rank int,
  reward int not null default 0,
  claimed boolean not null default false,
  joined_at timestamptz not null default now(),
  primary key (user_id, week));
create index if not exists jr_league_members_group on public.jr_league_members (group_id);
alter table public.jr_league_groups enable row level security;
alter table public.jr_league_members enable row level security;
revoke all on public.jr_league_groups, public.jr_league_members from anon, authenticated;

create or replace function jr_private.league_next_tier(tier int, rnk int, players int) returns int language sql immutable as $$
  select case when rnk <= 0 then tier
              when rnk <= 7 then least(4, tier + 1)
              when players >= 10 and rnk > players - 5 then greatest(0, tier - 1)
              else tier end $$;
create or replace function jr_private.league_reward(tier int, rnk int) returns int language sql immutable as $$
  select (case when rnk = 1 then 500 when rnk = 2 then 300 when rnk = 3 then 200 when rnk between 4 and 7 then 100 else 0 end) * (2 + least(4, greatest(0, tier))) / 2 $$;

-- standings of a group: members with their weekly best score (0 if none), ranked
create or replace function jr_private.league_table(p_group bigint, p_week int)
returns table (user_id uuid, score int, rnk int, players int)
language sql stable security definer set search_path = public, jr_private as $$
  with t as (
    select m.user_id, coalesce(s.score, 0) as score, m.joined_at
    from public.jr_league_members m
    left join public.jr_scores s on s.user_id = m.user_id and s.board = 'week:' || p_week
    where m.group_id = p_group)
  select t.user_id, t.score, (row_number() over (order by t.score desc, t.joined_at))::int,
         (count(*) filter (where t.score > 0) over ())::int
  from t $$;

-- settles the driver's last unsettled past week; returns the tier for this week
create or replace function jr_private.league_settle(uid uuid, wk int) returns int
language plpgsql security definer set search_path = public, jr_private as $$
declare m public.jr_league_members; r record;
begin
  select * into m from public.jr_league_members where user_id = uid and week < wk order by week desc limit 1 for update;
  if not found then return 0; end if;
  if m.result_tier is null then
    select * into r from jr_private.league_table(m.group_id, m.week) t where t.user_id = uid;
    if coalesce(r.score, 0) <= 0 then
      update public.jr_league_members set result_tier = m.tier, final_rank = 0 where user_id = uid and week = m.week;
      return m.tier;
    end if;
    m.result_tier := jr_private.league_next_tier(m.tier, r.rnk, r.players);
    update public.jr_league_members set result_tier = m.result_tier, final_rank = r.rnk, reward = jr_private.league_reward(m.tier, r.rnk)
    where user_id = uid and week = m.week;
  end if;
  return m.result_tier;
end $$;

create or replace function public.jr_league_status() returns jsonb
language plpgsql security definer set search_path = public, jr_private as $$
declare
  uid uuid := auth.uid(); d date := jr_private.ph_date(); wk int := jr_private.week_key(d);
  v_tier int; m public.jr_league_members; g bigint; my int; last public.jr_league_members; rows jsonb; me_rank int := 0; me_players int := 0;
begin
  perform jr_private.ensure_profile(uid);
  v_tier := jr_private.league_settle(uid, wk);
  select * into m from public.jr_league_members where user_id = uid and week = wk;
  if not found then
    select score into my from public.jr_scores where user_id = uid and board = 'week:' || wk;
    if coalesce(my, 0) > 0 then
      -- join: the oldest open group of this tier, or a new one
      select gr.id into g from public.jr_league_groups gr
       where gr.week = wk and gr.tier = v_tier and (select count(*) from public.jr_league_members x where x.group_id = gr.id) < 30
       order by gr.id limit 1 for update skip locked;
      if g is null then insert into public.jr_league_groups (week, tier) values (wk, v_tier) returning id into g; end if;
      insert into public.jr_league_members (user_id, week, tier, group_id) values (uid, wk, v_tier, g) returning * into m;
    end if;
  end if;
  select * into last from public.jr_league_members where user_id = uid and week < wk order by week desc limit 1;
  if m.group_id is not null then
    select coalesce(jsonb_agg(jsonb_build_object('rank', t.rnk, 'display_name', p.display_name, 'level', jr_private.level_for(p.xp), 'score', t.score, 'is_me', t.user_id = uid) order by t.rnk), '[]'::jsonb)
      into rows from jr_private.league_table(m.group_id, wk) t join public.jr_profiles p on p.user_id = t.user_id;
    select t.rnk, t.players into me_rank, me_players from jr_private.league_table(m.group_id, wk) t where t.user_id = uid;
  end if;
  return jsonb_build_object('joined', m.group_id is not null, 'week', wk, 'tier', coalesce(m.tier, v_tier),
    'days_left', 7 - ((d - date '2024-01-01') % 7), 'rank', coalesce(me_rank, 0), 'players', coalesce(me_players, 0),
    'rows', coalesce(rows, '[]'::jsonb),
    'last', case when last is null or last.user_id is null then null else jsonb_build_object('week', last.week, 'tier', last.tier, 'result_tier', last.result_tier,
      'rank', coalesce(last.final_rank, 0), 'reward', last.reward, 'claimed', last.claimed) end);
end $$;

create or replace function public.jr_league_claim() returns jsonb
language plpgsql security definer set search_path = public, jr_private as $$
declare uid uuid := auth.uid(); wk int := jr_private.week_key(jr_private.ph_date()); m public.jr_league_members;
begin
  perform jr_private.league_settle(uid, wk);
  select * into m from public.jr_league_members where user_id = uid and week < wk order by week desc limit 1 for update;
  if not found or m.reward <= 0 then return jsonb_build_object('ok', false, 'reason', 'no_reward', 'snapshot', jr_private.snapshot(uid)); end if;
  if m.claimed then return jsonb_build_object('ok', false, 'reason', 'claimed', 'snapshot', jr_private.snapshot(uid)); end if;
  update public.jr_league_members set claimed = true where user_id = uid and week = m.week;
  update public.jr_profiles set coins = coins + m.reward, updated_at = now() where user_id = uid;
  insert into public.jr_transactions (user_id, kind, amount, ref, currency) values (uid, 'league', m.reward, 'week' || m.week, 'coins');
  return jsonb_build_object('ok', true, 'reward', m.reward, 'snapshot', jr_private.snapshot(uid));
end $$;

revoke all on function public.jr_league_status(), public.jr_league_claim() from public, anon;
grant execute on function public.jr_league_status(), public.jr_league_claim() to authenticated;
revoke all on function jr_private.league_settle(uuid, int), jr_private.league_table(bigint, int) from public, anon, authenticated;
