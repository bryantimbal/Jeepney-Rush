-- Talyer cosmetics: bought once with coins at the catalog price; earned items can't be bought.
\set ON_ERROR_STOP on
\pset tuples_only on
\pset format unaligned
\set u 'eeeeeeee-0000-0000-0000-00000000000a'
insert into auth.users (id) values (:'u');
select set_config('request.jwt.claim.sub', :'u', false) \g /dev/null
select jr_private.ensure_profile(:'u') \g /dev/null
update public.jr_profiles set coins = 1000 where user_id = :'u';
insert into public.jr_transactions (user_id, kind, amount, ref, currency) values (:'u', 'test_grant', 1000, 'test', 'coins');
set role authenticated;
do $$ declare r jsonb; begin
  assert (select price from public.jr_catalog_cosmetics where id = 'paint_asul') = 400, 'price from C#';
  r := public.jr_buy_cosmetic('paint_asul');
  assert (r->>'ok')::boolean and (r->'snapshot'->>'coins')::int = 600, 'bought: ' || r::text;
  assert (r->'snapshot'->'cosmetics') ? 'paint_asul', 'snapshot lists it';
  r := public.jr_buy_cosmetic('paint_asul');
  assert not (r->>'ok')::boolean and r->>'reason' = 'owned', 'only once';
  r := public.jr_buy_cosmetic('paint_ginto');
  assert r->>'reason' = 'not_for_sale', 'earned with a license';
  r := public.jr_buy_cosmetic('paint_stock');
  assert r->>'reason' = 'not_for_sale', 'stock is free';
  r := public.jr_buy_cosmetic('lights_neon_blue');
  assert r->>'reason' = 'coins' and (r->'snapshot'->>'coins')::int = 600, 'not enough coins';
  r := public.jr_buy_cosmetic('nope');
  assert r->>'reason' = 'unknown_item';
  assert (select count(*) from public.jr_cosmetics) = 1, 'player reads own rows';
  begin insert into public.jr_cosmetics (user_id, item_id) values (auth.uid(), 'lights_fog'); assert false, 'no direct writes';
  exception when insufficient_privilege then null; end;
  raise notice 'ok  cosmetics: catalog price, once each, earned items not for sale, no direct writes';
end $$;
reset role;
do $$ begin
  assert (select sum(amount) from public.jr_transactions where user_id = 'eeeeeeee-0000-0000-0000-00000000000a' and currency = 'coins')
       = (select coins from public.jr_profiles where user_id = 'eeeeeeee-0000-0000-0000-00000000000a'), 'ledger balances';
end $$;
