# Supabase setup (Phase 3 online features)

The game is **offline-first**. Without these steps it plays exactly as before, and the menu shows
"OFFLINE MODE". Once you complete them you get guest and email accounts, cloud saves,
rankings (all-time, weekly, daily challenge, friends) and friend codes.

About 10 minutes, free tier is enough.

## 1. Create the project
1. Go to <https://supabase.com>, sign in, and click **New project**.
2. Pick a name (e.g. `jeepney-rush`), a strong database password, and region **Southeast Asia (Singapore)**, the closest to the Philippines.
3. Wait until the project is ready.

## 2. Turn on the sign-in methods
**Authentication ▸ Sign In / Providers**:
* **Allow anonymous sign-ins: ON.** Every new player gets a guest account automatically.
* **Email: enabled.** Players can attach an email to keep their progress across phones.
* *Confirm email*: keep it ON for release. For quick testing you can turn it off. With it on, a player who
  creates an email account must click the link in the email before signing in on another phone.
  Linking a guest to an email keeps working on the current phone right away.

## 3. Create the database objects
Option A, Dashboard (no tools needed):
1. **SQL Editor ▸ New query.**
2. Paste the whole file `supabase/migrations/20261121000000_jr_online.sql` and click **Run**.
3. New query: paste `supabase/migrations/20261121000100_jr_catalog.sql` and click **Run**.

Option B, Supabase CLI:
```bash
supabase link --project-ref <your-project-ref>
supabase db push
```

Both files can be run again safely (e.g. after an update).

## 4. Connect the game
1. In Supabase: **Project Settings ▸ API**. Copy the **Project URL** and the **anon public** key.
2. In Unity: run **Jeepney Rush ▸ Setup Project** once (it creates `Assets/JeepneyRush/Resources/OnlineConfig.asset`).
3. Select **OnlineConfig** and paste:
   * **Supabase Url**: `https://<ref>.supabase.co`
   * **Anon Key**: the *anon public* key
4. Press Play. After the splash, the menu chip shows **"ONLINE · Driver-XXXX (guest)"**.

> **Never** paste the `service_role` key into the game. It bypasses all security. The game
> only needs the anon key, and the database protects everything else.

## 5. Check it works
* Play a run. The Game Over card shows **"RANK: #1 today · #1 this week · #1 all-time"**.
* **RANKINGS** lists your score. **ACCOUNT** shows your name, friend code and guest status.
* In the Supabase **Table Editor** you can see `jr_profiles`, `jr_runs` and `jr_scores` filling up.

## How the security works
* All tables have **Row Level Security**. A player can only *read* their own rows. There are no insert, update or
  delete permissions for clients at all.
* Every change goes through `jr_*` database functions that check the request on the server:
  * **Runs** (`jr_submit_run`): impossible runs are rejected (faster than 50 m/s on average, too short,
    negative values, parts larger than the whole). Inflated coins, score, passengers and power-ups are clamped to what the
    route can actually give. Each run id is only counted once, so the offline queue can safely retry. Nobody
    can log more than 20 hours of play in 24 hours.
  * **Purchases**: prices come from the server catalog, coins are checked, and ownership is unique.
  * **Missions**: the server tracks progress from validated runs and pays each reward once.
  * **Achievements**: checked against server-side stats.
  * **Leaderboards**: only validated runs are ranked. Daily-challenge scores only count with today's seed and route.
* Every coin movement is in `jr_transactions`. The tests check that each wallet equals its ledger.
* Days and weeks use **Philippine time (UTC+8)** on both the phone and the server.

## Keeping the catalog in sync
Prices, upgrade costs, missions and achievements are defined in C#. The SQL catalog is **generated**:
```bash
dotnet run --project unity/tools/ExportCatalog     # rewrites supabase/migrations/*_jr_catalog.sql + test vectors
```
Then run the catalog file in the SQL editor again. A unit test fails if you forget to regenerate it.

## Testing the backend locally (developers)
```bash
supabase/tests/run.sh
```
This starts a throw-away PostgreSQL 15/16 server, applies the migrations twice (they're idempotent), and runs:
* `vectors.sql`: the SQL rules give **bit-identical** results to the C# game (random numbers, 2,510 mission rows,
  730 daily challenges, day/week keys, levels, 400 run-validation cases).
* `behaviour.sql`: security (RLS, no direct writes, private functions hidden, anon denied), runs, purchases,
  missions, achievements, daily challenge, friends, leaderboards, names, the one-time capped import of old offline
  progress, the play-time cap and the ledger.

## Optional hardening for release
* **Authentication ▸ Rate Limits**: keep the default limit on anonymous sign-ups per IP.
* **Authentication ▸ Attack Protection**: enable CAPTCHA if you see guest-account spam. The client would then need
  a captcha token, which is not implemented yet.
* Back up the database (Pro plan) before big updates.
