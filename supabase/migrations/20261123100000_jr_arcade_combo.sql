-- Arcade combo (near misses and overtakes): the score bound grows with them, mirroring C#
-- RunBounds.MaxScore(d, coins, passengers, deliveries, humps, nearMisses, overtakes) and
-- ComboMeter.MaxPoints (150 / 40 points x max multiplier 5 x Turbo 2). Runs without these
-- fields clamp exactly as before.

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
  return r || jsonb_build_object('passengers', pas, 'deliveries', del, 'power_ups', pu, 'smooth_humps', hu, 'refuels', rf, 'coins', co, 'score', sc, 'overtakes', ot, 'near_misses', nm);
end $$;
