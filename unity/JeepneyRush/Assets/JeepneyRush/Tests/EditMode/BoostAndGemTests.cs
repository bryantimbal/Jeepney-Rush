using JeepneyRush.Logic;
using JeepneyRush.Logic.Geometry;
using NUnit.Framework;

namespace JeepneyRush.Tests
{
    public class BoostAndGemTests
    {
        [Test]
        public void BoostersAreStoredAndUsedOnDemand()
        {
            var bank = new BoostBank();
            Assert.AreEqual(BoostBank.UseResult.Empty, bank.TryUse(false, float.PositiveInfinity));
            for (int i = 0; i < BoostBank.Max; i++) Assert.IsTrue(bank.Add());
            Assert.IsFalse(bank.Add(), "bank holds at most 3");
            Assert.AreEqual(BoostBank.UseResult.TooClose, bank.TryUse(false, 80f), "never right before a stop / station / intersection");
            Assert.AreEqual(BoostBank.UseResult.AlreadyActive, bank.TryUse(true, 500f));
            Assert.AreEqual(3, bank.Count, "refused uses don't spend a booster");
            Assert.AreEqual(BoostBank.UseResult.Used, bank.TryUse(false, 500f));
            Assert.AreEqual(BoostBank.UseResult.Used, bank.TryUse(false, float.PositiveInfinity));
            Assert.AreEqual(1, bank.Count);
            Assert.AreEqual(2, bank.UsedThisRun);
            bank.Reset();
            Assert.AreEqual(0, bank.Count);
        }

        [TestCase(0f, 0)] [TestCase(299f, 0)] [TestCase(300f, 1)] [TestCase(2000f, 3)] [TestCase(99999f, 5)]
        public void GemsPerRunFollowDistance(float d, int max) => Assert.AreEqual(max, RoadGems.MaxForDistance(d));

        [Test]
        public void ServerBoundsClampGems()
        {
            var r = new RunReport { run_id = "x", duration = 300, distance = 2000, gems = 50, env = 0 };
            Assert.IsNull(RunBounds.HardViolation(r));
            RunBounds.Clamp(r);
            Assert.AreEqual(3, r.gems);
            r.gems = -1;
            Assert.AreEqual("negative", RunBounds.HardViolation(r));
        }

        [Test]
        public void LocalSaveCreditsGemsUpToTheDailyCap()
        {
            var d = new SaveData();
            int day = 20261123, total = 0;
            for (int i = 0; i < 6; i++) total += Progress.Apply(d, new RunSummary { Distance = 4000, Gems = 5 }, day, 1).Gems;
            Assert.AreEqual(RoadGems.DailyCap, total);
            Assert.AreEqual(RoadGems.DailyCap, d.gems);
            Assert.AreEqual(5, Progress.Apply(d, new RunSummary { Distance = 4000, Gems = 5 }, day + 1, 1).Gems, "a new day resets the cap");
            Assert.AreEqual(0, Progress.Apply(d, new RunSummary { Distance = 200, Gems = 4 }, day + 1, 1).Gems, "short runs can't hold gems");
        }

        [Test]
        public void GemModelIsSmallAndBright()
        {
            var m = PropBuilder.Gem();
            Assert.Less(m.Positions.Count, 600);
            CollectionAssert.Contains(m.SubmeshKeys, "glow");
            var marker = PropBuilder.GasMarker();
            CollectionAssert.Contains(marker.SubmeshKeys, "bubble"); // light beam
            CollectionAssert.Contains(PropBuilder.GasStation(3.2f, 2.7f, 18f).SubmeshKeys, "utexa:gas_label"); // GAS painted on the road
        }
    }
}
