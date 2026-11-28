using System.Linq;
using JeepneyRush.Logic;
using NUnit.Framework;

namespace JeepneyRush.Tests
{
    public class JeepneyPerkTests
    {
        private static JeepneyLivery J(string id) => JeepneyCatalog.Get(id);

        [Test]
        public void SixModernPuvsWithTheirPerks()
        {
            Assert.AreEqual(JeepneyPerk.Aircon, J("aircon").Perk);
            Assert.AreEqual(JeepneyPerk.Electric, J("ejeep").Perk);
            Assert.AreEqual(JeepneyPerk.BigCapacity, J("minibus").Perk);
            Assert.AreEqual(JeepneyPerk.Heritage, J("sarao").Perk);
            Assert.AreEqual(JeepneyPerk.Airport, J("airport").Perk);
            Assert.AreEqual(JeepneyPerk.NightOwl, J("nightowl").Perk);
            Assert.AreEqual(8, J("minibus").CapacityBonus, "the minibus has the most seats");
            Assert.IsTrue(JeepneyCatalog.All.Where(j => j.Perk != JeepneyPerk.None).All(j => j.ClosedBody), "all new PUVs are closed aircon bodies");
            Assert.IsTrue(JeepneyCatalog.All.All(j => j.Price > 0 || j.Id == "pilipinas" || j.Id == "sanjuan"), "price 0 would make it free");
        }

        [Test]
        public void ModernJeepneysAreCleanerAndQuieter()
        {
            Assert.AreEqual(1f, JeepneyPerks.Smoke(J("pilipinas")));
            Assert.Less(JeepneyPerks.Smoke(J("modern")), 1f, "the old modern jeep too");
            Assert.Less(JeepneyPerks.Smoke(J("sarao")), 1f, "retro front, modern engine");
            Assert.AreEqual(0f, JeepneyPerks.Smoke(J("ejeep")), "electric: no smoke");
            Assert.Less(JeepneyPerks.EngineVolume(J("ejeep")), JeepneyPerks.EngineVolume(J("aircon")));
            Assert.Less(JeepneyPerks.EngineVolume(J("aircon")), JeepneyPerks.EngineVolume(J("pilipinas")));
        }

        [Test]
        public void PerkNumbers()
        {
            Assert.AreEqual(0.5f, JeepneyPerks.FuelPrice(J("ejeep")));
            Assert.AreEqual(1f, JeepneyPerks.FuelPrice(J("aircon")));
            Assert.Greater(JeepneyPerks.Patience(J("aircon")), 1f);
            Assert.AreEqual(1.1f, JeepneyPerks.FareFactor(J("nightowl"), true), 1e-5);
            Assert.AreEqual(1f, JeepneyPerks.FareFactor(J("nightowl"), false));
            Assert.AreEqual(2f, JeepneyPerks.BalikbayanBoost(J("airport")));
            Assert.Greater(JeepneyPerks.TipMultiplier(J("sarao")), 1f);
            Assert.IsNotNull(JeepneyPerks.Text(J("ejeep")));
            Assert.IsNull(JeepneyPerks.Text(J("pilipinas")));
        }

        [Test]
        public void ElectricFuelIsHalfPrice()
        {
            var cfg = new BalanceConfig();
            var full = FuelRules.Quote(0, 100, 100000, cfg);
            var half = FuelRules.Quote(0, 100, 100000, cfg, 0.5f);
            Assert.AreEqual(full.Paid, half.Paid, 0.01f);
            Assert.AreEqual(full.Cost / 2f, half.Cost, 1.01f);
        }

        [Test]
        public void AirportBringsMoreBalikbayans()
        {
            int Count(float boost)
            {
                var rng = new GameRandom(5); int n = 0;
                for (int i = 0; i < 20000; i++) if (Riders.Pick(rng, EnvironmentKind.Manila, false, 3, false, boost) == RiderKind.Balikbayan) n++;
                return n;
            }
            int normal = Count(1f), airport = Count(2f);
            Assert.Greater(airport, normal * 1.6f, $"{airport} vs {normal}");
        }
    }
}
