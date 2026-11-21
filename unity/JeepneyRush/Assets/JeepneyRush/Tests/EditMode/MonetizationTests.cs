using System.Linq;
using JeepneyRush.Logic;
using NUnit.Framework;

namespace JeepneyRush.Tests
{
    public class MonetizationTests
    {
        [Test]
        public void NoInterstitialsForNewPlayersOrTooOften()
        {
            var s = new AdState();
            long now = 1_000_000;
            for (int i = 0; i < AdPolicy.FirstInterstitialAfterRuns - 1; i++) { AdPolicy.RunEnded(s); Assert.IsFalse(AdPolicy.InterstitialDue(s, false, now, 90), "first runs are ad-free"); }
            AdPolicy.RunEnded(s);
            Assert.IsTrue(AdPolicy.InterstitialDue(s, false, now, 90));
            Assert.IsFalse(AdPolicy.InterstitialDue(s, true, now, 90), "ad-free purchase");
            Assert.IsFalse(AdPolicy.InterstitialDue(s, false, now, 10), "not after a very short run");
            AdPolicy.InterstitialShown(s, now);
            for (int i = 0; i < AdPolicy.RunsBetweenInterstitials; i++) AdPolicy.RunEnded(s);
            Assert.IsFalse(AdPolicy.InterstitialDue(s, false, now + 60, 90), "at least 3 minutes apart");
            Assert.IsTrue(AdPolicy.InterstitialDue(s, false, now + AdPolicy.SecondsBetweenInterstitials, 90));
            s.runsSinceInterstitial = 1;
            Assert.IsFalse(AdPolicy.InterstitialDue(s, false, now + 9999, 90), "at most every 3 runs");
        }

        [Test]
        public void BannerNeverDuringGameplay()
        {
            Assert.IsFalse(AdPolicy.BannerAllowed(false, true));
            Assert.IsTrue(AdPolicy.BannerAllowed(false, false));
            Assert.IsFalse(AdPolicy.BannerAllowed(true, false));
        }

        [Test]
        public void RewardedRules()
        {
            Assert.IsTrue(AdPolicy.CanRevive(false, 500));
            Assert.IsFalse(AdPolicy.CanRevive(true, 500), "one revive per run");
            Assert.IsFalse(AdPolicy.CanRevive(false, 50), "not right at the start");
            var s = new AdState();
            Assert.IsTrue(AdPolicy.DailyBonusAvailable(s, 20261121));
            s.dailyBonusDayKey = 20261121;
            Assert.IsFalse(AdPolicy.DailyBonusAvailable(s, 20261121));
            Assert.IsTrue(AdPolicy.DailyBonusAvailable(s, 20261122));
            Assert.IsFalse(AdPolicy.CanTrial(s, 1, "gold", owned: true));
            for (int i = 0; i < AdPolicy.TrialsPerDay; i++) { Assert.IsTrue(AdPolicy.CanTrial(s, 1, "j" + i, false)); AdPolicy.TrialUsed(s, 1, "j" + i); }
            Assert.IsFalse(AdPolicy.CanTrial(s, 1, "x", false), "3 trials a day");
            Assert.IsTrue(AdPolicy.CanTrial(s, 2, "x", false), "resets next day");
            Assert.AreEqual("doublecoins:run-1", AdPolicy.RewardData(RewardedKind.DoubleCoins, "run-1"));
        }

        [Test]
        public void CatalogIsConsistent()
        {
            Assert.AreEqual(Products.All.Length, Products.All.Select(p => p.Id).Distinct().Count());
            foreach (var p in Products.All)
            {
                Assert.IsTrue(p.Id.StartsWith("jr."), p.Id);
                foreach (var j in p.Jeepneys) Assert.IsTrue(JeepneyCatalog.Exists(j), $"{p.Id}: unknown jeepney {j}");
                Assert.IsTrue(p.Coins > 0 || p.Gems > 0 || p.Jeepneys.Length > 0 || p.AdFree, p.Id + " gives nothing");
                if (p.AdFree || p.Jeepneys.Length > 0) Assert.AreEqual(ProductKind.NonConsumable, p.Kind, p.Id);
            }
            Assert.IsTrue(Products.All.Any(p => p.AdFree));
            Assert.IsTrue(JeepneyCatalog.All.Any(j => j.GemPrice > 0));
        }

        [Test]
        public void GemsBuyPremiumJeepneysOnce()
        {
            var d = SaveValidation.Sanitize(new SaveData { gems = 600 });
            Assert.IsFalse(Progress.BuyJeepneyWithGems(d, "manila"), "coins-only jeepney");
            Assert.IsTrue(Progress.BuyJeepneyWithGems(d, "gold"));
            Assert.AreEqual(100, d.gems);
            Assert.IsFalse(Progress.BuyJeepneyWithGems(d, "gold"), "already owned");
            Assert.IsFalse(Progress.BuyJeepneyWithGems(d, "flag"), "not enough gems");
        }

        [Test]
        public void LocalTestGrantMatchesTheProduct()
        {
            var d = SaveValidation.Sanitize(new SaveData());
            Progress.GrantProductLocally(d, Products.Get("jr.pack.fiesta"));
            Progress.GrantProductLocally(d, Products.Get("jr.pack.fiesta"));
            Assert.AreEqual(5000, d.coins, "non-consumable only once");
            Assert.IsTrue(d.Owns("fiesta") && d.Owns("flag"));
            Progress.GrantProductLocally(d, Products.Get("jr.adfree"));
            Assert.IsTrue(d.adFree);
            Progress.GrantProductLocally(d, Products.Get("jr.gems.small"));
            Progress.GrantProductLocally(d, Products.Get("jr.gems.small"));
            Assert.AreEqual(200, d.gems, "consumables stack");
        }

        [Test]
        public void SnapshotCarriesGemsAndAdFree()
        {
            var d = new SaveData();
            ServerSync.Apply(d, new ServerSnapshot { gems = 42, ad_free = true, owned_products = new[] { "jr.adfree" }, owned = new[] { "pilipinas" } });
            Assert.AreEqual(42, d.gems);
            Assert.IsTrue(d.adFree);
            Assert.Contains("jr.adfree", d.ownedProducts);
        }
    }
}
