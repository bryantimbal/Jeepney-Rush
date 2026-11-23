-- Near misses / overtakes raise the score bound (same formula as C# RunBounds.MaxScore + ComboMeter.MaxPoints).
\set ON_ERROR_STOP on
\pset tuples_only on
\pset format unaligned
do $$ declare base jsonb; c jsonb; begin
  base := jsonb_build_object('run_id', 'x', 'duration', 300, 'distance', 3000, 'coins', 0, 'score', 999999999,
    'passengers', 0, 'deliveries', 0, 'power_ups', 0, 'smooth_humps', 0, 'refuels', 0);
  c := jr_private.run_clamp(base);
  assert (c->>'score')::int = trunc(3000 * 1.5)::int + trunc(150 * (3000 / 300.0 + 1))::int + 1000, 'unchanged without combo fields: ' || c::text;
  c := jr_private.run_clamp(base || '{"near_misses": 20, "overtakes": 100}');
  assert (c->>'score')::int = 4500 + 1650 + 1000 + 20 * 1500 + 100 * 400, 'combo points allowed: ' || c::text;
  c := jr_private.run_clamp(base || '{"near_misses": 99999, "overtakes": 99999}');
  assert (c->>'near_misses')::int = 305 and (c->>'overtakes')::int = 505, 'combo counts clamped by distance: ' || c::text;
  raise notice 'ok  arcade combo: score bound includes near misses / overtakes, counts clamped by distance';
end $$;
