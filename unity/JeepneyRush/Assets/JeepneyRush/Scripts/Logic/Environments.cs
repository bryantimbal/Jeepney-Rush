using System.Collections.Generic;

namespace JeepneyRush.Logic
{
    public enum EnvironmentKind { Manila, Provincial, Beach, Mountain }

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
            new Def { Kind = EnvironmentKind.Mountain, Name = "Mountain Province", Tagline = "Muddy, rugged dirt roads, pine forests and rice terraces.", UnlockDistance = 4000 },
        };

        public static Def Get(EnvironmentKind k) => All[(int)k];

        public static bool IsUnlocked(EnvironmentKind k, float bestDistance) => bestDistance >= Get(k).UnlockDistance;

        /// <summary>How much traffic the route has (multiplies the vehicle patterns).</summary>
        public static float TrafficDensity(EnvironmentKind k) => k == EnvironmentKind.Mountain ? 0.35f : 1f;
        /// <summary>PARA stops are this much farther apart (remote mountain barangays).</summary>
        public static float StopSpacingScale(EnvironmentKind k) => k == EnvironmentKind.Mountain ? 1.75f : 1f;
        /// <summary>Gas stations farther apart (still within one tank – checked by tests).</summary>
        public static float StationSpacingScale(EnvironmentKind k) => k == EnvironmentKind.Mountain ? 1.12f : 1f;
        /// <summary>Signalised intersections are rare on mountain roads.</summary>
        public static float IntersectionSpacingScale(EnvironmentKind k) => k == EnvironmentKind.Mountain ? 3.2f : 1f;
        /// <summary>Unpaved, rutted road: constant shaking and loose (slippery) ground. 0 = paved.</summary>
        public static float Roughness(EnvironmentKind k) => k == EnvironmentKind.Mountain ? 1f : 0f;
        public static bool Paved(EnvironmentKind k) => k != EnvironmentKind.Mountain;

        /// <summary>Relative traffic weights (sedan, suv, tricycle, motorcycle, bus, truck) per environment.</summary>
        public static float[] TrafficWeights(EnvironmentKind k)
        {
            switch (k)
            {
                case EnvironmentKind.Provincial: return new[] { 1.2f, 0.8f, 3.2f, 2.4f, 0.5f, 1.0f };
                case EnvironmentKind.Beach: return new[] { 2.2f, 2.6f, 1.6f, 1.4f, 0.8f, 0.5f };
                case EnvironmentKind.Mountain: return new[] { 0.4f, 1.0f, 0.3f, 2.4f, 0.15f, 2.2f }; // vegetable trucks and motorbikes
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
            // Mountain Province (44..55) – Cordillera towns and landmarks
            "BONTOC", "SAGADA", "BESAO", "SABANGAN", "TADIAN", "BAUKO",
            "SADANGA", "BARLIG", "NATONIN", "ECHO VALLEY", "RICE TERRACES", "VIEWDECK",
        };

        public const int AtlasColumns = 4, AtlasRows = 14;
        public const int ManilaFrom = 0, ManilaTo = 19, ProvincialFrom = 20, ProvincialTo = 31, BeachFrom = 32, BeachTo = 43, MountainFrom = 44, MountainTo = 55;

        public static IReadOnlyList<int> ForEnvironment(EnvironmentKind k)
        {
            var l = new List<int>();
            int from = k == EnvironmentKind.Provincial ? ProvincialFrom : k == EnvironmentKind.Beach ? BeachFrom : k == EnvironmentKind.Mountain ? MountainFrom : ManilaFrom;
            int to = k == EnvironmentKind.Provincial ? ProvincialTo : k == EnvironmentKind.Beach ? BeachTo : k == EnvironmentKind.Mountain ? MountainTo : ManilaTo;
            for (int i = from; i <= to; i++) l.Add(i);
            return l;
        }
    }
}
