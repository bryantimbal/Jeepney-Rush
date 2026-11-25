using System;
using System.Collections.Generic;

namespace JeepneyRush.Logic
{
    public enum EnvironmentKind { Manila, Provincial, Beach, Mountain, Edsa, Cebu, Baguio, Fiesta }

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
            new Def { Kind = EnvironmentKind.Edsa, Name = "EDSA", Tagline = "MRT overhead, buses, malls and the worst rush-hour traffic in the country.", UnlockDistance = 1800 },
            new Def { Kind = EnvironmentKind.Cebu, Name = "Cebu City", Tagline = "Downtown Colon, IT Park towers, the sea and the mountains.", UnlockDistance = 3200 },
            new Def { Kind = EnvironmentKind.Baguio, Name = "Baguio", Tagline = "Pine trees, fog, steep hills and Session Road.", UnlockDistance = 5000 },
            new Def { Kind = EnvironmentKind.Fiesta, Name = "Fiesta Town", Tagline = "Banderitas, parade floats, brass bands and crowded streets.", UnlockDistance = 2200 },
        };

        public static Def Get(EnvironmentKind k) => All[(int)k];

        public static bool IsUnlocked(EnvironmentKind k, float bestDistance) => bestDistance >= Get(k).UnlockDistance;

        /// <summary>How much traffic the route has (multiplies the vehicle patterns).</summary>
        public static float TrafficDensity(EnvironmentKind k) =>
            k == EnvironmentKind.Mountain ? 0.3f : k == EnvironmentKind.Edsa ? 1.9f : k == EnvironmentKind.Cebu ? 1.15f : k == EnvironmentKind.Baguio ? 0.8f
            : k == EnvironmentKind.Fiesta ? 1.2f : k == EnvironmentKind.Manila ? 1.3f : 1f;
        /// <summary>Chance of an extra vehicle between two rows (busy city roads: Manila streets, EDSA).</summary>
        public static float ExtraTrafficChance(EnvironmentKind k) => k == EnvironmentKind.Edsa ? 0.6f : k == EnvironmentKind.Manila ? 0.55f : 0f;
        /// <summary>Rows come closer together on the busiest city roads (never below the reaction-time gap).</summary>
        public static float RowGapScale(EnvironmentKind k) => k == EnvironmentKind.Edsa ? 0.8f : k == EnvironmentKind.Manila ? 0.85f : 1f;
        /// <summary>How bad the traffic jams get (0 = none). EDSA is the real deal; Cebu has lighter jams.</summary>
        public static float JamIntensity(EnvironmentKind k) => k == EnvironmentKind.Edsa ? 1f : k == EnvironmentKind.Cebu ? 0.55f : 0f;
        /// <summary>Two-way roads: the left lane (0) carries oncoming traffic (provincial and coastal highways).</summary>
        public static bool TwoWay(EnvironmentKind k) => k == EnvironmentKind.Provincial || k == EnvironmentKind.Beach;
        public const int OncomingLane = 0;

        /// <summary>Tricycles are banned on EDSA and in Baguio.</summary>
        public static bool Tricycles(EnvironmentKind k) => k != EnvironmentKind.Edsa && k != EnvironmentKind.Baguio;
        /// <summary>PARA stops are this much farther apart (remote mountain barangays).</summary>
        public static float StopSpacingScale(EnvironmentKind k) => k == EnvironmentKind.Mountain ? 1.75f : 1f;
        /// <summary>Gas stations farther apart (still within one tank – checked by tests).</summary>
        public static float StationSpacingScale(EnvironmentKind k) => k == EnvironmentKind.Mountain ? 1.12f : 1f;
        /// <summary>Signalised intersections are rare on mountain roads.</summary>
        public static float IntersectionSpacingScale(EnvironmentKind k) => k == EnvironmentKind.Mountain ? 3.2f : k == EnvironmentKind.Edsa ? 1.8f : k == EnvironmentKind.Baguio ? 1.4f : 1f;
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
                case EnvironmentKind.Edsa: return new[] { 3.2f, 2.2f, 0f, 2.0f, 2.4f, 0.6f };    // cars, SUVs, motorbikes and lots of buses
                case EnvironmentKind.Cebu: return new[] { 2.6f, 1.6f, 1.0f, 2.6f, 0.9f, 0.7f };  // motorbikes everywhere ("habal-habal")
                case EnvironmentKind.Baguio: return new[] { 3.0f, 2.0f, 0f, 1.0f, 0.4f, 0.6f };  // taxis and SUVs up the hills
                case EnvironmentKind.Fiesta: return new[] { 1.4f, 0.8f, 3.4f, 2.6f, 0.2f, 0.3f }; // town streets: tricycles and motorbikes
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
            // EDSA (56..67) – stations and landmarks from Monumento to Taft
            "MONUMENTO", "BALINTAWAK", "NORTH AVE", "QUEZON AVE", "KAMUNING", "CUBAO",
            "ORTIGAS", "SHAW", "GUADALUPE", "BUENDIA", "AYALA", "TAFT AVE",
            // Cebu City (68..79)
            "COLON", "CARBON", "FUENTE OSMENA", "IT PARK", "AYALA CEBU", "SM CEBU",
            "MANDAUE", "MABOLO", "LAHUG", "TALAMBAN", "PIER 1", "CAPITOL",
            // Baguio (80..91)
            "SESSION RD", "BURNHAM PARK", "MINES VIEW", "CAMP JOHN HAY", "WRIGHT PARK", "TEACHERS CAMP",
            "LA TRINIDAD", "PUBLIC MARKET", "CATHEDRAL", "LOURDES GROTTO", "BOTANICAL", "MARCOS HWY",
            // Fiesta Town (92..103) – the town fiesta: church, plaza, perya
            "PLAZUELA", "SIMBAHAN", "PATIO", "PERYAHAN", "KUMBENTO", "MUNISIPYO",
            "COVERED COURT", "PALENGKE", "STA. CRUZ", "SAN JOSE", "BRGY. HALL", "KAPILYA",
        };

        public const int AtlasColumns = 4, AtlasRows = 26;

        /// <summary>
        /// Each stop has its own sign picture named after the stop ("stop_sadanga"), so a sign can
        /// only ever show its own name – no atlas index that could drift out of sync.
        /// </summary>
        public static string SignTexture(int index)
        {
            index = Math.Max(0, Math.Min(All.Length - 1, index));
            var n = All[index].ToLowerInvariant();
            // the route goes in the name too: CUBAO and MONUMENTO exist on both Manila streets and EDSA (different sign colours)
            string route = index <= ManilaTo ? "mnl" : index <= ProvincialTo ? "prov" : index <= BeachTo ? "beach" : index <= MountainTo ? "mtn" : index <= EdsaTo ? "edsa" : index <= CebuTo ? "cebu" : index <= BaguioTo ? "bgo" : "fiesta";
            var sb = new System.Text.StringBuilder("stop_" + route + "_");
            foreach (char ch in n) sb.Append(char.IsLetterOrDigit(ch) ? ch : '_');
            return sb.ToString().Replace("__", "_").TrimEnd('_');
        }
        public const int ManilaFrom = 0, ManilaTo = 19, ProvincialFrom = 20, ProvincialTo = 31, BeachFrom = 32, BeachTo = 43, MountainFrom = 44, MountainTo = 55;
        public const int EdsaFrom = 56, EdsaTo = 67, CebuFrom = 68, CebuTo = 79, BaguioFrom = 80, BaguioTo = 91, FiestaFrom = 92, FiestaTo = 103;

        public static IReadOnlyList<int> ForEnvironment(EnvironmentKind k)
        {
            var l = new List<int>();
            int from, to;
            switch (k)
            {
                case EnvironmentKind.Provincial: from = ProvincialFrom; to = ProvincialTo; break;
                case EnvironmentKind.Beach: from = BeachFrom; to = BeachTo; break;
                case EnvironmentKind.Mountain: from = MountainFrom; to = MountainTo; break;
                case EnvironmentKind.Edsa: from = EdsaFrom; to = EdsaTo; break;
                case EnvironmentKind.Cebu: from = CebuFrom; to = CebuTo; break;
                case EnvironmentKind.Baguio: from = BaguioFrom; to = BaguioTo; break;
                case EnvironmentKind.Fiesta: from = FiestaFrom; to = FiestaTo; break;
                default: from = ManilaFrom; to = ManilaTo; break;
            }
            for (int i = from; i <= to; i++) l.Add(i);
            return l;
        }
    }
}
