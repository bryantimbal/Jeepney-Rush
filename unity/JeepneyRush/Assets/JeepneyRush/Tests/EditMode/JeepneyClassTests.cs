using JeepneyRush.Logic;
using NUnit.Framework;

namespace JeepneyRush.Tests
{
    public class JeepneyClassTests
    {
        [Test]
        public void FreeJeepneysAreStandard()
        {
            foreach (var j in JeepneyCatalog.All)
                if (j.Price == 0 && j.GemPrice == 0)
                    Assert.AreEqual(JeepneyClass.Standard, JeepneyClasses.For(j), j.Id);
        }

        [Test]
        public void ClassesFollowStatPower()
        {
            Assert.AreEqual(JeepneyClass.Standard, JeepneyClasses.For("pilipinas"));
            Assert.AreEqual(JeepneyClass.Standard, JeepneyClasses.For("fiesta"));
            Assert.AreEqual(JeepneyClass.Sport, JeepneyClasses.For("neon"));
            Assert.AreEqual(JeepneyClass.Sport, JeepneyClasses.For("antipolo"));
            Assert.AreEqual(JeepneyClass.Elite, JeepneyClasses.For("gold"));
            // a stronger jeepney is never in a lower class
            foreach (var a in JeepneyCatalog.All)
                foreach (var b in JeepneyCatalog.All)
                    if (JeepneyClasses.Power(a) > JeepneyClasses.Power(b))
                        Assert.GreaterOrEqual((int)JeepneyClasses.For(a), (int)JeepneyClasses.For(b), a.Id + " vs " + b.Id);
        }

        [Test]
        public void EveryClassHasAJeepney()
        {
            var seen = new bool[JeepneyClasses.Count];
            foreach (var j in JeepneyCatalog.All) seen[(int)JeepneyClasses.For(j)] = true;
            for (int c = 0; c < JeepneyClasses.Count; c++) Assert.IsTrue(seen[c], JeepneyClasses.Name((JeepneyClass)c));
        }

        [Test]
        public void ServerCatalogCarriesTheClass()
        {
            string sql = CatalogExport.Sql();
            StringAssert.Contains("values ('gold', 'Gold VIP Jeepney', 8000, 500, 2);", sql);
            StringAssert.Contains("values ('pilipinas', 'Pilipinas Classic', 0, 0, 0);", sql);
            Assert.AreEqual("week:c1", JeepneyClasses.BoardKey("week", JeepneyClass.Sport));
        }
    }
}
