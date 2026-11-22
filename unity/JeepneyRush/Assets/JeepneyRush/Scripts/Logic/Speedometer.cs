using System;

namespace JeepneyRush.Logic
{
    /// <summary>Old analog jeepney speedometer: 0–120 km/h over a 270° sweep, plus a rolling odometer.</summary>
    public static class Speedometer
    {
        public const float MaxKmh = 120f;
        public const float Sweep = 270f;

        public static float Kmh(float metresPerSecond) => Math.Abs(metresPerSecond) * 3.6f;

        /// <summary>Needle angle in degrees, clockwise from straight up: −135° at 0 km/h, +135° at 120 km/h (pinned beyond).</summary>
        public static float NeedleAngle(float kmh) => -Sweep / 2f + Sweep * Math.Max(0f, Math.Min(1f, kmh / MaxKmh));

        /// <summary>Six odometer digits: kilometres with one decimal (the last digit is tenths), rolling over at 99999.9.</summary>
        public static string Odometer(double metres)
        {
            long tenths = (long)Math.Floor(Math.Max(0, metres) / 100.0) % 1000000;
            return tenths.ToString("000000");
        }
    }

    /// <summary>
    /// "Sabit": on provincial roads riders also hang on the back of a full jeepney (standing on the
    /// rear step, holding the grab bar). They pay like everyone else but fall off on a fast hump or a crash.
    /// </summary>
    public static class Sabit
    {
        public const int ProvinceExtra = 3;

        public static bool Allowed(EnvironmentKind env) =>
            env == EnvironmentKind.Provincial || env == EnvironmentKind.Beach || env == EnvironmentKind.Mountain;

        public static int ExtraFor(EnvironmentKind env) => Allowed(env) ? ProvinceExtra : 0;

        /// <summary>Riders hanging on the back given how many are aboard and how many seats there are.</summary>
        public static int Hanging(int aboard, int seats) => Math.Max(0, aboard - seats);
    }
}
