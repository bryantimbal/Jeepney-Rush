# Unity Phase 2 – Game Expansion report

## What's new
| Feature | Details |
|---|---|
| **GAS / BRAKE buttons** | Hold-to-accelerate (+30% speed, ×1.5 score) and hold-to-brake pedals, one per thumb. Multi-touch: swipe with one finger while holding a pedal. Keyboard: Shift and B/Ctrl. |
| **Passenger delivery** | Every passenger has a destination. A **green arrow** above a stop, and "N bababa" in the HUD, means riders get off there. Driving through pays fares, a group bonus and a delivery-streak multiplier (up to ×3). Missing their stop loses them ("LAGPAS!") and resets the streak. |
| **Power-ups** | Coin Magnet, Shield (blocks one crash, shown as a bubble), Turbo Boost (×1.6 speed, smash through traffic), Double Coins, Passenger Bonus (×2 fares), Repair Kit (+1 heart, or coins at full health). Each has its own pickup model, sound and a HUD timer bar. Picking one up again refreshes its timer; they don't stack. Spawns are spaced 320–560 m apart. |
| **Garage shop** | 11 jeepneys. Two are free; the other nine cost 600–8,000 coins: Manila Classic, Modern Jeepney (boxy PUV body), Cebu Express, Baguio Cruiser, Davao Explorer, Fiesta Edition (banderitas), Neon Street Racer (glowing strips), Philippine Flag Edition, Gold VIP (gold chrome). Each has its own art, roof sign and stats (seats, health, speed, handling). |
| **Upgrades** | Engine, Brakes & Steering, Armor, Passenger Capacity (up to 15 seats), Coin Bonus, Power-up Duration. 5 levels each, shared across all jeepneys. |
| **Missions** | 3 daily and 2 weekly missions, generated from the date (works offline). Progress bars, claim buttons, and a reset countdown. A red dot on the MISSIONS button shows when a reward is ready. |
| **Achievements** | 11 achievements: First Passenger, 1,000 Meters Driven, Traffic Survivor, Coin Collector, Passenger Master, 100 Deliveries Completed, Highway Legend, Power Player, Jeepney Collector, Fully Loaded, Island Hopper. Each has a coin reward. |
| **Player level / XP** | XP from distance, passengers, deliveries and power-ups. Every level-up pays coins. The level and XP bar appear on the main menu. |
| **New routes** | **Provincial Road** (rice paddies, coconut trees, bahay kubo, carabao, basketball court, chapel, mountains) unlocks at a best of 1,000 m. **Beach Highway** (sea, bangkas, umbrellas, lighthouse, resorts) unlocks at 2,500 m. Each route has its own traffic mix, stop names, sky/fog and music arrangement. |
| **Game over** | Adds deliveries, coin-bonus payout, XP, level-ups, completed missions, new achievements and unlocked routes. |

The visible seats inside the jeepney went from 5 to 10, so larger capacities show more passengers.

## Verification
| Check | Result |
|---|---|
| `dotnet test unity/Tests.DotNet` | **54/54 pass**: delivery/fares/streak/miss, power-up timers/shield/turbo/duration scaling, shop and upgrade purchases and caps, effective-stat calculation, coin bonus, XP/levels, deterministic daily/weekly missions and claiming, achievements, route unlocks, save migration v1→v2, plus all Phase 1 tests |
| Fairness bot | 8 seeds × 4 km across **all three routes** with power-ups spawning: 0 collisions |
| Mesh checks | All 11 jeepneys, power-ups, shield bubble, 6 seeds each of Manila/Provincial/Beach scenery: indices, normals, triangle winding |
| Compile check (runtime + editor) | 0 errors, 0 warnings |
| In Unity / on your phone | **Not yet.** Please test as described below. |

## Updating your Unity project (keeps your scenes and save)
1. Close Unity.
2. Download the latest ZIP from branch `ccr-5502d442-jkkg4f` and extract it.
3. Copy `unity/JeepneyRush/Assets/JeepneyRush` and paste it **onto** your project's `Assets` folder.
   When Windows asks, choose **Replace the files in the destination**.
   Don't delete the old folder first, because that would also delete the Scenes that *Setup Project* created.
4. Open Unity and wait for it to compile. Check the Console for red errors.
5. Run **Jeepney Rush ▸ Setup Project** again. It's safe to re-run: it keeps your scenes and adds the new jeepney assets and texture settings.
6. Open the **Bootstrap** scene and press Play, or build to your phone again.

Your existing coins, best score and settings carry over: the save migrates from version 1 to version 2 automatically.

## What to test on the phone
1. Hold GAS: the speed number rises, and the jeepney tilts back slightly. Hold BRAKE: it slows down. Swipe left or right while holding GAS.
2. Pick up passengers. A later stop shows a **green arrow**. Drive into that lane: "N BABA +coins" and confetti. Skip one: "LAGPAS!".
3. Grab each power-up and watch its timer in the HUD. Shield: crash into a car and you see "SHIELD!" with no heart lost. Turbo: drive through traffic.
4. Garage: buy Manila Classic (600 coins) and an Engine upgrade, select it, and play.
5. Missions: complete one and claim it. Reach 1,000 m, then switch to Provincial Road under ROUTES.

## Not in this phase (still planned)
* Paint, wheel and sticker customisation, and gems. These come with the Phase 4 cosmetics and monetisation work.
* Extra game modes (Passenger Challenge, Time Attack, City Tour).
* Online features: Supabase login, cloud save, leaderboards and server-checked rewards (Phase 3).
* "Fuel efficiency" upgrade: this version has no fuel mechanic, so it was replaced by **Power-up Duration**.

## Update 2 – visual fixes and game-feel features (from phone testing)
| Reported | Change |
|---|---|
| Grainy screen | Native-resolution rendering (removed the 85% resolution scale), MSAA 4x (2x on Low), forced anisotropic filtering, URP render scale 1, and camera post-processing **off** so template film grain/vignette volumes no longer apply. Particles use a soft round sprite instead of hard squares, speed lines are fewer and subtler, and the road and sidewalk textures are less noisy. |
| Flickering jeepney | The body's engine-shake animation ran at 38 Hz, which strobes at 30–60 fps; it's now a gentle 9 Hz sway. The damage blink is a soft 5 Hz pulse instead of a hard 12 Hz strobe. Shadow bias was raised and shadows use a single cascade to stop acne and shimmer. |
| Garage preview | The garage is now a showroom: the selected model spins on a turntable above the shop card, and switching models makes it pop in with confetti. |
| Passengers visible inside | The jeepney cabin is hollow now: floor, inner walls, ceiling lamps and grab rails. Passengers on the benches are visible through the windows and the **open rear doorway**, and up to 10 seats are shown. |
| Addictive coin effects | Coins fly into the coin counter, which bumps. The coin sound rises in pitch with your **COMBO** (counter on screen, changing colour at 10/20/30). Every 10-coin combo triggers **COIN RUSH**, a bonus coin payout with a golden fountain, its own sound and a light haptic tick. Coin sparkle bursts are bigger. |
| Passengers complain | Comic speech bubbles: crash → "ARAY KO!", "KUYA, INGAT!"… with a 60% chance a rider storms out ("BABABA NA AKO!", -1 passenger, delivery streak resets). Hard landings → "SUSMARYOSEP!". Boarding → "BAYAD PO!", alighting → "PARA PO!". |
| MMDA enforcer | After a crash, an **MMDA CHECKPOINT** sets up 140 m ahead on the sidewalk: an enforcer with a STOP paddle, a patrol bike with flashing red/blue sirens, an A-frame sign and cones. A banner says "BRAKE!" with the distance. Pass at no more than 62% of normal speed (hold BRAKE) for +150 score; speed past and you get a **TICKET** (15% of this run's coins, at least 10) plus a whistle. There's a 25 s cooldown, and the enforcer never blocks a lane. |

Tests: **57/57 pass** (adds coin combo, enforcer fine rules, angry-passenger logic). The compile check is clean.
