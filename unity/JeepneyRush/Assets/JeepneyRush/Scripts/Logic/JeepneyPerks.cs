using System;

namespace JeepneyRush.Logic
{
    /// <summary>Special ability of a jeepney (the new modern PUVs).</summary>
    public enum JeepneyPerk { None, Aircon, Electric, BigCapacity, Heritage, Airport, NightOwl }

    /// <summary>
    /// Jeepney perks and emissions (engine-free rules, also used by the garage text):
    ///  • Aircon PUJ – riders stay patient longer in traffic (complaints come slower)
    ///  • E-Jeepney – charging/fuel stops cost half; no smoke, an electric hum instead of a diesel
    ///  • Modern Minibus – the most seats (the catalog stats), slower lane changes
    ///  • Retro-Modern "Sarao" – more barkers call riders, and riders tip more
    ///  • Airport Express – balikbayans board twice as often
    ///  • Night Owl – +10 % fares after dark
    /// Every modern / closed-body PUV is cleaner and quieter than an old diesel jeepney.
    /// </summary>
    public static class JeepneyPerks
    {
        public const float AirconPatience = 1.6f;      // complaint interval multiplier
        public const int AirconExtraComplaints = 2;    // complaints before anyone walks off
        public const float ElectricFuelPrice = 0.5f;   // fuel price multiplier
        public const float HeritageBarkerBonus = 0.25f;
        public const float HeritageTip = 1.25f;
        public const float AirportBalikbayan = 2f;
        public const float NightOwlFare = 0.10f;

        public static bool LowEmission(JeepneyLivery j) => j != null && (j.Style == JeepneyStyle.Modern || j.ClosedBody);

        /// <summary>Exhaust smoke amount (1 = old diesel jeepney).</summary>
        public static float Smoke(JeepneyLivery j) => j?.Perk == JeepneyPerk.Electric ? 0f : LowEmission(j) ? 0.3f : 1f;

        /// <summary>Engine loudness (1 = old diesel jeepney).</summary>
        public static float EngineVolume(JeepneyLivery j) => j?.Perk == JeepneyPerk.Electric ? 0.3f : LowEmission(j) ? 0.6f : 1f;

        public static float FuelPrice(JeepneyLivery j) => j?.Perk == JeepneyPerk.Electric ? ElectricFuelPrice : 1f;
        public static float Patience(JeepneyLivery j) => j?.Perk == JeepneyPerk.Aircon ? AirconPatience : 1f;
        public static int ExtraComplaints(JeepneyLivery j) => j?.Perk == JeepneyPerk.Aircon ? AirconExtraComplaints : 0;
        public static float BarkerBonus(JeepneyLivery j) => j?.Perk == JeepneyPerk.Heritage ? HeritageBarkerBonus : 0f;
        public static float TipMultiplier(JeepneyLivery j) => j?.Perk == JeepneyPerk.Heritage ? HeritageTip : 1f;
        public static float BalikbayanBoost(JeepneyLivery j) => j?.Perk == JeepneyPerk.Airport ? AirportBalikbayan : 1f;
        public static float FareFactor(JeepneyLivery j, bool night) => j?.Perk == JeepneyPerk.NightOwl && night ? 1f + NightOwlFare : 1f;

        /// <summary>Garage text for the perk (null = none).</summary>
        public static string Text(JeepneyLivery j)
        {
            switch (j?.Perk ?? JeepneyPerk.None)
            {
                case JeepneyPerk.Aircon: return "AIRCON: riders stay patient longer in traffic";
                case JeepneyPerk.Electric: return "ELECTRIC: charging costs half · no smoke, quiet";
                case JeepneyPerk.BigCapacity: return "MINIBUS: most seats, slower lane changes";
                case JeepneyPerk.Heritage: return "CROWD FAVORITE: more barkers, bigger tips";
                case JeepneyPerk.Airport: return "AIRPORT: balikbayans board twice as often";
                case JeepneyPerk.NightOwl: return "NIGHT OWL: +10% fares after dark";
                default: return LowEmission(j) ? "Modern engine: less smoke, less noise" : null;
            }
        }
    }
}
