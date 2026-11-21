# Jeepney Rush (Unity): Phase 3 report, online features

Phase 3 covers what the master prompt lists: **Supabase authentication, guest accounts, player profiles, cloud
saves, global leaderboards, friends rankings, daily challenges, player inventory, Row Level Security and
server-side validation.** The game stays fully playable offline.

## What was built
| Feature | How it works |
|---|---|
| **Guest accounts** | On first launch (if online is configured) the game signs in anonymously in the background. Nothing blocks the menu. |
| **Email accounts** | ACCOUNT ▸ *Save progress with email* turns the guest into an email account (same id, progress kept). *Sign in* on another phone loads that account. *Sign out* clears this phone's progress (it stays on the account). |
| **Profiles** | Display name (3–16 chars, filtered), level, best score and an 8-character **friend code** with a COPY button. |
| **Cloud save / inventory** | The server owns coins, XP, owned jeepneys, upgrade levels, bests, lifetime stats, mission progress and claimed achievements. Every response returns a full snapshot that replaces those fields in the local save. Settings stay local. |
| **Offline-first runs** | A finished run is applied locally at once (instant rewards). If signed in, it is also written to an **on-disk queue** and uploaded. The server re-checks it, credits it and returns the authoritative wallet. If you're offline, runs wait (up to 50) and upload automatically (reconnect every 30 s and on app resume). |
| **Purchases & claims** | When online, the garage, upgrades, missions and achievements go through server functions. When signed in but offline they show "Kailangan ng internet…" instead of spending local coins that the server would not honour. With online not configured, everything works locally as before. |
| **Leaderboards** | RANKINGS: **All-time / Weekly / Daily** × **Everyone / Friends**. Top 50 plus your own row pinned if you're lower. Game Over shows "RANK: #3 today · #12 this week · #40 all-time". |
| **Friends** | Add by friend code (mutual), list with level and best score, remove. Friends rankings filter the boards. |
| **Daily Challenge** | DAILY on the menu: today's route and seed are the same for every player (traffic, stops, weather, road shape, lights). It has its own daily ranking. It's computed identically on the phone and the server, so it also works offline (offline runs just aren't ranked). |
| **Old progress** | The first time an existing install signs in, its offline progress is uploaded **once**, capped (≤ 20,000 coins etc.), and it never reaches the leaderboards. |
| **Menu status** | Chip under BEST: OFFLINE MODE / CONNECTING… / ONLINE · name / OFFLINE · N runs waiting / SIGNED OUT. |

## Architecture decisions
* **Server rules live in Postgres functions (`SECURITY DEFINER`), not Edge Functions.** The roadmap planned Edge
  Functions, but database functions run on the server just the same. They are atomic with the data, need no extra
  deployment, and could be **tested for real here** against PostgreSQL 16. Clients call them through PostgREST
  (`/rest/v1/rpc/jr_*`) with the public anon key and their user token.
* **No Supabase SDK.** A ~200-line `SupabaseClient` on `UnityWebRequest` does auth (guest, email, refresh, link,
  sign-out) and RPC. That means no third-party dependency (development rule 12) and nothing new to import.
* **One source of truth for game data.** Prices, upgrade costs, mission pools, achievements and routes are generated
  from C# (`CatalogExport` → `unity/tools/ExportCatalog`). Test vectors prove the SQL port of the PRNG, mission
  generator, daily challenge, level curve and run bounds is identical.
* **Philippine time** (UTC+8) for missions, weekly boards and the daily challenge, on the phone and the server.

## Files
**New**
* `supabase/migrations/20261121000000_jr_online.sql`: schema, RLS, 13 RPCs, private helpers
* `supabase/migrations/20261121000100_jr_catalog.sql`: generated catalog
* `supabase/dev-tests/run.sh`, `shim.sql`, `vectors.sql`, `behaviour.sql`, `vectors/*.csv`, `contract/*.json`
* `unity/tools/ExportCatalog/`: catalog + vector generator
* `Scripts/Logic/Online.cs`: PhilippineTime, DailyChallenge, RunReport, RunBounds, ServerSnapshot & DTOs, ServerSync, OfflineQueue, OnlineText, Json
* `Scripts/Logic/CatalogExport.cs`
* `Scripts/Runtime/Online/OnlineConfig.cs`, `SupabaseClient.cs`, `OnlineService.cs`
* `Scripts/Runtime/UI/UIManager.Online.cs`: ACCOUNT, RANKINGS, DAILY screens, menu chip, game-over rank line
* `Tests/EditMode/OnlineTests.cs`, `OnlineContractTests.cs` (.NET only)
* `docs/SUPABASE_SETUP.md`, this report

**Modified**: `SaveData` (onlineLinked, signedOut, lastUserId), `Missions` (pools public, achievement stat keys),
`SaveManager` (PH time), `Services`, `Bootstrap` (connect after splash), `GameManager` (PlayDaily), `GameplaySession`
(daily seed/route, run upload), `UIManager` (menu row DAILY · RANKINGS · ACCOUNT, game-over line),
`UIManager.Shop/Missions` (server purchases and claims), `ProjectSetup` (creates OnlineConfig.asset).

## Tests
* **.NET unit tests: 104/104 pass.** New ones cover the daily challenge, PH day keys, run bounds (honest runs pass
  untouched, impossible ones rejected, inflated ones clamped), snapshot merge, offline queue, names and codes, catalog
  freshness, achievement stat keys, and **JSON contract** tests (the real server responses contain every field the
  client reads, every RPC and argument the client uses exists, the server reads every report field).
* **Supabase tests (`supabase/dev-tests/run.sh`, PostgreSQL 16): all pass.**
  * **vectors:** 54 PRNG values, 2,510 mission rows, 730 daily challenges, 267 dates, 10 levels and 400 run-bound
    cases, all identical to C#.
  * **behaviour:** sign-up trigger, RLS and grants, anon denied, accept / duplicate / reject / clamp runs,
    purchases, upgrade effect on coin bonus, missions, achievements, daily challenge seed check, friends,
    leaderboards (global / friends / weekly / daily, no wallet data leaked), names, capped one-time import, play-time
    cap, and wallet = ledger.
* **Compile check** against the Unity API: 0 errors.

## Not verified / limitations (honest list)
* **Not run against a live Supabase project or in Unity.** The SQL ran on stock PostgreSQL 16 with a small
  `auth` shim, and the HTTP client follows Supabase's documented REST endpoints, but the first real sign-in should
  be watched (check the Console for `[Online]` messages).
* Email confirmation: with *Confirm email* on, a new email account must be confirmed before signing in elsewhere.
  The app tells the player.
* No password-reset screen yet (players can use Supabase's email flow later).
* Anti-cheat is plausibility-based: a modified client can still submit *plausible* fake runs. Full replay
  verification would need the server to re-simulate runs from inputs (possible later because runs are seeded).
* Friend adding is instant and mutual (no request/accept step).
* CAPTCHA for guest sign-ups is not wired in.

## Next milestone
Phase 4 (monetisation): AdMob (banner on menus, capped interstitials between selected runs, rewarded revive /
double coins), IAP (coin bundles, premium skins, ad-free) with **server-side receipt validation** via a Supabase
Edge Function, which builds on this phase's ledger.

## Update – live jeepney engine sound
The engine is no longer a pitched loop. It's **synthesised live** (`DieselEngineSynth`, played through `EngineAudio.OnAudioFilterRead`) and modelled on the surplus 4-cylinder diesel that powers most Manila jeepneys (Isuzu 4BC2 / 4BA1 class):
* one combustion pulse per firing (4 per 2 revolutions, so 25 Hz at the 750 rpm idle), with one weak and one strong cylinder and timing jitter. That gives the lumpy, uneven idle of a tired engine.
* diesel clatter (injector knock), strongest at idle
* exhaust pulses through an open-pipe resonance, with throttle-dependent rasp
* valve-train ticks and loose body-panel rattle excited by the pulses
* a **5-speed manual gearbox** (`JeepneyDrivetrain`): revs climb on GAS, and at every upshift there's a short pause and a rev drop while the clutch is in. That's the familiar "rrRRAA… rrRRAA" of a jeepney pulling away. When standing still, GAS free-revs the engine (a quick blip); BRAKE and stops settle back to idle.

Tests check the firing frequency at 750 and 1,500 rpm, louder sound on GAS, no clipping or DC offset, upshifts with audible rev drops, and idle / free-rev behaviour.

**Use a real recording instead (optional):** record a real jeepney idling steadily (about 800 rpm, 3–10 s, seamless loop, no talking) and save it as `Assets/JeepneyRush/Resources/Audio/jeepney_engine.wav` (or `.ogg`). The game then pitches that recording with the revs instead of using the synthesiser. You can also add `Resources/Audio/jeepney_rev.wav`, which plays when you press GAS. Only use recordings you made yourself or have a licence for.

## Update – new route: Mountain Province, and easier controls
**Controls**: the bottom row is now **BRAKE** (left), **BUSINA** (centre, reachable with either thumb) and **GAS** (right). The **D/R gear** sits where the horn used to be, above GAS. The pedals are bigger (245 px), with stronger colours and thicker outlines, and the gear letters are larger.

**Mountain Province** (unlocks at 4,000 m in one run). It's the 4th route and plays differently:
* **Unpaved road**: a muddy dirt surface with wandering wheel ruts and puddles, no lane paint, a dirt shoulder and rocky edges.
* **Rugged**: the road climbs and winds more than any other route, still at most a 12% grade (tested). Small jolts keep shaking the jeepney, and loose ground makes steering a little slippery all the time.
* **Mud patches** ("PUTIK!") are soft obstacles: they slow you down, throw brown mud and get complaints from passengers. **Fallen rocks** from small landslides are solid obstacles. There are no painted speed humps on a dirt road.
* **Scenery**: cut rock walls with moss and boulders, stone rice terraces, Benguet pine forests, fence posts, highland huts, vegetable stands, misty peaks, and sometimes a **waterfall**. Cooler, mistier light and fog.
* **Less traffic**: about 35% of the usual vehicle patterns, mostly vegetable trucks and motorcycles (tested to be under 60% of Manila).
* **Longer distances**: PARA stops are 1.75× farther apart, gas stations 1.12× (a full tank still reaches the next one), and traffic lights are rare (3.2× spacing).
* Stop names: Bontoc, Sagada, Besao, Sabangan, Tadian, Bauko, Sadanga, Barlig, Natonin, Echo Valley, Rice Terraces and Viewdeck. There's also its own slower, lower music variant and a new route picture.
* Daily challenges can now land on any of the 4 routes. The server catalog was updated with a **new** migration (`*_jr_catalog.sql`); Supabase applies it on the next deploy.

Tests: 111 .NET tests pass. The fairness bot drives Mountain Province without crashing and finds mud and rocks; mountain traffic is checked to be much lower; the daily challenge covers all 4 routes. Supabase tests pass, with vectors regenerated for 4 routes.
