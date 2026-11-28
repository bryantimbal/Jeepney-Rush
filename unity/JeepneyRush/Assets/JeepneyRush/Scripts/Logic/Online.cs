using System;
using System.Collections.Generic;

namespace JeepneyRush.Logic
{
    /// <summary>
    /// Mission days, weeks and the daily challenge follow Philippine time (UTC+8) so every
    /// player – and the server – agree on what "today" is.
    /// </summary>
    public static class PhilippineTime
    {
        public static DateTime Now => DateTime.UtcNow.AddHours(8);
        public static int DayKey => Missions.DayKey(Now);
        public static int WeekKey => Missions.WeekKey(Now);
    }

    /// <summary>
    /// Same seeded run for everyone today: identical traffic, stops, weather and road.
    /// Must match <c>jr_daily_challenge()</c> in the Supabase migration (cross-checked by tests).
    /// </summary>
    public struct DailyChallenge
    {
        public int DayKey;
        public int Seed;
        public EnvironmentKind Environment;

        public static DailyChallenge For(int dayKey)
        {
            var rng = new GameRandom(dayKey * 31 + 7);
            int seed = 1 + (int)(rng.Next() * 2147483000.0);
            var env = (EnvironmentKind)rng.Int(0, Environments.All.Length - 1);
            return new DailyChallenge { DayKey = dayKey, Seed = seed, Environment = env };
        }

        public static DailyChallenge Today => For(PhilippineTime.DayKey);
    }

    /// <summary>
    /// What the client sends to the server after a run (JsonUtility-friendly, snake_case
    /// to match the SQL function arguments).
    /// </summary>
    [Serializable]
    public class RunReport
    {
        public string run_id;
        public int seed;
        public int env;
        public float duration;
        public int score;
        public float distance;
        public int coins;
        public int gems;
        public int near_misses;
        public int overtakes;
        public int passengers;
        public int deliveries;
        public int power_ups;
        public int smooth_humps;
        public float wet_distance;
        public int refuels;
        public int hits;
        public float clean_distance;
        public int best_streak;
        public bool daily;
        public int day_key;
        public string jeepney;
        /// <summary>Career shift id ("" = free ride).</summary>
        public string career = "";
        /// <summary>Game mode ("" free ride, "rush", "night", "ghost"): mode runs go on their own boards.</summary>
        public string mode = "";
        /// <summary>Passenger money (part of coins): the career boundary is paid from it.</summary>
        public int fares;
        /// <summary>Spent on diesel (already taken out of coins).</summary>
        public int gas;
        // passenger personalities (missions)
        public int tips;
        public int dodgers_caught;
        public int smooth_lolas;
        public int on_time;
        /// <summary>Christmas event: parol tokens picked up and caroler groups carried (the server caps them).</summary>
        public int parols, carolers;
        /// <summary>Sticker album: the postcard picked up this run (-1 = none).</summary>
        public int postcard = -1;
        public int client_version = 3;

        public static RunReport From(RunSummary r, float duration, int seed, bool daily, int dayKey, string jeepney) => new RunReport
        {
            run_id = Guid.NewGuid().ToString(),
            seed = seed, env = (int)r.Environment, duration = duration, score = r.Score, distance = r.Distance, coins = r.Coins, gems = r.Gems, near_misses = r.NearMisses, overtakes = r.Overtakes,
            passengers = r.Passengers, deliveries = r.Deliveries, power_ups = r.PowerUps, smooth_humps = r.SmoothHumps,
            wet_distance = r.WetDistance, refuels = r.Refuels, hits = r.Hits, clean_distance = r.CleanDistance,
            best_streak = r.BestStreak, daily = daily, day_key = dayKey, jeepney = jeepney, career = r.Career ?? "",
            fares = r.Fares, gas = r.Gas, tips = r.Tips, dodgers_caught = r.DodgersCaught, smooth_lolas = r.SmoothLolas, on_time = r.OnTime,
        };
    }

    /// <summary>
    /// Plausibility limits for a run, derived from the game's own numbers with generous
    /// headroom (measured: ≤ 0.21 coins/m spawned, ≤ 4.6 stops/km, ≤ 2.2 power-ups/km).
    /// Hard violations reject the run; soft ones are clamped. Mirrors <c>jr_check_run</c> in SQL.
    /// </summary>
    public static class RunBounds
    {
        public const float MaxAverageSpeed = 50f;   // m/s – top speed with engine, gas and turbo bursts
        public const float MinDuration = 5f;
        public const float MaxDuration = 4 * 3600f;
        public const int MaxCapacity = 15;
        public const int MaxFarePerDelivery = 42;   // (fare 5 + group 2) × streak 3 × passenger bonus 2
        /// <summary>Biggest tip per delivery: balikbayan 10 × ★5 rating 1.2 × passenger bonus 2 (rounded up).</summary>
        public const int MaxTipPerDelivery = 25;
        /// <summary>A full tank at the station price (fuelCapacity 100 × 0.4), with headroom.</summary>
        public const int MaxGasPerRefuel = 60;

        public static string HardViolation(RunReport r)
        {
            if (r == null) return "empty";
            if (string.IsNullOrEmpty(r.run_id) || r.run_id.Length > 64) return "run_id";
            if (!(r.duration >= MinDuration) || r.duration > MaxDuration) return "duration";
            if (!(r.distance >= 0) || r.distance > MaxAverageSpeed * r.duration + 50f) return "too_fast";
            if (r.env < 0 || r.env >= Environments.All.Length) return "env";
            if (r.coins < 0 || r.gems < 0 || r.near_misses < 0 || r.overtakes < 0 || r.score < 0 || r.passengers < 0 || r.deliveries < 0 || r.power_ups < 0 || r.smooth_humps < 0 || r.refuels < 0 || r.hits < 0) return "negative";
            if (r.clean_distance > r.distance + 1f || r.wet_distance > r.distance + 1f || r.clean_distance < 0 || r.wet_distance < 0) return "distance_parts";
            if (r.deliveries > r.passengers) return "deliveries";
            return null;
        }

        public static int MaxPassengers(float d) => (int)((d / 200f + 2f) * MaxCapacity);
        public static int MaxPowerUps(float d) => (int)(d / 250f) + 3;
        public static int MaxHumps(float d) => (int)(d / 150f) + 2;
        public static int MaxRefuels(float d) => (int)(d / 700f) + 1;
        public static int MaxCoins(float d, int deliveries) => (int)(d * 1.0f) + deliveries * MaxFarePerDelivery + 50;
        /// <summary>Overtakes need traffic to pass: at most one per 6 m driven (+ slack).</summary>
        public static int MaxOvertakes(float d) => (int)(d / 6f) + 5;
        public static int MaxNearMisses(float d) => (int)(d / 10f) + 5;

        public static int MaxScore(float d, int coins, int passengers, int deliveries, int humps, int nearMisses, int overtakes) =>
            (int)Math.Min(int.MaxValue, MaxScore(d, coins, passengers, deliveries, humps) + ComboMeter.MaxPoints(nearMisses, overtakes));

        public static int MaxScore(float d, int coins, int passengers, int deliveries, int humps) =>
            (int)(d * 1.5f) + coins * 10 + passengers * 50 + deliveries * 600 + humps * 50 + (int)(150 * (d / 300f + 1)) + 1000;

        /// <summary>Clamps the soft values in place (after the hard check passed).</summary>
        public static void Clamp(RunReport r)
        {
            r.passengers = Math.Min(r.passengers, MaxPassengers(r.distance));
            r.deliveries = Math.Min(r.deliveries, r.passengers);
            r.power_ups = Math.Min(r.power_ups, MaxPowerUps(r.distance));
            r.smooth_humps = Math.Min(r.smooth_humps, MaxHumps(r.distance));
            r.refuels = Math.Min(r.refuels, MaxRefuels(r.distance));
            r.coins = Math.Min(r.coins, MaxCoins(r.distance, r.deliveries));
            r.gems = Math.Min(r.gems, RoadGems.MaxForDistance(r.distance));
            r.overtakes = Math.Min(r.overtakes, MaxOvertakes(r.distance));
            r.near_misses = Math.Min(r.near_misses, MaxNearMisses(r.distance));
            r.gas = Math.Max(0, Math.Min(r.gas, r.refuels * MaxGasPerRefuel));
            r.fares = Math.Max(0, Math.Min(r.fares, r.coins + r.gas));
            r.tips = Math.Max(0, Math.Min(r.tips, r.deliveries * MaxTipPerDelivery));
            r.dodgers_caught = Math.Max(0, Math.Min(r.dodgers_caught, r.deliveries));
            r.smooth_lolas = Math.Max(0, Math.Min(r.smooth_lolas, r.deliveries));
            r.on_time = Math.Max(0, Math.Min(r.on_time, r.deliveries));
            r.score = Math.Min(r.score, MaxScore(r.distance, r.coins, r.passengers, r.deliveries, r.smooth_humps, r.near_misses, r.overtakes));
        }
    }

    /// <summary>The server's authoritative copy of the player's progress (returned by every RPC).</summary>
    [Serializable]
    public class ServerSnapshot
    {
        public string user_id;
        public string display_name;
        public string friend_code;
        public bool is_anonymous;
        public int coins;
        public int xp;
        public int level;
        public int best_score;
        public float best_distance;
        public float best_clean;
        public int runs;
        public float total_distance;
        public int total_coins;
        public int total_passengers;
        public int total_deliveries;
        public int total_power_ups;
        public int total_smooth_humps;
        public float total_wet_distance;
        public int total_refuels;
        public int gems;
        public bool ad_free;
        public string[] owned_products = new string[0];
        public string[] owned = new string[0];
        public int[] upgrades = new int[0];
        public int day_key;
        public int[] daily_progress = new int[0];
        public bool[] daily_claimed = new bool[0];
        public int week_key;
        public int[] weekly_progress = new int[0];
        public bool[] weekly_claimed = new bool[0];
        public string[] achievements_unlocked = new string[0];
        public string[] achievements_claimed = new string[0];
        /// <summary>Career stars per shift (Career.Shifts order) and the license rank already rewarded.</summary>
        public int[] career = new int[0];
        public int career_rank;
        /// <summary>Talyer items bought (Customization ids).</summary>
        public string[] cosmetics = new string[0];
    }

    /// <summary>Result of <c>submit_run</c>.</summary>
    [Serializable]
    public class SubmitResult
    {
        public bool accepted;
        public bool duplicate;
        public string reason;
        public bool clamped;
        public int credited_coins;
        public int coin_bonus;
        public int xp_gained;
        public int level_reward;
        public int rank_all;
        public int rank_week;
        public int rank_daily;
        /// <summary>Stat class the run counted for (JeepneyClass; -1 = old server without classes).</summary>
        public int @class = -1;
        public int class_rank_all;
        public int class_rank_week;
        public int class_rank_daily;
        /// <summary>Game mode board the run went on ("rush" / "night", "" = the normal boards) and its weekly ranks.</summary>
        public string mode = "";
        public int mode_rank;
        public int mode_class_rank;
        public ServerSnapshot snapshot;
    }

    public static class ServerSync
    {
        /// <summary>
        /// Overwrites the economy and progress of the local save with the server's copy
        /// (server wins). Settings, tutorials and the selected route/jeepney stay local.
        /// </summary>
        public static void Apply(SaveData d, ServerSnapshot s)
        {
            if (d == null || s == null) return;
            d.coins = s.coins;
            d.xp = s.xp;
            d.level = PlayerLevel.LevelFor(s.xp);
            d.bestScore = s.best_score;
            d.bestDistance = s.best_distance;
            d.bestCleanDistance = s.best_clean;
            d.runs = s.runs;
            d.totalDistance = s.total_distance;
            d.totalCoins = s.total_coins;
            d.totalPassengers = s.total_passengers;
            d.totalDeliveries = s.total_deliveries;
            d.totalPowerUps = s.total_power_ups;
            d.totalSmoothHumps = s.total_smooth_humps;
            d.totalWetDistance = s.total_wet_distance;
            d.ownedJeepneys = new List<string>(s.owned ?? new string[0]);
            d.upgradeLevels = new int[Upgrades.All.Length];
            if (s.upgrades != null) for (int i = 0; i < d.upgradeLevels.Length && i < s.upgrades.Length; i++) d.upgradeLevels[i] = s.upgrades[i];
            if (s.day_key != 0)
            {
                d.dailyKey = s.day_key;
                d.dailyProgress = Copy(s.daily_progress, Missions.DailyCount);
                d.dailyClaimed = Copy(s.daily_claimed, Missions.DailyCount);
            }
            if (s.week_key != 0)
            {
                d.weeklyKey = s.week_key;
                d.weeklyProgress = Copy(s.weekly_progress, Missions.WeeklyCount);
                d.weeklyClaimed = Copy(s.weekly_claimed, Missions.WeeklyCount);
            }
            d.achievementsUnlocked = new List<string>(s.achievements_unlocked ?? new string[0]);
            d.achievementsClaimed = new List<string>(s.achievements_claimed ?? new string[0]);
            d.gems = s.gems;
            d.adFree = s.ad_free;
            d.ownedProducts = new List<string>(s.owned_products ?? new string[0]);
            // an older server (without cosmetics) keeps the local list; the server never forgets an item
            if (s.cosmetics != null && s.cosmetics.Length > 0) d.ownedCosmetics = new List<string>(s.cosmetics);
            if (s.career != null && s.career.Length > 0) // an older server without career mode keeps the local progress
            {
                d.careerMasks = Copy(s.career, Career.Shifts.Length);
                d.careerRank = s.career_rank;
            }
            SaveValidation.Sanitize(d);
        }

        private static T[] Copy<T>(T[] src, int n)
        {
            var a = new T[n];
            if (src != null) Array.Copy(src, a, Math.Min(n, src.Length));
            return a;
        }

        /// <summary>One-time upload of progress made before signing in (the server caps it).</summary>
        public static LegacyImport ToImport(SaveData d) => new LegacyImport
        {
            coins = d.coins, xp = d.xp, best_score = d.bestScore, best_distance = d.bestDistance, runs = d.runs,
            owned = (d.ownedJeepneys ?? new List<string>()).ToArray(), upgrades = (int[])(d.upgradeLevels ?? new int[0]).Clone(),
        };
    }

    [Serializable]
    public class LegacyImport
    {
        public int coins;
        public int xp;
        public int best_score;
        public float best_distance;
        public int runs;
        public string[] owned;
        public int[] upgrades;
        /// <summary>Server-side caps (also enforced in SQL).</summary>
        public const int MaxCoins = 20000, MaxXp = 200000, MaxScore = 2000000;
    }

    /// <summary>Runs waiting to be uploaded (played offline or while the server was unreachable).</summary>
    [Serializable]
    public class OfflineQueue
    {
        public const int Limit = 50;
        public List<RunReport> runs = new List<RunReport>();

        public void Add(RunReport r)
        {
            if (r == null) return;
            runs.RemoveAll(x => x.run_id == r.run_id);
            runs.Add(r);
            while (runs.Count > Limit) runs.RemoveAt(0); // oldest first
        }

        public void Remove(string runId) => runs.RemoveAll(x => x.run_id == runId);
        public int Count => runs.Count;
    }

    /// <summary>Player-facing validation of names and friend codes (the server re-checks).</summary>
    public static class OnlineText
    {
        public const int MinPassword = 6;

        /// <summary>Create-account form: null if fine, else what to fix.</summary>
        public static string AccountProblem(string email, string password, string confirm)
        {
            email = (email ?? "").Trim();
            int at = email.IndexOf('@');
            if (at < 1 || email.LastIndexOf('.') < at + 2 || email.EndsWith(".") || email.Contains(" ")) return "Enter a valid email address.";
            if ((password ?? "").Length < MinPassword) return $"Password: at least {MinPassword} characters.";
            if (password != confirm) return "Passwords don't match – type the same password twice.";
            return null;
        }

        public static string CleanName(string n)
        {
            if (n == null) return null;
            var sb = new System.Text.StringBuilder();
            foreach (char c in n.Trim())
                if (char.IsLetterOrDigit(c) || c == ' ' || c == '_' || c == '-' || c == '.') sb.Append(c);
            var s = sb.ToString().Trim();
            return s.Length >= 3 && s.Length <= 16 ? s : null;
        }

        public static string NormalizeCode(string code)
        {
            if (code == null) return null;
            var sb = new System.Text.StringBuilder();
            foreach (char c in code.ToUpperInvariant()) if ((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')) sb.Append(c);
            return sb.Length == 8 ? sb.ToString() : null;
        }
    }

    /// <summary>One leaderboard row (rank, name, score) from <c>get_leaderboard</c>.</summary>
    [Serializable]
    public class LeaderboardRow
    {
        public int rank;
        public string user_id;
        public string display_name;
        public int score;
        public float distance;
        public int level;
        public bool is_me;
    }

    [Serializable]
    public class LeaderboardPage
    {
        public string board;
        public string scope;
        /// <summary>Stat class of this board (-1 = everyone).</summary>
        public int @class = -1;
        public LeaderboardRow[] rows = new LeaderboardRow[0];
        public LeaderboardRow me;
    }

    [Serializable]
    public class FriendRow
    {
        public string user_id;
        public string display_name;
        public int level;
        public int best_score;
    }

    [Serializable]
    public class FriendList
    {
        public FriendRow[] friends = new FriendRow[0];
    }

    /// <summary>Generic result of purchases / claims / name changes.</summary>
    [Serializable]
    public class OpResult
    {
        public bool ok;
        public string reason;
        public int reward;
        public string friend;
        public ServerSnapshot snapshot;
    }

    /// <summary>Sticker album from the server (jr_album_status).</summary>
    [Serializable]
    public class AlbumInfo
    {
        public int[] cards = new int[0];
        public int[] sets = new int[0];
    }

    /// <summary>Pasada Pass from the server (jr_pass_status).</summary>
    [Serializable]
    public class PassInfo
    {
        public int season, xp, tier, days_left;
        public bool premium;
        public long claimed_free, claimed_premium;
    }

    [Serializable]
    public class FleetSlot
    {
        public int slot, rate, pending;
        public string jeepney;
        public float hours;
    }

    /// <summary>Operator mode fleet from the server (jr_fleet_status).</summary>
    [Serializable]
    public class FleetInfo
    {
        public bool unlocked;
        public int slots, bought, next_slot_price;
        public FleetSlot[] fleet = new FleetSlot[0];
    }

    [Serializable]
    public class LeagueRow
    {
        public int rank, level, score;
        public string display_name;
        public bool is_me;
    }

    [Serializable]
    public class LeagueLast
    {
        public int week, tier, result_tier, rank, reward;
        public bool claimed;
    }

    /// <summary>Weekly league from the server (jr_league_status).</summary>
    [Serializable]
    public class LeagueInfo
    {
        public bool joined;
        public int week, tier, days_left, rank, players;
        public LeagueRow[] rows = new LeagueRow[0];
        public LeagueLast last;
    }

    /// <summary>Christmas event progress from the server (jr_event_status).</summary>
    [Serializable]
    public class EventInfo
    {
        public bool active, golden;
        public int season, tokens, nights, claimed, today_tokens, days_left;
        /// <summary>Today's Simbang Gabi night by the server's date (0 = none).</summary>
        public int night;
    }

    /// <summary>Login streak status from the server (jr_streak_status).</summary>
    [Serializable]
    public class StreakInfo
    {
        public bool claimed, doubled, can_save, broken;
        public int day, streak, save_gems = 10;
    }

    [Serializable]
    public class StreakClaimResult
    {
        public bool ok;
        public string reason;
        public int day, streak, reward, gems;
        public ServerSnapshot snapshot;
    }

    [Serializable]
    public class DailyInfo
    {
        public int day_key;
        public int seed;
        public int env;
    }

    /// <summary>Minimal JSON helpers for building RPC argument objects.</summary>
    public static class Json
    {
        public static string Str(string s)
        {
            if (s == null) return "null";
            var sb = new System.Text.StringBuilder("\"");
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.Append('"').ToString();
        }

        public static string Bool(bool b) => b ? "true" : "false";
    }
}
