-- Google Play test purchases (license testers) are granted without opening the door to simulated
-- 'TEST-…' purchases. Before: one switch (allow_test_purchases) covered both, so turning it on for
-- a license-tester test would have let anyone grant themselves items with a made-up TEST- token.
-- Now: Google-verified test purchases need allow_play_test_purchases (default on); TEST- tokens
-- still need allow_test_purchases, which must stay 'false' on the live project.

insert into public.jr_server_settings (key, value) values ('allow_play_test_purchases', 'true') on conflict (key) do nothing;

create or replace function public.jr_srv_grant_purchase(p_user uuid, p_product text, p_token text, p_order text, p_test boolean default false)
returns jsonb
language plpgsql security definer set search_path = public, jr_private as $$
declare pr public.jr_catalog_products; existing public.jr_purchases; owned_before boolean; j text;
begin
  if p_user is null or coalesce(p_token, '') = '' then return jsonb_build_object('ok', false, 'reason', 'bad_request'); end if;
  perform jr_private.ensure_profile(p_user);
  select * into pr from public.jr_catalog_products where id = p_product;
  if not found then return jsonb_build_object('ok', false, 'reason', 'unknown_product'); end if;
  -- test purchases: Google Play license-tester purchases (checked with Google, only accounts you listed in
  -- Play Console ▸ License testing can make them) are allowed unless 'allow_play_test_purchases' = 'false'.
  -- Simulated 'TEST-…' tokens from development builds are NOT checked by anyone: only with 'allow_test_purchases'.
  if p_test then
    if p_token like 'TEST-%' then
      if coalesce(jr_private.setting('allow_test_purchases'), 'false') <> 'true' then
        return jsonb_build_object('ok', false, 'reason', 'test_not_allowed'); end if;
    elsif coalesce(jr_private.setting('allow_play_test_purchases'), 'true') = 'false' then
      return jsonb_build_object('ok', false, 'reason', 'test_not_allowed');
    end if;
  end if;
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
