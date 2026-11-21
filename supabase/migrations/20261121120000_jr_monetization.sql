-- =====================================================================================
-- JEEPNEY RUSH – Phase 4 monetization (gems, verified purchases, rewarded-ad rewards)
--
-- * Purchases are granted ONLY by jr_srv_grant_purchase, callable by the service role, i.e.
--   by the verify-purchase Edge Function after Google Play confirmed the purchase token.
-- * Rewarded-ad rewards need a server-side-verification (SSV) record written by the admob-ssv
--   Edge Function from Google's signed callback; the client then claims it once.
-- * Test purchases / test ads are refused unless jr_server_settings allows them (dev only).
-- =====================================================================================

alter table public.jr_profiles add column if not exists gems int not null default 0;
alter table public.jr_profiles add column if not exists ad_free boolean not null default false;
do $$ begin
  if not exists (select 1 from pg_constraint where conname = 'jr_profiles_gems_nonneg') then
    alter table public.jr_profiles add constraint jr_profiles_gems_nonneg check (gems >= 0);
  end if;
end $$;
alter table public.jr_catalog_jeepneys add column if not exists gem_price int not null default 0;
alter table public.jr_transactions add column if not exists currency text not null default 'coins';

create table if not exists public.jr_catalog_products (
  id text primary key,
  kind text not null check (kind in ('consumable', 'non_consumable')),
  title text not null,
  coins int not null default 0,
  gems int not null default 0,
  jeepneys text[] not null default '{}',
  ad_free boolean not null default false);

-- developer switches (NOT generated, NOT client-writable). Flip in the SQL editor for testing only.
create table if not exists public.jr_server_settings (key text primary key, value text not null);
insert into public.jr_server_settings (key, value) values ('allow_test_purchases', 'false'), ('allow_test_rewards', 'false')
on conflict (key) do nothing;

create table if not exists public.jr_purchases (
  purchase_token text primary key,
  user_id uuid not null references public.jr_profiles (user_id) on delete cascade,
  product_id text not null,
  order_id text,
  platform text not null default 'google_play',
  test boolean not null default false,
  state text not null default 'granted' check (state in ('granted', 'revoked')),
  created_at timestamptz not null default now());
create index if not exists jr_purchases_user on public.jr_purchases (user_id, created_at desc);

create table if not exists public.jr_ad_rewards (
  nonce text primary key,             -- AdMob transaction_id (or test-… in development)
  user_id uuid not null references public.jr_profiles (user_id) on delete cascade,
  kind text not null,                 -- revive | doublecoins | dailybonus | trial
  ref text,
  test boolean not null default false,
  verified_at timestamptz not null default now(),
  claimed_at timestamptz);
create index if not exists jr_ad_rewards_user on public.jr_ad_rewards (user_id, kind, claimed_at);

do $$
declare t text;
begin
  foreach t in array array['jr_catalog_products', 'jr_server_settings', 'jr_purchases', 'jr_ad_rewards'] loop
    execute format('alter table public.%I enable row level security', t);
    execute format('revoke all on public.%I from anon, authenticated', t);
  end loop;
  grant select on public.jr_catalog_products to anon, authenticated;
  drop policy if exists catalog_read on public.jr_catalog_products;
  create policy catalog_read on public.jr_catalog_products for select to anon, authenticated using (true);
  foreach t in array array['jr_purchases', 'jr_ad_rewards'] loop
    execute format('grant select on public.%I to authenticated', t);
    execute format('drop policy if exists own_rows on public.%I', t);
    execute format('create policy own_rows on public.%I for select to authenticated using (user_id = auth.uid())', t);
  end loop;
end $$;

create or replace function jr_private.setting(k text) returns text
language sql stable security definer set search_path = public as $$ select value from public.jr_server_settings where key = k $$;

-- snapshot now also carries gems, ad-free and owned products
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
                       union all select 'weekly', m.* from jr_private.missions('weekly', wk) m) x));
end $$;


-- ------------------------------------------------------------------ gems
create or replace function public.jr_buy_jeepney_gems(p_id text) returns jsonb
language plpgsql security definer set search_path = public, jr_private as $$
declare uid uuid := auth.uid(); gp int; g int;
begin
  perform jr_private.ensure_profile(uid);
  select j.gem_price into gp from public.jr_catalog_jeepneys j where j.id = p_id;
  if not found or coalesce(gp, 0) <= 0 then return jsonb_build_object('ok', false, 'reason', 'unknown_item', 'snapshot', jr_private.snapshot(uid)); end if;
  if exists (select 1 from public.jr_inventory where user_id = uid and item_kind = 'jeepney' and item_id = p_id) then
    return jsonb_build_object('ok', false, 'reason', 'owned', 'snapshot', jr_private.snapshot(uid)); end if;
  select gems into g from public.jr_profiles where user_id = uid for update;
  if g < gp then return jsonb_build_object('ok', false, 'reason', 'gems', 'snapshot', jr_private.snapshot(uid)); end if;
  update public.jr_profiles set gems = gems - gp, updated_at = now() where user_id = uid;
  insert into public.jr_inventory (user_id, item_kind, item_id, level) values (uid, 'jeepney', p_id, 1);
  insert into public.jr_transactions (user_id, kind, amount, ref, currency) values (uid, 'buy_jeepney', -gp, p_id, 'gems');
  return jsonb_build_object('ok', true, 'snapshot', jr_private.snapshot(uid));
end $$;

-- ------------------------------------------------------------------ verified purchases (service role only)
create or replace function public.jr_srv_grant_purchase(p_user uuid, p_product text, p_token text, p_order text, p_test boolean default false)
returns jsonb
language plpgsql security definer set search_path = public, jr_private as $$
declare pr public.jr_catalog_products; existing public.jr_purchases; owned_before boolean; j text;
begin
  if p_user is null or coalesce(p_token, '') = '' then return jsonb_build_object('ok', false, 'reason', 'bad_request'); end if;
  perform jr_private.ensure_profile(p_user);
  select * into pr from public.jr_catalog_products where id = p_product;
  if not found then return jsonb_build_object('ok', false, 'reason', 'unknown_product'); end if;
  if p_test and coalesce(jr_private.setting('allow_test_purchases'), 'false') <> 'true' then
    return jsonb_build_object('ok', false, 'reason', 'test_not_allowed'); end if;
  select * into existing from public.jr_purchases where purchase_token = p_token;
  if found then
    if existing.user_id <> p_user then return jsonb_build_object('ok', false, 'reason', 'token_used'); end if;
    return jsonb_build_object('ok', true, 'duplicate', true, 'snapshot', jr_private.snapshot(p_user));
  end if;
  owned_before := pr.kind = 'non_consumable' and exists (select 1 from public.jr_purchases where user_id = p_user and product_id = p_product and state = 'granted');
  insert into public.jr_purchases (purchase_token, user_id, product_id, order_id, test) values (p_token, p_user, p_product, p_order, p_test);
  if not owned_before then
    update public.jr_profiles set coins = coins + pr.coins, gems = gems + pr.gems, ad_free = ad_free or pr.ad_free, updated_at = now() where user_id = p_user;
    if pr.coins <> 0 then insert into public.jr_transactions (user_id, kind, amount, ref, currency) values (p_user, 'purchase', pr.coins, p_product, 'coins'); end if;
    if pr.gems <> 0 then insert into public.jr_transactions (user_id, kind, amount, ref, currency) values (p_user, 'purchase', pr.gems, p_product, 'gems'); end if;
    foreach j in array pr.jeepneys loop
      insert into public.jr_inventory (user_id, item_kind, item_id, level) values (p_user, 'jeepney', j, 1) on conflict do nothing;
    end loop;
  end if;
  return jsonb_build_object('ok', true, 'snapshot', jr_private.snapshot(p_user));
end $$;

-- refunds / chargebacks (called by an admin or a future Play notifications handler)
create or replace function public.jr_srv_revoke_purchase(p_token text) returns jsonb
language plpgsql security definer set search_path = public, jr_private as $$
declare pu public.jr_purchases; pr public.jr_catalog_products;
begin
  select * into pu from public.jr_purchases where purchase_token = p_token for update;
  if not found or pu.state = 'revoked' then return jsonb_build_object('ok', false, 'reason', 'not_found'); end if;
  select * into pr from public.jr_catalog_products where id = pu.product_id;
  update public.jr_purchases set state = 'revoked' where purchase_token = p_token;
  update public.jr_profiles set coins = greatest(0, coins - pr.coins), gems = greatest(0, gems - pr.gems),
    ad_free = case when pr.ad_free then false else ad_free end, updated_at = now() where user_id = pu.user_id;
  if pr.coins <> 0 then insert into public.jr_transactions (user_id, kind, amount, ref, currency) values (pu.user_id, 'refund', -pr.coins, pu.product_id, 'coins'); end if;
  if pr.gems <> 0 then insert into public.jr_transactions (user_id, kind, amount, ref, currency) values (pu.user_id, 'refund', -pr.gems, pu.product_id, 'gems'); end if;
  return jsonb_build_object('ok', true);
end $$;

-- ------------------------------------------------------------------ rewarded ads
-- written by the admob-ssv Edge Function after verifying Google's signature
create or replace function public.jr_srv_record_ad_reward(p_user uuid, p_nonce text, p_kind text, p_ref text, p_test boolean default false)
returns jsonb
language plpgsql security definer set search_path = public, jr_private as $$
begin
  if p_user is null or coalesce(p_nonce, '') = '' or p_kind not in ('revive', 'doublecoins', 'dailybonus', 'trial') then
    return jsonb_build_object('ok', false, 'reason', 'bad_request'); end if;
  perform jr_private.ensure_profile(p_user);
  insert into public.jr_ad_rewards (nonce, user_id, kind, ref, test) values (p_nonce, p_user, p_kind, p_ref, p_test) on conflict (nonce) do nothing;
  return jsonb_build_object('ok', true);
end $$;

-- the player claims a verified reward once
create or replace function public.jr_claim_ad_reward(p_nonce text) returns jsonb
language plpgsql security definer set search_path = public, jr_private as $$
declare
  uid uuid := auth.uid(); r public.jr_ad_rewards; amount int := 0; run public.jr_runs;
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
  end if;
  update public.jr_ad_rewards set claimed_at = now() where nonce = r.nonce;
  if amount > 0 then
    update public.jr_profiles set coins = coins + amount, updated_at = now() where user_id = uid;
    insert into public.jr_transactions (user_id, kind, amount, ref, currency) values (uid, 'ad_' || r.kind, amount, r.ref, 'coins');
  end if;
  return jsonb_build_object('ok', true, 'reward', amount, 'snapshot', jr_private.snapshot(uid));
end $$;

-- ------------------------------------------------------------------ permissions
revoke all on all functions in schema jr_private from public, anon, authenticated;
revoke all on function public.jr_buy_jeepney_gems(text), public.jr_claim_ad_reward(text) from public, anon;
grant execute on function public.jr_buy_jeepney_gems(text), public.jr_claim_ad_reward(text) to authenticated;
revoke all on function public.jr_srv_grant_purchase(uuid, text, text, text, boolean), public.jr_srv_revoke_purchase(text),
  public.jr_srv_record_ad_reward(uuid, text, text, text, boolean) from public, anon, authenticated;
grant execute on function public.jr_srv_grant_purchase(uuid, text, text, text, boolean), public.jr_srv_revoke_purchase(text),
  public.jr_srv_record_ad_reward(uuid, text, text, text, boolean) to service_role;
grant usage on schema jr_private to service_role;
grant execute on all functions in schema jr_private to service_role;
