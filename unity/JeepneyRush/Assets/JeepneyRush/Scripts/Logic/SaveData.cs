using System;
using System.Collections.Generic;
using System.Text;

namespace JeepneyRush.Logic
{
    /// <summary>Versioned save model (serialised with Unity's JsonUtility: public fields only).</summary>
    [Serializable]
    public class SaveData
    {
        public const int CurrentVersion = 2;

        public int version = CurrentVersion;
        public int coins;
        public int bestScore;
        public float bestDistance;
        public string selectedJeepney = JeepneyCatalog.DefaultId;

        // settings
        public bool music = true;
        public bool sfx = true;
        public bool vibration = true;
        /// <summary>Patok jeepneys play their loud "sound trip" while driving (instead of the route music).</summary>
        public bool soundTrip = true;
        /// <summary>Camera: false = behind the jeepney (LIKOD), true = from the driver's seat.</summary>
        public bool driverView;
        /// <summary>Steering sensitivity 0.5 .. 1.5 (tilt angle for full lock). Field name kept for old saves.</summary>
        public float swipeSensitivity = 1f;
        /// <summary>How the jeepney is steered: 0 = hold the left/right side of the screen, 1 = tilt the phone.</summary>
        public int steerMode;
        /// <summary>Lane-keeping assist: with hands off, the jeepney settles into the nearest lane.</summary>
        public bool laneAssist = true;
        // ---- steering & controls (Settings ▸ Controls)
        /// <summary>Most steering the input can ask for (0.7 .. 1).</summary>
        public float maxSteer = 1f;
        /// <summary>How fast the wheel straightens when released (0.5 slow .. 2 fast).</summary>
        public float steerReturn = 1f;
        /// <summary>Tilt: calibrated "straight ahead" (gravity x in g) and whether it was set.</summary>
        public float tiltCentre;
        public bool tiltCalibrated;
        /// <summary>Tilt dead zone (g) and sensor smoothing (filter speed 1/s).</summary>
        public float tiltDeadZone = 0.035f;
        public float tiltSmoothing = 14f;
        /// <summary>Virtual wheel: 0 small, 1 medium, 2 large; opacity; largest rotation (degrees).</summary>
        public int wheelSize = 1;
        public float wheelOpacity = 0.9f;
        public float wheelMaxAngle = 135f;
        /// <summary>HUD buttons: size multiplier and opacity; mirrored layout for left-handed players.</summary>
        public float buttonScale = 1f;
        public float buttonOpacity = 1f;
        public bool leftHanded;
        /// <summary>Settings ▸ Screen: 0 portrait, 1 landscape, 2 auto-rotate (see OrientationMode).</summary>
        public int orientation;
        /// <summary>Tilt neutral as a roll angle (degrees), per orientation – replaces the old g-based values.</summary>
        public float tiltRoll, tiltRollLand;
        public bool tiltRollCal, tiltRollLandCal;
        /// <summary>Detected once: does this phone's input backend already rotate the sensor to the screen?</summary>
        public bool tiltAxesKnown, tiltAxesRotated;
        /// <summary>(old, g-based – no longer used)</summary>
        public float tiltCentreLand;
        public bool tiltCalibratedLand;
        /// <summary>The steering tutorial has been shown for these modes (bit per SteerMode).</summary>
        public int steerTutorials;
        public int graphicsQuality = 3; // 0 low, 1 medium, 2 high, 3 auto (adapts to the device)
        public bool tutorialDone;
        /// <summary>Shows a camera button on the HUD for taking screenshots (F12 / F11 on PC always work).</summary>
        public bool screenshotButton;
        /// <summary>Camera punch / tilt on near misses and combos (motion comfort: can be switched off).</summary>
        public bool cameraEffects = true;
        /// <summary>Letterboxes the game to 9:16 so screenshots are accepted by Google Play as they are.</summary>
        public bool storeFrame;

        // lifetime stats
        public int runs;
        public float totalDistance;
        public int totalCoins;
        public int totalPassengers;
        public int crashes;

        // ---- Phase 2 (save version 2)
        public List<string> ownedJeepneys = new List<string>();
        public int[] upgradeLevels = new int[6];
        public int selectedEnvironment;
        public int xp;
        public int level = 1;
        public int totalDeliveries;
        public int totalPowerUps;
        /// <summary>Legacy (jumping was removed); kept so old saves still load.</summary>
        public int totalJumps;
        public int totalSmoothHumps;
        public float totalWetDistance;
        public float bestCleanDistance;
        public List<string> achievementsUnlocked = new List<string>();
        public List<string> achievementsClaimed = new List<string>();
        public int dailyKey;
        public int[] dailyProgress = new int[Missions.DailyCount];
        public bool[] dailyClaimed = new bool[Missions.DailyCount];
        public int weeklyKey;
        public int[] weeklyProgress = new int[Missions.WeeklyCount];
        public bool[] weeklyClaimed = new bool[Missions.WeeklyCount];
        public bool controlsTutorialDone;

        // ---- Phase 3 (online)
        /// <summary>This device's progress has been uploaded to / replaced by an online account.</summary>
        public bool onlineLinked;
        /// <summary>The player signed out on purpose: don't auto-create a guest account.</summary>
        public bool signedOut;
        public string lastUserId;

        // ---- Phase 4 (monetization)
        /// <summary>Premium currency (from purchases; server-authoritative when online).</summary>
        public int gems;
        /// <summary>Road gems picked up today (PH day key) – the daily cap is shared with the server.</summary>
        public int roadGemsDay, roadGemsToday;
        /// <summary>"No Ads" purchased.</summary>
        public bool adFree;
        public List<string> ownedProducts = new List<string>();
        public AdState ads = new AdState();
        // ---- Phase B (career)
        /// <summary>Stars per career shift (Career.Shifts order; bits 1 finish, 2 boundary, 4 goal).</summary>
        public int[] careerMasks = new int[0];
        /// <summary>Highest driver's license already rewarded (index into Career.Licenses).</summary>
        public int careerRank;
        // ---- Phase B (customization)
        /// <summary>Cosmetic items bought in the talyer (Customization.All ids).</summary>
        public List<string> ownedCosmetics = new List<string>();
        /// <summary>Setup per jeepney (paint, stickers, sign, horn, mudflaps, ornament, lights).</summary>
        public List<JeepneyLook> looks = new List<JeepneyLook>();

        public bool Owns(string jeepneyId) => ownedJeepneys != null && ownedJeepneys.Contains(jeepneyId);
        public int UpgradeLevel(UpgradeKind k) => upgradeLevels != null && (int)k < upgradeLevels.Length ? upgradeLevels[(int)k] : 0;
    }

    /// <summary>On-disk wrapper: the body plus an integrity checksum.</summary>
    [Serializable]
    public class SaveEnvelope
    {
        public int version;
        public string checksum;
        public string body;
    }

    public static class SaveValidation
    {
        /// <summary>FNV-1a over UTF-8. Detects truncated/corrupted files – not an anti-cheat measure.</summary>
        public static string Checksum(string s)
        {
            uint h = 0x811C9DC5;
            foreach (byte b in Encoding.UTF8.GetBytes(s ?? string.Empty))
            {
                h ^= b;
                h = unchecked(h * 0x01000193);
            }
            return h.ToString("x8");
        }

        private static float Fix(float v, float lo, float hi, float def) => float.IsNaN(v) || float.IsInfinity(v) || v <= 0 ? def : Math.Max(lo, Math.Min(hi, v));
        private static int NonNeg(int v, int max = 1_000_000_000) => v < 0 ? 0 : v > max ? max : v;
        private static float NonNeg(float v, float max = 1e9f) => float.IsNaN(v) || float.IsInfinity(v) || v < 0 ? 0 : v > max ? max : v;

        private static T[] Sized<T>(T[] a, int n)
        {
            if (a != null && a.Length == n) return a;
            var r = new T[n];
            if (a != null) Array.Copy(a, r, Math.Min(n, a.Length));
            return r;
        }

        /// <summary>Clamp every field into a valid range and migrate older versions. Returns the same instance.</summary>
        public static SaveData Sanitize(SaveData d)
        {
            if (d == null) d = new SaveData();
            // v1 -> v2: everyone keeps the two free Phase 1 jeepneys
            d.version = SaveData.CurrentVersion;
            d.coins = NonNeg(d.coins);
            if (d.careerMasks == null || d.careerMasks.Length != Career.Shifts.Length)
            {
                var cm = new int[Career.Shifts.Length];
                if (d.careerMasks != null) Array.Copy(d.careerMasks, cm, Math.Min(cm.Length, d.careerMasks.Length));
                d.careerMasks = cm;
            }
            for (int i = 0; i < d.careerMasks.Length; i++) d.careerMasks[i] &= 7;
            d.careerRank = Math.Max(0, Math.Min(Career.Licenses.Length - 1, d.careerRank));
            d.ownedCosmetics ??= new List<string>();
            d.ownedCosmetics.RemoveAll(id => Customization.Get(id) == null);
            d.looks ??= new List<JeepneyLook>();
            d.looks.RemoveAll(l => l == null || string.IsNullOrEmpty(l.jeepney));
            d.bestScore = NonNeg(d.bestScore);
            d.bestDistance = NonNeg(d.bestDistance);
            if (float.IsNaN(d.swipeSensitivity) || d.swipeSensitivity < 0.5f || d.swipeSensitivity > 1.5f) d.swipeSensitivity = 1f;
            if (d.graphicsQuality < 0 || d.graphicsQuality > 3) d.graphicsQuality = 3;
            if (d.steerMode < 0 || d.steerMode > 2) d.steerMode = 0;
            d.maxSteer = Fix(d.maxSteer, 0.7f, 1f, 1f);
            d.steerReturn = Fix(d.steerReturn, 0.5f, 2f, 1f);
            d.tiltDeadZone = Fix(d.tiltDeadZone, 0.01f, 0.12f, 0.035f);
            d.tiltSmoothing = Fix(d.tiltSmoothing, 4f, 30f, 14f);
            if (float.IsNaN(d.tiltCentre) || Math.Abs(d.tiltCentre) > 1f) { d.tiltCentre = 0; d.tiltCalibrated = false; }
            if (d.wheelSize < 0 || d.wheelSize > 2) d.wheelSize = 1;
            if (d.orientation < 0 || d.orientation > 2) d.orientation = 0;
            if (float.IsNaN(d.tiltCentreLand) || Math.Abs(d.tiltCentreLand) > 1f) { d.tiltCentreLand = 0; d.tiltCalibratedLand = false; }
            if (float.IsNaN(d.tiltRoll) || Math.Abs(d.tiltRoll) > 90f) { d.tiltRoll = 0; d.tiltRollCal = false; }
            if (float.IsNaN(d.tiltRollLand) || Math.Abs(d.tiltRollLand) > 90f) { d.tiltRollLand = 0; d.tiltRollLandCal = false; }
            d.wheelOpacity = Fix(d.wheelOpacity, 0.3f, 1f, 0.9f);
            d.wheelMaxAngle = Fix(d.wheelMaxAngle, 60f, 180f, 135f);
            d.buttonScale = Fix(d.buttonScale, 0.8f, 1.25f, 1f);
            d.buttonOpacity = Fix(d.buttonOpacity, 0.3f, 1f, 1f);
            d.runs = NonNeg(d.runs);
            d.totalDistance = NonNeg(d.totalDistance);
            d.totalCoins = NonNeg(d.totalCoins);
            d.totalPassengers = NonNeg(d.totalPassengers);
            d.crashes = NonNeg(d.crashes);

            d.ownedJeepneys ??= new List<string>();
            d.ownedJeepneys.RemoveAll(id => !JeepneyCatalog.Exists(id));
            foreach (var j in JeepneyCatalog.All)
                if (j.Price == 0 && !d.ownedJeepneys.Contains(j.Id)) d.ownedJeepneys.Add(j.Id);
            if (!JeepneyCatalog.Exists(d.selectedJeepney) || !d.ownedJeepneys.Contains(d.selectedJeepney)) d.selectedJeepney = JeepneyCatalog.DefaultId;

            d.upgradeLevels = Sized(d.upgradeLevels, Upgrades.All.Length);
            for (int i = 0; i < d.upgradeLevels.Length; i++) d.upgradeLevels[i] = Math.Max(0, Math.Min(Upgrades.MaxLevel, d.upgradeLevels[i]));
            if (d.selectedEnvironment < 0 || d.selectedEnvironment >= Environments.All.Length || !Environments.IsUnlocked((EnvironmentKind)d.selectedEnvironment, d.bestDistance))
                d.selectedEnvironment = 0;
            d.xp = NonNeg(d.xp);
            d.level = PlayerLevel.LevelFor(d.xp);
            d.totalDeliveries = NonNeg(d.totalDeliveries);
            d.totalPowerUps = NonNeg(d.totalPowerUps);
            d.totalJumps = NonNeg(d.totalJumps);
            d.totalSmoothHumps = NonNeg(d.totalSmoothHumps);
            if (!(d.totalWetDistance >= 0) || float.IsInfinity(d.totalWetDistance)) d.totalWetDistance = 0;
            d.bestCleanDistance = NonNeg(d.bestCleanDistance);
            d.achievementsUnlocked ??= new List<string>();
            d.achievementsClaimed ??= new List<string>();
            d.achievementsClaimed.RemoveAll(a => !d.achievementsUnlocked.Contains(a));
            d.gems = NonNeg(d.gems);
            d.ownedProducts ??= new List<string>();
            d.ads ??= new AdState();
            d.ads.trialsToday ??= new List<string>();
            d.dailyProgress = Sized(d.dailyProgress, Missions.DailyCount);
            d.dailyClaimed = Sized(d.dailyClaimed, Missions.DailyCount);
            d.weeklyProgress = Sized(d.weeklyProgress, Missions.WeeklyCount);
            d.weeklyClaimed = Sized(d.weeklyClaimed, Missions.WeeklyCount);
            return d;
        }

        public static bool Verify(SaveEnvelope env) => env != null && !string.IsNullOrEmpty(env.body) && env.checksum == Checksum(env.body);
    }

    public static class Progress
    {
        public sealed class RunRewards
        {
            public bool NewBestScore, NewBestDistance;
            public int CoinBonus;            // from the Coin Bonus upgrade
            public int Gems;                 // road gems credited (after the daily cap)
            public int XpGained;
            public int LevelsGained;
            public int LevelReward;
            public List<MissionDef> MissionsCompleted = new List<MissionDef>();
            public List<AchievementDef> AchievementsUnlocked = new List<AchievementDef>();
            public List<EnvironmentKind> RoutesUnlocked = new List<EnvironmentKind>();
            /// <summary>Career shift result (null for free-ride runs).</summary>
            public ShiftResult Career;
        }

        /// <summary>Applies a finished run to the save: bests, coins (+bonus), stats, XP, missions, achievements.</summary>
        public static RunRewards Apply(SaveData d, RunSummary r, int dayKey = 0, int weekKey = 0)
        {
            var res = new RunRewards();
            int score = Math.Max(0, r.Score);
            res.NewBestScore = score > d.bestScore;
            res.NewBestDistance = r.Distance > d.bestDistance;
            var lockedBefore = new List<EnvironmentKind>();
            foreach (var e in Environments.All) if (!Environments.IsUnlocked(e.Kind, d.bestDistance)) lockedBefore.Add(e.Kind);
            if (res.NewBestScore) d.bestScore = score;
            if (res.NewBestDistance) d.bestDistance = r.Distance;
            foreach (var k in lockedBefore) if (Environments.IsUnlocked(k, d.bestDistance)) res.RoutesUnlocked.Add(k);

            int coins = Math.Max(0, r.Coins);
            res.CoinBonus = (int)Math.Round(coins * Upgrades.CoinBonusFraction(d.UpgradeLevel(UpgradeKind.CoinBonus)));
            d.coins += coins + res.CoinBonus;
            int gems = Math.Max(0, Math.Min(r.Gems, RoadGems.MaxForDistance(r.Distance)));
            if (dayKey != 0)
            {
                if (d.roadGemsDay != dayKey) { d.roadGemsDay = dayKey; d.roadGemsToday = 0; }
                gems = Math.Min(gems, RoadGems.LeftToday(d.roadGemsToday));
                d.roadGemsToday += gems;
            }
            res.Gems = gems;
            d.gems += gems;
            d.runs++;
            d.totalDistance += Math.Max(0, r.Distance);
            d.totalCoins += coins + res.CoinBonus;
            d.totalPassengers += Math.Max(0, r.Passengers);
            d.totalDeliveries += Math.Max(0, r.Deliveries);
            d.totalPowerUps += Math.Max(0, r.PowerUps);
            d.totalSmoothHumps += Math.Max(0, r.SmoothHumps);
            d.totalWetDistance += Math.Max(0, r.WetDistance);
            d.bestCleanDistance = Math.Max(d.bestCleanDistance, r.CleanDistance);
            if (r.Crashed) d.crashes++;

            res.XpGained = PlayerLevel.XpFromRun(r);
            int before = PlayerLevel.LevelFor(d.xp);
            d.xp += res.XpGained;
            int after = PlayerLevel.LevelFor(d.xp);
            for (int lv = before + 1; lv <= after; lv++) res.LevelReward += PlayerLevel.Reward(lv);
            res.LevelsGained = after - before;
            d.level = after;
            d.coins += res.LevelReward;

            if (dayKey != 0) res.MissionsCompleted.AddRange(Missions.ApplyRun(d, r, dayKey, weekKey));
            res.Career = Logic.Career.Apply(d, r);
            res.AchievementsUnlocked.AddRange(Achievements.Evaluate(d));
            SaveValidation.Sanitize(d);
            return res;
        }

        /// <summary>Premium alternative: pay a jeepney's gem price.</summary>
        public static bool BuyJeepneyWithGems(SaveData d, string id)
        {
            var j = JeepneyCatalog.Get(id);
            if (j.Id != id || j.GemPrice <= 0 || d.Owns(id) || d.gems < j.GemPrice) return false;
            d.gems -= j.GemPrice;
            d.ownedJeepneys.Add(id);
            return true;
        }

        /// <summary>
        /// Applies a product's contents locally. Only used by the offline test store in development
        /// builds without a server – real purchases are granted by the server after verification.
        /// </summary>
        public static void GrantProductLocally(SaveData d, ProductDef p)
        {
            if (p == null) return;
            if (p.Kind == ProductKind.NonConsumable && d.ownedProducts.Contains(p.Id)) return;
            d.coins += p.Coins;
            d.gems += p.Gems;
            foreach (var j in p.Jeepneys) if (JeepneyCatalog.Exists(j) && !d.Owns(j)) d.ownedJeepneys.Add(j);
            if (p.AdFree) d.adFree = true;
            if (p.Kind == ProductKind.NonConsumable) d.ownedProducts.Add(p.Id);
        }

        public static bool BuyJeepney(SaveData d, string id)
        {
            var j = JeepneyCatalog.Get(id);
            if (j.Id != id || d.Owns(id) || d.coins < j.Price) return false;
            d.coins -= j.Price;
            d.ownedJeepneys.Add(id);
            return true;
        }

        public static bool BuyUpgrade(SaveData d, UpgradeKind k)
        {
            int lv = d.UpgradeLevel(k);
            int cost = Upgrades.Cost(k, lv);
            if (cost < 0 || d.coins < cost) return false;
            d.coins -= cost;
            d.upgradeLevels[(int)k] = lv + 1;
            return true;
        }
    }
}
