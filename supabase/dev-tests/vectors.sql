-- Cross-language checks: the SQL rules must give exactly the same results as the C# game
-- logic (vectors written by unity/tools/ExportCatalog).
\set ON_ERROR_STOP on
create temp table v_rng (seed bigint, n int, value double precision);
\copy v_rng from 'vectors/rng.csv' csv
create temp table v_missions (period text, k int, slot int, type text, target int, reward int);
\copy v_missions from 'vectors/missions.csv' csv
create temp table v_daily (k int, seed int, env int);
\copy v_daily from 'vectors/daily.csv' csv
create temp table v_dates (d date, day_key int, week_key int);
\copy v_dates from 'vectors/dates.csv' csv
create temp table v_levels (xp int, lvl int);
\copy v_levels from 'vectors/levels.csv' csv
create temp table v_bounds (run_id text, duration real, distance real, env int, coins int, score int, passengers int, deliveries int,
  power_ups int, smooth_humps int, refuels int, hits int, wet real, clean real,
  verdict text, o_coins int, o_score int, o_passengers int, o_deliveries int, o_power_ups int, o_humps int, o_refuels int);
\copy v_bounds from 'vectors/bounds.csv' csv

do $$
declare r record; s bigint; v double precision; i int; bad int := 0;
begin
  for r in select distinct seed from v_rng loop
    s := jr_private.rng_seed(r.seed);
    for i in 0 .. 5 loop
      select x.s, x.v into s, v from jr_private.rng_next(s) x;
      if v <> (select value from v_rng where seed = r.seed and n = i) then bad := bad + 1; raise warning 'rng seed % n %: % vs %', r.seed, i, v, (select value from v_rng where seed = r.seed and n = i); end if;
    end loop;
  end loop;
  if bad > 0 then raise exception 'rng mismatch: %', bad; end if;
  raise notice 'rng: % values match', (select count(*) from v_rng);
end $$;

do $$
declare bad int;
begin
  select count(*) into bad from v_missions m
  left join lateral (select * from jr_private.missions(m.period, m.k) x where x.slot = m.slot) s on true
  where s.type is distinct from m.type or s.target is distinct from m.target or s.reward is distinct from m.reward;
  if bad > 0 then raise exception 'missions mismatch: % of %', bad, (select count(*) from v_missions); end if;
  raise notice 'missions: % rows match', (select count(*) from v_missions);
end $$;

do $$
declare bad int;
begin
  select count(*) into bad from v_daily d, lateral jr_private.daily_challenge(d.k) c where c.seed <> d.seed or c.env <> d.env;
  if bad > 0 then raise exception 'daily challenge mismatch: %', bad; end if;
  select count(*) into bad from v_dates d where jr_private.day_key(d.d) <> d.day_key or jr_private.week_key(d.d) <> d.week_key;
  if bad > 0 then raise exception 'day/week key mismatch: %', bad; end if;
  select count(*) into bad from v_levels l where jr_private.level_for(l.xp) <> l.lvl;
  if bad > 0 then raise exception 'level mismatch: %', bad; end if;
  raise notice 'daily challenges, dates, levels match';
end $$;

do $$
declare b record; rep jsonb; v text; c jsonb; bad int := 0;
begin
  for b in select * from v_bounds loop
    rep := jsonb_build_object('run_id', b.run_id, 'duration', b.duration, 'distance', b.distance, 'env', b.env, 'coins', b.coins, 'score', b.score,
      'passengers', b.passengers, 'deliveries', b.deliveries, 'power_ups', b.power_ups, 'smooth_humps', b.smooth_humps, 'refuels', b.refuels,
      'hits', b.hits, 'wet_distance', b.wet, 'clean_distance', b.clean);
    v := coalesce(jr_private.run_violation(rep), 'ok');
    if v <> b.verdict then bad := bad + 1; raise warning '% verdict % vs %', b.run_id, v, b.verdict; continue; end if;
    if v = 'ok' then
      c := jr_private.run_clamp(rep);
      if (c->>'coins')::int <> b.o_coins or (c->>'score')::int <> b.o_score or (c->>'passengers')::int <> b.o_passengers
         or (c->>'deliveries')::int <> b.o_deliveries or (c->>'power_ups')::int <> b.o_power_ups or (c->>'smooth_humps')::int <> b.o_humps
         or (c->>'refuels')::int <> b.o_refuels then
        bad := bad + 1; raise warning '% clamp % vs coins % score % pas % del % pu % hu % rf %', b.run_id, c, b.o_coins, b.o_score, b.o_passengers, b.o_deliveries, b.o_power_ups, b.o_humps, b.o_refuels;
      end if;
    end if;
  end loop;
  if bad > 0 then raise exception 'run bounds mismatch: %', bad; end if;
  raise notice 'run bounds: % reports match (% accepted)', (select count(*) from v_bounds), (select count(*) from v_bounds where verdict = 'ok');
end $$;
