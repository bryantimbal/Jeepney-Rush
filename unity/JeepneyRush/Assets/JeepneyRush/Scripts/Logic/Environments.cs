using System.Collections.Generic;

namespace JeepneyRush.Logic
{
    public enum EnvironmentKind { Manila, Provincial, Beach }

    /// <summary>Route definitions: unlock rules, stop names and traffic mix per environment.</summary>
    public static class Environments
    {
        public sealed class Def
        {
            public EnvironmentKind Kind;
            public string Name;
            public string Tagline;
            /// <summary>Best single-run distance needed to unlock (0 = free).</summary>
            public float UnlockDistance;
        }

        public static readonly Def[] All =
        {
            new Def { Kind = EnvironmentKind.Manila, Name = "Manila Streets", Tagline = "Sari-sari stores, tangled wires and heavy traffic.", UnlockDistance = 0 },
            new Def { Kind = EnvironmentKind.Provincial, Name = "Provincial Road", Tagline = "Rice fields, coconut trees and tricycles.", UnlockDistance = 1000 },
            new Def { Kind = EnvironmentKind.Beach, Name = "Beach Highway", Tagline = "Ocean views, palm trees and bangkas.", UnlockDistance = 2500 },
        };

        public static Def Get(EnvironmentKind k) => All[(int)k];

        public static bool IsUnlocked(EnvironmentKind k, float bestDistance) => bestDistance >= Get(k).UnlockDistance;

        /// <summary>Relative traffic weights (sedan, suv, tricycle, motorcycle, bus, truck) per environment.</summary>
        public static float[] TrafficWeights(EnvironmentKind k)
        {
            switch (k)
            {
                case EnvironmentKind.Provincial: return new[] { 1.2f, 0.8f, 3.2f, 2.4f, 0.5f, 1.0f };
                case EnvironmentKind.Beach: return new[] { 2.2f, 2.6f, 1.6f, 1.4f, 0.8f, 0.5f };
                default: return new[] { 3f, 1.6f, 1.5f, 1.1f, 1f, 0.8f };
            }
        }
    }

    /// <summary>All stop names. The stop-sign texture atlas is drawn in this exact order (4 x 8 cells).</summary>
    public static class StopNames
    {
        public static readonly string[] All =
        {
            // Manila (0..19)
            "QUIAPO", "CUBAO", "DIVISORIA", "RECTO", "LAWTON", "ESPANA", "TAFT", "MONUMENTO", "BACLARAN",
            "BLUMENTRITT", "KALAW", "PACO", "STA. MESA", "PEDRO GIL", "VITO CRUZ", "LIBERTAD", "SAMPALOC",
            "ERMITA", "MALATE", "TONDO",
            // Provincial (20..25)
            "POBLACION", "SAN ISIDRO", "PALENGKE", "SIMBAHAN", "MUNISIPYO", "BRGY. MALIGAYA",
            // Beach (26..31)
            "WHITE BEACH", "PUERTO", "BAYBAYIN", "PIER", "LIGHTHOUSE", "RESORT",
        };

        public static IReadOnlyList<int> ForEnvironment(EnvironmentKind k)
        {
            var l = new List<int>();
            int from = k == EnvironmentKind.Provincial ? 20 : k == EnvironmentKind.Beach ? 26 : 0;
            int to = k == EnvironmentKind.Provincial ? 25 : k == EnvironmentKind.Beach ? 31 : 19;
            for (int i = from; i <= to; i++) l.Add(i);
            return l;
        }
    }
}
