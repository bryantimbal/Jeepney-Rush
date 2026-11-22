# City routes, night time and EDSA traffic

## New routes
| Route | Unlock | Scenery | Traffic |
|---|---|---|---|
| **EDSA** | 1,800 m | Median + busway, elevated MRT with trains and stations, malls, office towers, giant billboards, footbridges, bus stops | Heaviest in the game (×1.6), lots of buses, no tricycles, **rush-hour jams** |
| **Cebu City** | 3,200 m | Colon-style arcaded downtown, IT Park towers, seaside stretches with boats and mountains across the water, Sinulog banners | Busy, lots of motorbikes, lighter jams |
| **Baguio** | 5,000 m | Pine-covered hills, colourful houses stacked on the slopes, Session Road shops, Burnham lake, thick fog, steep hills | Lighter (×0.8), taxis/SUVs, no tricycles |

Each route has 12 stops with real place names (EDSA: Monumento → Taft Ave; Cebu: Colon, Carbon, IT Park…; Baguio:
Session Rd, Burnham Park, Mines View…), its own music variant, fog and ground. All signs/billboards use the game's
made-up shop brands. No real logos.

## Night time (sunset during the run)
* Every run starts at **4:30 PM**; one in-game hour passes every 20 s, so sunset comes after ~20 s,
  full night at ~50 s, and a long run drives through midnight into dawn and the next morning.
* HUD clock under the weather; "PALUBOG NA ANG ARAW…", "GABI NA! · BUKAS ANG ILAW", "UMAGA NA!" messages.
* Lighting: golden-hour orange sky and long shadows, then dark blue sky, blue moonlight, darker fog.
* Lights: lit windows on buildings and houses, lamp posts with pools of light on the road, lit shop fronts, MRT trains,
  self-lit billboards, glowing tail lights. Your jeepney gets a **headlight beam** on the road; on Medium/High graphics
  also a real spot light. All other night lights are cheap transparent quads (fine on low-end phones).

## EDSA traffic jams
* Jams are planned along the road from the run seed (EDSA: every 400–650 m, 200–340 m long; Cebu: rarer and lighter).
* In a jam: traffic crawls (1.2–3.2 m/s) bumper-to-bumper, rows are packed closer, and your top speed eases down to
  ~42 %. "RUSH HOUR SA EDSA!" / "TRAFFIC AHEAD · 120 m" / "LUMUWAG NA ANG DAAN!" on the HUD.
* Riders complain while you're stuck: late for work in the morning ("MALE-LATE NA AKO SA TRABAHO!"), tired and hungry
  at night ("GUSTO KO NANG UMUWI!", "NAGHIHINTAY ANG MGA ANAK KO!"), plus EDSA classics ("SANA NAG-MRT NA LANG AKO!").
  After 4 complaints in one jam, a fed-up rider may get off and walk.
* Fairness: every row is still validated by the lane planner, also at the slower speeds you'll have in and after a
  jam, and braking to a stop is always safe. The no-brake test bot passes all fixed seeds; in a long 140-seed sweep it
  still crashes in ~8 % of 4 km runs (old routes ~4 %): a human can always brake.

## Also fixed
* The shared colour palette was full (256 colours): newer colours (including some Patok details) were drawn with the
  fallback colour. It now holds 1,024 colours, and a test keeps headroom.

## Tests
135 .NET tests (new: routes/stops, tricycle bans, jam schedule and speed cap, day/night clock, commuter lines, palette
headroom; the fairness bot now drives EDSA/Cebu/Baguio and slows in jams). Server catalog migration regenerated
(7 routes); Supabase tests pass.

## Driver rating, panic and police chase
* **Rating ★1.0–5.0**, starts at ★4.0 every run (HUD under the clock, final value on Game Over).
  +0.1 serving a stop · +0.05 smooth hump · +0.1 slow past a checkpoint · −0.4 skipped stop (someone waiting or
  getting off) · −0.2 crash · −0.1 hump too fast with riders · −0.3 red light · −0.2 ticket · −0.2 rider walks off.
  Fares/tips scale with it: ★5 +20 %, ★3.5 normal, ★2 −20 % (floor −30 %).
* **Panic**: 3 skipped stops in a row → riders shout ("KUYA, LAMPAS NA AKO!", "PARA! PARA! PARA!"), bang on the roof
  (sound + shake), and 1–2 jump off without paying at the next stop you serve, which also calms the rest down.
* **Police chase** at ★1.0: patrol car with lights and siren starts 75 m behind and closes at 3.2 m/s (~23 s).
  Come to a full stop for 1.5 s (pull over, or stop at a PARA) → warning, fine, rating back to ★2.0.
  Keep driving → **BUSTED · HULI KA NG PULIS**, game over (no revive offer). Always police, on every route.

## Fixes
* EDSA billboards were wider than the sidewalk (half inside the buildings) and mall signs sat inside the mall wall:
  roadside boards now fit between curb and building line; mall signs stand on the roof.
* Speech bubbles stay fully on screen (clamped to the edges, long lines wrap).
