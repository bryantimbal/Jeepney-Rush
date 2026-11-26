-- Passenger missions: new pool entries only from their start day / week, clamps for the new report fields.
\set ON_ERROR_STOP on
\pset tuples_only on
\pset format unaligned
do $$
declare n int; c jsonb; d int;
begin
  -- days before the start never get a passenger mission; after it they show up
  select count(*) into n from generate_series(0, 120) g, lateral jr_private.missions('daily', 20261130 - g) m
   where m.type in ('TotalDodgersCaught', 'TotalTips', 'TotalSmoothLolas', 'TotalOnTime');
  assert n = 0, 'no passenger missions before 1 Dec 2026';
  select count(*) into n from generate_series(1, 60) g, lateral jr_private.missions('daily', jr_private.day_key(date '2026-11-30' + g)) m
   where m.type in ('TotalDodgersCaught', 'TotalTips', 'TotalSmoothLolas', 'TotalOnTime');
  assert n > 10, 'passenger missions appear from December: ' || n;
  select count(*) into n from generate_series(100, 153) w, lateral jr_private.missions('weekly', w) m where m.type = 'TotalTips';
  assert n = 0, 'weekly tips mission only from week 154';
  -- clamps (same as C# RunBounds.Clamp)
  c := jr_private.run_clamp(jsonb_build_object('distance', 2000, 'passengers', 10, 'deliveries', 2, 'coins', 0, 'score', 0,
        'tips', 9999, 'dodgers_caught', 9, 'smooth_lolas', -3, 'on_time', 1));
  assert (c->>'tips')::int = 50 and (c->>'dodgers_caught')::int = 2 and (c->>'smooth_lolas')::int = 0 and (c->>'on_time')::int = 1, 'clamped: ' || c::text;
  c := jr_private.run_clamp(jsonb_build_object('distance', 500, 'passengers', 1, 'deliveries', 1, 'coins', 0, 'score', 0));
  assert (c->>'tips')::int = 0 and (c->>'on_time')::int = 0, 'old clients without the fields';
  raise notice 'ok  passenger missions: start day / week respected, new report fields clamped';
end $$;
