using System;
using System.Collections.Generic;

namespace JeepneyRush.Logic
{
    public enum MissionType
    {
        DistanceInRun, CoinsInRun, PassengersInRun, DeliveriesInRun, CleanDistanceInRun, StreakInRun,
        TotalDistance, TotalCoins, TotalPassengers, TotalDeliveries, TotalPowerUps, TotalSmoothHumps, Runs, TotalWetDistance, TotalRefuels,
    }

    public sealed class MissionDef
    {
        public MissionType Type;
        public int Target;
        public int Reward;
        public string Text;
        public bool InRun => Type <= MissionType.StreakInRun;
    }

    /// <summary>
    /// Daily (3) and weekly (2) missions, generated deterministically from the date so
    /// every player gets the same set offline. Progress lives in the save.
    /// </summary>
    public static class Missions
    {
        public const int DailyCount = 3;
        public const int WeeklyCount = 2;

        private static readonly (MissionType type, int min, int max, int step, string text)[] DailyPool =
        {
            (MissionType.DistanceInRun, 600, 1800, 100, "Drive {0} m in one run"),
            (MissionType.CoinsInRun, 40, 120, 10, "Collect {0} coins in one run"),
            (MissionType.PassengersInRun, 8, 20, 1, "Pick up {0} passengers in one run"),
            (MissionType.DeliveriesInRun, 5, 15, 1, "Deliver {0} passengers in one run"),
            (MissionType.CleanDistanceInRun, 400, 1200, 100, "Drive {0} m without crashing"),
            (MissionType.StreakInRun, 3, 6, 1, "Reach a x{0} delivery streak"),
            (MissionType.TotalPowerUps, 3, 8, 1, "Use {0} power-ups"),
            (MissionType.TotalSmoothHumps, 5, 15, 1, "Cross {0} speed humps slowly"),
            (MissionType.TotalWetDistance, 300, 900, 100, "Drive {0} m in the rain"),
            (MissionType.TotalRefuels, 2, 5, 1, "Refuel at {0} gas stations"),
            (MissionType.Runs, 3, 6, 1, "Play {0} runs"),
        };

        private static readonly (MissionType type, int min, int max, int step, string text)[] WeeklyPool =
        {
            (MissionType.TotalDistance, 10000, 25000, 1000, "Drive {0} m this week"),
            (MissionType.TotalCoins, 800, 2000, 100, "Earn {0} coins this week"),
            (MissionType.TotalPassengers, 120, 300, 10, "Pick up {0} passengers this week"),
            (MissionType.TotalDeliveries, 80, 200, 10, "Deliver {0} passengers this week"),
            (MissionType.TotalPowerUps, 25, 60, 5, "Use {0} power-ups this week"),
            (MissionType.TotalWetDistance, 3000, 8000, 500, "Drive {0} m in rain or typhoon this week"),
        };

        public static int DayKey(DateTime local) => local.Year * 10000 + local.Month * 100 + local.Day;

        /// <summary>Weeks since Monday 1 Jan 2024 (stable, timezone-local).</summary>
        public static int WeekKey(DateTime local) => (int)Math.Floor((local.Date - new DateTime(2024, 1, 1)).TotalDays / 7.0) + 1;

        private static MissionDef[] Generate((MissionType type, int min, int max, int step, string text)[] pool, int count, int seed, float rewardBase)
        {
            var rng = new GameRandom(seed * 7919 + 17);
            var idx = new List<int>();
            for (int i = 0; i < pool.Length; i++) idx.Add(i);
            var result = new MissionDef[count];
            for (int m = 0; m < count; m++)
            {
                int pick = idx[rng.Int(0, idx.Count - 1)];
                idx.Remove(pick);
                var p = pool[pick];
                int steps = (p.max - p.min) / p.step;
                int target = p.min + rng.Int(0, steps) * p.step;
                float difficulty = (target - p.min) / (float)Math.Max(1, p.max - p.min);
                result[m] = new MissionDef { Type = p.type, Target = target, Reward = (int)(rewardBase * (1 + difficulty)), Text = string.Format(p.text, target.ToString("N0")) };
            }
            return result;
        }

        public static MissionDef[] Daily(int dayKey) => Generate(DailyPool, DailyCount, dayKey, 100);
        public static MissionDef[] Weekly(int weekKey) => Generate(WeeklyPool, WeeklyCount, weekKey, 500);

        public static int Value(MissionType t, RunSummary r)
        {
            switch (t)
            {
                case MissionType.DistanceInRun: case MissionType.TotalDistance: return (int)r.Distance;
                case MissionType.CoinsInRun: case MissionType.TotalCoins: return r.Coins;
                case MissionType.PassengersInRun: case MissionType.TotalPassengers: return r.Passengers;
                case MissionType.DeliveriesInRun: case MissionType.TotalDeliveries: return r.Deliveries;
                case MissionType.CleanDistanceInRun: return (int)r.CleanDistance;
                case MissionType.StreakInRun: return r.BestStreak;
                case MissionType.TotalPowerUps: return r.PowerUps;
                case MissionType.TotalSmoothHumps: return r.SmoothHumps;
                case MissionType.TotalWetDistance: return (int)r.WetDistance;
                case MissionType.TotalRefuels: return r.Refuels;
                case MissionType.Runs: return 1;
            }
            return 0;
        }

        /// <summary>Resets daily/weekly progress when the day/week changes.</summary>
        public static void Refresh(SaveData d, int dayKey, int weekKey)
        {
            if (d.dailyKey != dayKey)
            {
                d.dailyKey = dayKey;
                d.dailyProgress = new int[DailyCount];
                d.dailyClaimed = new bool[DailyCount];
            }
            if (d.weeklyKey != weekKey)
            {
                d.weeklyKey = weekKey;
                d.weeklyProgress = new int[WeeklyCount];
                d.weeklyClaimed = new bool[WeeklyCount];
            }
        }

        /// <summary>Adds a run's progress. Returns missions completed by this run.</summary>
        public static List<MissionDef> ApplyRun(SaveData d, RunSummary r, int dayKey, int weekKey)
        {
            Refresh(d, dayKey, weekKey);
            var done = new List<MissionDef>();
            void Update(MissionDef[] defs, int[] progress)
            {
                for (int i = 0; i < defs.Length; i++)
                {
                    bool wasDone = progress[i] >= defs[i].Target;
                    int v = Value(defs[i].Type, r);
                    progress[i] = defs[i].InRun ? Math.Max(progress[i], v) : progress[i] + v;
                    progress[i] = Math.Min(progress[i], defs[i].Target);
                    if (!wasDone && progress[i] >= defs[i].Target) done.Add(defs[i]);
                }
            }
            Update(Daily(dayKey), d.dailyProgress);
            Update(Weekly(weekKey), d.weeklyProgress);
            return done;
        }

        /// <summary>Claims a completed mission's reward. Returns coins paid (0 if not claimable).</summary>
        public static int Claim(SaveData d, bool weekly, int index)
        {
            var defs = weekly ? Weekly(d.weeklyKey) : Daily(d.dailyKey);
            var progress = weekly ? d.weeklyProgress : d.dailyProgress;
            var claimed = weekly ? d.weeklyClaimed : d.dailyClaimed;
            if (index < 0 || index >= defs.Length || claimed[index] || progress[index] < defs[index].Target) return 0;
            claimed[index] = true;
            d.coins += defs[index].Reward;
            return defs[index].Reward;
        }
    }

    public sealed class AchievementDef
    {
        public string Id;
        public string Name;
        public string Description;
        public float Target;
        public int Reward;
        public Func<SaveData, float> Stat;
    }

    /// <summary>Lifetime achievements (from the design document plus a few extras).</summary>
    public static class Achievements
    {
        public static readonly AchievementDef[] All =
        {
            new AchievementDef { Id = "first_passenger", Name = "First Passenger", Description = "Pick up your first passenger.", Target = 1, Reward = 50, Stat = d => d.totalPassengers },
            new AchievementDef { Id = "meters_1000", Name = "1,000 Meters Driven", Description = "Drive 1,000 m in a single run.", Target = 1000, Reward = 150, Stat = d => d.bestDistance },
            new AchievementDef { Id = "traffic_survivor", Name = "Traffic Survivor", Description = "Drive 1,500 m without a single crash.", Target = 1500, Reward = 300, Stat = d => d.bestCleanDistance },
            new AchievementDef { Id = "coin_collector", Name = "Coin Collector", Description = "Earn 1,000 coins in total.", Target = 1000, Reward = 200, Stat = d => d.totalCoins },
            new AchievementDef { Id = "passenger_master", Name = "Passenger Master", Description = "Pick up 500 passengers in total.", Target = 500, Reward = 400, Stat = d => d.totalPassengers },
            new AchievementDef { Id = "deliveries_100", Name = "100 Deliveries Completed", Description = "Deliver 100 passengers to their stop.", Target = 100, Reward = 300, Stat = d => d.totalDeliveries },
            new AchievementDef { Id = "highway_legend", Name = "Highway Legend", Description = "Drive 5,000 m in a single run.", Target = 5000, Reward = 1000, Stat = d => d.bestDistance },
            new AchievementDef { Id = "power_player", Name = "Power Player", Description = "Use 50 power-ups.", Target = 50, Reward = 250, Stat = d => d.totalPowerUps },
            new AchievementDef { Id = "rain_or_shine", Name = "Rain or Shine", Description = "Drive 2,000 m in rain or typhoon.", Target = 2000, Reward = 300, Stat = d => d.totalWetDistance },
            new AchievementDef { Id = "smooth_operator", Name = "Smooth Operator", Description = "Cross 50 speed humps slowly.", Target = 50, Reward = 250, Stat = d => d.totalSmoothHumps },
            new AchievementDef { Id = "collector", Name = "Jeepney Collector", Description = "Own 5 jeepneys.", Target = 5, Reward = 500, Stat = d => d.ownedJeepneys?.Count ?? 0 },
            new AchievementDef { Id = "fully_loaded", Name = "Fully Loaded", Description = "Max out any upgrade.", Target = Upgrades.MaxLevel, Reward = 300, Stat = d => { int m = 0; if (d.upgradeLevels != null) foreach (var l in d.upgradeLevels) m = Math.Max(m, l); return m; } },
            new AchievementDef { Id = "island_hopper", Name = "Island Hopper", Description = "Unlock every route.", Target = Environments.All.Length, Reward = 500, Stat = d => { int n = 0; foreach (var e in Environments.All) if (Environments.IsUnlocked(e.Kind, d.bestDistance)) n++; return n; } },
        };

        public static float Progress(SaveData d, AchievementDef a) => Math.Min(1f, a.Stat(d) / a.Target);

        /// <summary>Unlocks every achievement whose target is reached. Returns the newly unlocked ones.</summary>
        public static List<AchievementDef> Evaluate(SaveData d)
        {
            var list = new List<AchievementDef>();
            d.achievementsUnlocked ??= new List<string>();
            foreach (var a in All)
                if (!d.achievementsUnlocked.Contains(a.Id) && a.Stat(d) >= a.Target)
                {
                    d.achievementsUnlocked.Add(a.Id);
                    list.Add(a);
                }
            return list;
        }

        public static int Claim(SaveData d, string id)
        {
            var a = Array.Find(All, x => x.Id == id);
            if (a == null || !d.achievementsUnlocked.Contains(id) || d.achievementsClaimed.Contains(id)) return 0;
            d.achievementsClaimed.Add(id);
            d.coins += a.Reward;
            return a.Reward;
        }
    }
}
