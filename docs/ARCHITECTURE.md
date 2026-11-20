# Architecture

## Frame order
`GameManager.frame()` runs one fixed order every frame (dt clamped to 50 ms):

1. **Input** – `Input` turns swipes / keys into actions (`left right up down horn pause`) plus a `holding` flag (gas).
2. **Speed model** – base speed from `baseSpeedAt(distance)` × gas × brake × crash slow-down; smoothed.
3. **Player** – `PlayerController.update` (lane easing, jump physics, visual tilt/pitch/bob, wheel spin).
4. **Fuel / score / distance**.
5. **Spawning** – `SpawnDirector.update(distance, speed)` fills the road up to 175 m ahead.
6. **World** – `RoadManager` (texture scroll + scenery chunk recycling), `TrafficManager`, `ObstacleManager`, `Effects`.
7. **Collisions** – AABB in lane space (`overlapsAt`) with forgiveness margins; height check lets jumps clear low obstacles.
8. **Pickups** – `CoinManager` (coins + fuel), `PassengerManager` (stop zones → `PassengerRoute.serve/miss`).
9. **Camera, audio, HUD**.

## World convention
The jeepney stays at `z = 0`; the world moves toward `+z`. Static objects move at the
player's speed, traffic at `(playerSpeed − ownSpeed)`. This keeps floating-point
precision perfect for arbitrarily long runs. A vertex-shader "bent world"
(`render/Curve.ts`) drops the horizon and adds gentle road bends; it is visual only.

## Level generation & fairness (`game/logic`)
* `SpawnDirector` works in world distance. Each **row** picks a weighted pattern
  (single obstacle, vehicle, double block, jump wall, convoy, road works, parked
  car, motorcycle weavers, breather) according to difficulty.
* Every candidate row is validated by `Occupancy.isPassable`: blocked time
  intervals per lane are predicted from closing speeds, then a reachability DP on
  a 50 ms grid checks that a player starting in their **current** lane can survive.
  A lane change costs `laneChangeTime + 0.1 s` during which both lanes must be clear,
  which builds in human slack.
* Jumpable obstacles are planned as lane-blocking (jumping is a shortcut), except
  full-width **jump walls** which require a jump.
* Passenger stop zones reserve their curb lane so a stop is always reachable.
* `TrafficManager` only lets a vehicle swerve if the road stays passable.
* `tests/unit/director.test.ts` drives a bot with real steering time through 6 km
  of generated traffic on 8 seeds and requires zero collisions.

## Passengers
`PassengerRoute` (pure) plans stops ahead of the spawn cursor, assigns each boarding
passenger a destination among the next stops, pays fares + group bonus × streak
multiplier, and handles missed stops. `PassengerManager` renders sheds, signs,
zones, waving commuters, and boarding/alighting animations.

## Rendering & performance
* Models are built from primitives in `ModelKit` and **merged per material with
  vertex colours** → 1–4 draw calls per vehicle.
* Pools for every hazard variant, fuel can, person and stop; coins and particles
  are single `InstancedMesh`es. No allocations in the steady-state loop.
* Scenery: 9 pre-built 40 m Manila chunks recycled in a ring.
* Quality levels: `low` (no shadows, DPR 1), `medium` (default; 1024² shadows,
  DPR ≤ 1.5), `high` (2048² shadows, DPR ≤ 2).
* Typical frame: ~140 draw calls, ~42k triangles, ~0.1 ms simulation CPU.

## Events
`core/Events.ts` is a typed event bus. Gameplay emits (`coin`, `pickup`, `deliver`,
`hit`, …); audio and haptics subscribe. Later phases (missions, achievements,
analytics) plug in here without touching gameplay code.

## Saves
`SaveManager` stores `{sum, body}` where `sum` is an FNV-1a checksum of the JSON
body. Before each write the previous good save is copied to a backup key. Load
order: main → backup → defaults, and `sanitize()` clamps every field. On Android,
`PlatformStore` mirrors writes to Capacitor Preferences and restores from it if the
WebView's storage was cleared. The checksum detects corruption, not cheating.
Anti-cheat belongs on the server and comes in Phase 3.

## Platform lifecycle
`platform/Platform.ts`: app `pause` → game pauses + audio context suspends;
`resume` → audio resumes (game stays paused until the player taps Resume);
Android back → close modal / pause / resume / exit from the main menu.
