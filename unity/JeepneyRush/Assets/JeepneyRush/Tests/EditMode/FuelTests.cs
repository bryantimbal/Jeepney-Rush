using JeepneyRush.Logic;
using NUnit.Framework;

namespace JeepneyRush.Tests
{
    public class FuelTests
    {
        [Test]
        public void TankStartsFullAndBurnsMoreOnGas()
        {
            var c = new BalanceConfig();
            var a = new FuelTank(c);
            var b = new FuelTank(c);
            Assert.AreEqual(1f, a.Fraction);
            a.Burn(100, false, 0);
            b.Burn(100, true, 0);
            Assert.Less(b.Fuel, a.Fuel);
            Assert.AreEqual(c.fuelCapacity - 100 * c.fuelPerMetre, a.Fuel, 1e-3);
        }

        [Test]
        public void IdlingAndReversingUseFuelToo()
        {
            var c = new BalanceConfig();
            var t = new FuelTank(c);
            t.Burn(0, false, 10);
            Assert.Less(t.Fuel, c.fuelCapacity);
            float before = t.Fuel;
            t.Burn(-5, false, 0);
            Assert.Less(t.Fuel, before);
        }

        [Test]
        public void TankRunsDryAndLowWarning()
        {
            var c = new BalanceConfig();
            var t = new FuelTank(c);
            t.Burn(c.fuelCapacity / c.fuelPerMetre * 0.8f, false, 0);
            Assert.IsTrue(t.Low);
            Assert.IsFalse(t.Empty);
            t.Burn(100000, true, 0);
            Assert.IsTrue(t.Empty);
            Assert.AreEqual(0f, t.Fuel);
        }

        [Test]
        public void FullTankOutlastsTheGapBetweenStations()
        {
            var c = new BalanceConfig();
            Assert.Greater(FuelTank.RangeOnGas(c), c.stationSpacingMax + 200f);
            Assert.Greater(FuelTank.RangeOnGas(c), c.firstStationAt + 150f + 200f);
        }

        [Test]
        public void RefuelCostsCoins()
        {
            var c = new BalanceConfig();
            var q = FuelRules.Quote(40, 100, 1000, c);
            Assert.AreEqual(60f, q.Total, 1e-3);
            Assert.AreEqual((int)System.Math.Ceiling(60 * c.fuelPricePerLitre), q.Cost);
            Assert.AreEqual(0f, q.Credit);
        }

        [Test]
        public void BrokeDriverGetsCreditUpToTheFloor()
        {
            var c = new BalanceConfig();
            var q = FuelRules.Quote(10, 100, 0, c);
            Assert.AreEqual(0, q.Cost);
            Assert.AreEqual(100 * c.fuelCreditFraction - 10, q.Credit, 1e-3);
            var partial = FuelRules.Quote(10, 100, 8, c);
            Assert.LessOrEqual(partial.Cost, 8);
            Assert.AreEqual(100 * c.fuelCreditFraction, 10 + partial.Total, 1e-3);
        }

        [Test]
        public void StationPadUsesItsOwnLength()
        {
            var c = new BalanceConfig();
            float x = c.LaneX(2);
            Assert.IsTrue(StopRules.InZone(c.stationZoneLength / 2f + c.playerHalfLength, x, x, c, c.stationZoneLength));
            Assert.IsFalse(StopRules.InZone(c.stationZoneLength / 2f + c.playerHalfLength + c.zoneSlack + 1f, x, x, c, c.stationZoneLength));
        }
    }
}
