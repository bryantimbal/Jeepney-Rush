using System;

namespace JeepneyRush.Logic
{
    /// <summary>
    /// Pasada Pass (monthly season pass, PH calendar month): 30 tiers of <see cref="XpPerTier"/> pass XP.
    /// Every run gives XP (distance and deliveries, at most <see cref="MaxRunXp"/>). Each tier has a free
    /// reward and a premium reward; premium is unlocked for the month with <see cref="PremiumGems"/> gems.
    /// The server (jr_pass_*) computes XP from the runs it accepted, with the same formula.
    /// </summary>
    public static class PasadaPass
    {
        public const int Tiers = 30, XpPerTier = 100, MaxRunXp = 60, PremiumGems = 300;
        public const string PremiumPaint = "paint_pasada", PremiumBanner = "ws_pasada";

        public static int Season(DateTime ph) => ph.Year * 100 + ph.Month;
        public static int DaysLeft(DateTime ph) => (int)Math.Ceiling((new DateTime(ph.Year, ph.Month, 1).AddMonths(1) - ph).TotalDays);

        /// <summary>Pass XP for one run.</summary>
        public static int RunXp(float distance, int deliveries) => Math.Max(0, Math.Min(MaxRunXp, (int)(Math.Max(0f, distance) / 100f) + Math.Max(0, deliveries) * 2));

        public static int Tier(int xp) => Math.Max(0, Math.Min(Tiers, xp / XpPerTier));

        /// <summary>Reward of a tier (1..30): kind, amount (coins / gems) or cosmetic id.</summary>
        public static (EventRewardKind kind, int amount, string id) Reward(int tier, bool premium)
        {
            if (!premium) return tier % 5 == 0 ? (EventRewardKind.Gems, 5, null) : (EventRewardKind.Coins, 50 + tier * 5, null);
            if (tier == Tiers) return (EventRewardKind.Cosmetic, 0, PremiumPaint);
            if (tier == 15) return (EventRewardKind.Cosmetic, 0, PremiumBanner);
            return tier % 5 == 0 ? (EventRewardKind.Gems, 15, null) : (EventRewardKind.Coins, 150 + tier * 10, null);
        }

        public static string RewardText(int tier, bool premium)
        {
            var (k, a, id) = Reward(tier, premium);
            return k == EventRewardKind.Coins ? $"{a} coins" : k == EventRewardKind.Gems ? $"{a} gems" : Customization.Get(id)?.Name ?? id;
        }
    }
}
