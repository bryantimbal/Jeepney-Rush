using System;
using System.Collections.Generic;
using JeepneyRush.Logic;
using NUnit.Framework;

namespace JeepneyRush.Tests
{
    public class OnlineTests
    {
        [Test]
        public void DailyChallengeIsTheSameForEveryoneAndVariesByDay()
        {
            var a = DailyChallenge.For(20261121);
            var b = DailyChallenge.For(20261121);
            Assert.AreEqual(a.Seed, b.Seed);
            Assert.AreEqual(a.Environment, b.Environment);
            var seeds = new HashSet<int>();
            var envs = new HashSet<EnvironmentKind>();
            for (int d = 1; d <= 28; d++)
            {
                var c = DailyChallenge.For(20261100 + d);
                Assert.Greater(c.Seed, 0);
                seeds.Add(c.Seed);
                envs.Add(c.Environment);
            }
            Assert.AreEqual(28, seeds.Count);
            Assert.AreEqual(Environments.All.Length, envs.Count, "every route shows up as a daily challenge");
        }

        [Test]
        public void PhilippineDayKeyUsesUtcPlus8()
        {
            var utc = new DateTime(2026, 11, 20, 17, 30, 0, DateTimeKind.Utc); // 01:30 on the 21st in Manila
            Assert.AreEqual(20261121, Missions.DayKey(utc.AddHours(8)));
        }

        private static RunReport Legit(float distance)
        {
            // a strong but honest run: speed curve average, coins/passengers at measured densities
            var cfg = new BalanceConfig();
            float t = 0;
            for (float d = 0; d < distance; d += 1) t += 1f / (cfg.SpeedAt(d) * 1.3f); // always on GAS
            int passengers = (int)(distance / 250f * 5);
            int deliveries = (int)(passengers * 0.8f);
            int coins = (int)(distance * 0.21f * 1.5f) + deliveries * 15;
            return new RunReport
            {
                run_id = Guid.NewGuid().ToString(), seed = 1, env = 0, duration = t, distance = distance,
                coins = coins, passengers = passengers, deliveries = deliveries, power_ups = (int)(distance / 450f),
                smooth_humps = (int)(distance / 300f), wet_distance = distance * 0.4f, clean_distance = distance * 0.5f, refuels = (int)(distance / 1100f),
                score = (int)(distance * 1.4f) + coins * 10 + passengers * 50 + deliveries * 250,
            };
        }

        [TestCase(300f)] [TestCase(2000f)] [TestCase(8000f)]
        public void HonestRunsPassUnclamped(float distance)
        {
            var r = Legit(distance);
            Assert.IsNull(RunBounds.HardViolation(r));
            var before = JsonClone(r);
            RunBounds.Clamp(r);
            Assert.AreEqual(before.coins, r.coins);
            Assert.AreEqual(before.score, r.score);
            Assert.AreEqual(before.passengers, r.passengers);
            Assert.AreEqual(before.power_ups, r.power_ups);
        }

        private static RunReport JsonClone(RunReport r) => (RunReport)r.GetType().GetMethod("MemberwiseClone", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(r, null);

        [Test]
        public void ImpossibleRunsAreRejected()
        {
            var r = Legit(1000); r.distance = 1000; r.duration = 10;
            Assert.AreEqual("too_fast", RunBounds.HardViolation(r));
            r = Legit(1000); r.duration = 1;
            Assert.AreEqual("duration", RunBounds.HardViolation(r));
            r = Legit(1000); r.coins = -5;
            Assert.AreEqual("negative", RunBounds.HardViolation(r));
            r = Legit(1000); r.deliveries = r.passengers + 1;
            Assert.AreEqual("deliveries", RunBounds.HardViolation(r));
            r = Legit(1000); r.clean_distance = 5000;
            Assert.AreEqual("distance_parts", RunBounds.HardViolation(r));
            r = Legit(1000); r.env = 9;
            Assert.AreEqual("env", RunBounds.HardViolation(r));
        }

        [Test]
        public void InflatedNumbersAreClamped()
        {
            var r = Legit(1000);
            r.coins = 999999; r.score = 99999999; r.passengers = 5000; r.power_ups = 500;
            RunBounds.Clamp(r);
            Assert.LessOrEqual(r.coins, RunBounds.MaxCoins(1000, r.deliveries));
            Assert.LessOrEqual(r.passengers, RunBounds.MaxPassengers(1000));
            Assert.LessOrEqual(r.score, RunBounds.MaxScore(1000, r.coins, r.passengers, r.deliveries, r.smooth_humps));
            Assert.LessOrEqual(r.power_ups, RunBounds.MaxPowerUps(1000));
        }

        [Test]
        public void ServerSnapshotWinsForEconomyButKeepsSettings()
        {
            var d = new SaveData { coins = 99999, music = false, swipeSensitivity = 1.3f, selectedJeepney = "pilipinas" };
            var s = new ServerSnapshot
            {
                coins = 420, xp = 500, best_score = 1234, best_distance = 2100, owned = new[] { "pilipinas", "sanjuan", "manila" },
                upgrades = new[] { 2, 1, 0, 0, 3, 0 }, day_key = 20261121, daily_progress = new[] { 5, 0, 1 }, daily_claimed = new[] { true, false, false },
                week_key = 152, weekly_progress = new[] { 1000, 3 }, weekly_claimed = new[] { false, false },
                achievements_unlocked = new[] { "first_passenger" }, achievements_claimed = new string[0],
            };
            ServerSync.Apply(d, s);
            Assert.AreEqual(420, d.coins);
            Assert.AreEqual(PlayerLevel.LevelFor(500), d.level);
            Assert.IsTrue(d.Owns("manila"));
            Assert.AreEqual(3, d.UpgradeLevel(UpgradeKind.CoinBonus));
            Assert.AreEqual(20261121, d.dailyKey);
            Assert.IsTrue(d.dailyClaimed[0]);
            Assert.AreEqual(1000, d.weeklyProgress[0]);
            Assert.IsFalse(d.music, "settings stay local");
            Assert.AreEqual(1.3f, d.swipeSensitivity);
        }

        [Test]
        public void OfflineQueueDeduplicatesAndCaps()
        {
            var q = new OfflineQueue();
            var r = Legit(500);
            q.Add(r);
            q.Add(r);
            Assert.AreEqual(1, q.Count);
            for (int i = 0; i < OfflineQueue.Limit + 10; i++) q.Add(Legit(400));
            Assert.AreEqual(OfflineQueue.Limit, q.Count);
            q.Remove(q.runs[0].run_id);
            Assert.AreEqual(OfflineQueue.Limit - 1, q.Count);
        }

        [Test]
        public void NamesAndFriendCodes()
        {
            Assert.AreEqual("Kuya Jun", OnlineText.CleanName("  Kuya Jun!! "));
            Assert.IsNull(OnlineText.CleanName("ab"));
            Assert.IsNull(OnlineText.CleanName(new string('x', 30)));
            Assert.AreEqual("AB12CD34", OnlineText.NormalizeCode("ab12-cd34"));
            Assert.IsNull(OnlineText.NormalizeCode("ABC"));
        }

        [Test]
        public void ReportCarriesTheRun()
        {
            var s = new RunSummary { Score = 10, Distance = 200, Coins = 7, Passengers = 2, Deliveries = 1, Environment = EnvironmentKind.Beach, Refuels = 1 };
            var r = RunReport.From(s, 30, 77, true, 20261121, "pilipinas");
            Assert.AreEqual(2, r.env);
            Assert.AreEqual(77, r.seed);
            Assert.IsTrue(r.daily);
            Assert.AreEqual(1, r.refuels);
            Assert.IsFalse(string.IsNullOrEmpty(r.run_id));
        }
    }
}

namespace JeepneyRush.Tests
{
    public class CatalogExportTests
    {
        private static string RepoRoot()
        {
            var d = new System.IO.DirectoryInfo(System.IO.Directory.GetCurrentDirectory());
            while (d != null && !System.IO.Directory.Exists(System.IO.Path.Combine(d.FullName, "supabase", "migrations"))) d = d.Parent;
            return d?.FullName;
        }

        [Test]
        public void ServerCatalogMatchesTheGame()
        {
            string root = RepoRoot();
            if (root == null) Assert.Ignore("supabase/ folder not found next to the project");
            string path = CatalogExport.LatestFile(System.IO.Path.Combine(root, "supabase", "migrations"));
            Assert.IsNotNull(path, "catalog migration missing – run unity/tools/ExportCatalog");
            Assert.AreEqual(CatalogExport.Sql(), System.IO.File.ReadAllText(path).Replace("\r\n", "\n"),
                "Prices/missions/achievements changed in C# – re-run: dotnet run --project unity/tools/ExportCatalog");
        }

        [Test]
        public void EveryAchievementHasAServerStat()
        {
            var d = new JeepneyRush.Logic.SaveData { totalPassengers = 3, bestDistance = 1200, totalCoins = 40 };
            foreach (var a in JeepneyRush.Logic.Achievements.All)
            {
                float v = JeepneyRush.Logic.CatalogExport.StatValue(d, a.StatKey);
                Assert.IsFalse(float.IsNaN(v), a.Id + " has no stat key");
                Assert.AreEqual(a.Stat(d), v, 1e-3, a.Id);
            }
        }
    }
}
