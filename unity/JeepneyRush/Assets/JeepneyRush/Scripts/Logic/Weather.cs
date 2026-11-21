using System;
using System.Collections.Generic;

namespace JeepneyRush.Logic
{
    public enum WeatherKind { Clear, Rain, Typhoon }

    /// <summary>
    /// Philippine weather over a run: sunny stretches, rain showers and typhoons
    /// ("bagyo") that roll in and out with smooth transitions. Deterministic per seed.
    /// Wet roads are slippery (slower lane changes) and floods appear on the road.
    /// </summary>
    public sealed class WeatherSchedule
    {
        private struct Spell { public float From, To; public WeatherKind Kind; }

        public const float Transition = 80f; // metres to blend between spells
        private readonly List<Spell> _spells = new List<Spell>();
        private readonly GameRandom _rng;
        private readonly EnvironmentKind _env;
        private float _plannedTo;

        public WeatherSchedule(int seed, EnvironmentKind env)
        {
            _rng = new GameRandom(seed ^ 0x5EA7);
            _env = env;
            _spells.Add(new Spell { From = 0, To = _rng.Range(250, 450), Kind = WeatherKind.Clear });
            _plannedTo = _spells[0].To;
        }

        private void PlanUntil(float distance)
        {
            while (_plannedTo < distance + 400)
            {
                var prev = _spells[_spells.Count - 1].Kind;
                // typhoons are likelier by the sea, and never right after the start
                float typhoon = _plannedTo < 900 ? 0 : _env == EnvironmentKind.Beach ? 1.3f : 0.8f;
                WeatherKind k;
                int guard = 0;
                do k = _rng.Weighted(new (WeatherKind, float)[] { (WeatherKind.Clear, 2.2f), (WeatherKind.Rain, 1.8f), (WeatherKind.Typhoon, typhoon) });
                while (k == prev && guard++ < 5);
                float len = k == WeatherKind.Clear ? _rng.Range(400, 800) : k == WeatherKind.Rain ? _rng.Range(450, 750) : _rng.Range(350, 550);
                _spells.Add(new Spell { From = _plannedTo, To = _plannedTo + len, Kind = k });
                _plannedTo += len;
            }
        }

        private static float Strength(WeatherKind k) => k == WeatherKind.Clear ? 0 : k == WeatherKind.Rain ? 0.6f : 1f;

        /// <summary>Dominant weather at a world distance.</summary>
        public WeatherKind KindAt(float distance)
        {
            PlanUntil(distance);
            foreach (var s in _spells) if (distance >= s.From && distance < s.To) return s.Kind;
            return WeatherKind.Clear;
        }

        /// <summary>
        /// Wetness 0 (dry) .. 1 (typhoon), blended over <see cref="Transition"/> metres at spell
        /// boundaries. Drives rain density, road slipperiness and flood spawning.
        /// </summary>
        public float WetnessAt(float distance)
        {
            PlanUntil(distance);
            for (int i = 0; i < _spells.Count; i++)
            {
                var s = _spells[i];
                if (distance < s.From || distance >= s.To) continue;
                float w = Strength(s.Kind);
                float intoNext = distance - (s.To - Transition / 2);
                if (intoNext > 0 && i + 1 < _spells.Count)
                    return Lerp(w, Strength(_spells[i + 1].Kind), intoNext / Transition);
                float fromPrev = (s.From + Transition / 2) - distance;
                if (fromPrev > 0 && i > 0)
                    return Lerp(w, Strength(_spells[i - 1].Kind), fromPrev / Transition);
                return w;
            }
            return 0;
        }

        private static float Lerp(float a, float b, float t) => a + (b - a) * Math.Max(0, Math.Min(1, t));

        public static string Label(WeatherKind k) => k == WeatherKind.Clear ? "MAARAW" : k == WeatherKind.Rain ? "UMUULAN" : "BAGYO!";

        /// <summary>Lane-change time multiplier for a wetness level (slippery road).</summary>
        public static float LaneChangeFactor(BalanceConfig cfg, float wetness) => 1f + cfg.wetLaneChangePenalty * Math.Max(0, Math.Min(1, wetness));
    }

    /// <summary>Speed humps and potholes: cross them slowly (hold BRAKE) or jolt your passengers.</summary>
    public static class HumpRules
    {
        public const int SmoothBonusScore = 50;
        /// <summary>
        /// Smooth crossing: either already slow, or braking at the hump (and not still
        /// flying in at full gas). Passengers only complain when the driver didn't slow down.
        /// </summary>
        public static bool Smooth(float speed, float baseSpeed, BalanceConfig cfg, bool braking = false) =>
            speed <= baseSpeed * cfg.humpSafeFraction + 0.01f || (braking && speed <= baseSpeed * cfg.humpBrakingFraction + 0.01f);
    }

    /// <summary>PARA stops: passengers only board or get off when the jeepney has fully stopped in the zone.</summary>
    public static class StopRules
    {
        public static bool Stopped(float speed, BalanceConfig cfg) => speed <= cfg.stopSpeed;

        /// <summary>
        /// Is the jeepney touching the PARA zone? Any overlap counts – it may stand on the
        /// lines, straddle two lanes or stop a little short / past the box (<see cref="BalanceConfig.zoneSlack"/>).
        /// <paramref name="zoneZ"/> = zone centre ahead of the jeepney's centre, x = lateral positions.
        /// </summary>
        public static bool InZone(float zoneZ, float playerX, float zoneX, BalanceConfig cfg) =>
            Math.Abs(playerX - zoneX) <= cfg.laneSpacing / 2f + cfg.playerHalfWidth &&
            Math.Abs(zoneZ) <= cfg.stopZoneLength / 2f + cfg.playerHalfLength + cfg.zoneSlack;

        /// <summary>A stop only counts as missed once it is behind the jeepney and out of reversing reach.</summary>
        public static bool OutOfReach(float zoneZ, BalanceConfig cfg) =>
            zoneZ < -(cfg.stopZoneLength / 2f + cfg.playerHalfLength + cfg.zoneSlack + cfg.reverseLimit);

        /// <summary>How long the jeepney waits while people get on and off.</summary>
        public static float LoadTime(PickupResult r, BalanceConfig cfg) =>
            Math.Min(2.4f, 0.4f + cfg.loadTimePerPassenger * (r.Boarded + r.Alighted));

        /// <summary>Distance needed to stop from <paramref name="speed"/> with the brakes.</summary>
        public static float StoppingDistance(float speed, BalanceConfig cfg) => speed * speed / (2f * Math.Max(0.1f, cfg.brakeDecel));
    }

    /// <summary>
    /// Road shape: gentle hills (height) and bends (lateral offset) as smooth functions
    /// of world distance. Rendering only – gameplay stays in straight lane space, so the
    /// fairness guarantees are unaffected. Values are relative to the jeepney's position.
    /// </summary>
    public sealed class RoadShape
    {
        private readonly float _h1, _h2, _l1, _l2, _hp, _lp;
        private readonly float _hL1, _hL2, _lL1, _lL2;

        public RoadShape(int seed, EnvironmentKind env)
        {
            var rng = new GameRandom(seed ^ 0x7A11);
            // hill height (m) and bend amplitude (m) per route: Manila has flyovers and gentle
            // street bends, the province rolls, the coastal road winds along the shore
            float hills = env == EnvironmentKind.Provincial ? 6f : env == EnvironmentKind.Beach ? 4f : 3f;
            float bends = env == EnvironmentKind.Manila ? 22f : env == EnvironmentKind.Provincial ? 34f : 40f;
            _h1 = hills * rng.Range(0.7f, 1f);
            _h2 = hills * rng.Range(0.3f, 0.5f);
            _l1 = bends * rng.Range(0.7f, 1f);
            _l2 = bends * rng.Range(0.3f, 0.5f);
            _hL1 = rng.Range(95, 130);
            _hL2 = rng.Range(45, 60);
            _lL1 = rng.Range(160, 220);
            _lL2 = rng.Range(80, 110);
            _hp = rng.Range(0, 6.28f);
            _lp = rng.Range(0, 6.28f);
        }

        /// <summary>Flat at the start so the run begins on a level, straight road.</summary>
        private static float Ramp(float w) { float t = Math.Max(0, Math.Min(1, (w - 120f) / 250f)); return t * t * (3 - 2 * t); }

        public float Height(float w) => Ramp(w) * (float)(_h1 * Math.Sin(w / _hL1 + _hp) + _h2 * Math.Sin(w / _hL2 + _hp * 2));
        public float Lateral(float w) => Ramp(w) * (float)(_l1 * Math.Sin(w / _lL1 + _lp) + _l2 * Math.Sin(w / _lL2 + _lp * 3));

        private const float Eps = 0.5f;
        public float Slope(float w) => (Height(w + Eps) - Height(w - Eps)) / (2 * Eps);
        private float LateralSlope(float w) => (Lateral(w + Eps) - Lateral(w - Eps)) / (2 * Eps);

        /// <summary>Height of the road at <paramref name="z"/> metres ahead, relative to the jeepney (which climbs and descends).</summary>
        public float RelHeight(float distance, float z) => Height(distance + z) - Height(distance);

        /// <summary>
        /// Sideways offset of the road at <paramref name="z"/> metres ahead. The jeepney's own
        /// heading is subtracted so the road under the jeepney is always straight ahead and
        /// bends appear in front (the classic endless-runner curved road).
        /// </summary>
        public float RelLateral(float distance, float z) => Lateral(distance + z) - Lateral(distance) - z * LateralSlope(distance);

        /// <summary>Road heading (yaw, radians) at z relative to the jeepney's heading.</summary>
        public float RelYaw(float distance, float z) => (float)Math.Atan(LateralSlope(distance + z) - LateralSlope(distance));

        /// <summary>Road pitch (radians, positive = uphill) at z.</summary>
        public float Pitch(float distance, float z) => (float)Math.Atan(Slope(distance + z));
    }
}
