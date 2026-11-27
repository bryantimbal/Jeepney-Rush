# Jeepney Rush (Unity): Phase 4 report, monetization

Scope from the master prompt: **AdMob** (banners on non-gameplay screens, interstitials between selected sessions,
optional rewarded ads: revive, double coins, bonus daily reward, temporary cosmetic trial), **in-app purchases**
(coin bundles, gem bundles, premium jeepney skins, cosmetic packs, ad-free), **never interrupt gameplay**, **test
ad IDs during development**, and **purchases validated securely**.

## What was built
| Feature | Details |
|---|---|
| **Ad rules** (`AdPolicy`, tested) | **Banner** only on the main menu and Game Over, never while driving, gone after "No Ads". **Interstitial** only when *leaving* Game Over (RESTART / HOME): never in a new player's first 3 runs, at most every 3 runs, at least 3 minutes apart, not after runs under 25 s, never with "No Ads". **Rewarded** ads only when the player taps for them. |
| **Revive** | When the last heart is lost (from 150 m), the game freezes and offers "BUHAYIN? Watch ad" with a 5-second countdown. If you watch it: +1 heart, 3 s invulnerability, and the road just ahead is cleared. Once per run. No ad ever plays while you're driving. |
| **Double coins** | Game Over: "x2 COINS (+N) · WATCH AD". The server pays the run's credited coins again (max 5,000), once per run, only for your own run from the last 2 hours. |
| **Daily bonus** | Missions ▸ Daily: "Watch a short ad for +150 coins", once per Philippine day (also enforced by the server). |
| **Cosmetic trial** | Garage ▸ a locked jeepney ▸ "TRY 1 RUN · AD": drive it for the next run only (3 trials per day). |
| **SHOP** (menu) | Coins, gems, jeepney packs, No Ads; localized Play prices; RESTORE PURCHASES (re-syncs the account). A TEST STORE notice appears in development builds. |
| **Gems** | Premium currency. The gem counter on the menu opens the shop. Fiesta, Neon, Flag and Gold can also be bought with gems (250 / 320 / 350 / 500). |
| **Store adapter** | Unity IAP 5.x (StoreController API) / Google Play Billing (`UnityIapProvider`, `JR_UNITY_IAP`). Purchases stay *pending* until the server granted them. (Moved from IAP 4.x, which Unity deprecated; IAP 5 attaches no obfuscated account id – the server still refuses a purchase tagged with another player's id.) |
| **Ads adapter** | Google Mobile Ads 9.x (`AdMobProvider`, `JR_ADMOB`), with test unit ids by default. Rewarded ads carry server-side-verification data. |
| **Development** | Without the SDKs (or in the Editor): **TEST AD** full-screen screens (rewarded ones must play 5 s to pay out), a **TEST BANNER** strip, and a **TEST PURCHASE** dialog. They are disabled in release builds. |

## Server (Supabase)
* **Migration `*_jr_monetization.sql`:**
  * Tables and columns: gems and ad-free on profiles, a product catalog (generated from C#), gem prices, a `jr_purchases` ledger, `jr_ad_rewards`, and developer switches.
  * `jr_srv_grant_purchase` / `jr_srv_revoke_purchase` / `jr_srv_record_ad_reward` can only be called with the **service role**, i.e. by the Edge Functions.
  * Players can call `jr_buy_jeepney_gems` and `jr_claim_ad_reward`.
* **Edge Function `verify-purchase`:** Google Android Publisher API v3. It uses a service-account JWT (RS256), checks the purchase state, product and account, acknowledges, and grants. Each token is idempotent.
* **Edge Function `admob-ssv`:** verifies Google's ECDSA P-256 signature (keys from Google's key server, cached) and records the reward under the game's signed claim id.
* Test purchases and test ads are **refused by default** (`jr_server_settings`).
* The ledger now records the currency: coins and gems each must equal the sum of their transactions (tested).

## Tests
* **.NET: 118/118** (new: ad pacing, banner rule, rewarded rules, product catalog consistency, gem purchases, local test grants, snapshot fields, catalog freshness incl. products).
* **Postgres (`run.sh`):** Phase 3 suites plus `monetization.sql`. Players can't grant themselves purchases or rewards, settings are hidden, verified purchases are idempotent per token, a token can't move to another player, packs and No Ads work, test purchases are refused by default, gems buy premium jeepneys, ad rewards need verification and pay once, one daily bonus per day, double coins once per run, refunds work, and both ledgers balance.
* **Deno (`run-functions.sh`):** type-check of both functions and 7 tests. SSV signatures are checked with freshly generated ECDSA keys (valid / tampered / wrong key / unknown key id), DER→raw conversion, custom data, and the handler records only valid callbacks. Play verdicts cover purchased / pending / cancelled / wrong product / wrong account / license tester. The service-account JWT is verified with a generated RSA key. The full verify-purchase flow (fake Google: OAuth → get → acknowledge → grant) is tested.
* **Compile checks:** game code against the Unity API (0 errors). The AdMob and Unity IAP adapters were compiled with `JR_ADMOB;JR_UNITY_IAP` against API stand-ins (0 errors).

## Not verified / limitations (honest list)
* **Not run on a device or in Unity, and no real ads or purchases were made.** The real SDK packages and Google's APIs can't be reached from this environment. The adapters follow the documented APIs of Google Mobile Ads 9.x and Unity IAP 4.x, but were compiled against hand-written stand-ins. If a newer SDK renamed something, Unity's compiler will point to the exact line.
* **Consent (GDPR/UMP)** for EEA/UK players is not implemented. That matters if you publish outside the Philippines.
* **Refunds** are not automatic yet. `jr_srv_revoke_purchase` exists; wiring Google's Real-time Developer Notifications or the Voided Purchases API to it is a later step.
* iOS (App Store) is not covered.
* Restoring purchases on a new phone works through the **account**, so players should save their progress with an email. A guest account on a new phone can't claim another account's purchase token.

## Next milestone
Phase 5 (Google Play release): optimisation pass, store listing, privacy policy (ads, IAP, accounts), Data safety form,
internal → closed → production tracks.
