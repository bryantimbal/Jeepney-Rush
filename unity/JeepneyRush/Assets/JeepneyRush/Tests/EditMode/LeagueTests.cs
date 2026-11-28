using JeepneyRush.Logic;
using NUnit.Framework;

namespace JeepneyRush.Tests
{
    public class LeagueTests
    {
        [Test]
        public void PromotionAndDemotion()
        {
            Assert.AreEqual(1, Leagues.NextTier(0, 1, 30));
            Assert.AreEqual(1, Leagues.NextTier(0, 7, 30));
            Assert.AreEqual(0, Leagues.NextTier(0, 8, 30));
            Assert.AreEqual(0, Leagues.NextTier(0, 30, 30), "Bronze can't drop");
            Assert.AreEqual(1, Leagues.NextTier(2, 26, 30), "bottom 5 drop");
            Assert.AreEqual(2, Leagues.NextTier(2, 25, 30));
            Assert.AreEqual(2, Leagues.NextTier(2, 9, 9), "small group: no demotion");
            Assert.AreEqual(4, Leagues.NextTier(4, 1, 30), "top tier stays");
        }

        [Test]
        public void Rewards()
        {
            Assert.AreEqual(500, Leagues.Reward(0, 1));
            Assert.AreEqual(1500, Leagues.Reward(4, 1), "Hari ng Kalsada pays 3x");
            Assert.AreEqual(100, Leagues.Reward(0, 7));
            Assert.AreEqual(0, Leagues.Reward(0, 8));
        }
    }
}
