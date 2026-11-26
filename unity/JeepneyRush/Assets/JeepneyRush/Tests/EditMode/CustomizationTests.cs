using System.Collections.Generic;
using JeepneyRush.Logic;
using JeepneyRush.Logic.Geometry;
using NUnit.Framework;

namespace JeepneyRush.Tests
{
    public class CustomizationTests
    {
        [Test]
        public void CatalogIsWellFormed()
        {
            var ids = new HashSet<string>();
            foreach (var c in Customization.All)
            {
                Assert.IsTrue(ids.Add(c.Id), "unique " + c.Id);
                Assert.That(c.Id.Length, Is.LessThanOrEqualTo(32));
                Assert.GreaterOrEqual(c.Price, 0);
                if (c.Earned) Assert.That(c.RankNeeded, Is.InRange(1, Career.Licenses.Length - 1), c.Id);
            }
            foreach (var s in Customization.Slots)
            {
                int stock = 0;
                foreach (var c in Customization.ForSlot(s)) if (c.IsStock) stock++;
                Assert.AreEqual(1, stock, "one stock item per slot: " + s);
            }
        }

        [Test]
        public void BuyEquipAndEarn()
        {
            var d = SaveValidation.Sanitize(new SaveData { coins = 1000 });
            var blue = Customization.Get("paint_asul");
            Assert.IsFalse(Customization.Equip(d, "pilipinas", blue), "not owned yet");
            Assert.IsTrue(Customization.Buy(d, blue));
            Assert.AreEqual(1000 - blue.Price, d.coins);
            Assert.IsFalse(Customization.Buy(d, blue), "bought once");
            Assert.IsTrue(Customization.Equip(d, "pilipinas", blue));
            Assert.AreEqual("paint_asul", Customization.Effective(d, "pilipinas").paint);
            Assert.AreEqual("", Customization.Effective(d, "sanjuan").paint, "each jeepney has its own setup");
            Assert.IsTrue(Customization.Equip(d, "sanjuan", blue), "a bought item works on every jeepney");
            var gold = Customization.Get("paint_ginto");
            Assert.IsFalse(Customization.Buy(d, gold), "earned items can't be bought");
            Assert.IsFalse(Customization.Owns(d, gold));
            d.careerRank = 4;
            Assert.IsTrue(Customization.Owns(d, gold), "Hari ng Kalsada license");
            Assert.IsTrue(Customization.Equip(d, "pilipinas", Customization.Get("paint_stock")));
            Assert.AreEqual("", Customization.LookFor(d, "pilipinas").paint, "stock clears the slot");
        }

        [Test]
        public void UnownedItemsFallBackToStock()
        {
            var d = SaveValidation.Sanitize(new SaveData());
            d.looks.Add(new JeepneyLook { jeepney = "pilipinas", paint = "paint_asul", horn = "horn_air", ornament = "nope" });
            var e = Customization.Effective(d, "pilipinas");
            Assert.IsTrue(e.IsStockModel, "an edited save can't wear what it doesn't own");
            Assert.AreEqual(HornKind.Classic, Customization.HornOf(d, "pilipinas"));
        }

        [Test]
        public void CosmeticsNeverChangeStats()
        {
            var look = new JeepneyLook { jeepney = "gold", paint = "paint_itim", ornament = "orn_five", lights = "lights_fog", sign = "sign_quiapo", mudflap = "mud_mahal" };
            var L = JeepneyCatalog.Get("gold");
            var c = Customization.Apply(L, look);
            Assert.AreEqual(L.SpeedBonus, c.SpeedBonus);
            Assert.AreEqual(L.CapacityBonus, c.CapacityBonus);
            Assert.AreEqual(JeepneyClasses.For(L), JeepneyClasses.For(c));
            Assert.AreEqual("cos_sign_quiapo", c.SignTexture);
            Assert.AreEqual("jeepney_sign_gold", L.SignTexture, "the catalog entry is untouched");
            Assert.AreEqual(OrnamentKind.Horses, L.Ornament);
        }

        [Test]
        public void EveryItemBuildsOnEveryStyle()
        {
            foreach (var id in new[] { "pilipinas", "modern" })
                foreach (var item in Customization.All)
                {
                    var look = new JeepneyLook { jeepney = id };
                    look.Set(item.Slot, item.Id);
                    var mesh = JeepneyBuilder.Build(Customization.Apply(JeepneyCatalog.Get(id), look));
                    Assert.Greater(mesh.TriangleCount, 1000, item.Id);
                }
        }

        [Test]
        public void HornsSoundDifferent()
        {
            var seen = new HashSet<int>();
            foreach (HornKind k in System.Enum.GetValues(typeof(HornKind)))
            {
                var s = JeepneyRush.Logic.Audio.SoundBank.Make(Customization.HornSound(k));
                Assert.Greater(s.Length, 1000, k.ToString());
                Assert.IsTrue(seen.Add(s.Length * 31 + (int)(s[s.Length / 3] * 1000)), "distinct " + k);
            }
        }
    }
}
