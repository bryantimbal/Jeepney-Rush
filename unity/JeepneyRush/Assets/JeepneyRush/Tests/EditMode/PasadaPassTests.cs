using System;
using JeepneyRush.Logic;
using NUnit.Framework;

namespace JeepneyRush.Tests
{
    public class PasadaPassTests
    {
        [Test]
        public void XpAndTiers()
        {
            Assert.AreEqual(0, PasadaPass.RunXp(50f, 0));
            Assert.AreEqual(20 + 10, PasadaPass.RunXp(2000f, 5));
            Assert.AreEqual(PasadaPass.MaxRunXp, PasadaPass.RunXp(99999f, 99), "capped per run");
            Assert.AreEqual(0, PasadaPass.Tier(99));
            Assert.AreEqual(7, PasadaPass.Tier(799));
            Assert.AreEqual(30, PasadaPass.Tier(999999));
            Assert.AreEqual(202612, PasadaPass.Season(new DateTime(2026, 12, 3)));
            Assert.AreEqual(1, PasadaPass.DaysLeft(new DateTime(2026, 11, 30, 10, 0, 0)));
        }

        [Test]
        public void Rewards()
        {
            Assert.AreEqual((EventRewardKind.Coins, 55, (string)null), PasadaPass.Reward(1, false));
            Assert.AreEqual((EventRewardKind.Gems, 5, (string)null), PasadaPass.Reward(5, false));
            Assert.AreEqual(EventRewardKind.Cosmetic, PasadaPass.Reward(30, true).kind);
            Assert.AreEqual(EventRewardKind.Cosmetic, PasadaPass.Reward(15, true).kind);
            foreach (var id in new[] { PasadaPass.PremiumPaint, PasadaPass.PremiumBanner })
            {
                var c = Customization.Get(id);
                Assert.IsNotNull(c, id);
                Assert.IsTrue(c.IsEvent, "pass items are never sold: " + id);
            }
        }
    }
}
