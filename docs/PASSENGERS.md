# Passenger personalities

Every rider who boards has a personality (`Scripts/Logic/Riders.cs`, rules in
`PassengerRoute.Serve`, tests in `Tests/EditMode/RiderTests.cs`). Most are ordinary; the rest:

| Rider | Tag on the HUD | What they do |
|---|---|---|
| Estudyante | ESTUDYANTE | Student discount (80 % fare); mostly daytime |
| Lola / Lolo | LOLA | Smooth ride = +3 tip. A crash or a fast hump/pothole upsets her (no tip) |
| Chismosa | CHISMOSA | Gossips the whole way; pays normally |
| Nagmamadali | NAGMAMADALI 12s | Deadline countdown: on time = +4 tip, late = complaint and −0.15 rating |
| Lasing | LASING | Night only; 25 % chance to pay double ("Sa'yo na ang sukli!") |
| Hindi nagbayad | – (looks like anyone) | Jumps off without paying: honk (BUSINA) within 2.5 s to make them pay |
| Tindera | TINDERA | Big bayong takes two seats; +3 for the cargo |
| Balikbayan | BALIKBAYAN | Rare (one at a time); +10 tip if no crash while aboard |

Chances depend on the route (more people in a hurry in the city, more tinderas in towns) and the
time of day. Tips scale with the driver rating / passenger bonus, not the delivery streak.
Results show the tips and the fare dodgers caught.

Personalities come from their own seeded stream, so the Daily Challenge road, stop names and
destinations are unchanged. Tips fit inside the server's coin bound for a run (see the test
`TipsStayWithinTheServerCoinBound`), so no server change is needed.

## Models
`Logic/Geometry/RiderBuilder.cs`: each personality has its own standing and seated model (white
polo + ID lace + backpack, grey bun + duster + cane, curlers + pamaypay, lanyard + laptop bag,
sando + towel + bottle, hoodie + cap, apron + bayong, shades + aloha shirt + balikbayan box).
Seated riders in the jeepney wear their look; the notable rider (or the fare dodger) is the one
you see getting off. People waiting at a stop are still generic (who they are is decided when
they board).

## Missions (from 1 December 2026, PH time)
Daily: catch fare dodgers, earn tips, give lolas a smooth ride, get riders in a hurry there on
time. Weekly (from week 154, Monday 7 December): earn tips. Earlier days and weeks keep exactly
the missions they had (`Missions.FromKey`, `jr_catalog_missions.from_key`). The run report carries
`tips`, `dodgers_caught`, `smooth_lolas` and `on_time`; the server clamps them (tips ≤ 25 per
delivery, the counts ≤ deliveries). Migration: `20261126102000_jr_passenger_missions.sql` + the
catalog `20261126102833_jr_catalog.sql` – apply them before 1 December.
