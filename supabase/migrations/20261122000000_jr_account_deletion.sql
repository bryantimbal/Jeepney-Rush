-- Phase 5: account deletion (Google Play requires apps with accounts to let players delete
-- them in the app). Deleting the auth user removes the profile and, through ON DELETE CASCADE,
-- every jr_ row (inventory, runs, scores, friends, missions, transactions, purchases, ad rewards).
-- Purchase tokens and ad nonces are kept (without any user link) so they can't be replayed
-- on a new account.

create table if not exists jr_private.retired_purchase_tokens (
  purchase_token text primary key,
  product_id text not null,
  retired_at timestamptz not null default now());
create table if not exists jr_private.retired_ad_nonces (
  nonce text primary key,
  retired_at timestamptz not null default now());
revoke all on jr_private.retired_purchase_tokens, jr_private.retired_ad_nonces from public, anon, authenticated;

create or replace function public.jr_delete_account() returns jsonb
language plpgsql security definer set search_path = public, jr_private, auth as $$
declare uid uuid := auth.uid();
begin
  if uid is null then return jsonb_build_object('ok', false, 'reason', 'not_signed_in'); end if;
  insert into jr_private.retired_purchase_tokens (purchase_token, product_id)
    select purchase_token, product_id from public.jr_purchases where user_id = uid on conflict do nothing;
  insert into jr_private.retired_ad_nonces (nonce)
    select nonce from public.jr_ad_rewards where user_id = uid on conflict do nothing;
  delete from public.jr_profiles where user_id = uid; -- cascades to every jr_ table
  delete from auth.users where id = uid;              -- the sign-in identity (e-mail) too
  return jsonb_build_object('ok', true);
end $$;

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
  -- a token from a deleted account can't be granted again on a new one
  if exists (select 1 from jr_private.retired_purchase_tokens where purchase_token = p_token) then
    return jsonb_build_object('ok', false, 'reason', 'token_used'); end if;
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

create or replace function public.jr_srv_record_ad_reward(p_user uuid, p_nonce text, p_kind text, p_ref text, p_test boolean default false)
returns jsonb
language plpgsql security definer set search_path = public, jr_private as $$
begin
  if p_user is null or coalesce(p_nonce, '') = '' or p_kind not in ('revive', 'doublecoins', 'dailybonus', 'trial') then
    return jsonb_build_object('ok', false, 'reason', 'bad_request'); end if;
  if exists (select 1 from jr_private.retired_ad_nonces where nonce = p_nonce) then return jsonb_build_object('ok', true); end if;
  perform jr_private.ensure_profile(p_user);
  insert into public.jr_ad_rewards (nonce, user_id, kind, ref, test) values (p_nonce, p_user, p_kind, p_ref, p_test) on conflict (nonce) do nothing;
  return jsonb_build_object('ok', true);
end $$;

revoke all on function public.jr_delete_account() from public, anon;
grant execute on function public.jr_delete_account() to authenticated;
revoke all on function public.jr_srv_grant_purchase(uuid, text, text, text, boolean), public.jr_srv_record_ad_reward(uuid, text, text, text, boolean) from public, anon, authenticated;
grant execute on function public.jr_srv_grant_purchase(uuid, text, text, text, boolean), public.jr_srv_record_ad_reward(uuid, text, text, text, boolean) to service_role;
