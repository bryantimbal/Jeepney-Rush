using JeepneyRush.Logic;
using NUnit.Framework;

namespace JeepneyRush.Tests
{
    public class DriverStyleTests
    {
        [Test]
        public void StockLookAndOwnership()
        {
            var d = new SaveData();
            Assert.AreEqual("drv_sando", DriverStyle.Worn(d, DriverPart.Shirt).Id);
            d.driverShirt = "drv_barong";
            Assert.AreEqual("drv_sando", DriverStyle.Worn(d, DriverPart.Shirt).Id, "not owned yet");
            d.ownedCosmetics.Add("drv_barong");
            Assert.AreEqual("drv_barong", DriverStyle.Worn(d, DriverPart.Shirt).Id);
            Assert.IsFalse(DriverStyle.HasKonduktor(d));
            d.ownedCosmetics.Add(DriverStyle.KonduktorId);
            Assert.IsTrue(DriverStyle.HasKonduktor(d));
            d.konduktorOff = true;
            Assert.IsFalse(DriverStyle.HasKonduktor(d), "day off");
            foreach (var p in new[] { DriverPart.Shirt, DriverPart.Hat, DriverPart.Extra }) Assert.IsNotNull(DriverStyle.Stock(p), p.ToString());
        }

        [Test]
        public void IdsDontClashWithTalyerItems()
        {
            foreach (var i in DriverStyle.All) Assert.IsNull(Customization.Get(i.Id), i.Id);
        }
    }
}
