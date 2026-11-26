using System;
using System.Collections.Generic;

namespace JeepneyRush.Logic
{
    public enum UpgradeKind { Engine, Brakes, Armor, Capacity, CoinBonus, PowerUpDuration }

    /// <summary>
    /// Garage upgrades (shared by all jeepneys) and how upgrades + the chosen jeepney turn
    /// the base balance into the run's effective settings.
    /// Note: "Fuel efficiency" from the design doc has no meaning in this version (there is
    /// no fuel mechanic), so it is replaced by Power-up Duration.
    /// </summary>
    public static class Upgrades
    {
        public const int MaxLevel = 5;
        public static readonly UpgradeKind[] All = (UpgradeKind[])Enum.GetValues(typeof(UpgradeKind));

        public static string Name(UpgradeKind k)
        {
            switch (k)
            {
                case UpgradeKind.Engine: return "Engine";
                case UpgradeKind.Brakes: return "Brakes & Steering";
                case UpgradeKind.Armor: return "Armor";
                case UpgradeKind.Capacity: return "Passenger Capacity";
                case UpgradeKind.CoinBonus: return "Coin Bonus";
                default: return "Power-up Duration";
            }
        }

        public static string Effect(UpgradeKind k, int level)
        {
            switch (k)
            {
                case UpgradeKind.Engine: return $"+{level} m/s top speed, stronger gas";
                case UpgradeKind.Brakes: return $"{level * 8}% faster lane changes & stops";
                case UpgradeKind.Armor: return $"+{ArmorHealth(level)} health, longer invulnerability";
                case UpgradeKind.Capacity: return $"+{level * 2} seats";
                case UpgradeKind.CoinBonus: return $"+{level * 10}% coins per run";
                default: return $"+{level * 12}% power-up time";
            }
        }

        /// <summary>Cost of buying the next level (from <paramref name="currentLevel"/>), or -1 at max.</summary>
        public static int Cost(UpgradeKind k, int currentLevel)
        {
            if (currentLevel >= MaxLevel) return -1;
            int[] costs = { 150, 350, 700, 1200, 2000 };
            float kindScale = k == UpgradeKind.Armor || k == UpgradeKind.Capacity ? 1.25f : 1f;
            return (int)(costs[currentLevel] * kindScale);
        }

        public static int ArmorHealth(int level) => level >= 4 ? 2 : level >= 2 ? 1 : 0;

        public static float CoinBonusFraction(int level) => level * 0.10f;
        public static float PowerUpDurationScale(int level) => 1f + level * 0.12f;

        /// <summary>Effective balance for a run with this jeepney and these upgrade levels.</summary>
        public static BalanceConfig Apply(BalanceConfig baseCfg, JeepneyLivery jeepney, Func<UpgradeKind, int> level)
        {
            var c = baseCfg.Clone();
            int engine = level(UpgradeKind.Engine), brakes = level(UpgradeKind.Brakes), armor = level(UpgradeKind.Armor), cap = level(UpgradeKind.Capacity);
            c.maxSpeed += engine * 1f + jeepney.SpeedBonus;
            c.gasMultiplier += engine * 0.03f;
            c.laneChangeDuration = Math.Max(0.12f, c.laneChangeDuration * (1f - brakes * 0.08f) - jeepney.HandlingBonus);
            c.brakeDecel *= 1f + brakes * 0.08f;
            c.startHealth += ArmorHealth(armor) + jeepney.HealthBonus;
            c.invulnerabilityTime += armor * 0.1f;
            c.passengerCapacity = Math.Min(15, c.passengerCapacity + cap * 2 + jeepney.CapacityBonus);
            return c;
        }
    }

    /// <summary>Player level from XP (earned every run). Each level-up pays a coin reward.</summary>
    public static class PlayerLevel
    {
        public static int LevelFor(int xp) => 1 + (int)Math.Floor(Math.Sqrt(Math.Max(0, xp) / 120.0));
        public static int XpForLevel(int level) => (int)(120 * Math.Pow(Math.Max(0, level - 1), 2));
        public static int Reward(int newLevel) => 100 * newLevel;

        public static int XpFromRun(RunSummary r) =>
            (int)(r.Distance / 10f) + r.Passengers * 5 + r.Deliveries * 10 + r.PowerUps * 5;
    }

    /// <summary>Everything that happened in one run (missions, achievements, XP use this).</summary>
    public struct RunSummary
    {
        public int Score;
        public float Distance;
        public int Coins;
        /// <summary>Gems picked up on the road (premium currency, capped per run and per day).</summary>
        public int Gems;
        /// <summary>Near misses and overtakes this run (arcade combo).</summary>
        public int NearMisses, Overtakes;
        public int Passengers;
        public int Deliveries;
        public int PowerUps;
        /// <summary>Speed humps crossed slowly (no jolt).</summary>
        public int SmoothHumps;
        /// <summary>Metres driven on a wet road (rain/typhoon).</summary>
        public float WetDistance;
        /// <summary>Gas-station fill-ups this run.</summary>
        public int Refuels;
        public int Hits;
        /// <summary>Longest distance driven without a collision.</summary>
        public float CleanDistance;
        public int BestStreak;
        public bool Crashed;
        public EnvironmentKind Environment;
        /// <summary>Career shift id (null = free ride).</summary>
        public string Career;
    }
}
