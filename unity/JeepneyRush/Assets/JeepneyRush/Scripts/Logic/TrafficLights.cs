using System;

namespace JeepneyRush.Logic
{
    public enum LightColor { Green, Yellow, Red }

    /// <summary>A signalised intersection across the road. All lanes are kept clear around it.</summary>
    public sealed class Intersection
    {
        public int Id;
        /// <summary>World distance of the centre of the crossing.</summary>
        public float At;
        /// <summary>Seconds into the light cycle when the intersection appears (varies the timing).</summary>
        public float Phase;
    }

    /// <summary>
    /// Green → yellow → red cycle for the jeepney's direction. Yellow lasts long enough that a
    /// jeepney too close to stop when it turns yellow always clears the line before red
    /// (see <see cref="TrafficLightRules.YellowIsFair"/>).
    /// </summary>
    public sealed class TrafficLightCycle
    {
        private readonly BalanceConfig _cfg;
        private float _t;
        public float Period => _cfg.lightGreen + _cfg.lightYellow + _cfg.lightRed;

        public TrafficLightCycle(BalanceConfig cfg, float phase)
        {
            _cfg = cfg;
            _t = ((phase % Period) + Period) % Period;
        }

        public void Tick(float dt) => _t = (_t + Math.Max(0, dt)) % Period;

        public LightColor Color => _t < _cfg.lightGreen ? LightColor.Green : _t < _cfg.lightGreen + _cfg.lightYellow ? LightColor.Yellow : LightColor.Red;

        /// <summary>Seconds until the current colour changes (shown as a countdown).</summary>
        public float Remaining
        {
            get
            {
                if (_t < _cfg.lightGreen) return _cfg.lightGreen - _t;
                if (_t < _cfg.lightGreen + _cfg.lightYellow) return _cfg.lightGreen + _cfg.lightYellow - _t;
                return Period - _t;
            }
        }
    }

    public static class TrafficLightRules
    {
        /// <summary>Distance before the intersection centre where the stop line is painted.</summary>
        public const float StopLineOffset = 9.5f;
        public const float HalfBox = 5f;

        /// <summary>
        /// The jeepney's front bumper crossed the stop line this frame.
        /// <paramref name="lineZBefore"/>/<paramref name="lineZ"/> = stop line ahead of the jeepney centre.
        /// </summary>
        public static bool CrossedLine(float lineZBefore, float lineZ, BalanceConfig cfg) =>
            lineZBefore - cfg.playerHalfLength > 0 && lineZ - cfg.playerHalfLength <= 0;

        /// <summary>Only crossing on RED is an offence (yellow = finish crossing if you can't stop).</summary>
        public static bool IsViolation(LightColor c) => c == LightColor.Red;

        /// <summary>
        /// Yellow is fair at <paramref name="speed"/>: if the jeepney can't stop before the line when
        /// it turns yellow, it reaches the line before red.
        /// </summary>
        public static bool YellowIsFair(float speed, BalanceConfig cfg) =>
            speed <= 0.5f || StopRules.StoppingDistance(speed, cfg) / speed <= cfg.lightYellow;
    }
}
