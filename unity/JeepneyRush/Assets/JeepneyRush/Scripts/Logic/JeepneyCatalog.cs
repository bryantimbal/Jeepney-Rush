using System;

namespace JeepneyRush.Logic
{
    public enum JeepneyStyle { Classic, Modern }
    public enum ArtMotif { SunStars, Sunburst, Flowers, Waves, Mountains, Eagle, Neon, Fiesta, Flag, Gold, Manila, Patok }

    /// <summary>Identity, paint, art, price and stats of a jeepney. Colours are hex strings (engine-free).</summary>
    public sealed class JeepneyLivery
    {
        public string Id;
        public string DisplayName;
        public string Description;
        public int Price;                 // coins; 0 = free
        /// <summary>Optional premium price in gems (0 = coins only). Phase 4.</summary>
        public int GemPrice;
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
        /// <summary>
        /// "Patok" jeepney (Cogeo–Antipolo style): roof speakers, LED strips, disco lights inside,
        /// fog-light bar and a loud original "sound trip" mix that plays while you drive.
        /// </summary>
        public bool SoundSystem;

        // ---- modern PUV body kit
        /// <summary>Closed aircon body: tinted glass windows and a side door instead of open sides.</summary>
        public bool ClosedBody;
        public bool RoofAc, SolarRoof, LuggageRack, BusNose;
        public JeepneyPerk Perk;

        // stats (added on top of the base balance & upgrades)
        public int CapacityBonus;
        public int HealthBonus;
        public float SpeedBonus;     // m/s added to max speed
        public float HandlingBonus;  // seconds removed from lane-change time

        public string SideTexture => $"jeepney_side_{Id}";
        public string RearTexture => $"jeepney_rear_{Id}";
        public string SignTexture => SignTextureOverride ?? $"jeepney_sign_{Id}";

        // ---- customization (set on a copy by Customization.Apply; the catalog entries keep the defaults)
        public string SignTextureOverride;
        public OrnamentKind Ornament = OrnamentKind.Horses;
        public LightsKind ExtraLights = LightsKind.None;
        public string ExtraLightColor = "#FFF4C2";
        /// <summary>Banner across the top of the windshield, sticker on the rear panel, print on the rear mudflaps (textures; null = none).</summary>
        public string WindshieldTexture, RearStickerTexture, MudflapTexture;

        public JeepneyLivery Clone() => (JeepneyLivery)MemberwiseClone();
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
                Id = "fiesta", GemPrice = 250, DisplayName = "Fiesta Edition", Price = 3000, Motif = ArtMotif.Fiesta,
                Description = "Every day is a fiesta: banderitas on the roof and party colours.",
                Body = "#EF476F", Hood = "#06D6A0", Accent1 = "#FFD166", Accent2 = "#118AB2", Roof = "#FFD166", Trim = "#06D6A0", Mudflap = "#118AB2",
                RoofSign = "FIESTA!", Route = "PISTA SA NAYON", Slogan = "Fiesta!", RearText = "MABUHAY!", Plate = "PISTA 7",
                Fiesta = true, BeadedAntennas = true, CapacityBonus = 2,
            },
            new JeepneyLivery
            {
                Id = "neon", GemPrice = 320, DisplayName = "Neon Street Racer", Price = 4000, Motif = ArtMotif.Neon,
                Description = "Midnight paint with glowing neon. Built for speed.",
                Body = "#14112B", Hood = "#14112B", Accent1 = "#00F5D4", Accent2 = "#F15BB5", Roof = "#241E4E", Trim = "#00F5D4", Mudflap = "#F15BB5",
                RoofSign = "NEON", Route = "MIDNIGHT RUN", Slogan = "Neon Rush", RearText = "CATCH ME IF YOU CAN", Plate = "NEON 88",
                Neon = true, SpeedBonus = 2f, HandlingBonus = 0.02f,
            },
            new JeepneyLivery
            {
                Id = "flag", GemPrice = 350, DisplayName = "Philippine Flag Edition", Price = 5000, Motif = ArtMotif.Flag,
                Description = "Blue, red, white and the golden sun – the full flag livery.",
                Body = "#FFFFFF", Hood = "#0038A8", Accent1 = "#CE1126", Accent2 = "#FCD116", Roof = "#0038A8", Trim = "#CE1126",
                RoofSign = "MABUHAY", Route = "LUZON - VISAYAS - MINDANAO", Slogan = "Mabuhay!", RearText = "PILIPINAS KONG MAHAL", Plate = "PH 1898",
                ChromeLowerBody = true, SpareTire = true, CapacityBonus = 1, HealthBonus = 1,
            },
            new JeepneyLivery
            {
                Id = "cogeo", GemPrice = 420, DisplayName = "Patok Cogeo", Price = 6500, Motif = ArtMotif.Patok,
                Description = "Cogeo–Cubao patok: LED lights, roof speakers and a loud sound trip while you drive.",
                Body = "#0E0E12", Hood = "#0E0E12", Accent1 = "#FF2E4D", Accent2 = "#FFC300", Roof = "#16161C", Trim = "#FF2E4D", Mudflap = "#FFC300",
                RoofSign = "COGEO", Route = "COGEO - CUBAO", Slogan = "Sound Trip!", RearText = "PATOK PA RIN, INGAT LANG!", Plate = "PTK 143",
                ChromeLowerBody = true, BeadedAntennas = true, SoundSystem = true, SpeedBonus = 1.5f, HandlingBonus = 0.02f,
            },
            new JeepneyLivery
            {
                Id = "antipolo", GemPrice = 460, DisplayName = "Patok Antipolo", Price = 7500, Motif = ArtMotif.Patok,
                Description = "Antipolo–Cubao patok in chrome white with purple LEDs and a bass-heavy sound system.",
                Body = "#EEF1F6", Hood = "#7B2FF7", Accent1 = "#7B2FF7", Accent2 = "#00E5FF", Roof = "#7B2FF7", Trim = "#00E5FF", Mudflap = "#7B2FF7",
                RoofSign = "ANTIPOLO", Route = "ANTIPOLO - CUBAO", Slogan = "Byaheng Langit", RearText = "SAKAY NA, SOUNDTRIP NA!", Plate = "ANT 1591",
                ChromeLowerBody = true, SpareTire = true, SoundSystem = true, CapacityBonus = 1, SpeedBonus = 1.5f, HandlingBonus = 0.01f,
            },
            new JeepneyLivery
            {
                Id = "gold", GemPrice = 500, DisplayName = "Gold VIP Jeepney", Price = 8000, Motif = ArtMotif.Gold,
                Description = "Gold-plated everything. The ultimate status symbol.",
                Body = "#1A1A1A", Hood = "#1A1A1A", Accent1 = "#FFD700", Accent2 = "#FFF3B0", Roof = "#1A1A1A", Trim = "#FFD700", Chrome = "#FFC93C",
                RoofSign = "VIP", Route = "VIP EXPRESS", Slogan = "VIP", RearText = "VERY IMPORTANT PASAHERO", Plate = "VIP 1",
                ChromeLowerBody = true, SpareTire = true, CapacityBonus = 2, HealthBonus = 1, SpeedBonus = 1f, HandlingBonus = 0.02f,
            },
            // ---- new-generation modern PUVs (PUV Modernization types)
            new JeepneyLivery
            {
                Id = "aircon", DisplayName = "Aircon PUJ", Price = 2800, Style = JeepneyStyle.Modern, Motif = ArtMotif.Waves, Perk = JeepneyPerk.Aircon,
                Description = "Class 2 modern jeep: closed aircon body, tinted windows and a side door.",
                Body = "#F8F9FA", Hood = "#F8F9FA", Accent1 = "#1F3FBF", Accent2 = "#00A6E0", Roof = "#E9ECEF", Trim = "#1F3FBF",
                RoofSign = "CUBAO - FAIRVIEW", Route = "CUBAO - FAIRVIEW", Slogan = "Transport Coop", RearText = "AIRCON - CLOSE THE DOOR", Plate = "MPUJ 22",
                ClosedBody = true, RoofAc = true, CapacityBonus = 5, HandlingBonus = -0.02f,
            },
            new JeepneyLivery
            {
                Id = "ejeep", DisplayName = "E-Jeepney", Price = 3500, GemPrice = 200, Style = JeepneyStyle.Modern, Motif = ArtMotif.Waves, Perk = JeepneyPerk.Electric,
                Description = "Electric and quiet, with solar panels on the roof. No smoke at all.",
                Body = "#F1F8E9", Hood = "#F1F8E9", Accent1 = "#7CC142", Accent2 = "#1B5E20", Roof = "#DDE8D0", Trim = "#1B5E20",
                RoofSign = "MAKATI LOOP", Route = "MAKATI LOOP", Slogan = "100% Electric", RearText = "ZERO EMISSION", Plate = "EJP 100",
                ClosedBody = true, SolarRoof = true, CapacityBonus = 3,
            },
            new JeepneyLivery
            {
                Id = "minibus", DisplayName = "Modern Minibus", Price = 5000, Style = JeepneyStyle.Modern, Motif = ArtMotif.Waves, Perk = JeepneyPerk.BigCapacity,
                Description = "Class 4 PUV: front folding door, big windows and room for standing riders.",
                Body = "#FFFFFF", Hood = "#FFFFFF", Accent1 = "#E63946", Accent2 = "#F4A261", Roof = "#EDEDED", Trim = "#E63946",
                RoofSign = "QUIAPO - PITX", Route = "QUIAPO - PITX", Slogan = "PITX Express", RearText = "STANDING ROOM ONLY", Plate = "PUV 4040",
                ClosedBody = true, RoofAc = true, BusNose = true, CapacityBonus = 8, HandlingBonus = -0.05f,
            },
            new JeepneyLivery
            {
                Id = "sarao", DisplayName = "Retro-Modern Sarao", Price = 6000, GemPrice = 380, Motif = ArtMotif.SunStars, Perk = JeepneyPerk.Heritage,
                Description = "A modern, safe body with a classic soul: chrome grille, silver horses and hand-painted art.",
                Body = "#C8102E", Hood = "#C8102E", Accent1 = "#1F3FBF", Accent2 = "#FCD116", Roof = "#F1F1F1", Trim = "#FCD116",
                RoofSign = "BACLARAN - DIVISORIA", Route = "BACLARAN - DIVISORIA", Slogan = "Hari ng Kalsada", RearText = "TRADISYON AT MODERNO", Plate = "SAR 1953",
                ClosedBody = true, RoofAc = true, CapacityBonus = 3, HealthBonus = 1,
            },
            new JeepneyLivery
            {
                Id = "airport", DisplayName = "Airport Express", Price = 9000, GemPrice = 450, Style = JeepneyStyle.Modern, Motif = ArtMotif.Waves, Perk = JeepneyPerk.Airport,
                Description = "Clean white with orange speed stripes and a luggage rack. Balikbayans love it.",
                Body = "#FFFFFF", Hood = "#FFFFFF", Accent1 = "#F77F00", Accent2 = "#D62828", Roof = "#EEEEEE", Trim = "#F77F00",
                RoofSign = "NAIA LOOP", Route = "NAIA LOOP", Slogan = "NAIA Express", RearText = "WELCOME HOME, KABAYAN!", Plate = "NAIA 3",
                ClosedBody = true, RoofAc = true, LuggageRack = true, CapacityBonus = 4, HandlingBonus = 0.02f,
            },
            new JeepneyLivery
            {
                Id = "nightowl", DisplayName = "Night Owl Modern", Price = 9500, GemPrice = 500, Style = JeepneyStyle.Modern, Motif = ArtMotif.Neon, Perk = JeepneyPerk.NightOwl,
                Description = "Dark body with LED underglow and a glowing sign, made for the night shift.",
                Body = "#22223B", Hood = "#22223B", Accent1 = "#00F5D4", Accent2 = "#F15BB5", Roof = "#1A1A2E", Trim = "#00F5D4",
                RoofSign = "24/7 PASADA", Route = "24/7 PASADA", Slogan = "Night Owl", RearText = "GISING PA KAMI", Plate = "OWL 247",
                ClosedBody = true, RoofAc = true, Neon = true, CapacityBonus = 4,
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
