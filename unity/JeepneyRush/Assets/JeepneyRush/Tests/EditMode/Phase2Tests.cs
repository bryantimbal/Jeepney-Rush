using System;
using System.Linq;
using JeepneyRush.Logic;
using NUnit.Framework;

namespace JeepneyRush.Tests
{
    public class PowerUpTests
    {
        [Test]
        public void TimersRunOutAndRefreshInsteadOfStacking()
        {
            var cfg = new BalanceConfig();
            var p = new PowerUpState(cfg);
            p.Activate(PowerUpKind.Magnet);
            Assert.IsTrue(p.IsActive(PowerUpKind.Magnet));
            p.Tick(cfg.magnetDuration - 1);
            p.Activate(PowerUpKind.Magnet); // refresh
            Assert.AreEqual(cfg.magnetDuration, p.Remaining(PowerUpKind.Magnet), 1e-4);
            p.Tick(cfg.magnetDuration + 0.1f);
            Assert.IsFalse(p.IsActive(PowerUpKind.Magnet));
            Assert.AreEqual(2, p.UsedThisRun);
        }

        [Test]
        public void ShieldBlocksExactlyOneHit()
        {
            var p = new PowerUpState(new BalanceConfig());
            Assert.IsFalse(p.ConsumeShield());
            p.Activate(PowerUpKind.Shield);
            Assert.IsTrue(p.ConsumeShield());
            Assert.IsFalse(p.ConsumeShield());
        }

        [Test]
        public void EffectsAndDurationUpgrade()
        {
            var cfg = new BalanceConfig();
            var p = new PowerUpState(cfg);
            p.Activate(PowerUpKind.Turbo, Upgrades.PowerUpDurationScale(5));
            Assert.AreEqual(cfg.turboDuration * 1.6f, p.Remaining(PowerUpKind.Turbo), 1e-3);
            Assert.IsTrue(p.Invulnerable);
            Assert.AreEqual(cfg.turboSpeedMultiplier, p.SpeedMultiplier);
            p.Activate(PowerUpKind.DoubleCoins);
            Assert.AreEqual(2, p.CoinMultiplier);
            p.Activate(PowerUpKind.PassengerBonus);
            Assert.AreEqual(2f, p.FareMultiplier);
            p.Activate(PowerUpKind.RepairKit);
            Assert.IsFalse(p.IsActive(PowerUpKind.RepairKit)); // instant
        }

        [Test]
        public void EveryKindCanSpawn()
        {
            var rng = new GameRandom(3);
            var seen = Enumerable.Range(0, 400).Select(_ => PowerUpState.RandomKind(rng)).Distinct().Count();
            Assert.AreEqual(PowerUpState.KindCount, seen);
        }
    }

    public class EconomyTests
    {
        [Test]
        public void CatalogHasElevenDistinctJeepneysWithTwoFree()
        {
            Assert.AreEqual(11, JeepneyCatalog.All.Length);
            Assert.AreEqual(JeepneyCatalog.All.Length, JeepneyCatalog.All.Select(j => j.Id).Distinct().Count());
            Assert.AreEqual(2, JeepneyCatalog.All.Count(j => j.Price == 0));
        }

        [Test]
        public void BuyingJeepneysAndUpgradesSpendsCoins()
        {
            var d = SaveValidation.Sanitize(new SaveData { coins = 2000 });
            Assert.IsFalse(Progress.BuyJeepney(d, "gold"));          // too expensive
            Assert.IsTrue(Progress.BuyJeepney(d, "cebu"));            // 1200
            Assert.AreEqual(800, d.coins);
            Assert.IsFalse(Progress.BuyJeepney(d, "cebu"));           // already owned
            Assert.IsTrue(Progress.BuyUpgrade(d, UpgradeKind.Engine)); // 150
            Assert.AreEqual(650, d.coins);
            Assert.AreEqual(1, d.UpgradeLevel(UpgradeKind.Engine));
        }

        [Test]
        public void UpgradesCapAtMaxLevel()
        {
            var d = SaveValidation.Sanitize(new SaveData { coins = 1_000_000 });
            for (int i = 0; i < 10; i++) Progress.BuyUpgrade(d, UpgradeKind.Capacity);
            Assert.AreEqual(Upgrades.MaxLevel, d.UpgradeLevel(UpgradeKind.Capacity));
            Assert.AreEqual(-1, Upgrades.Cost(UpgradeKind.Capacity, Upgrades.MaxLevel));
        }

        [Test]
        public void LoadoutAppliesUpgradesAndJeepneyStatsToACopy()
        {
            var baseCfg = new BalanceConfig();
            var gold = JeepneyCatalog.Get("gold");
            var c = Upgrades.Apply(baseCfg, gold, k => Upgrades.MaxLevel);
            Assert.AreEqual(15, c.passengerCapacity);            // capped at 15 as in the design doc
            Assert.AreEqual(3 + 2 + 1, c.startHealth);            // armor 5 -> +2, gold +1
            Assert.Greater(c.maxSpeed, baseCfg.maxSpeed);
            Assert.Less(c.laneChangeDuration, baseCfg.laneChangeDuration);
            Assert.AreEqual(24f, baseCfg.maxSpeed);               // base untouched
            Assert.AreEqual(5, baseCfg.passengerCapacity);
        }

        [Test]
        public void CoinBonusUpgradeAddsToRunEarnings()
        {
            var d = SaveValidation.Sanitize(new SaveData());
            d.upgradeLevels[(int)UpgradeKind.CoinBonus] = 3;
            var r = Progress.Apply(d, new RunSummary { Coins = 100, Distance = 10 });
            Assert.AreEqual(30, r.CoinBonus);
        }

        [Test]
        public void LevelsComeFromXp()
        {
            Assert.AreEqual(1, PlayerLevel.LevelFor(0));
            Assert.AreEqual(2, PlayerLevel.LevelFor(PlayerLevel.XpForLevel(2)));
            Assert.AreEqual(1, PlayerLevel.LevelFor(PlayerLevel.XpForLevel(2) - 1));
            var d = SaveValidation.Sanitize(new SaveData());
            var res = Progress.Apply(d, new RunSummary { Distance = 3000, Deliveries = 20, Passengers = 25 });
            Assert.Greater(res.LevelsGained, 0);
            Assert.Greater(res.LevelReward, 0);
        }
    }

    public class MissionTests
    {
        [Test]
        public void DailyMissionsAreDeterministicAndDistinct()
        {
            var a = Missions.Daily(20261121);
            var b = Missions.Daily(20261121);
            Assert.AreEqual(Missions.DailyCount, a.Length);
            CollectionAssert.AreEqual(a.Select(m => m.Text), b.Select(m => m.Text));
            Assert.AreEqual(a.Length, a.Select(m => m.Type).Distinct().Count());
            Assert.AreNotEqual(string.Join("|", a.Select(m => m.Text)), string.Join("|", Missions.Daily(20261122).Select(m => m.Text)));
        }

        [Test]
        public void ProgressCompletesClaimsOnceAndResetsNextDay()
        {
            var d = SaveValidation.Sanitize(new SaveData());
            const int day = 20261121, week = 152;
            Missions.Refresh(d, day, week);
            var huge = new RunSummary { Distance = 99999, Coins = 9999, Passengers = 999, Deliveries = 999, CleanDistance = 99999, BestStreak = 99, PowerUps = 99, SmoothHumps = 999, WetDistance = 99999 };
            var done = Missions.ApplyRun(d, huge, day, week);
            Assert.GreaterOrEqual(done.Count, Missions.DailyCount); // every daily is done (Runs needs several runs only if picked)
            int coins = d.coins;
            int paid = 0;
            for (int i = 0; i < Missions.DailyCount; i++) paid += Missions.Claim(d, false, i);
            Assert.Greater(paid, 0);
            Assert.AreEqual(coins + paid, d.coins);
            Assert.AreEqual(0, Missions.Claim(d, false, 0)); // no double claim
            Missions.Refresh(d, day + 1, week);
            Assert.IsTrue(d.dailyProgress.All(p => p == 0));
            Assert.IsTrue(d.dailyClaimed.All(c => !c));
        }

        [Test]
        public void InRunMissionsTakeTheBestRunNotTheSum()
        {
            var d = SaveValidation.Sanitize(new SaveData());
            Missions.Refresh(d, 1, 1);
            var defs = Missions.Daily(1);
            int idx = Array.FindIndex(defs, m => m.InRun);
            if (idx < 0) Assert.Ignore("no in-run mission today");
            var small = new RunSummary { Distance = 1, Coins = 1, Passengers = 1, Deliveries = 1, CleanDistance = 1, BestStreak = 1 };
            Missions.ApplyRun(d, small, 1, 1);
            Missions.ApplyRun(d, small, 1, 1);
            Assert.AreEqual(1, d.dailyProgress[idx]);
        }
    }

    public class AchievementTests
    {
        [Test]
        public void UnlockAndClaimOnce()
        {
            var d = SaveValidation.Sanitize(new SaveData());
            var r = Progress.Apply(d, new RunSummary { Passengers = 1, Distance = 1200, Coins = 10 });
            Assert.IsTrue(r.AchievementsUnlocked.Any(a => a.Id == "first_passenger"));
            Assert.IsTrue(r.AchievementsUnlocked.Any(a => a.Id == "meters_1000"));
            int before = d.coins;
            Assert.AreEqual(50, Achievements.Claim(d, "first_passenger"));
            Assert.AreEqual(before + 50, d.coins);
            Assert.AreEqual(0, Achievements.Claim(d, "first_passenger"));
            Assert.AreEqual(0, Achievements.Claim(d, "highway_legend")); // not unlocked
        }

        [Test]
        public void RoutesUnlockByBestDistance()
        {
            var d = SaveValidation.Sanitize(new SaveData());
            Assert.IsFalse(Environments.IsUnlocked(EnvironmentKind.Provincial, d.bestDistance));
            var r = Progress.Apply(d, new RunSummary { Distance = 2600 });
            CollectionAssert.AreEquivalent(new[] { EnvironmentKind.Provincial, EnvironmentKind.Beach }, r.RoutesUnlocked);
        }
    }

    public class MigrationTests
    {
        [Test]
        public void Version1SaveGainsFreeJeepneysAndPhase2Fields()
        {
            var v1 = new SaveData { version = 1, coins = 300, selectedJeepney = "sanjuan", ownedJeepneys = null, upgradeLevels = null, dailyProgress = null };
            var d = SaveValidation.Sanitize(v1);
            Assert.AreEqual(SaveData.CurrentVersion, d.version);
            Assert.IsTrue(d.Owns("pilipinas") && d.Owns("sanjuan"));
            Assert.AreEqual("sanjuan", d.selectedJeepney);
            Assert.AreEqual(Upgrades.All.Length, d.upgradeLevels.Length);
            Assert.AreEqual(Missions.DailyCount, d.dailyProgress.Length);
            Assert.AreEqual(300, d.coins);
        }

        [Test]
        public void CannotSelectUnownedJeepneyOrLockedRoute()
        {
            var d = SaveValidation.Sanitize(new SaveData { selectedJeepney = "gold", selectedEnvironment = 2, bestDistance = 100 });
            Assert.AreEqual(JeepneyCatalog.DefaultId, d.selectedJeepney);
            Assert.AreEqual(0, d.selectedEnvironment);
        }
    }
}
