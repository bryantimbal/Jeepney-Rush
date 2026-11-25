using JeepneyRush.Logic;
using NUnit.Framework;

namespace JeepneyRush.Tests
{
    public class TrafficFollowTests
    {
        private static readonly BalanceConfig Cfg = new BalanceConfig();

        [Test]
        public void StoppingAtAStopOnlyHoldsUpTheCarDirectlyBehind()
        {
            float player = Cfg.LaneX(2);      // stopped at a PARA stop in the curb lane
            float car = HazardCatalog.Get(HazardKind.Sedan).HalfWidth;
            // same lane, behind: waits (never drives into the jeepney)
            Assert.IsTrue(FollowRules.BlockedBehind(Cfg.LaneX(2), car, player, Cfg.playerHalfWidth));
            Assert.AreEqual(0f, FollowRules.EffectiveSpeed(7f, -12f, 0f, true));
            // other lanes, behind: keep driving at their own speed
            Assert.IsFalse(FollowRules.BlockedBehind(Cfg.LaneX(1), car, player, Cfg.playerHalfWidth));
            Assert.IsFalse(FollowRules.BlockedBehind(Cfg.LaneX(0), car, player, Cfg.playerHalfWidth));
            Assert.AreEqual(7f, FollowRules.EffectiveSpeed(7f, -12f, 0f, false));
            // ahead of the jeepney nothing changes
            Assert.AreEqual(7f, FollowRules.EffectiveSpeed(7f, 20f, 0f, true));
            // oncoming traffic (negative speed) is never held
            Assert.AreEqual(-6f, FollowRules.EffectiveSpeed(-6f, -12f, 0f, true));
            // straddling two lanes: cars behind in both of them wait
            float between = (Cfg.LaneX(1) + Cfg.LaneX(2)) / 2;
            Assert.IsTrue(FollowRules.BlockedBehind(Cfg.LaneX(1), car, between, Cfg.playerHalfWidth));
            Assert.IsTrue(FollowRules.BlockedBehind(Cfg.LaneX(2), car, between, Cfg.playerHalfWidth));
        }
    }
}
