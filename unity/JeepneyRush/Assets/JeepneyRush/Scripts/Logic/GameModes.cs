using System;

namespace JeepneyRush.Logic
{
    /// <summary>Ways to play (free ride, career and the daily challenge have their own screens).</summary>
    public enum GameMode { FreeRide, RushHour, NightShift, Ghost }

    /// <summary>
    /// Game modes:
    ///  • RUSH HOUR – <see cref="RushSeconds"/> on the clock, heavy traffic, crowded stops: earn as
    ///    much as you can before time is up (the run ends there, no crash needed).
    ///  • NIGHT SHIFT – it stays night (the clock creeps between 7 PM and 4 AM), fewer riders but
    ///    better fares (+<see cref="NightFareBonus"/>), balut vendors and drunks; tap UWI NA to end the
    ///    shift and keep everything.
    /// Both have their own weekly rankings (also per stat class) on the server; their runs don't
    /// count on the normal boards.
    /// </summary>
    public static class GameModes
    {
        public const float RushSeconds = 180f;
        /// <summary>The server accepts a Rush Hour score only from a run this short (clock + slack for the slow-motion ending).</summary>
        public const float RushMaxDuration = RushSeconds + 15f;
        public const float NightFareBonus = 0.3f;

        public static string Name(GameMode m)
        {
            switch (m)
            {
                case GameMode.RushHour: return "RUSH HOUR";
                case GameMode.NightShift: return "NIGHT SHIFT";
                case GameMode.Ghost: return "GHOST RACE";
                default: return "FREE RIDE";
            }
        }

        public static string Blurb(GameMode m)
        {
            switch (m)
            {
                case GameMode.RushHour: return "3 minutes, heavy traffic, crowded stops. Kumita hangga't kaya!";
                case GameMode.NightShift: return "All night: fewer riders, +30% fares, balut and lasing. UWI NA ends the shift.";
                case GameMode.Ghost: return "Race the ghost of a driver in your stat class on the same road.";
                default: return "Drive as far as you can.";
            }
        }

        /// <summary>Server / report key ("" for free ride).</summary>
        public static string Key(GameMode m) => m == GameMode.RushHour ? "rush" : m == GameMode.NightShift ? "night" : m == GameMode.Ghost ? "ghost" : "";

        public static GameMode FromKey(string key) => key == "rush" ? GameMode.RushHour : key == "night" ? GameMode.NightShift : key == "ghost" ? GameMode.Ghost : GameMode.FreeRide;

        /// <summary>Traffic density multiplier.</summary>
        public static float Density(GameMode m) => m == GameMode.RushHour ? 1.45f : m == GameMode.NightShift ? 0.7f : 1f;

        /// <summary>Extra (or fewer) people waiting at each stop.</summary>
        public static int ExtraWaiting(GameMode m) => m == GameMode.RushHour ? 2 : m == GameMode.NightShift ? -1 : 0;

        public static float FareFactor(GameMode m) => m == GameMode.NightShift ? 1f + NightFareBonus : 1f;

        /// <summary>Seconds left on the Rush Hour clock (∞ for the other modes).</summary>
        public static float TimeLeft(GameMode m, float runSeconds) => m == GameMode.RushHour ? Math.Max(0f, RushSeconds - runSeconds) : float.PositiveInfinity;

        /// <summary>Seconds into the night (8 PM) on the <see cref="DayNight"/> clock.</summary>
        public static readonly float NightStartSeconds = (20f - DayNight.StartHour) * DayNight.SecondsPerHour - (20f - DayNight.NightFrom) * (DayNight.SecondsPerHour - DayNight.NightSecondsPerHour);
        /// <summary>Night shift: the clock runs at this fraction of normal speed and wraps back before dawn.</summary>
        public const float NightClockSpeed = 0.3f;
        private static float NightSpan => (24f - 20f + 4f) * DayNight.NightSecondsPerHour; // 8 PM .. 4 AM

        /// <summary>The <see cref="DayNight"/> clock (in run seconds) for a mode: night shift stays between 8 PM and 4 AM.</summary>
        public static float Clock(GameMode m, float runSeconds)
        {
            if (m != GameMode.NightShift) return runSeconds;
            float t = Math.Max(0f, runSeconds) * NightClockSpeed;
            return NightStartSeconds + (t % NightSpan);
        }
    }
}
