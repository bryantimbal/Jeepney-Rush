using System;
using System.Collections.Generic;

namespace JeepneyRush.Logic
{
    public enum ProductKind { Consumable, NonConsumable }

    /// <summary>An in-app product sold on Google Play (ids must match the Play Console).</summary>
    public sealed class ProductDef
    {
        public string Id;
        public ProductKind Kind;
        public string Title;
        public string Description;
        public int Coins;
        public int Gems;
        public string[] Jeepneys = new string[0];
        public bool AdFree;
        /// <summary>Shown only until the store returns the localized price.</summary>
        public string FallbackPrice;
    }

    /// <summary>
    /// The store catalog. The same definitions are exported to the server (Supabase), which is
    /// the only place that grants what a verified purchase contains.
    /// </summary>
    public static class Products
    {
        public static readonly ProductDef[] All =
        {
            new ProductDef { Id = "jr.coins.small", Kind = ProductKind.Consumable, Title = "Bag of Coins", Description = "5,000 coins", Coins = 5000, FallbackPrice = "₱49" },
            new ProductDef { Id = "jr.coins.medium", Kind = ProductKind.Consumable, Title = "Coin Sack", Description = "15,000 coins", Coins = 15000, FallbackPrice = "₱129" },
            new ProductDef { Id = "jr.coins.large", Kind = ProductKind.Consumable, Title = "Coin Jeepney-load", Description = "40,000 coins", Coins = 40000, FallbackPrice = "₱299" },
            new ProductDef { Id = "jr.gems.small", Kind = ProductKind.Consumable, Title = "Handful of Gems", Description = "100 gems", Gems = 100, FallbackPrice = "₱49" },
            new ProductDef { Id = "jr.gems.medium", Kind = ProductKind.Consumable, Title = "Gem Pouch", Description = "550 gems (+10%)", Gems = 550, FallbackPrice = "₱249" },
            new ProductDef { Id = "jr.gems.large", Kind = ProductKind.Consumable, Title = "Gem Chest", Description = "1,200 gems (+20%)", Gems = 1200, FallbackPrice = "₱499" },
            new ProductDef { Id = "jr.pack.fiesta", Kind = ProductKind.NonConsumable, Title = "Fiesta Pack", Description = "Fiesta Edition + Philippine Flag Edition jeepneys and 5,000 coins", Coins = 5000, Jeepneys = new[] { "fiesta", "flag" }, FallbackPrice = "₱199" },
            new ProductDef { Id = "jr.pack.premium", Kind = ProductKind.NonConsumable, Title = "Premium Skins", Description = "Gold VIP + Neon Street Racer jeepneys", Jeepneys = new[] { "gold", "neon" }, FallbackPrice = "₱249" },
            new ProductDef { Id = "jr.adfree", Kind = ProductKind.NonConsumable, Title = "No Ads", Description = "Removes banner and between-run ads forever (optional reward ads stay available)", AdFree = true, FallbackPrice = "₱149" },
        };

        public static ProductDef Get(string id) => Array.Find(All, p => p.Id == id);
    }

    public enum RewardedKind { Revive, DoubleCoins, DailyBonus, Trial }

    /// <summary>Per-player ad bookkeeping (saved locally; rewards themselves are granted by the server).</summary>
    [Serializable]
    public class AdState
    {
        public long lastInterstitialUnix;
        public int runsSinceInterstitial;
        public int totalRunsEnded;
        public int dailyBonusDayKey;
        public int trialDayKey;
        public List<string> trialsToday = new List<string>();
    }

    /// <summary>
    /// When ads may appear. Hard rules from the design brief: never during gameplay, never
    /// forced except a capped interstitial BETWEEN runs, rewarded ads are always optional.
    /// </summary>
    public static class AdPolicy
    {
        public const int FirstInterstitialAfterRuns = 3;   // a new player's first runs are ad-free
        public const int RunsBetweenInterstitials = 3;
        public const int SecondsBetweenInterstitials = 180;
        public const float MinRunSecondsForInterstitial = 25f; // never right after a very short run
        public const float MinReviveDistance = 150f;
        public const int DailyBonusCoins = 150;
        public const int MaxDoubleCoins = 5000;
        public const int TrialsPerDay = 3;

        public static bool BannerAllowed(bool adFree, bool inGameplay) => !adFree && !inGameplay;

        /// <summary>Call when a run ended; returns whether an interstitial may be shown before the next screen.</summary>
        public static bool InterstitialDue(AdState s, bool adFree, long nowUnix, float runSeconds)
        {
            if (adFree || s == null) return false;
            if (s.totalRunsEnded < FirstInterstitialAfterRuns) return false;
            if (s.runsSinceInterstitial < RunsBetweenInterstitials) return false;
            if (nowUnix - s.lastInterstitialUnix < SecondsBetweenInterstitials) return false;
            return runSeconds >= MinRunSecondsForInterstitial;
        }

        public static void RunEnded(AdState s) { s.totalRunsEnded++; s.runsSinceInterstitial++; }

        public static void InterstitialShown(AdState s, long nowUnix) { s.lastInterstitialUnix = nowUnix; s.runsSinceInterstitial = 0; }

        public static bool CanRevive(bool revivedThisRun, float distance) => !revivedThisRun && distance >= MinReviveDistance;

        public static bool CanDoubleCoins(int runCoins, bool alreadyDoubled) => runCoins > 0 && !alreadyDoubled;

        public static bool DailyBonusAvailable(AdState s, int dayKey) => s != null && s.dailyBonusDayKey != dayKey;

        public static bool CanTrial(AdState s, int dayKey, string jeepney, bool owned)
        {
            if (owned || s == null) return false;
            if (s.trialDayKey != dayKey) return true;
            return s.trialsToday.Count < TrialsPerDay && !s.trialsToday.Contains(jeepney);
        }

        public static void TrialUsed(AdState s, int dayKey, string jeepney)
        {
            if (s.trialDayKey != dayKey) { s.trialDayKey = dayKey; s.trialsToday.Clear(); }
            s.trialsToday.Add(jeepney);
        }

        /// <summary>Custom data sent with a rewarded ad so the server-side verification knows what it was for.</summary>
        public static string RewardData(RewardedKind kind, string reference) => kind.ToString().ToLowerInvariant() + ":" + (reference ?? "");

        /// <summary>
        /// Custom data with the client's claim id: "kind:ref:nonce". It is part of the message Google
        /// signs, so the server can store the reward under the nonce the game later claims.
        /// </summary>
        public static string RewardData(RewardedKind kind, string reference, string nonce) => RewardData(kind, reference) + ":" + nonce;

        /// <summary>Claim id for a simulated (development) ad: "test-&lt;id&gt;:kind:ref" (server accepts only if allowed).</summary>
        public static string TestNonce(RewardedKind kind, string reference) => "test-" + Guid.NewGuid().ToString("N") + ":" + RewardData(kind, reference);
    }
}
