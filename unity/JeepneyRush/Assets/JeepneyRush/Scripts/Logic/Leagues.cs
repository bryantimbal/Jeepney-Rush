using System;

namespace JeepneyRush.Logic
{
    /// <summary>
    /// Weekly leagues: each week a driver's first run puts them in a group of up to <see cref="GroupSize"/>
    /// drivers of the same tier, ranked by their best score that week (PH week, Monday reset). When the
    /// week is over the top <see cref="Promote"/> move up a tier and the bottom <see cref="Demote"/> move
    /// down (Bronze can't drop, Hari ng Kalsada can't rise); the top places win coins. The server
    /// (jr_league_*) applies the same rules.
    /// </summary>
    public static class Leagues
    {
        public const int GroupSize = 30, Promote = 7, Demote = 5, Tiers = 5;
        /// <summary>Fewer drivers than this in a group: nobody is demoted (small groups early in the week).</summary>
        public const int MinForDemotion = 10;

        public static readonly string[] Names = { "BRONZE", "SILVER", "GOLD", "DIAMOND", "HARI NG KALSADA" };
        public static readonly string[] Colours = { "#B87333", "#9AA3B2", "#E0B23A", "#4CC9F0", "#C8102E" };

        public static string Name(int tier) => Names[Math.Max(0, Math.Min(Tiers - 1, tier))];
        public static string Colour(int tier) => Colours[Math.Max(0, Math.Min(Tiers - 1, tier))];

        /// <summary>Next week's tier for a driver who finished at <paramref name="rank"/> (1-based) of <paramref name="players"/> with a score.</summary>
        public static int NextTier(int tier, int rank, int players)
        {
            if (rank <= 0) return tier;
            if (rank <= Promote) return Math.Min(Tiers - 1, tier + 1);
            if (players >= MinForDemotion && rank > players - Demote) return Math.Max(0, tier - 1);
            return tier;
        }

        /// <summary>Coins for the final rank (higher tiers pay more).</summary>
        public static int Reward(int tier, int rank)
        {
            int baseCoins = rank == 1 ? 500 : rank == 2 ? 300 : rank == 3 ? 200 : rank <= Promote ? 100 : 0;
            return baseCoins * (2 + Math.Max(0, Math.Min(Tiers - 1, tier))) / 2;
        }
    }
}
