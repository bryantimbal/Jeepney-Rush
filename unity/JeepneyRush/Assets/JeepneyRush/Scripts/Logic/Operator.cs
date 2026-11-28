using System;

namespace JeepneyRush.Logic
{
    /// <summary>
    /// Operator mode (unlocked with the OPERATOR – PRANGKISA license): hire drivers for jeepneys you
    /// own. Each one earns boundary while you are away – <see cref="RatePerHour"/> coins an hour, up to
    /// <see cref="CapHours"/> hours between collections. Slots: 1 with the operator license, 2 with
    /// HARI NG KALSADA, plus up to <see cref="SlotPrices"/>.Length bought with coins. The server
    /// (jr_fleet_*) computes the same from its own clock.
    /// </summary>
    public static class Operator
    {
        public const int MinRank = 3, CapHours = 8, MaxRate = 80;
        public static readonly int[] SlotPrices = { 3000, 8000, 15000 };

        public static readonly string[] DriverNames =
        {
            "Mang Kanor", "Kuya Boy", "Mang Ambo", "Kuya Jun-Jun", "Ate Baby", "Mang Pido", "Kuya Dodong", "Mang Islaw", "Kuya Bert", "Aling Nena",
        };

        public static bool Unlocked(int careerRank) => careerRank >= MinRank;
        public static int BaseSlots(int careerRank) => careerRank >= 4 ? 2 : careerRank >= MinRank ? 1 : 0;
        public static int Slots(int careerRank, int bought) => careerRank < MinRank ? 0 : BaseSlots(careerRank) + Math.Max(0, Math.Min(SlotPrices.Length, bought));

        /// <summary>Coins the next bought slot costs (0 = no more slots to buy).</summary>
        public static int NextSlotPrice(int bought) => bought >= 0 && bought < SlotPrices.Length ? SlotPrices[bought] : 0;

        /// <summary>Boundary an hour for a jeepney (better jeepneys earn more).</summary>
        public static int RatePerHour(JeepneyLivery j) => RateForPrice(j?.Price ?? 0);
        public static int RateForPrice(int price) => Math.Min(MaxRate, 30 + Math.Max(0, price) / 200);

        /// <summary>Coins waiting since <paramref name="sinceUnix"/> (capped at <see cref="CapHours"/>).</summary>
        public static int Pending(int rate, long sinceUnix, long nowUnix)
        {
            double hours = Math.Max(0, Math.Min(CapHours, (nowUnix - sinceUnix) / 3600.0));
            return (int)Math.Floor(rate * hours);
        }

        public static string DriverFor(int slot) => DriverNames[Math.Abs(slot) % DriverNames.Length];
    }
}
