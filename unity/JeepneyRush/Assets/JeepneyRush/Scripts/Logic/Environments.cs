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

    /// <summary>All stop names. The stop-sign texture atlas is drawn in this exact order (4 x 11 cells).</summary>
    public static class StopNames
    {
        public static readonly string[] All =
        {
            // Manila (0..19)
            "QUIAPO", "CUBAO", "DIVISORIA", "RECTO", "LAWTON", "ESPANA", "TAFT", "MONUMENTO", "BACLARAN",
            "BLUMENTRITT", "KALAW", "PACO", "STA. MESA", "PEDRO GIL", "VITO CRUZ", "LIBERTAD", "SAMPALOC",
            "ERMITA", "MALATE", "TONDO",
            // Provincial (20..31) – town and barangay landmarks, no Metro Manila names
            "POBLACION", "SAN ISIDRO", "PALENGKE", "SIMBAHAN", "MUNISIPYO", "BRGY. MALIGAYA",
            "BAYAN", "PLAZA", "CROSSING", "SAN ROQUE", "KAPITOLYO", "TERMINAL",
            // Beach (32..43) – coastal stops
            "WHITE BEACH", "PUERTO", "BAYBAYIN", "PIER", "LIGHTHOUSE", "RESORT",
            "DAUNGAN", "FISHPORT", "BOULEVARD", "COVE", "LAGUNDI", "DIVE SHOP",
        };

        public const int AtlasColumns = 4, AtlasRows = 11;
        public const int ManilaFrom = 0, ManilaTo = 19, ProvincialFrom = 20, ProvincialTo = 31, BeachFrom = 32, BeachTo = 43;

        public static IReadOnlyList<int> ForEnvironment(EnvironmentKind k)
        {
            var l = new List<int>();
            int from = k == EnvironmentKind.Provincial ? ProvincialFrom : k == EnvironmentKind.Beach ? BeachFrom : ManilaFrom;
            int to = k == EnvironmentKind.Provincial ? ProvincialTo : k == EnvironmentKind.Beach ? BeachTo : ManilaTo;
            for (int i = from; i <= to; i++) l.Add(i);
            return l;
        }
    }
}
