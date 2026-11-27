-- Daily Challenge ghosts: everyone drives the same road today, so every player's best daily run is
-- kept as a ghost for that day. Players race today's #1, the next driver above them, or a friend.
-- Ghosts from older days are removed after 3 days. Same path rules as jr_save_ghost (C# Logic/Ghost.cs).

create table if not exists public.jr_daily_ghosts (
  user_id uuid not null references public.jr_profiles (user_id) on delete cascade,
  day_key int not null,
  run_id text not null,
  seed int not null,
  env int not null,
  jeepney text not null,
  score int not null,
  distance real not null,
  d int[] not null,
  x int[] not null,
  updated_at timestamptz not null default now(),
  primary key (user_id, day_key));
create index if not exists jr_daily_ghosts_day on public.jr_daily_ghosts (day_key, score desc);
alter table public.jr_daily_ghosts enable row level security;
revoke all on public.jr_daily_ghosts from anon, authenticated;

-- null if the path is plausible for this run, else why not
create or replace function jr_private.ghost_path_violation(run public.jr_runs, p_d int[], p_x int[]) returns text
language plpgsql immutable as $$
declare n int := coalesce(array_length(p_d, 1), 0); i int; dist real := (run.report->>'distance')::real;
begin
  if n <> coalesce(array_length(p_x, 1), 0) then return 'shape'; end if;
  if n < 2 or n > 3600 or n > run.duration / 0.5 + 10 then return 'samples'; end if;
  for i in 1 .. n loop
    if p_d[i] < 0 or abs(p_x[i]) > 400 then return 'range'; end if;
    if i > 1 and (p_d[i] < p_d[i - 1] or p_d[i] - p_d[i - 1] > 250) then return 'step'; end if;
  end loop;
  if abs(p_d[n] / 10.0 - dist) > greatest(5, dist * 0.03) then return 'distance'; end if;
  return null;
end $$;

create or replace function public.jr_save_ghost(p_run_id text, p_d int[], p_x int[]) returns jsonb
language plpgsql security definer set search_path = public, jr_private as $$
declare
  uid uuid := auth.uid(); run public.jr_runs; rep jsonb; bad text; cls int; cur int; dk int;
begin
  select * into run from public.jr_runs where run_id = p_run_id and user_id = uid;
  if not found or not run.accepted then return jsonb_build_object('ok', false, 'reason', 'run'); end if;
  rep := run.report;
  if coalesce(rep->>'career', '') <> '' or (not run.daily and coalesce(rep->>'mode', '') not in ('', 'ghost')) then
    return jsonb_build_object('ok', false, 'reason', 'mode'); end if;
  bad := jr_private.ghost_path_violation(run, p_d, p_x);
  if bad is not null then return jsonb_build_object('ok', false, 'reason', bad); end if;

  if run.daily then
    dk := (rep->>'day_key')::int;
    delete from public.jr_daily_ghosts where updated_at < now() - interval '3 days';
    select score into cur from public.jr_daily_ghosts where user_id = uid and day_key = dk;
    if found and cur >= (rep->>'score')::int then return jsonb_build_object('ok', true, 'kept', 'older', 'daily', true); end if;
    insert into public.jr_daily_ghosts (user_id, day_key, run_id, seed, env, jeepney, score, distance, d, x)
    values (uid, dk, p_run_id, (rep->>'seed')::int, (rep->>'env')::int, coalesce(rep->>'jeepney', 'pilipinas'), (rep->>'score')::int,
            (rep->>'distance')::real, p_d, p_x)
    on conflict (user_id, day_key) do update set run_id = excluded.run_id, jeepney = excluded.jeepney, score = excluded.score,
      distance = excluded.distance, d = excluded.d, x = excluded.x, updated_at = now();
    return jsonb_build_object('ok', true, 'kept', 'new', 'daily', true);
  end if;

  cls := jr_private.run_class(uid, rep->>'jeepney');
  select score into cur from public.jr_ghosts where user_id = uid and env = (rep->>'env')::int and class = cls;
  if found and cur >= (rep->>'score')::int then return jsonb_build_object('ok', true, 'kept', 'older'); end if;
  insert into public.jr_ghosts (user_id, env, class, run_id, seed, jeepney, score, distance, d, x)
  values (uid, (rep->>'env')::int, cls, p_run_id, (rep->>'seed')::int, coalesce(rep->>'jeepney', 'pilipinas'), (rep->>'score')::int,
          (rep->>'distance')::real, p_d, p_x)
  on conflict (user_id, env, class) do update set run_id = excluded.run_id, seed = excluded.seed, jeepney = excluded.jeepney,
    score = excluded.score, distance = excluded.distance, d = excluded.d, x = excluded.x, updated_at = now();
  return jsonb_build_object('ok', true, 'kept', 'new');
end $$;

-- Today's daily ghost to race. p_who: 'top' = today's #1 (not you), 'rival' = the closest score above
-- yours (the #1 if you are on top), 'friend' = your best friend's ghost today.
create or replace function public.jr_daily_ghost_pick(p_who text) returns jsonb
language plpgsql stable security definer set search_path = public, jr_private as $$
declare uid uuid := auth.uid(); dk int := jr_private.day_key(jr_private.ph_date()); mine int; g public.jr_daily_ghosts; nm text; rk int;
begin
  select score into mine from public.jr_daily_ghosts where user_id = uid and day_key = dk;
  if p_who = 'friend' then
    select dg.* into g from public.jr_daily_ghosts dg join public.jr_friends f on f.friend_id = dg.user_id and f.user_id = uid
      where dg.day_key = dk order by dg.score desc limit 1;
  elsif p_who = 'rival' and mine is not null then
    select * into g from public.jr_daily_ghosts where day_key = dk and user_id <> uid and score > mine order by score asc limit 1;
  end if;
  if not found or p_who not in ('friend', 'rival') or (p_who = 'rival' and mine is null) then
    if p_who = 'friend' then return jsonb_build_object('found', false, 'who', p_who); end if;
    select * into g from public.jr_daily_ghosts where day_key = dk and user_id <> uid order by score desc, updated_at limit 1;
  end if;
  if not found then return jsonb_build_object('found', false, 'who', p_who); end if;
  select display_name into nm from public.jr_profiles where user_id = g.user_id;
  select count(*) + 1 into rk from public.jr_daily_ghosts where day_key = dk and score > g.score;
  return jsonb_build_object('found', true, 'who', p_who, 'name', nm, 'rank', rk, 'seed', g.seed, 'env', g.env, 'jeepney', g.jeepney,
    'score', g.score, 'distance', g.distance, 'd', to_jsonb(g.d), 'x', to_jsonb(g.x), 'mine', coalesce(mine, 0));
end $$;

revoke all on function public.jr_save_ghost(text, int[], int[]), public.jr_daily_ghost_pick(text) from public, anon;
grant execute on function public.jr_save_ghost(text, int[], int[]), public.jr_daily_ghost_pick(text) to authenticated;
revoke all on function jr_private.ghost_path_violation(public.jr_runs, int[], int[]) from public, anon, authenticated;
