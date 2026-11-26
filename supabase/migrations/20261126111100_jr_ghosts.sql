-- Ghost Race: each player's best recorded run per route and stat class, raced by others in the
-- same class on the same road (same seed). Same rules as C# Logic/Ghost.cs: one sample every
-- 0.5 s, distances never go backwards or faster than the game allows, and the path must end
-- where the server-checked run ended. Only free-ride / ghost runs (not career, daily or modes).

create table if not exists public.jr_ghosts (
  user_id uuid not null references public.jr_profiles (user_id) on delete cascade,
  env int not null,
  class int not null,
  run_id text not null,
  seed int not null,
  jeepney text not null,
  score int not null,
  distance real not null,
  d int[] not null,
  x int[] not null,
  updated_at timestamptz not null default now(),
  primary key (user_id, env, class));
create index if not exists jr_ghosts_board on public.jr_ghosts (env, class, score);
alter table public.jr_ghosts enable row level security;
revoke all on public.jr_ghosts from anon, authenticated;

create or replace function public.jr_save_ghost(p_run_id text, p_d int[], p_x int[]) returns jsonb
language plpgsql security definer set search_path = public, jr_private as $$
declare
  uid uuid := auth.uid(); run public.jr_runs; rep jsonb; n int; i int; dist real; cls int; cur int;
begin
  select * into run from public.jr_runs where run_id = p_run_id and user_id = uid;
  if not found or not run.accepted then return jsonb_build_object('ok', false, 'reason', 'run'); end if;
  rep := run.report;
  if run.daily or coalesce(rep->>'career', '') <> '' or coalesce(rep->>'mode', '') not in ('', 'ghost') then
    return jsonb_build_object('ok', false, 'reason', 'mode'); end if;
  n := coalesce(array_length(p_d, 1), 0);
  if n <> coalesce(array_length(p_x, 1), 0) then return jsonb_build_object('ok', false, 'reason', 'shape'); end if;
  if n < 2 or n > 3600 or n > run.duration / 0.5 + 10 then return jsonb_build_object('ok', false, 'reason', 'samples'); end if;
  for i in 1 .. n loop
    if p_d[i] < 0 or abs(p_x[i]) > 400 then return jsonb_build_object('ok', false, 'reason', 'range'); end if;
    if i > 1 and (p_d[i] < p_d[i - 1] or p_d[i] - p_d[i - 1] > 250) then return jsonb_build_object('ok', false, 'reason', 'step'); end if;
  end loop;
  dist := (rep->>'distance')::real;
  if abs(p_d[n] / 10.0 - dist) > greatest(5, dist * 0.03) then return jsonb_build_object('ok', false, 'reason', 'distance'); end if;
  cls := jr_private.run_class(uid, rep->>'jeepney');
  select score into cur from public.jr_ghosts where user_id = uid and env = (rep->>'env')::int and class = cls;
  if found and cur >= (rep->>'score')::int then return jsonb_build_object('ok', true, 'kept', 'older'); end if;
  insert into public.jr_ghosts (user_id, env, class, run_id, seed, jeepney, score, distance, d, x)
  values (uid, (rep->>'env')::int, cls, p_run_id, (rep->>'seed')::int, coalesce(rep->>'jeepney', 'pilipinas'), (rep->>'score')::int, dist, p_d, p_x)
  on conflict (user_id, env, class) do update set run_id = excluded.run_id, seed = excluded.seed, jeepney = excluded.jeepney,
    score = excluded.score, distance = excluded.distance, d = excluded.d, x = excluded.x, updated_at = now();
  return jsonb_build_object('ok', true, 'kept', 'new');
end $$;

-- A rival for the caller: another player's ghost on this route in this stat class, the closest
-- score above the caller's own ghost (or the best one if the caller is already on top).
create or replace function public.jr_ghost_pick(p_env int, p_class int) returns jsonb
language plpgsql stable security definer set search_path = public, jr_private as $$
declare uid uuid := auth.uid(); mine int; g public.jr_ghosts; nm text;
begin
  select score into mine from public.jr_ghosts where user_id = uid and env = p_env and class = p_class;
  select * into g from public.jr_ghosts where env = p_env and class = p_class and user_id <> uid and score > coalesce(mine, 0)
    order by score asc limit 1;
  if not found then
    select * into g from public.jr_ghosts where env = p_env and class = p_class and user_id <> uid order by score desc limit 1;
  end if;
  if not found then return jsonb_build_object('found', false); end if;
  select display_name into nm from public.jr_profiles where user_id = g.user_id;
  return jsonb_build_object('found', true, 'name', nm, 'seed', g.seed, 'env', g.env, 'jeepney', g.jeepney, 'score', g.score,
    'distance', g.distance, 'd', to_jsonb(g.d), 'x', to_jsonb(g.x));
end $$;

revoke all on function public.jr_save_ghost(text, int[], int[]), public.jr_ghost_pick(int, int) from public, anon;
grant execute on function public.jr_save_ghost(text, int[], int[]), public.jr_ghost_pick(int, int) to authenticated;
