using System;

namespace JeepneyRush.Logic
{
    /// <summary>A roadside gas station ("gasolinahan") with a refuelling pad in the right curb lane.</summary>
    public sealed class GasStation
    {
        public int Id;
        /// <summary>World distance (metres since the run started) of the pad centre.</summary>
        public float At;
        public int Lane;
        public bool Resolved;
    }

    /// <summary>The jeepney's diesel tank: burns per metre (more on GAS) plus a little while idling.</summary>
    public sealed class FuelTank
    {
        private readonly BalanceConfig _cfg;
        public float Capacity { get; }
        public float Fuel { get; private set; }
        public float Fraction => Capacity > 0 ? Fuel / Capacity : 0;
        public bool Empty => Fuel <= 0f;
        public bool Low => Fraction <= _cfg.fuelLowFraction;

        public FuelTank(BalanceConfig cfg)
        {
            _cfg = cfg;
            Capacity = Math.Max(1f, cfg.fuelCapacity);
            Fuel = Capacity;
        }

        /// <summary>Burns fuel for <paramref name="metres"/> driven (forward or reverse) over <paramref name="dt"/> seconds.</summary>
        public void Burn(float metres, bool gas, float dt)
        {
            float use = Math.Abs(metres) * _cfg.fuelPerMetre * (gas ? _cfg.fuelGasFactor : 1f) + Math.Max(0, dt) * _cfg.fuelIdlePerSecond;
            Fuel = Math.Max(0f, Fuel - use);
        }

        public void Add(float litres) => Fuel = Math.Min(Capacity, Fuel + Math.Max(0, litres));

        /// <summary>Metres a full tank lasts while always holding GAS (worst case).</summary>
        public static float RangeOnGas(BalanceConfig cfg) => cfg.fuelCapacity / (cfg.fuelPerMetre * cfg.fuelGasFactor);
    }

    public struct RefuelQuote
    {
        /// <summary>Litres paid for with this run's coins.</summary>
        public float Paid;
        /// <summary>Litres given on credit ("utang muna") when you can't afford enough to reach the next station.</summary>
        public float Credit;
        public int Cost;
        public float Total => Paid + Credit;
    }

    public static class FuelRules
    {
        /// <summary>
        /// Fill-up at a station: as much as the run's coins can buy; if that leaves the tank
        /// below <see cref="BalanceConfig.fuelCreditFraction"/>, the attendant tops it up to
        /// that level on credit so a broke driver is never stranded right after a station.
        /// </summary>
        public static RefuelQuote Quote(float fuel, float capacity, int coins, BalanceConfig cfg)
        {
            var q = new RefuelQuote();
            float need = Math.Max(0, capacity - fuel);
            float price = Math.Max(0.0001f, cfg.fuelPricePerLitre);
            q.Paid = Math.Min(need, Math.Max(0, coins) / price);
            q.Cost = Math.Min(Math.Max(0, coins), (int)Math.Ceiling(q.Paid * price - 1e-4));
            float floor = capacity * cfg.fuelCreditFraction;
            if (fuel + q.Paid < floor) q.Credit = floor - fuel - q.Paid;
            return q;
        }
    }
}
