using JeepneyRush.Logic;
using NUnit.Framework;

namespace JeepneyRush.Tests
{
    public class JuiceTests
    {
        [Test]
        public void ComboBuildsWithinWindowAndResetsAfter()
        {
            var c = new CoinCombo();
            int bonus = 0;
            for (int i = 0; i < 10; i++) { bonus += c.Add(); c.Tick(0.2f); }
            Assert.AreEqual(10, c.Count);
            Assert.AreEqual(CoinCombo.MilestoneBonus(10), bonus);
            Assert.Greater(c.Pitch, 1.5f);
            c.Tick(CoinCombo.Window + 0.1f);
            Assert.AreEqual(0, c.Count);
            c.Add();
            Assert.AreEqual(1, c.Count);
            Assert.AreEqual(10, c.Best);
        }

        [Test]
        public void EnforcerFinesOnlySpeeders()
        {
            Assert.IsTrue(EnforcerRules.PassedSafely(8f, 20f));
            Assert.IsFalse(EnforcerRules.PassedSafely(18f, 20f));
            Assert.AreEqual(10, EnforcerRules.Fine(40));
            Assert.AreEqual(30, EnforcerRules.Fine(200));
            Assert.AreEqual(3, EnforcerRules.Fine(3)); // never more than you have
            Assert.AreEqual(0, EnforcerRules.Fine(0));
        }

        [Test]
        public void AngryPassengerLeavesAndBreaksStreak()
        {
            var r = new PassengerRoute(new BalanceConfig(), new GameRandom(1));
            Assert.AreEqual(0, r.EjectAngry());
            var a = r.Plan(100, -1, 0, 3);
            r.Plan(300, 1, 2, 0);
            r.Serve(a);
            Assert.AreEqual(1, r.EjectAngry());
            Assert.AreEqual(2, r.Count);
            Assert.AreEqual(0, r.Streak);
        }
    }
}
