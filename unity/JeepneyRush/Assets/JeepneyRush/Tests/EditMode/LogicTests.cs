using System.Collections.Generic;
using System.Linq;
using JeepneyRush.Logic;
using JeepneyRush.Logic.Audio;
using NUnit.Framework;

namespace JeepneyRush.Tests
{
    public class OccupancyTests
    {
        private static readonly BalanceConfig Cfg = new BalanceConfig();
        private static PredictBlocker B(int lane, float z, float halfL = 2, float speed = 0, bool soft = false, int lane2 = -1) => new PredictBlocker(lane, z, halfL, speed, soft, lane2);
        private static bool Pass(List<PredictBlocker> b, float speed, float horizon, IReadOnlyList<int> start = null) =>
            LaneOccupancy.IsPassable(b, speed, horizon, Cfg.playerHalfLength, 3, Cfg.laneChangeDuration + Cfg.laneChangeSlack, start);

        [Test] public void EmptyRoadIsPassable() => Assert.IsTrue(Pass(new List<PredictBlocker>(), 15, 5));
        [Test] public void FullWallIsNotPassable() => Assert.IsFalse(Pass(new List<PredictBlocker> { B(0, -50), B(1, -50), B(2, -50) }, 15, 6));
        [Test] public void SoftHazardsNeverBlock() => Assert.IsTrue(Pass(new List<PredictBlocker> { B(0, -50, soft: true), B(1, -50, soft: true), B(2, -50, soft: true) }, 15, 6));
        [Test] public void TwoBlockedLanesLeaveOne() => Assert.IsTrue(Pass(new List<PredictBlocker> { B(0, -50), B(1, -50) }, 15, 6));

        [Test]
        public void StartLaneMatters()
        {
            // lane 0 blocked very soon; lane 1 blocked the whole time -> from lane 0 you cannot escape to lane 2
            var b = new List<PredictBlocker> { B(0, -8), B(1, -10, halfL: 60) };
            Assert.IsFalse(Pass(b, 15, 4, new[] { 0 }));
            Assert.IsTrue(Pass(b, 15, 4, new[] { 2 }));
        }

        [Test]
        public void IntervalsUseClosingSpeed()
        {
            var iv = LaneOccupancy.BlockedIntervals(new List<PredictBlocker> { B(1, -100, speed: 5) }, 15, 30, Cfg.playerHalfLength);
            Assert.AreEqual(1, iv.Count);
            Assert.That(iv[0].T0, Is.InRange(9f, 9.8f)); // closing 10 m/s, reach 4.4 m
        }

        [Test]
        public void LaneChangingVehicleBlocksBothLanes()
        {
            var iv = LaneOccupancy.BlockedIntervals(new List<PredictBlocker> { B(0, -40, lane2: 1) }, 15, 10, Cfg.playerHalfLength);
            CollectionAssert.AreEquivalent(new[] { 0, 1 }, iv.Select(i => i.Lane));
        }
    }

    public class BalanceTests
    {
        [Test]
        public void DefaultsMatchDesignSpec()
        {
            var c = new BalanceConfig();
            Assert.AreEqual(12f, c.startSpeed);
            Assert.AreEqual(24f, c.maxSpeed);
            Assert.AreEqual(3.2f, c.laneSpacing);
            Assert.AreEqual(0.42f, c.laneChangeDuration);
            Assert.AreEqual(3, c.startHealth);
            Assert.AreEqual(5, c.passengerCapacity);
            Assert.AreEqual(-3.2f, c.LaneX(0), 1e-4);
            Assert.AreEqual(3.2f, c.LaneX(2), 1e-4);
        }

        [Test]
        public void SpeedRampsMonotonically()
        {
            var c = new BalanceConfig();
            Assert.AreEqual(12f, c.SpeedAt(0), 1e-3);
            float prev = 0;
            for (float d = 0; d <= 5000; d += 50) { Assert.GreaterOrEqual(c.SpeedAt(d), prev); prev = c.SpeedAt(d); }
            Assert.AreEqual(24f, c.SpeedAt(5000), 1e-3);
        }

        [Test]
        public void OnlyHumpsPotholesAndFloodsAreSoft()
        {
            foreach (HazardKind k in System.Enum.GetValues(typeof(HazardKind)))
            {
                bool expected = k == HazardKind.Pothole || k == HazardKind.SpeedHump || k == HazardKind.FloodPuddle || k == HazardKind.FloodWide || k == HazardKind.MudPatch
                    || k == HazardKind.StrayDog || k == HazardKind.Basketball; // road events you drive over (a scare, not a crash)
                Assert.AreEqual(expected, HazardCatalog.Get(k).Soft, k.ToString());
            }
            Assert.IsTrue(HazardCatalog.IsFlood(HazardKind.FloodWide));
            Assert.IsFalse(HazardCatalog.IsFlood(HazardKind.SpeedHump));
        }
    }

    public class PassengerTests
    {
        private static PassengerRoute Route(int capacity = 5, int seed = 1)
        {
            var cfg = new BalanceConfig { passengerCapacity = capacity };
            return new PassengerRoute(cfg, new GameRandom(seed));
        }

        [Test]
        public void BoardsUpToCapacityWithFutureDestinations()
        {
            var r = Route();
            var a = r.Plan(100, -1, 0, 4);
            r.Plan(300, 1, 2, 4);
            r.Plan(500, 1, 2, 4);
            var res = r.Serve(a);
            Assert.AreEqual(4, res.Boarded);
            Assert.AreEqual(4, r.Count);
            Assert.AreEqual(0, r.AlightingAt(a.Id));
        }

        [Test]
        public void CapacityIsEnforced()
        {
            var r = Route(capacity: 3);
            var a = r.Plan(100, -1, 0, 4);
            r.Plan(300, 1, 2, 0);
            var res = r.Serve(a);
            Assert.AreEqual(3, res.Boarded);
            Assert.AreEqual(1, res.LeftBehind);
            Assert.IsTrue(res.Full);
        }

        [Test]
        public void DeliveringPaysFaresGroupBonusAndStreak()
        {
            var cfg = new BalanceConfig();
            var r = new PassengerRoute(cfg, new GameRandom(5));
            var a = r.Plan(100, -1, 0, 3);
            var b = r.Plan(300, 1, 2, 0); // the only future stop: everyone rides to b
            r.Serve(a);
            var res = r.Serve(b);
            Assert.AreEqual(3, res.Alighted);
            Assert.AreEqual(3 * cfg.fareCoins + 2 * cfg.groupBonusCoins, res.Coins);
            Assert.AreEqual(1, res.Streak);
            Assert.AreEqual(3, r.Delivered);
            Assert.AreEqual(0, r.Count);
        }

        [Test]
        public void PassengerBonusDoublesFares()
        {
            var cfg = new BalanceConfig();
            var r = new PassengerRoute(cfg, new GameRandom(5)) { FareMultiplier = 2f };
            var a = r.Plan(100, -1, 0, 1);
            var b = r.Plan(300, 1, 2, 0);
            r.Serve(a);
            Assert.AreEqual(cfg.fareCoins * 2, r.Serve(b).Coins);
        }

        [Test]
        public void MissingAStopLosesRidersAndResetsStreak()
        {
            var r = Route();
            var a = r.Plan(100, -1, 0, 2);
            var b = r.Plan(300, 1, 2, 0);
            r.Serve(a);
            Assert.AreEqual(2, r.Miss(b));
            Assert.AreEqual(0, r.Streak);
            Assert.AreEqual(2, r.Missed);
            Assert.AreEqual(0, r.Count);
        }

        [Test]
        public void StopNamesComeFromTheEnvironment()
        {
            var cfg = new BalanceConfig();
            var r = new PassengerRoute(cfg, new GameRandom(9), StopNames.ForEnvironment(EnvironmentKind.Beach));
            for (int i = 0; i < 10; i++)
            {
                var s = r.Plan(i * 300, 1, 2, 1);
                Assert.That(s.NameIndex, Is.InRange(StopNames.BeachFrom, StopNames.BeachTo));
                Assert.AreEqual(StopNames.All[s.NameIndex], s.Name);
                r.Miss(s);
            }
        }
    }

    public class SaveTests
    {
        [Test]
        public void SanitizeClampsHostileValues()
        {
            var d = new SaveData { coins = -5, bestDistance = float.NaN, selectedJeepney = "hacked", swipeSensitivity = 9, graphicsQuality = 7 };
            SaveValidation.Sanitize(d);
            Assert.AreEqual(0, d.coins);
            Assert.AreEqual(0, d.bestDistance);
            Assert.AreEqual(JeepneyCatalog.DefaultId, d.selectedJeepney);
            Assert.AreEqual(1f, d.swipeSensitivity);
            Assert.AreEqual(3, d.graphicsQuality); // back to Auto
        }

        [Test]
        public void ChecksumDetectsTampering()
        {
            var env = new SaveEnvelope { body = "{\"coins\":5}" };
            env.checksum = SaveValidation.Checksum(env.body);
            Assert.IsTrue(SaveValidation.Verify(env));
            env.body = "{\"coins\":9999}";
            Assert.IsFalse(SaveValidation.Verify(env));
            Assert.IsFalse(SaveValidation.Verify(null));
        }

        [Test]
        public void RunProgressNeverLowersBests()
        {
            var d = new SaveData();
            var r1 = Progress.Apply(d, new RunSummary { Score = 500, Distance = 300, Coins = 10, Passengers = 2, Crashed = true });
            Assert.IsTrue(r1.NewBestScore && r1.NewBestDistance);
            var r2 = Progress.Apply(d, new RunSummary { Score = 100, Distance = 50, Coins = 5 });
            Assert.IsFalse(r2.NewBestScore);
            Assert.AreEqual(500, d.bestScore);
            Assert.AreEqual(15 + r1.LevelReward + r2.LevelReward, d.coins);
            Assert.AreEqual(2, d.runs);
            Assert.AreEqual(1, d.crashes);
        }
    }

    public class AudioTests
    {
        [Test]
        public void AllSoundsRenderWithinRange()
        {
            foreach (var s in SoundBank.AllSfx)
            {
                var b = SoundBank.Make(s);
                Assert.Greater(b.Length, 100, s.ToString());
                Assert.IsTrue(b.All(v => !float.IsNaN(v) && v <= 1f && v >= -1f), s.ToString());
                Assert.Greater(b.Max(v => System.Math.Abs(v)), 0.1f, $"{s} is silent");
            }
        }

        [Test]
        public void EngineLoopIsSeamless()
        {
            var b = SoundBank.EngineLoop();
            Assert.Less(System.Math.Abs(b[0] - b[b.Length - 1]), 0.08f);
        }

        [Test]
        public void MusicLoopsHaveExpectedLength()
        {
            var run = MusicComposer.RenderRun();
            Assert.AreEqual(128 * 60.0 / 132 / 4, run.Length / (double)Synth.Rate, 0.01);
            Assert.IsTrue(run.All(v => !float.IsNaN(v)));
            Assert.Greater(MusicComposer.RenderMenu().Length, Synth.Rate * 5);
        }
    }
}
