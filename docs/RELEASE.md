# Releasing Jeepney Rush (Unity) on Google Play

> This guide is for the **Unity** game in `unity/JeepneyRush`. (`ANDROID_BUILD.md` covers the
> older web/Capacitor version.)

The release build has a **preflight check** that stops the build when something would get the
upload rejected, ship Google test ads, or leak a secret key. Run it any time from
**Jeepney Rush ▸ Release ▸ Run Preflight Check**.

---

## 1. One-time setup

### 1.1 Upload key (keystore)
Unity: **Edit ▸ Project Settings ▸ Player ▸ Android ▸ Publishing Settings ▸ Keystore Manager ▸
Create New**. Save it **outside the project folder** and back it up (two places). If you lose
it you can still ask Google to reset the upload key, but it takes days.

Alternatively, for command-line builds set these environment variables (never commit them):
`JR_KEYSTORE`, `JR_KEYSTORE_PASS`, `JR_KEY_ALIAS`, `JR_KEY_PASS`.

### 1.2 Application id
Player ▸ Other Settings ▸ **Package Name** (default `ph.jeepneyrush.game`). Choose it now: it
can **never change** after the first upload. Use the same value in:
* `Resources/MonetizationConfig` ▸ **Package Name**
* the Supabase Edge Function secret **`PLAY_PACKAGE_NAME`**

### 1.3 Target API level
Player ▸ Other Settings ▸ **Target API Level**. Google Play currently requires **API 35**
(and **36** from its next yearly deadline – check Play Console ▸ *Policy status*). Install the
platform in **Unity Hub ▸ Installs ▸ ⚙ ▸ Add modules ▸ Android SDK & NDK**, or let Unity
update the SDK when it asks.

### 1.4 Real ad and purchase ids
`Resources/MonetizationConfig`:
* **Use Test Ads** ▸ **off**
* AdMob **App ID** and the **Banner / Interstitial / Rewarded** unit ids from your AdMob account
* Play Console products created with the same ids as in the game (see `MONETIZATION_SETUP.md`)

Keep **Use Test Ads on** while *you* test on your phone: tapping your own real ads can get
the AdMob account suspended. Add your phone as a **test device** in AdMob before testing real
ad units.

### 1.5 Privacy policy and account-deletion page
Google Play requires both because the game has ads, purchases and accounts.
1. Publish two web pages (Google Sites, GitHub Pages or Notion all work) using
   `docs/PRIVACY_POLICY_TEMPLATE.md` and `docs/ACCOUNT_DELETION_PAGE.md` (optionally also
   `docs/TERMS_OF_SERVICE_TEMPLATE.md`). Fill in your name,
   e-mail and country.
2. Run **Jeepney Rush ▸ Setup ▸ Config Assets** once (creates `Resources/ReleaseConfig`).
3. In `Resources/ReleaseConfig` paste the **Privacy Policy Url** and **Account Deletion Url**.
   Both appear in the game: Settings ▸ *Privacy policy* and Account ▸ *Privacy*.
4. Apply the Supabase migration `20261122000000_jr_account_deletion.sql` (SQL editor, or
   `supabase db push`). It powers **Account ▸ DELETE ACCOUNT**.

---

## 2. Every release

1. **Bump the version**: Jeepney Rush ▸ Release ▸ **Bump Version** (1.0.3 → 1.0.4, version code + 1).
2. **Preflight**: Jeepney Rush ▸ Release ▸ **Run Preflight Check**. Fix every *ERROR*.
3. **Remove obsolete files** if the preflight lists any: Jeepney Rush ▸ Release ▸ **Remove
   Obsolete Files** (copying a new version over the project never deletes old files).
4. **Build**: Jeepney Rush ▸ Build ▸ **Android AAB (release, Google Play)**. This also applies
   the release player settings:
   * IL2CPP, ARM64 + ARMv7 (Play sends each phone only its own code)
   * Medium code stripping (`Assets/JeepneyRush/link.xml` keeps what reflection needs)
   * No stack traces for normal logs; only warnings and errors are logged in release
   * A **symbols .zip** next to the `.aab` so crash reports in Play Console are readable
   The `.aab` is written to `unity/JeepneyRush/Builds/`.
5. **Upload** to Play Console ▸ *Test and release* ▸ **Internal testing** first. Upload the
   symbols zip under *App bundle explorer ▸ Downloads ▸ Native debug symbols*.
6. After the upload: Jeepney Rush ▸ Release ▸ **Mark Current Version As Uploaded**, so the next
   preflight insists on a higher version code.

Command line (CI or a build machine):
```
Unity -batchmode -quit -projectPath unity/JeepneyRush -executeMethod JeepneyRush.EditorTools.BuildTools.BuildAab
```
Exit code 2 means the preflight stopped the build (the log lists why).

---

## 3. Testing tracks
| Track | Who | Notes |
|---|---|---|
| Internal testing | up to 100 testers you invite by e-mail | Available within minutes; use it for every build. Test purchases with *licence testers* (Play Console ▸ Settings ▸ License testing). |
| Closed testing | **New personal developer accounts must run a closed test with at least 12 testers for 14 days in a row** before production access | Ask friends/family to keep the app installed and open it now and then. |
| Production | everyone | Start with a staged rollout (e.g. 20 %) and watch *Android vitals*. |

---

## 4. Play Console forms (App content)

These answers match what the code does today. Re-check them whenever you add an SDK.

**Data safety**
| Data | Collected | Why | Notes |
|---|---|---|---|
| E-mail address | Yes (only if the player adds one) | Account management | Supabase Auth |
| User ids (account id) | Yes | App functionality, account management | Supabase |
| App activity: in-game scores, progress, friends | Yes | App functionality | Leaderboards, cloud save |
| Purchase history | Yes | App functionality | Server-verified Google Play purchases |
| Device or other ids (advertising id) | Yes | Advertising | Google Mobile Ads SDK |
| Crash logs / diagnostics | Yes, if Unity Cloud Diagnostics or Play vitals | Analytics | Play vitals is automatic |
* Data is **encrypted in transit** (HTTPS) – *Yes*.
* Players **can request deletion** – *Yes*: in the app (Account ▸ DELETE ACCOUNT) and via the
  account-deletion web page.
* Data is **shared** with third parties: advertising id with Google (AdMob).

**Ads**: *Yes, the app contains ads.*  
**Advertising ID**: *Yes* – used for advertising (the AdMob SDK adds the `AD_ID` permission).  
**Target audience**: choose **13+** unless you want to go through the *Families* policy
(child-directed ad settings, certified ad SDKs). The game itself is suitable for everyone.  
**Content rating**: fill in the IARC questionnaire – no violence/gambling; mention *in-app
purchases* and *users interact* (friends list, display names on leaderboards).  
**Government / news / health apps**: No.

**Consent in Europe / UK**: if you publish there, enable Google's consent message (UMP) in
AdMob ▸ *Privacy & messaging* before release. The Philippines doesn't require it.

---

## 5. Store listing
* **App name**: Jeepney Rush (30 characters max)
* **Short description** (80): e.g. *Drive a Filipino jeepney, pick up passengers and beat the traffic!*
* **Full description** (4000): routes, jeepneys, barkers and vendors, leaderboards
* **Icon** 512×512 PNG, **feature graphic** 1024×500 – see `store/`
* **Screenshots**: at least 2 phone screenshots (portrait, 1080×1920 or larger). Take them in a
  Development Build with *Show FPS* off.
* **Category**: Game ▸ Racing (or Arcade); **Tags**: Driving, Casual, Offline

---

## 6. Performance features in this build
* **Graphics: Auto** (new default): starts Low/Medium/High from the phone's memory and CPU
  cores, then steps down if the game stays under ~48 fps for 3 seconds (shadows off, lower
  render scale). It climbs back at most once per session, so it never flickers between
  levels. Players can still pick Low / Medium / High in Settings.
* Menus run at 30 fps (saves battery and heat); driving runs at 60 fps.
* Textures compress to ASTC on Android automatically (`TextureRules.cs`): 4×4 for text and
  UI, 6×6 for the rest, max 1024 px.
* `MeshBudgetTests` keeps every model, scenery chunk and intersection within vertex and
  material budgets.
* GitHub Actions (`.github/workflows/unity-checks.yml`) runs the compile checks and all
  .NET, SQL and Edge Function tests on every push.

**Profile on a real phone before release**: Development Build ▸ *Autoconnect Profiler*, then
drive EDSA at night in the rain (the heaviest scene). Aim for a CPU main thread under
**12 ms** on a mid-range phone.

---

## 7. Taking real store screenshots

1. **Settings ▸ Screenshot button: ON** – a camera button appears next to pause during a run.
2. **Settings ▸ Store frame 9:16: ON** – the game letterboxes itself to 9:16 (black bars on
   tall phones). Google Play rejects screenshots longer than 2:1, and most phones today are
   19.5:9 or taller, so keep this on while you take store shots.
3. Drive, and tap the **camera button** for a shot with the HUD. For a clean shot without the
   HUD, **pause** and tap **PHOTO WITHOUT HUD** (the scene is frozen while paused).
4. Find them in the **Gallery ▸ Pictures/Jeepney Rush** (Android 10+), or in
   `Android/data/ph.jeepneyrush.game/files/Screenshots` on Android 8–9 (copy over USB).
   In the Unity Editor they go to `unity/JeepneyRush/Screenshots/`, on PC to Pictures/Jeepney Rush.
5. Turn both options off again before playing normally.

Shots are saved as 24-bit PNG at the phone's resolution (1080 px wide or more = eligible for
Play's promotional placements). For tablet shots, take them on a tablet (or the Unity Editor's
Game view set to a tablet resolution).

## 8. PC / keyboard controls (Editor, Chromebooks, Play Games on PC)

| Action | Keys |
|---|---|
| Accelerate (gas) | hold **↑** (or W / Shift) |
| Brake | hold **Space** (or B) |
| Horn (busina) | **Ctrl** (or H) |
| Change lane | **← →** (or A / D) |
| Gear D / R | **R** |
| Pause | **P** / Esc |
| Screenshot with HUD / without HUD | **F12** / **F11** |
