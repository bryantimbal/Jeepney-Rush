# Monetization setup (Phase 4): AdMob + Google Play billing

Out of the box the project **compiles and runs without any ad or store SDK**. In the Editor and in development
builds you get clearly labelled **TEST AD** and **TEST PURCHASE** screens. Release builds show no ads and no
store until the steps below are done.

## 1. Unity packages
1. **Google Mobile Ads Unity plugin 9.x**: download the `.unitypackage` from
   <https://github.com/googleads/googleads-mobile-unity/releases> and import it.
   Then **Assets ▸ Google Mobile Ads ▸ Settings**: set the **Android App ID**. Use the test ID
   `ca-app-pub-3940256099942544~3347511713` until your app is approved.
   Run **Assets ▸ External Dependency Manager ▸ Android Resolver ▸ Resolve**.
2. **Unity In-App Purchasing 5.x** (5.0 or newer; 4.x is deprecated and no longer works with this code): **Window ▸ Package Manager ▸ Unity Registry ▸ In-App Purchasing ▸ Install**. Do not use its *Configure* / Dashboard setup: the game talks to Google Play directly and the server verifies every purchase.
3. **Edit ▸ Project Settings ▸ Player ▸ Android ▸ Other Settings ▸ Scripting Define Symbols**: add
   `JR_ADMOB;JR_UNITY_IAP`. This switches on `AdMobProvider.cs` and `UnityIapProvider.cs`.
4. Run **Jeepney Rush ▸ Setup Project** once. It creates `Resources/MonetizationConfig.asset`.

## 2. MonetizationConfig (Resources/MonetizationConfig)
| Field | Development | Release |
|---|---|---|
| Use Test Ads | ✔ (Google's test units; safe to click) | untick |
| Banner / Interstitial / Rewarded Unit Id | leave as is | your AdMob unit ids |
| Package Name | your Play package name | same (must match Player Settings ▸ Package Name) |
| Simulate In Development | ✔ | ignored in release builds |

> Never click your own **real** ads. Use the test ids or add your phone as a test device in AdMob.

## 3. Google Play Console
1. Create the app with the same package name, and upload a build to **Internal testing**. Billing only works for
   builds installed from Play.
2. **Monetize ▸ Products ▸ In-app products**: create and **activate** these products with exactly these ids:

| Product id | Type | Contents | Suggested price |
|---|---|---|---|
| `jr.coins.small` | consumable | 5,000 coins | ₱49 |
| `jr.coins.medium` | consumable | 15,000 coins | ₱129 |
| `jr.coins.large` | consumable | 40,000 coins | ₱299 |
| `jr.gems.small` | consumable | 100 gems | ₱49 |
| `jr.gems.medium` | consumable | 550 gems | ₱249 |
| `jr.gems.large` | consumable | 1,200 gems | ₱499 |
| `jr.pack.fiesta` | non-consumable | Fiesta + Flag jeepneys + 5,000 coins | ₱199 |
| `jr.pack.premium` | non-consumable | Gold VIP + Neon jeepneys | ₱249 |
| `jr.adfree` | non-consumable | No banner / between-run ads | ₱149 |

   (Unity IAP sets the type when the game starts. On Play, all of these are "managed products".)
3. **Setup ▸ License testing**: add your Google account, so test purchases are free.

## 4. Server-side purchase verification
1. **Play Console ▸ Setup ▸ API access**: link a Google Cloud project and **create a service account**.
2. In **Users and permissions**, invite the service account's email and give it, for this app, the
   **"View financial data"** and **"Manage orders and subscriptions"** permissions.
3. In Google Cloud, create a **JSON key** for the service account.
4. In Supabase: **Edge Functions ▸ Secrets** (or `supabase secrets set …`):
   * `GOOGLE_SERVICE_ACCOUNT_JSON` = the whole JSON key file
   * `PLAY_PACKAGE_NAME` = your package name
5. Deploy the functions. The GitHub integration deploys `supabase/functions/*`, or with the CLI:
   ```bash
   supabase functions deploy verify-purchase
   supabase functions deploy admob-ssv --no-verify-jwt
   ```

How it works: the store hands the game a purchase token. The game sends it to `verify-purchase`, which asks
Google whether the token is really paid, belongs to this player's account and is the right product. If so, it
acknowledges the purchase (otherwise Google refunds it after 3 days) and calls `jr_srv_grant_purchase`.
Each token can only be granted once. Only after the server says yes does the game finish the transaction on
the phone, so nothing is lost if the app closes halfway.

## 5. AdMob
1. Create the app and three ad units: **Banner**, **Interstitial** and **Rewarded**.
2. Rewarded unit ▸ **Server-side verification**: callback URL
   `https://<your-ref>.supabase.co/functions/v1/admob-ssv`, then press **Verify URL**. It should return OK.
3. Paste the unit ids into MonetizationConfig and untick **Use Test Ads** for the release build.
4. Publish `app-ads.txt` on your developer website, and fill in the **Data safety** form (advertising ID).
5. If you distribute outside the Philippines (EEA/UK), set up a consent message under **AdMob ▸ Privacy & messaging**.
   The in-game consent flow (Google UMP) is **not implemented yet**. See the report.

Rewarded ads: the game puts `kind:ref:claimId` into the ad's custom data. Google signs it and calls `admob-ssv`.
The function checks Google's ECDSA signature and records the reward. The game then claims it once with
`jr_claim_ad_reward`. A modified game can't invent rewards.

## 6. Testing against the server with the simulated store/ads
Simulated purchases (`TEST-…` tokens) and simulated rewarded ads (`test-…` claims) are **refused by the database**
unless you switch them on. Only do this on a development project:
```sql
update jr_server_settings set value = 'true' where key in ('allow_test_purchases', 'allow_test_rewards');
-- before release:
update jr_server_settings set value = 'false';
```

## 7. Local checks (developers)
```bash
supabase/dev-tests/run.sh             # Postgres: purchases, gems, ad rewards, security, ledger
supabase/dev-tests/run-functions.sh   # Deno: Play verification + AdMob SSV signature tests
dotnet build unity/tools/CompileCheckAdapters   # adapters against API stand-ins
```
