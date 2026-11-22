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
        /// <summary>0.5 (needs long swipes) .. 1.5 (very sensitive).</summary>
        public float swipeSensitivity = 1f;
        public int graphicsQuality = 3; // 0 low, 1 medium, 2 high, 3 auto (adapts to the device)
        public bool tutorialDone;

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
        /// <summary>"No Ads" purchased.</summary>
        public bool adFree;
        public List<string> ownedProducts = new List<string>();
        public AdState ads = new AdState();

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
            d.bestScore = NonNeg(d.bestScore);
            d.bestDistance = NonNeg(d.bestDistance);
            if (float.IsNaN(d.swipeSensitivity) || d.swipeSensitivity < 0.5f || d.swipeSensitivity > 1.5f) d.swipeSensitivity = 1f;
            if (d.graphicsQuality < 0 || d.graphicsQuality > 3) d.graphicsQuality = 3;
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
            public int XpGained;
            public int LevelsGained;
            public int LevelReward;
            public List<MissionDef> MissionsCompleted = new List<MissionDef>();
            public List<AchievementDef> AchievementsUnlocked = new List<AchievementDef>();
            public List<EnvironmentKind> RoutesUnlocked = new List<EnvironmentKind>();
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
