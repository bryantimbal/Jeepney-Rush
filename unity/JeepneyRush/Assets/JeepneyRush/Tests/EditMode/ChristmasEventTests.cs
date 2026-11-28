using System;
using JeepneyRush.Logic;
using NUnit.Framework;

namespace JeepneyRush.Tests
{
    public class ChristmasEventTests
    {
        [Test]
        public void EventWindow()
        {
            Assert.IsFalse(ChristmasEvent.Active(new DateTime(2026, 11, 30)));
            Assert.IsTrue(ChristmasEvent.Active(new DateTime(2026, 12, 1)));
            Assert.IsTrue(ChristmasEvent.Active(new DateTime(2027, 1, 6, 23, 0, 0)));
            Assert.IsFalse(ChristmasEvent.Active(new DateTime(2027, 1, 7)));
            Assert.AreEqual(2026, ChristmasEvent.Season(new DateTime(2027, 1, 3)));
            Assert.AreEqual(0, ChristmasEvent.SimbangGabiNight(new DateTime(2026, 12, 15)));
            Assert.AreEqual(1, ChristmasEvent.SimbangGabiNight(new DateTime(2026, 12, 16)));
            Assert.AreEqual(9, ChristmasEvent.SimbangGabiNight(new DateTime(2026, 12, 24)));
            Assert.AreEqual(0, ChristmasEvent.SimbangGabiNight(new DateTime(2026, 12, 25)));
        }

        [Test]
        public void TokensAreCapped()
        {
            Assert.AreEqual(0, ChristmasEvent.RunTokens(10, 0, 50f));
            Assert.AreEqual(9, ChristmasEvent.RunTokens(99, 0, 1000f), "by distance");
            Assert.AreEqual(25 + 6, ChristmasEvent.RunTokens(99, 99, 99999f), "run cap + 3 carolers");
            int tokens = 0, day = 0, dayTokens = 0;
            Assert.AreEqual(100, ChristmasEvent.AddTokens(ref tokens, ref day, ref dayTokens, 20261201, 100));
            Assert.AreEqual(20, ChristmasEvent.AddTokens(ref tokens, ref day, ref dayTokens, 20261201, 100), "daily cap");
            Assert.AreEqual(30, ChristmasEvent.AddTokens(ref tokens, ref day, ref dayTokens, 20261202, 30), "new day");
            Assert.AreEqual(150, tokens);
        }

        [Test]
        public void SimbangGabi()
        {
            var night = new DateTime(2026, 12, 18, 21, 0, 0);
            Assert.IsTrue(ChristmasEvent.LightsNight(night, "night", 1600f));
            Assert.IsFalse(ChristmasEvent.LightsNight(night, "", 5000f), "Night Shift only");
            Assert.IsFalse(ChristmasEvent.LightsNight(night, "night", 900f), "long enough");
            Assert.AreEqual(9, ChristmasEvent.CountNights(ChristmasEvent.AllNights));
        }

        [Test]
        public void RewardsAreOrderedAndExist()
        {
            for (int i = 1; i < ChristmasEvent.Rewards.Length; i++) Assert.Greater(ChristmasEvent.Rewards[i].Need, ChristmasEvent.Rewards[i - 1].Need);
            foreach (var r in ChristmasEvent.Rewards)
            {
                if (r.Kind == EventRewardKind.Cosmetic) Assert.IsNotNull(Customization.Get(r.Id), r.Id);
                if (r.Kind == EventRewardKind.Jeepney) { Assert.IsTrue(JeepneyCatalog.Exists(r.Id)); Assert.IsTrue(JeepneyCatalog.Get(r.Id).EventOnly); }
            }
            Assert.IsNotNull(Customization.Get(ChristmasEvent.GoldenParol.Id));
            // event cosmetics can't be bought, and nobody owns them until the event grants them
            var d = new SaveData();
            foreach (var id in new[] { "horn_jingle", "orn_parol", "rear_belen", "sign_goldenparol" })
            {
                var c = Customization.Get(id);
                Assert.IsTrue(c.Earned && !c.IsStock, id);
                Assert.IsFalse(Customization.Owns(d, c), id);
                d.ownedCosmetics.Add(id);
                Assert.IsTrue(Customization.Owns(d, c), id + " once granted");
            }
        }
    }
}
