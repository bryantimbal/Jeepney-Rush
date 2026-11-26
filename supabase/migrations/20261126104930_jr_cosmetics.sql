-- Talyer customization: cosmetic items bought with coins (C# Logic/Customization.cs). Cosmetics
-- never change stats. Prices come from the generated catalog (jr_catalog_cosmetics); items earned
-- with a career license (rank_needed >= 0) can't be bought. The snapshot lists owned items.
-- Which item is fitted on which jeepney is kept on the phone.

create table if not exists public.jr_catalog_cosmetics (
  id text primary key,
  slot text not null,
  price int not null check (price >= 0),
  rank_needed int not null default -1);
create table if not exists public.jr_cosmetics (
  user_id uuid not null references public.jr_profiles (user_id) on delete cascade,
  item_id text not null,
  acquired_at timestamptz not null default now(),
  primary key (user_id, item_id));

do $$
declare t text;
begin
  foreach t in array array['jr_catalog_cosmetics','jr_cosmetics'] loop
    execute format('alter table public.%I enable row level security', t);
    execute format('revoke all on public.%I from anon, authenticated', t);
  end loop;
end $$;
alter table public.jr_catalog_cosmetics enable row level security;
alter table public.jr_cosmetics enable row level security;
grant select on public.jr_catalog_cosmetics to anon, authenticated;
drop policy if exists catalog_read on public.jr_catalog_cosmetics;
create policy catalog_read on public.jr_catalog_cosmetics for select to anon, authenticated using (true);
grant select on public.jr_cosmetics to authenticated;
drop policy if exists own_rows on public.jr_cosmetics;
create policy own_rows on public.jr_cosmetics for select to authenticated using (user_id = auth.uid());

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
    'career_rank', p.career_rank,
    'cosmetics', coalesce((select jsonb_agg(item_id order by item_id) from public.jr_cosmetics where user_id = p_uid), '[]'::jsonb));
end $$;

create or replace function public.jr_buy_cosmetic(p_id text) returns jsonb
language plpgsql security definer set search_path = public, jr_private as $$
declare uid uuid := auth.uid(); it public.jr_catalog_cosmetics; c int;
begin
  perform jr_private.ensure_profile(uid);
  select * into it from public.jr_catalog_cosmetics where id = p_id;
  if not found then return jsonb_build_object('ok', false, 'reason', 'unknown_item', 'snapshot', jr_private.snapshot(uid)); end if;
  if it.rank_needed >= 0 or it.price <= 0 then return jsonb_build_object('ok', false, 'reason', 'not_for_sale', 'snapshot', jr_private.snapshot(uid)); end if;
  if exists (select 1 from public.jr_cosmetics where user_id = uid and item_id = p_id) then
    return jsonb_build_object('ok', false, 'reason', 'owned', 'snapshot', jr_private.snapshot(uid)); end if;
  select coins into c from public.jr_profiles where user_id = uid for update;
  if c < it.price then return jsonb_build_object('ok', false, 'reason', 'coins', 'snapshot', jr_private.snapshot(uid)); end if;
  update public.jr_profiles set coins = coins - it.price, updated_at = now() where user_id = uid;
  insert into public.jr_cosmetics (user_id, item_id) values (uid, p_id);
  insert into public.jr_transactions (user_id, kind, amount, ref, currency) values (uid, 'buy_cosmetic', -it.price, p_id, 'coins');
  return jsonb_build_object('ok', true, 'snapshot', jr_private.snapshot(uid));
end $$;
revoke all on function public.jr_buy_cosmetic(text) from public, anon;
grant execute on function public.jr_buy_cosmetic(text) to authenticated;
