# Testing

## Automated

| Command | What it covers |
|---|---|
| `npm run typecheck` | Strict TypeScript across game + tests |
| `npm test` | **Vitest unit tests** (26): occupancy solver, passenger bookkeeping & rewards, save/backup/recovery/sanitising, and the **level fairness simulation** (bot with real steering time drives 6 km × 8 seeds, must not collide; also asserts stops, coins, fuel and deliveries appear) |
| `npm run test:e2e` | **Playwright end-to-end**: boots the real game in headless Chromium, opens every menu, starts a run, verifies lane change / buffered double move / jump / brake / gas, pause freezes the world, steers into a PARA stop and checks passengers board, waits for a crash → game over screen, checks best score and settings survive a reload, and fails on any console/page error. `SHOTS=1` saves screenshots to `tests/e2e/out/` |

The e2e script uses Playwright's Chromium (`require('playwright')`, falls back to the global install).

## Manual test checklist (device)

1. Install a debug build (`docs/ANDROID_BUILD.md`). The app launches in portrait and fullscreen.
2. Splash shows loading progress, then **TAP TO START**. Menu music starts after the tap.
3. Swipes: left/right change lanes; two quick swipes move two lanes; up jumps; down brakes (red brake bar); touch-and-hold shows **GAS!**.
4. Hit a car → heart lost, red flash, vibration, sparks; 3 hits → **BANGGA!** game over.
5. Drive through a yellow **PARA** zone → "+N SAKAY!", the passenger counter rises. A green arrow over a stop means someone gets off there. Drive through it for fare coins and confetti; skip it to see **LAGPAS!**
6. Let fuel run out → the jeepney coasts to a stop → **UBOS GAS!**
7. Press home during a run, then return → the game is paused and silent. Press back → it pauses or resumes; from the main menu back exits.
8. Settings: music/SFX toggles act independently and persist; the Low/Medium/High graphics setting changes shadows and resolution.
9. Records shows lifetime stats and the top 10 local runs.
10. Turn on **Show FPS**. On a mid-range phone, Medium should hold about 60 FPS.

## Debug hooks
* `?seed=123` → deterministic run layout.
* `?autostart` → skip the splash tap (for tests; audio may stay locked).
* `window.__jr.state()` → snapshot of the run (distance, speed, lane, health, passengers, …).
