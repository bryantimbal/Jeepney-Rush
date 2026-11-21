using System;

namespace JeepneyRush.Logic
{
    public enum JeepneyStyle { Classic, Modern }
    public enum ArtMotif { SunStars, Sunburst, Flowers, Waves, Mountains, Eagle, Neon, Fiesta, Flag, Gold, Manila }

    /// <summary>Identity, paint, art, price and stats of a jeepney. Colours are hex strings (engine-free).</summary>
    public sealed class JeepneyLivery
    {
        public string Id;
        public string DisplayName;
        public string Description;
        public int Price;                 // coins; 0 = free
        public JeepneyStyle Style = JeepneyStyle.Classic;
        public ArtMotif Motif = ArtMotif.SunStars;

        public string Body;      // main painted panels
        public string Hood;
        public string Accent1;   // stripes / art
        public string Accent2;
        public string Roof;
        public string Trim;      // pillars / visor
        public string Chrome = "#E6EBF2";
        public string Mudflap = "#141414";
        public string RoofSign;  // text on the roof sign (pre-rendered texture)
        public string Route;
        public string Slogan;
        public string RearText = "GOD BLESS OUR TRIP";
        public string Plate = "PUJ 2026";

        public bool ChromeLowerBody;
        public bool SpareTire;
        public bool BeadedAntennas;
        public bool Neon;        // glowing underbody / stripe lights
        public bool Fiesta;      // banderitas (bunting) strung on the roof

        // stats (added on top of the base balance & upgrades)
        public int CapacityBonus;
        public int HealthBonus;
        public float SpeedBonus;     // m/s added to max speed
        public float HandlingBonus;  // seconds removed from lane-change time

        public string SideTexture => $"jeepney_side_{Id}";
        public string RearTexture => $"jeepney_rear_{Id}";
        public string SignTexture => $"jeepney_sign_{Id}";
    }

    /// <summary>All jeepneys. The first two are free (Phase 1); the rest are bought in the garage.</summary>
    public static class JeepneyCatalog
    {
        public const string DefaultId = "pilipinas";

        public static readonly JeepneyLivery[] All =
        {
            new JeepneyLivery
            {
                Id = "pilipinas", DisplayName = "Pilipinas Classic", Price = 0, Motif = ArtMotif.SunStars,
                Description = "Chrome fenders, sun-and-stars art and a proud PILIPINAS roof sign.",
                Body = "#C8102E", Hood = "#1F3FBF", Accent1 = "#FCD116", Accent2 = "#FFFFFF", Roof = "#1F3FBF", Trim = "#FCD116",
                RoofSign = "PILIPINAS", Route = "PILIPINAS", Slogan = "Pilipinas", ChromeLowerBody = true, SpareTire = true,
            },
            new JeepneyLivery
            {
                Id = "sanjuan", DisplayName = "San Juan Express", Price = 0, Motif = ArtMotif.Sunburst,
                Description = "Purple pride of the San Juan route, with beaded antennas.",
                Body = "#8E2BD9", Hood = "#8E2BD9", Accent1 = "#FF8A1F", Accent2 = "#FFD23F", Roof = "#2FA4E7", Trim = "#25C98B", Mudflap = "#D62828",
                RoofSign = "SAN JUAN - SAN FERNANDO", Route = "SAN JUAN - SAN FERNANDO", Slogan = "San Juan - San Fernando", RearText = "SALAMAT PO!", Plate = "SJ 1965",
                BeadedAntennas = true, HandlingBonus = 0.01f,
            },
            new JeepneyLivery
            {
                Id = "manila", DisplayName = "Manila Classic", Price = 600, Motif = ArtMotif.Manila,
                Description = "Old-school stainless steel with hand-painted pinstripes.",
                Body = "#D7DBE2", Hood = "#C1121F", Accent1 = "#C1121F", Accent2 = "#1D3557", Roof = "#1D3557", Trim = "#C1121F",
                RoofSign = "DIVISORIA", Route = "DIVISORIA - BACLARAN", Slogan = "Hari ng Kalsada", RearText = "BAWAL ANG SIMANGOT", Plate = "MNL 1953",
                ChromeLowerBody = true, SpareTire = true, CapacityBonus = 1,
            },
            new JeepneyLivery
            {
                Id = "modern", DisplayName = "Modern Jeepney", Price = 1500, Style = JeepneyStyle.Modern, Motif = ArtMotif.Waves,
                Description = "The new-generation PUV: boxy, efficient and roomy.",
                Body = "#F8F9FA", Hood = "#F8F9FA", Accent1 = "#00A6A6", Accent2 = "#F4A261", Roof = "#E9ECEF", Trim = "#00A6A6",
                RoofSign = "BGC - AYALA", Route = "BGC - AYALA", Slogan = "Modern PUV", RearText = "PLEASE PAY EXACT FARE", Plate = "MPUJ 01",
                CapacityBonus = 3, HandlingBonus = -0.01f,
            },
            new JeepneyLivery
            {
                Id = "cebu", DisplayName = "Cebu Express", Price = 1200, Motif = ArtMotif.Waves,
                Description = "Queen City colours: sea blue with Sinulog orange.",
                Body = "#0077B6", Hood = "#F77F00", Accent1 = "#F77F00", Accent2 = "#FFFFFF", Roof = "#FCBF49", Trim = "#FCBF49",
                RoofSign = "CEBU CITY", Route = "COLON - IT PARK", Slogan = "Sugbo", RearText = "PIT SENYOR!", Plate = "CEB 2024",
                ChromeLowerBody = true, SpeedBonus = 0.5f,
            },
            new JeepneyLivery
            {
                Id = "baguio", DisplayName = "Baguio Cruiser", Price = 1800, Motif = ArtMotif.Mountains,
                Description = "Pine green for the mountain roads. Sure-footed on the curves.",
                Body = "#2D6A4F", Hood = "#1B4332", Accent1 = "#95D5B2", Accent2 = "#FFFFFF", Roof = "#40916C", Trim = "#D8F3DC",
                RoofSign = "BAGUIO", Route = "SESSION RD - MINES VIEW", Slogan = "City of Pines", RearText = "INGAT SA PAGMAMANEHO", Plate = "BAG 1909",
                SpareTire = true, HandlingBonus = 0.03f,
            },
            new JeepneyLivery
            {
                Id = "davao", DisplayName = "Davao Explorer", Price = 2500, Motif = ArtMotif.Eagle,
                Description = "Durian-yellow explorer with the Philippine eagle on its side.",
                Body = "#FFB703", Hood = "#6B4226", Accent1 = "#6B4226", Accent2 = "#FFFFFF", Roof = "#6B4226", Trim = "#3A2A1A",
                RoofSign = "DAVAO", Route = "BANGKAL - ROXAS", Slogan = "Davao Explorer", RearText = "LIFE IS HERE", Plate = "DVO 1937",
                ChromeLowerBody = true, SpareTire = true, HealthBonus = 1,
            },
            new JeepneyLivery
            {
                Id = "fiesta", DisplayName = "Fiesta Edition", Price = 3000, Motif = ArtMotif.Fiesta,
                Description = "Every day is a fiesta: banderitas on the roof and party colours.",
                Body = "#EF476F", Hood = "#06D6A0", Accent1 = "#FFD166", Accent2 = "#118AB2", Roof = "#FFD166", Trim = "#06D6A0", Mudflap = "#118AB2",
                RoofSign = "FIESTA!", Route = "PISTA SA NAYON", Slogan = "Fiesta!", RearText = "MABUHAY!", Plate = "PISTA 7",
                Fiesta = true, BeadedAntennas = true, CapacityBonus = 2,
            },
            new JeepneyLivery
            {
                Id = "neon", DisplayName = "Neon Street Racer", Price = 4000, Motif = ArtMotif.Neon,
                Description = "Midnight paint with glowing neon. Built for speed.",
                Body = "#14112B", Hood = "#14112B", Accent1 = "#00F5D4", Accent2 = "#F15BB5", Roof = "#241E4E", Trim = "#00F5D4", Mudflap = "#F15BB5",
                RoofSign = "NEON", Route = "MIDNIGHT RUN", Slogan = "Neon Rush", RearText = "CATCH ME IF YOU CAN", Plate = "NEON 88",
                Neon = true, SpeedBonus = 2f, HandlingBonus = 0.02f,
            },
            new JeepneyLivery
            {
                Id = "flag", DisplayName = "Philippine Flag Edition", Price = 5000, Motif = ArtMotif.Flag,
                Description = "Blue, red, white and the golden sun – the full flag livery.",
                Body = "#FFFFFF", Hood = "#0038A8", Accent1 = "#CE1126", Accent2 = "#FCD116", Roof = "#0038A8", Trim = "#CE1126",
                RoofSign = "MABUHAY", Route = "LUZON - VISAYAS - MINDANAO", Slogan = "Mabuhay!", RearText = "PILIPINAS KONG MAHAL", Plate = "PH 1898",
                ChromeLowerBody = true, SpareTire = true, CapacityBonus = 1, HealthBonus = 1,
            },
            new JeepneyLivery
            {
                Id = "gold", DisplayName = "Gold VIP Jeepney", Price = 8000, Motif = ArtMotif.Gold,
                Description = "Gold-plated everything. The ultimate status symbol.",
                Body = "#1A1A1A", Hood = "#1A1A1A", Accent1 = "#FFD700", Accent2 = "#FFF3B0", Roof = "#1A1A1A", Trim = "#FFD700", Chrome = "#FFC93C",
                RoofSign = "VIP", Route = "VIP EXPRESS", Slogan = "VIP", RearText = "VERY IMPORTANT PASAHERO", Plate = "VIP 1",
                ChromeLowerBody = true, SpareTire = true, CapacityBonus = 2, HealthBonus = 1, SpeedBonus = 1f, HandlingBonus = 0.02f,
            },
        };

        public static JeepneyLivery Get(string id)
        {
            foreach (var j in All) if (j.Id == id) return j;
            return All[0];
        }

        public static bool Exists(string id)
        {
            foreach (var j in All) if (j.Id == id) return true;
            return false;
        }
    }
}
