-- Sticker album (same rules as C# Logic/Album.cs): 32 landmark postcards, 4 per route (card / 4 =
-- route). A run may bring one postcard (report field 'postcard'): only a card of the run's route and
-- only after 800 m. Duplicates pay 20 coins. A full route set pays 300 coins + 10 gems, the whole
-- album 100 gems (claimed once each).

create table if not exists public.jr_album (
  user_id uuid not null references public.jr_profiles (user_id) on delete cascade,
  card int not null check (card between 0 and 31),
  count int not null default 1,
  primary key (user_id, card));
create table if not exists public.jr_album_sets (
  user_id uuid not null references public.jr_profiles (user_id) on delete cascade,
  set_id int not null,                 -- 0..7 = route sets, 8 = the whole album
  primary key (user_id, set_id));
create table if not exists public.jr_album_runs (run_id text primary key, user_id uuid not null references public.jr_profiles (user_id) on delete cascade);
alter table public.jr_album enable row level security;
alter table public.jr_album_sets enable row level security;
alter table public.jr_album_runs enable row level security;
revoke all on public.jr_album, public.jr_album_sets, public.jr_album_runs from anon, authenticated;

create or replace function public.jr_album_claim_run(p_run_id text) returns jsonb
language plpgsql security definer set search_path = public, jr_private as $$
declare uid uuid := auth.uid(); run public.jr_runs; v_card int; dup boolean;
begin
  select * into run from public.jr_runs where run_id = p_run_id and user_id = uid;
  if not found or not run.accepted or run.created_at < now() - interval '6 hours' then return jsonb_build_object('ok', false, 'reason', 'run'); end if;
  if exists (select 1 from public.jr_album_runs where run_id = p_run_id) then return jsonb_build_object('ok', true, 'duplicate', true); end if;
  v_card := coalesce((run.report->>'postcard')::int, -1);
  if v_card < 0 or v_card > 31 or v_card / 4 <> coalesce((run.report->>'env')::int, -1) or coalesce((run.report->>'distance')::real, 0) < 800 then
    return jsonb_build_object('ok', false, 'reason', 'card'); end if;
  insert into public.jr_album_runs (run_id, user_id) values (p_run_id, uid);
  dup := exists (select 1 from public.jr_album a where a.user_id = uid and a.card = v_card);
  insert into public.jr_album (user_id, card) values (uid, v_card) on conflict (user_id, card) do update set count = jr_album.count + 1;
  if dup then
    update public.jr_profiles set coins = coins + 20, updated_at = now() where user_id = uid;
    insert into public.jr_transactions (user_id, kind, amount, ref, currency) values (uid, 'album_dup', 20, 'card' || v_card, 'coins');
  end if;
  return jsonb_build_object('ok', true, 'card', v_card, 'new', not dup);
end $$;

create or replace function public.jr_album_status() returns jsonb
language sql stable security definer set search_path = public, jr_private as $$
  select jsonb_build_object(
    'cards', coalesce((select jsonb_agg(card order by card) from public.jr_album where user_id = auth.uid()), '[]'::jsonb),
    'sets', coalesce((select jsonb_agg(set_id order by set_id) from public.jr_album_sets where user_id = auth.uid()), '[]'::jsonb)) $$;

create or replace function public.jr_album_claim_set(p_set int) returns jsonb
language plpgsql security definer set search_path = public, jr_private as $$
declare uid uuid := auth.uid(); have int;
begin
  if p_set < 0 or p_set > 8 then return jsonb_build_object('ok', false, 'reason', 'unknown_item', 'snapshot', jr_private.snapshot(uid)); end if;
  if exists (select 1 from public.jr_album_sets where user_id = uid and set_id = p_set) then return jsonb_build_object('ok', false, 'reason', 'claimed', 'snapshot', jr_private.snapshot(uid)); end if;
  select count(*) into have from public.jr_album where user_id = uid and (p_set = 8 or card / 4 = p_set);
  if have < (case when p_set = 8 then 32 else 4 end) then return jsonb_build_object('ok', false, 'reason', 'not_done', 'snapshot', jr_private.snapshot(uid)); end if;
  insert into public.jr_album_sets (user_id, set_id) values (uid, p_set);
  if p_set = 8 then
    update public.jr_profiles set gems = gems + 100, updated_at = now() where user_id = uid;
    insert into public.jr_transactions (user_id, kind, amount, ref, currency) values (uid, 'album', 100, 'album', 'gems');
  else
    update public.jr_profiles set coins = coins + 300, gems = gems + 10, updated_at = now() where user_id = uid;
    insert into public.jr_transactions (user_id, kind, amount, ref, currency) values (uid, 'album', 300, 'set' || p_set, 'coins'), (uid, 'album', 10, 'set' || p_set, 'gems');
  end if;
  return jsonb_build_object('ok', true, 'snapshot', jr_private.snapshot(uid));
end $$;

revoke all on function public.jr_album_claim_run(text), public.jr_album_status(), public.jr_album_claim_set(int) from public, anon;
grant execute on function public.jr_album_claim_run(text), public.jr_album_status(), public.jr_album_claim_set(int) to authenticated;
