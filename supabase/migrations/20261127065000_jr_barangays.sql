-- Barangay vs Barangay: players form barangay teams (create one, others join with its code).
-- A barangay's weekly score = the sum of its top 10 members' best scores this week (the normal
-- weekly board), so size alone doesn't win. One barangay per player; switching barangay is
-- allowed once every 3 days (no carrying a score from team to team). Names are checked like
-- driver names. All writes go through these functions; the tables can't be read directly.

create table if not exists public.jr_barangays (
  id uuid primary key default gen_random_uuid(),
  code text not null unique,
  name text not null,
  city text not null,
  created_by uuid references public.jr_profiles (user_id) on delete set null,
  created_at timestamptz not null default now());
create table if not exists public.jr_barangay_members (
  user_id uuid primary key references public.jr_profiles (user_id) on delete cascade,
  barangay_id uuid not null references public.jr_barangays (id) on delete cascade,
  joined_at timestamptz not null default now());
create index if not exists jr_barangay_members_b on public.jr_barangay_members (barangay_id);
-- when the player last switched (kept after leaving, for the cooldown)
alter table public.jr_profiles add column if not exists barangay_changed_at timestamptz;
alter table public.jr_barangays enable row level security;
alter table public.jr_barangay_members enable row level security;
revoke all on public.jr_barangays, public.jr_barangay_members from anon, authenticated;

create or replace function jr_private.barangay_name_ok(p text, maxlen int) returns boolean
language sql immutable as $$
  select length(btrim(coalesce(p, ''))) between 3 and maxlen
     and btrim(p) ~ '^[A-Za-z0-9 .''\-ÑñÁáÉéÍíÓóÚú]+$'
     and lower(p) !~ '(putang|puta|gago|tanga|ulol|tarantado|bobo|fuck|shit|bitch|admin|moderator)'
$$;

create or replace function jr_private.new_barangay_code() returns text
language plpgsql volatile as $$
declare alphabet text := 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789'; v_code text; i int;
begin
  loop
    v_code := 'BRGY';
    for i in 1 .. 4 loop v_code := v_code || substr(alphabet, 1 + floor(random() * length(alphabet))::int, 1); end loop;
    exit when not exists (select 1 from public.jr_barangays b where b.code = v_code);
  end loop;
  return v_code;
end $$;

-- may this player switch barangay now? (null = yes, else when they can)
create or replace function jr_private.barangay_cooldown(p_uid uuid) returns timestamptz
language sql stable security definer set search_path = public as $$
  select case when p.barangay_changed_at is null or p.barangay_changed_at < now() - interval '3 days' then null
              else p.barangay_changed_at + interval '3 days' end
  from public.jr_profiles p where p.user_id = p_uid
$$;

-- weekly team scores (top 10 members' weekly best) for a week key
create or replace function jr_private.barangay_scores(p_wk int)
returns table (barangay_id uuid, score bigint, members int)
language sql stable security definer set search_path = public as $$
  with m as (
    select bm.barangay_id, coalesce(s.score, 0) as score,
           row_number() over (partition by bm.barangay_id order by coalesce(s.score, 0) desc) as rn
    from public.jr_barangay_members bm
    left join public.jr_scores s on s.user_id = bm.user_id and s.board = 'week:' || p_wk)
  select barangay_id, sum(score) filter (where rn <= 10)::bigint, count(*)::int from m group by barangay_id
$$;

create or replace function public.jr_barangay_create(p_name text, p_city text) returns jsonb
language plpgsql security definer set search_path = public, jr_private as $$
declare uid uuid := auth.uid(); n text := btrim(coalesce(p_name, '')); c text := btrim(coalesce(p_city, '')); cd timestamptz; b public.jr_barangays;
begin
  perform jr_private.ensure_profile(uid);
  if not jr_private.barangay_name_ok(n, 32) then return jsonb_build_object('ok', false, 'reason', 'invalid_name'); end if;
  if not jr_private.barangay_name_ok(c, 32) then return jsonb_build_object('ok', false, 'reason', 'invalid_city'); end if;
  cd := jr_private.barangay_cooldown(uid);
  if cd is not null and exists (select 1 from public.jr_barangay_members where user_id = uid) then
    return jsonb_build_object('ok', false, 'reason', 'cooldown', 'until', cd); end if;
  if (select count(*) from public.jr_barangays where created_by = uid and created_at > now() - interval '1 day') >= 3 then
    return jsonb_build_object('ok', false, 'reason', 'limit'); end if;
  insert into public.jr_barangays (code, name, city, created_by) values (jr_private.new_barangay_code(), n, c, uid) returning * into b;
  insert into public.jr_barangay_members (user_id, barangay_id) values (uid, b.id)
    on conflict (user_id) do update set barangay_id = excluded.barangay_id, joined_at = now();
  update public.jr_profiles set barangay_changed_at = now() where user_id = uid;
  return jsonb_build_object('ok', true, 'code', b.code);
end $$;

create or replace function public.jr_barangay_join(p_code text) returns jsonb
language plpgsql security definer set search_path = public, jr_private as $$
declare uid uuid := auth.uid(); v_code text := upper(regexp_replace(coalesce(p_code, ''), '[^A-Za-z0-9]', '', 'g')); b public.jr_barangays; cur uuid; cd timestamptz;
begin
  perform jr_private.ensure_profile(uid);
  select * into b from public.jr_barangays x where x.code = v_code;
  if not found then return jsonb_build_object('ok', false, 'reason', 'unknown_code'); end if;
  select barangay_id into cur from public.jr_barangay_members where user_id = uid;
  if cur = b.id then return jsonb_build_object('ok', true, 'name', b.name); end if;
  cd := jr_private.barangay_cooldown(uid);
  if cur is not null and cd is not null then return jsonb_build_object('ok', false, 'reason', 'cooldown', 'until', cd); end if;
  if (select count(*) from public.jr_barangay_members where barangay_id = b.id) >= 200 then return jsonb_build_object('ok', false, 'reason', 'full'); end if;
  insert into public.jr_barangay_members (user_id, barangay_id) values (uid, b.id)
    on conflict (user_id) do update set barangay_id = excluded.barangay_id, joined_at = now();
  update public.jr_profiles set barangay_changed_at = now() where user_id = uid;
  return jsonb_build_object('ok', true, 'name', b.name);
end $$;

create or replace function public.jr_barangay_leave() returns jsonb
language plpgsql security definer set search_path = public, jr_private as $$
declare uid uuid := auth.uid(); bid uuid;
begin
  perform jr_private.ensure_profile(uid);
  delete from public.jr_barangay_members where user_id = uid returning barangay_id into bid;
  if bid is not null then
    update public.jr_profiles set barangay_changed_at = now() where user_id = uid;
    -- an empty barangay disappears
    delete from public.jr_barangays where id = bid and not exists (select 1 from public.jr_barangay_members where barangay_id = bid);
  end if;
  return jsonb_build_object('ok', true);
end $$;

-- my barangay: name, code, this week's team score and rank, members with their weekly best
create or replace function public.jr_barangay_mine() returns jsonb
language plpgsql security definer set search_path = public, jr_private as $$
declare uid uuid := auth.uid(); b public.jr_barangays; wk int := jr_private.week_key(jr_private.ph_date()); sc bigint; rk int; cd timestamptz;
begin
  perform jr_private.ensure_profile(uid);
  cd := jr_private.barangay_cooldown(uid);
  select br.* into b from public.jr_barangay_members bm join public.jr_barangays br on br.id = bm.barangay_id where bm.user_id = uid;
  if not found then return jsonb_build_object('found', false, 'can_switch_at', cd); end if;
  with t as (select x.barangay_id, x.score, rank() over (order by x.score desc) as r from jr_private.barangay_scores(wk) x)
  select t.score, t.r into sc, rk from t where t.barangay_id = b.id;
  return jsonb_build_object('found', true, 'name', b.name, 'city', b.city, 'code', b.code, 'score', coalesce(sc, 0), 'rank', coalesce(rk, 0),
    'can_switch_at', cd,
    'members', coalesce((select jsonb_agg(jsonb_build_object('display_name', p.display_name, 'level', jr_private.level_for(p.xp),
        'score', coalesce(s.score, 0), 'is_me', p.user_id = uid) order by coalesce(s.score, 0) desc, p.display_name)
      from public.jr_barangay_members bm join public.jr_profiles p on p.user_id = bm.user_id
      left join public.jr_scores s on s.user_id = bm.user_id and s.board = 'week:' || wk
      where bm.barangay_id = b.id), '[]'::jsonb));
end $$;

-- weekly barangay ranking + last week's Barangay of the Week
create or replace function public.jr_barangay_board(p_limit int default 50) returns jsonb
language plpgsql security definer set search_path = public, jr_private as $$
declare uid uuid := auth.uid(); wk int := jr_private.week_key(jr_private.ph_date()); lim int := least(greatest(coalesce(p_limit, 50), 1), 100);
  mine uuid; rows jsonb; me jsonb; champ jsonb;
begin
  perform jr_private.ensure_profile(uid);
  select barangay_id into mine from public.jr_barangay_members where user_id = uid;
  with t as (select x.*, rank() over (order by x.score desc, b.created_at) as r, b.name, b.city
             from jr_private.barangay_scores(wk) x join public.jr_barangays b on b.id = x.barangay_id)
  select coalesce(jsonb_agg(jsonb_build_object('rank', t.r, 'name', t.name, 'city', t.city, 'score', t.score, 'members', t.members, 'is_mine', t.barangay_id = mine) order by t.r) filter (where t.r <= lim), '[]'::jsonb),
         (array_agg(jsonb_build_object('rank', t.r, 'name', t.name, 'city', t.city, 'score', t.score, 'members', t.members, 'is_mine', true)) filter (where t.barangay_id = mine))[1]
    into rows, me from t;
  select jsonb_build_object('name', b.name, 'city', b.city, 'score', x.score) into champ
    from jr_private.barangay_scores(wk - 1) x join public.jr_barangays b on b.id = x.barangay_id
    where x.score > 0 order by x.score desc, b.created_at limit 1;
  return jsonb_build_object('rows', rows, 'me', me, 'champion', champ);
end $$;

revoke all on function jr_private.barangay_name_ok(text, int), jr_private.new_barangay_code(), jr_private.barangay_cooldown(uuid), jr_private.barangay_scores(int)
  from public, anon, authenticated;
revoke all on function public.jr_barangay_create(text, text), public.jr_barangay_join(text), public.jr_barangay_leave(), public.jr_barangay_mine(), public.jr_barangay_board(int) from public, anon;
grant execute on function public.jr_barangay_create(text, text), public.jr_barangay_join(text), public.jr_barangay_leave(), public.jr_barangay_mine(), public.jr_barangay_board(int) to authenticated;
