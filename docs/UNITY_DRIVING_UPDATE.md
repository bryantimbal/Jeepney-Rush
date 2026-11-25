# Driving, controls, traffic, events and dialogue update

Unity can't run in the build environment. This was checked with both compile checks, the .NET
tests (233 pass, 1 long sweep skipped by default) and the long fairness sweep (140 runs × 4 km on
all 8 routes: 0 bad, previously 1). Every item below still needs the on-device checklist at the end.

## What changed and where

| # | Request | Implementation |
|---|---|---|
| 1 | Smaller startup logos | `UIManager.BuildDevSplash` (Ninja Tech 720 → 560 wide) and `BuildSplash` (Jeepney Rush 900 → 680 wide). Height comes from the texture's own aspect ratio, so nothing stretches. |
| 2 | Time-aware roadside events | `RoadEvents.For(env, night)`: taho and carabao are daytime only. At night a new **balut vendor** ("BALUUUT! PENOY!") takes the taho's place with the same weight. The spawner checks the clock now and ~12 s ahead (`SpawnDirector.IsNight`). A daytime event still far ahead when night falls is removed before you see it. Taho was removed from the sidewalk scenery because scenery is the same day and night. |
| 3 | Traffic freezing at stops | **Root cause:** `HazardManagerBase.Tick` held *every* vehicle behind the jeepney to its speed, in all lanes. **Fix:** `FollowRules`, which the game and the fairness test share. Only a vehicle directly behind the jeepney waits; other lanes keep moving. |
| 4–7 | Steering and controls | **One steering system** (`Logic/Steering.cs`). Buttons, tilt and the wheel all produce an input that turns a steering wheel. The wheel builds up over 0.3 s and returns over 0.25 s. Sideways speed follows the wheel, up to 35% tighter at low speed, and is never slower at top speed than what the spawner plans. A road-edge assist eases the jeepney to a stop on the shoulder and back, and lane assist is optional. The body heading follows the real direction of travel, and the cockpit wheel shows the real input. Tuning lives in `BalanceConfig.steering` (GameBalance asset). **Virtual wheel:** drag to rotate, ±90/135/180° limits, proportional input, springs back. **Tilt:** stored calibration, dead zone, smoothing, sensitivity presets. **Multi-touch:** every control keeps its own pointer, and all of them are released on pause or game over. **Gear D/R:** only shown when stopped or reversing. |
| 8, 11 | Steering mode and HUD settings | **Settings ▸ CONTROLS & STEERING**, also reachable from the pause menu and applied immediately: mode (Buttons / Tilt / Wheel), sensitivity, max steering (the spawner plans for capped steering), return-to-centre speed, lane assist, tilt calibration/dead zone/smoothing, wheel size/opacity/rotation, button size/opacity, and left- or right-thumb steering. The first time you use each mode, a steering hint is shown. |
| 9 | Speedometer by camera view | Top right, under the hearts and fuel. **Likod view:** compact digital "63 km/h" only. **Driver view:** analog gauge with a smooth needle and the km/h value in the middle. Both read the same speed. The bottom-left speed readout was removed. |
| 10 | Driver dialogue | `Logic/DriverTalk.cs`: natural, polite lines ("PAKI-ABOT NA LANG PO NG BAYAD.", "PASENSYA NA PO, PUNO NA!", "OKAY NA PO, BIYAHE NA!", "AY, MUNTIK NA!", "INGAT PO, MAY LUBAK!", "AY, PAHINGA MUNA TAYO!"). Each moment has 3–4 variations and never repeats a line twice in a row. One line plays at a time, and warnings can cut in. Hooked to boarding, fare, full, departure, near miss, pothole and game over. "BAYAD NATANGGAP!" is gone. The game has no voice recordings; these are speech bubbles. |
| – | Staying signed in | Only one login-token refresh runs at a time. A lost email login now asks you to sign in again (email filled in) instead of silently becoming a guest. |
| – | Daily challenge fairness | The spawner and the passenger route now use their own seeded random streams. Before, chatter and horn reactions drew from the same stream, so the road could change depending on how you drove. |

## Not done (on purpose)
- **Dragging controls to custom positions:** not built. Size, opacity and handedness are covered.
- **Gyroscope:** tilt uses the accelerometer (gravity) with filtering, which works on every
  Android phone. A gyro-fused version would only help phones that have a gyro.

## Device checklist
1. **Buttons:** hold LEFT for 0.3 s, 1.5 s and 10 s. The jeepney moves steadily, stops on the
   shoulder (never the curb), and settles back into the outer lane when released.
2. **Tilt:** calibrate in Settings while holding the phone the way you drive. Small wobbles do
   nothing. A steady tilt gives steady steering, not a growing turn. Straightening the phone
   straightens the jeepney.
3. **Wheel:** drag it clockwise and anticlockwise without lifting your finger. It stops at the
   limit and springs back when released. Wheel + GAS + TURBO and wheel + GAS + BUSINA work together.
4. **Multi-touch:** LEFT+GAS, RIGHT+BRAKE and so on. Releasing one finger never releases another.
   Pause while holding GAS, then resume: nothing stays stuck.
5. **Low speed vs top speed:** steering is a little tighter when crawling into a stop.
6. **Switching modes from the pause menu:** applies immediately.
7. **Driver view:** the analog gauge appears. **Likod view:** only the digital km/h. No overlap with the pause button, fuel or hearts.
8. **Stopping at a PARA stop:** cars in the other lanes keep driving past. The car right behind you waits.
9. **Night:** no taho or carabao, and the balut vendor appears. **Day:** no balut vendor.
10. **Dialogue:** lines don't overlap, and the full jeep, departure and game-over lines appear at the right moments.
