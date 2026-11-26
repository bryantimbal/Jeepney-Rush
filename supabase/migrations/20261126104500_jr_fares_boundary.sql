-- The boundary comes out of FARES (passenger money), not road coins; diesel bought at the
-- gas station also comes out of the driver's earnings. The run report carries 'fares' (gross
-- passenger money, part of coins) and 'gas' (already taken out of coins). In a career shift the
-- operator gets least(boundary, fares - gas, coins) – the driver never goes into debt. The
-- boundary star needs fares >= boundary. Same rules as C# Career.Apply / RunBounds.Clamp.

create or replace function jr_private.run_clamp(r jsonb) returns jsonb
language plpgsql immutable as $$
declare
  d real := (r->>'distance')::real;
  pas int := least(coalesce((r->>'passengers')::int, 0), trunc((d / 200::real + 2::real) * 15::real)::int);
  del int := least(coalesce((r->>'deliveries')::int, 0), pas);
  pu int := least(coalesce((r->>'power_ups')::int, 0), trunc(d / 250::real)::int + 3);
  hu int := least(coalesce((r->>'smooth_humps')::int, 0), trunc(d / 150::real)::int + 2);
  rf int := least(coalesce((r->>'refuels')::int, 0), trunc(d / 700::real)::int + 1);
  co int := least(coalesce((r->>'coins')::int, 0), trunc(d * 1.0::real)::int + del * 42 + 50);
  ot int := least(greatest(coalesce((r->>'overtakes')::int, 0), 0), trunc(d / 6::real)::int + 5);
  nm int := least(greatest(coalesce((r->>'near_misses')::int, 0), 0), trunc(d / 10::real)::int + 5);
  sc int := least(coalesce((r->>'score')::int, 0),
                  least(2147483647::bigint, trunc(d * 1.5::real)::bigint + co * 10 + pas * 50 + del * 600 + hu * 50 + trunc(150::real * (d / 300::real + 1::real))::bigint + 1000
                        + nm::bigint * 150 * 5 * 2 + ot::bigint * 40 * 5 * 2)::int);
begin
  return r || jsonb_build_object('passengers', pas, 'deliveries', del, 'power_ups', pu, 'smooth_humps', hu, 'refuels', rf, 'coins', co, 'score', sc, 'overtakes', ot, 'near_misses', nm,
    -- passenger personalities (C# RunBounds.Clamp): tips ≤ 25 per delivery, the counts ≤ deliveries
    'tips', greatest(0, least(coalesce((r->>'tips')::int, 0), del * 25)),
    'dodgers_caught', greatest(0, least(coalesce((r->>'dodgers_caught')::int, 0), del)),
    'smooth_lolas', greatest(0, least(coalesce((r->>'smooth_lolas')::int, 0), del)),
    'on_time', greatest(0, least(coalesce((r->>'on_time')::int, 0), del)),
    -- money (C# RunBounds.Clamp): diesel ≤ 60 per refuel; fares ≤ coins + diesel (coins are after the diesel)
    'gas', greatest(0, least(coalesce((r->>'gas')::int, 0), rf * 60)),
    'fares', greatest(0, least(coalesce((r->>'fares')::int, 0), co + greatest(0, least(coalesce((r->>'gas')::int, 0), rf * 60)))));
end $$;

create or replace function jr_private.career_apply(p_uid uuid, r jsonb) returns jsonb
language plpgsql security definer set search_path = public, jr_private as $$
declare
  s public.jr_catalog_career; prev public.jr_catalog_career;
  old_mask int; m int := 0; new_mask int; val int; met boolean;
  star_coins int := 0; rank_before int; rank_after int; rank_coins int := 0; rank_gems int := 0;
  fares int := coalesce((r->>'fares')::int, 0); gas int := coalesce((r->>'gas')::int, 0); paid int;
begin
  select * into s from public.jr_catalog_career where id = coalesce(r->>'career', '');
  if not found then return jsonb_build_object('shift', ''); end if;
  -- unlocked? the license for this chapter, and a star on the shift before it in the same chapter
  if jr_private.career_rank_for(jr_private.career_stars(p_uid)) < s.chapter then
    return jsonb_build_object('shift', s.id, 'mask', 0, 'new_mask', 0, 'locked', true);
  end if;
  select * into prev from public.jr_catalog_career where idx = s.idx - 1 and chapter = s.chapter;
  if found and coalesce((select c.mask from public.jr_career c where c.user_id = p_uid and c.shift_id = prev.id), 0) = 0 then
    return jsonb_build_object('shift', s.id, 'mask', 0, 'new_mask', 0, 'locked', true);
  end if;

  if (r->>'env')::int = s.env and (r->>'distance')::real >= s.length - 0.5 then
    m := 1;
    if fares >= s.boundary then m := m | 2; end if; -- the boundary is collected in FARES, not road coins
    val := case s.goal
      when 'Deliveries' then (r->>'deliveries')::int
      when 'MaxHits' then coalesce((r->>'hits')::int, 0)
      when 'NearMisses' then coalesce((r->>'near_misses')::int, 0)
      when 'Overtakes' then coalesce((r->>'overtakes')::int, 0)
      when 'Streak' then coalesce((r->>'best_streak')::int, 0)
      when 'CleanDistance' then trunc(coalesce((r->>'clean_distance')::real, 0))::int
      when 'Passengers' then (r->>'passengers')::int
      else (r->>'smooth_humps')::int end;
    met := case when s.goal = 'MaxHits' then val <= s.target else val >= s.target end;
    if met then m := m | 4; end if;
  end if;

  -- the operator takes the boundary out of the fares left after the diesel (no debt: never more than the run's coins)
  paid := greatest(0, least(s.boundary, fares - gas, (r->>'coins')::int));
  if paid > 0 then
    update public.jr_profiles set coins = greatest(0, coins - paid) where user_id = p_uid;
    insert into public.jr_transactions (user_id, kind, amount, ref, currency) values (p_uid, 'boundary', -paid, s.id, 'coins');
  end if;

  select coalesce(c.mask, 0) into old_mask from public.jr_career c where c.user_id = p_uid and c.shift_id = s.id;
  old_mask := coalesce(old_mask, 0);
  new_mask := m & ~old_mask;
  if m <> 0 then
    insert into public.jr_career (user_id, shift_id, mask) values (p_uid, s.id, m)
    on conflict (user_id, shift_id) do update set mask = public.jr_career.mask | excluded.mask, updated_at = now();
  end if;
  star_coins := ((new_mask & 1) + ((new_mask >> 1) & 1) + ((new_mask >> 2) & 1))
                * (jr_private.const('career_star_base')::int + jr_private.const('career_star_per_chapter')::int * s.chapter);
  if star_coins > 0 then
    update public.jr_profiles set coins = coins + star_coins where user_id = p_uid;
    insert into public.jr_transactions (user_id, kind, amount, ref, currency) values (p_uid, 'career_star', star_coins, s.id, 'coins');
  end if;

  select career_rank into rank_before from public.jr_profiles where user_id = p_uid;
  rank_after := jr_private.career_rank_for(jr_private.career_stars(p_uid));
  if rank_after > rank_before then
    select coalesce(sum(coins), 0), coalesce(sum(gems), 0) into rank_coins, rank_gems
      from public.jr_catalog_licenses where rank > rank_before and rank <= rank_after;
    update public.jr_profiles set career_rank = rank_after, coins = coins + rank_coins, gems = gems + rank_gems where user_id = p_uid;
    if rank_coins > 0 then insert into public.jr_transactions (user_id, kind, amount, ref, currency) values (p_uid, 'career_license', rank_coins, rank_after::text, 'coins'); end if;
    if rank_gems > 0 then insert into public.jr_transactions (user_id, kind, amount, ref, currency) values (p_uid, 'career_license', rank_gems, rank_after::text, 'gems'); end if;
  end if;
  return jsonb_build_object('shift', s.id, 'mask', m, 'new_mask', new_mask, 'star_coins', star_coins,
    'fares', fares, 'gas', gas, 'boundary_paid', paid,
    'new_rank', case when rank_after > rank_before then rank_after else -1 end, 'rank_coins', rank_coins, 'rank_gems', rank_gems);
end $$;

revoke all on function jr_private.career_apply(uuid, jsonb) from public, anon, authenticated;
