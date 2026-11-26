using System;

namespace JeepneyRush.Logic
{
    /// <summary>Stat class of a jeepney: rankings only compare drivers in the same class.</summary>
    public enum JeepneyClass { Standard = 0, Sport = 1, Elite = 2 }

    /// <summary>
    /// Fair competition without nerfing anyone: every jeepney keeps its stats, but scores are
    /// ranked per stat class, so boosted (often paid) jeepneys only compete with jeepneys of
    /// similar power, and stock jeepneys with stock ones. The server derives the class from
    /// the same catalog (jr_catalog_jeepneys.stat_class, generated from this file), never from
    /// what the client says.
    /// Garage upgrades are not counted: everyone can earn them by playing.
    /// </summary>
    public static class JeepneyClasses
    {
        public const int Count = 3;
        /// <summary>Power at or above this is Sport.</summary>
        public const float SportFrom = 1.2f;
        /// <summary>Power at or above this is Elite.</summary>
        public const float EliteFrom = 2.5f;

        /// <summary>
        /// One number for "how much does this jeepney help": +1 m/s top speed ≈ +1 health ≈
        /// +3 seats ≈ 0.05 s quicker lane changes.
        /// </summary>
        public static float Power(JeepneyLivery j)
        {
            if (j == null) return 0f;
            return j.SpeedBonus + j.HealthBonus + j.CapacityBonus * 0.35f + j.HandlingBonus * 20f;
        }

        public static JeepneyClass For(JeepneyLivery j)
        {
            float p = Power(j);
            return p >= EliteFrom - 1e-4f ? JeepneyClass.Elite : p >= SportFrom - 1e-4f ? JeepneyClass.Sport : JeepneyClass.Standard;
        }

        /// <summary>Class of a jeepney id (unknown ids fall back to the default jeepney).</summary>
        public static JeepneyClass For(string id) => For(JeepneyCatalog.Get(id));

        public static string Name(JeepneyClass c)
        {
            switch (c)
            {
                case JeepneyClass.Sport: return "SPORT";
                case JeepneyClass.Elite: return "ELITE";
                default: return "STANDARD";
            }
        }

        public static string Blurb(JeepneyClass c)
        {
            switch (c)
            {
                case JeepneyClass.Sport: return "Sport class – ranked against other Sport jeepneys";
                case JeepneyClass.Elite: return "Elite class – ranked against other Elite jeepneys";
                default: return "Standard class – ranked against other Standard jeepneys";
            }
        }

        /// <summary>Badge colour (hex) for the garage and rankings.</summary>
        public static string Colour(JeepneyClass c)
        {
            switch (c)
            {
                case JeepneyClass.Sport: return "#1E6FD9";
                case JeepneyClass.Elite: return "#C99700";
                default: return "#2E9E5B";
            }
        }

        /// <summary>Leaderboard key the server uses for a class board ("all" → "all:c1").</summary>
        public static string BoardKey(string board, JeepneyClass c) => board + ":c" + (int)c;
    }
}
