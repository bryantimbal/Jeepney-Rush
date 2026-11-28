using System;
using JeepneyRush.Logic;

namespace JeepneyRush
{
    /// <summary>Pasada Pass for the UI: the server when online, else the save file (same rules as Logic/PasadaPass).</summary>
    public static class PassProgress
    {
        private static bool Online => Services.Online != null && Services.Online.Configured;
        private static int Season => PasadaPass.Season(PhilippineTime.Now);

        private static SaveData Fresh()
        {
            var d = Services.Save.Data;
            if (d.passSeason != Season) { d.passSeason = Season; d.passXp = 0; d.passPremium = false; d.passClaimedFree = d.passClaimedPremium = 0; }
            return d;
        }

        public static void AddRunLocal(float distance, int deliveries)
        {
            var d = Fresh();
            d.passXp += PasadaPass.RunXp(distance, deliveries);
            Services.Save.Save();
        }

        public static void Status(Action<PassInfo, string> done)
        {
            if (Online) { if (Services.Online.SignedIn) Services.Online.PassStatus(done); else done(null, "Sign in under ACCOUNT first."); return; }
            var d = Fresh();
            done(new PassInfo { season = Season, xp = d.passXp, tier = PasadaPass.Tier(d.passXp), days_left = PasadaPass.DaysLeft(PhilippineTime.Now),
                premium = d.passPremium, claimed_free = d.passClaimedFree, claimed_premium = d.passClaimedPremium }, null);
        }

        public static void BuyPremium(Action<bool, string, int> done)
        {
            if (Online) { Services.Online.PassBuyPremium(done); return; }
            var d = Fresh();
            if (d.passPremium) { done(false, "Owned na.", 0); return; }
            if (d.gems < PasadaPass.PremiumGems) { done(false, "Kulang ang gems.", 0); return; }
            d.gems -= PasadaPass.PremiumGems; d.passPremium = true;
            Services.Save.Save();
            done(true, null, 0);
        }

        public static void Claim(int tier, bool premium, Action<bool, string, int> done)
        {
            if (Online) { Services.Online.PassClaim(tier, premium, done); return; }
            var d = Fresh();
            long bit = 1L << tier;
            if (tier < 1 || tier > PasadaPass.Tier(d.passXp)) { done(false, "Hindi pa tapos.", 0); return; }
            if (premium && !d.passPremium) { done(false, "Unlock the premium pass first.", 0); return; }
            if (((premium ? d.passClaimedPremium : d.passClaimedFree) & bit) != 0) { done(false, "Na-claim na.", 0); return; }
            if (premium) d.passClaimedPremium |= bit; else d.passClaimedFree |= bit;
            var (kind, amount, id) = PasadaPass.Reward(tier, premium);
            if (kind == EventRewardKind.Coins) d.coins += amount;
            else if (kind == EventRewardKind.Gems) d.gems += amount;
            else if (!d.ownedCosmetics.Contains(id)) d.ownedCosmetics.Add(id);
            Services.Save.Save();
            done(true, null, kind == EventRewardKind.Coins ? amount : 0);
        }
    }
}
