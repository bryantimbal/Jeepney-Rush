using System;

namespace JeepneyRush.Logic
{
    /// <summary>
    /// A few gems on the road. Rare on purpose: gems are the premium currency (jeepneys that
    /// otherwise cost real money), so a run gives at most a handful and a day is capped.
    /// The server applies the same limits (jr_submit_run), so a modified game gains nothing.
    /// </summary>
    public static class RoadGems
    {
        public const float FirstAt = 450f;
        public const float SpacingMin = 700f, SpacingMax = 1200f;
        public const int MaxPerRun = 5;
        public const int DailyCap = 15;

        /// <summary>Most gems a run of <paramref name="distance"/> metres can hold (server clamp).</summary>
        public static int MaxForDistance(float distance) => distance < 300f ? 0 : Math.Min(MaxPerRun, (int)((distance - 300f) / 600f) + 1);

        /// <summary>Gems still allowed today after <paramref name="collectedToday"/>.</summary>
        public static int LeftToday(int collectedToday) => Math.Max(0, DailyCap - Math.Max(0, collectedToday));
    }
}
