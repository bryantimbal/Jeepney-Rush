using System;

namespace JeepneyRush.Logic
{
    /// <summary>
    /// Time of day during a run: every run starts late in the morning and the clock runs
    /// while you drive (one daytime hour every <see cref="SecondsPerHour"/> seconds; the deep
    /// night passes twice as fast), so a long run goes through sunset, night and dawn while
    /// daylight always gets at least as much driving time as the night. Engine-free so it can be tested; the
    /// runtime turns these values into sky, fog, sun, street lights and headlights.
    /// </summary>
    public static class DayNight
    {
        public const float StartHour = 11f;
        public const float SecondsPerHour = 20f;
        /// <summary>Real seconds per in-game hour between <see cref="NightFrom"/> and <see cref="DawnFrom"/>.</summary>
        public const float NightSecondsPerHour = 10f;

        public const float SunsetStart = 17.4f, NightFrom = 19f, DawnFrom = 4.8f, DayFrom = 6.4f;

        /// <summary>Clock hour (0..24) after <paramref name="runSeconds"/> of driving.</summary>
        public static float HourAt(float runSeconds)
        {
            double h = StartHour, left = Math.Max(0, runSeconds);
            // one full day takes this many real seconds: skip whole days first
            double day = CycleSeconds;
            left -= Math.Floor(left / day) * day;
            for (int guard = 0; left > 1e-9 && guard < 8; guard++)
            {
                bool night = h >= NightFrom || h < DawnFrom;
                double rate = night ? NightSecondsPerHour : SecondsPerHour;
                double end = night ? (h >= NightFrom ? 24.0 + DawnFrom : DawnFrom) : NightFrom; // next boundary
                double span = (end - h) * rate;
                if (left < span) { h += left / rate; left = 0; }
                else { h = end; left -= span; }
                if (h >= 24.0) h -= 24.0;
            }
            return (float)(h - Math.Floor(h / 24.0) * 24.0);
        }

        /// <summary>Real seconds for a full 24-hour day.</summary>
        public static double CycleSeconds => (24.0 - NightFrom + DawnFrom) * NightSecondsPerHour + (NightFrom - DawnFrom) * SecondsPerHour;

        /// <summary>0 in daylight, 1 at full night, smooth through sunset and dawn.</summary>
        public static float Night(float hour)
        {
            if (hour >= NightFrom || hour < DawnFrom) return 1f;
            if (hour >= SunsetStart) return Smooth((hour - SunsetStart) / (NightFrom - SunsetStart));
            if (hour < DayFrom) return 1f - Smooth((hour - DawnFrom) / (DayFrom - DawnFrom));
            return 0f;
        }

        /// <summary>Golden-hour glow (orange sky) peaking at sunset and sunrise, 0 otherwise.</summary>
        public static float Dusk(float hour)
        {
            float a = Bell(hour, 18.1f, 0.9f), b = Bell(hour, 5.7f, 0.7f);
            return Math.Max(a, b);
        }

        /// <summary>Sun height 0..1 (1 = high noon-ish), used to tilt the sun for long evening shadows.</summary>
        public static float SunHeight(float hour) => hour < 6f || hour > 18.5f ? 0f : (float)Math.Sin((hour - 6f) / 12.5f * Math.PI);

        /// <summary>Commuters heading to work (late night to morning) vs going home (afternoon to night).</summary>
        public static bool GoingToWork(float hour) => hour >= 3.5f && hour < 12f;

        /// <summary>"6:45 PM" style label for the HUD.</summary>
        public static string Clock(float hour)
        {
            int total = (int)Math.Floor(hour * 60f) % (24 * 60);
            int h = total / 60, m = total % 60;
            m -= m % 5;
            string ampm = h >= 12 ? "PM" : "AM";
            int h12 = h % 12 == 0 ? 12 : h % 12;
            return $"{h12}:{m:00} {ampm}";
        }

        private static float Smooth(float t) { t = Math.Max(0, Math.Min(1, t)); return t * t * (3 - 2 * t); }
        private static float Bell(float x, float c, float w) { float d = (x - c) / w; return Math.Abs(d) >= 1 ? 0 : 1 - d * d * (3 - 2 * Math.Abs(d)); }
    }
}
