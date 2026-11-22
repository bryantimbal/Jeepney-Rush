# Roadmap

| Phase | Scope | Status |
|---|---|---|
| **1. Playable MVP** | Menu, one jeepney, 3-lane driving, traffic & obstacles, coins, passengers, score/distance, game over/restart, SFX/music, local saves, Android shell | ✅ **Done** |
| 2. Game expansion | Garage, more jeepneys & upgrades, power-ups, more environments, missions, achievements | ✅ Done |
| 3. Online | Supabase auth (guest + account), cloud save, leaderboards, daily challenges, profiles | ✅ Done (see UNITY_PHASE3_REPORT.md) |
| 4. Monetization | AdMob (banner on menus, interstitial between selected runs, rewarded), IAP, ad-free | ✅ Done (see UNITY_PHASE4_REPORT.md) |
| 5. Google Play release | Optimisation pass, release build + preflight, privacy & account deletion, store listing, internal → closed → production | 🚧 In progress (see RELEASE.md) |

## Phase 2 – design notes (extension points already in the code)
* **Garage / skins**: `JeepneySkin` (`art/JeepneySkins.ts`) already drives every colour,
  slogan, route sign and plate; `buildJeepney(skin)` builds any variant. Planned
  unlocks: Manila Classic, Modern Jeepney, Cebu Express, Baguio Cruiser, Davao
  Explorer, Neon Street Racer, Gold VIP, Fiesta Edition, Philippine Flag Edition.
* **Upgrades** map onto existing tuning: engine → `SPEED.max`, brakes →
  `laneChangeTime`/`brakeCooldown`, armour → `PLAYER.maxHealth`, fuel efficiency →
  `FUEL.drainPerSecond`, capacity → `PassengerRoute.capacity` (5 → 15),
  coin bonus → `REWARD`.
* **Power-ups**: coin magnet (`CoinManager.magnetRadius` already supported), shield
  and turbo (invulnerability path exists in collisions), double coins and passenger
  bonus (`PassengerRoute.fareMultiplier`), repair kit (`player.health`).
* **Environments**: `RoadManager` takes chunk builders; add EDSA, Cebu, Baguio
  (stronger `uCurveX` bends), provincial and beach builders plus per-environment sky/fog/music.
* **Missions & achievements** subscribe to the typed `GameEvents` bus.
* **Modes**: Passenger Challenge / Time Attack reuse the run loop with a goal and timer.

## Phase 3 – backend plan (Supabase)
> **As built:** the server rules ended up as Postgres `SECURITY DEFINER` functions (called via PostgREST RPC) instead of Edge Functions – same server-side authority, atomic with the data, and testable locally. See `docs/UNITY_PHASE3_REPORT.md` and `docs/SUPABASE_SETUP.md`.

Tables: `profiles, player_inventory, jeepneys, jeepney_upgrades, game_sessions,
player_scores, missions, mission_progress, achievements, player_achievements,
daily_rewards, transactions`, all with Row Level Security. Currency and score
changes go **only** through Edge Functions using the service role on the server.
The client submits a signed run summary (seed, duration, distance, events), and the
server checks it for plausibility (max speed × time, coins per metre, deliveries per stop)
before crediting. The service-role key never ships in the app. Offline guest progress is
queued locally and synced when the device reconnects.

## Phase 4 – monetization rules
Never show ads during gameplay, never force them, cap interstitial frequency, use test
IDs in development, keep production IDs in build-time config, and offer restore purchases.
