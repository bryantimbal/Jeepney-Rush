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
        // sudden showers ("biglang ulan") are cut into long dry spells on their own stream,
        // so the rest of the weather sequence stays the same
        private readonly GameRandom _showerRng;
        public const float ShowerMin = 160f, ShowerMax = 260f;
        public int Showers { get; private set; }
        private readonly EnvironmentKind _env;
        private float _plannedTo;

        public WeatherSchedule(int seed, EnvironmentKind env)
        {
            _rng = new GameRandom(seed ^ 0x5EA7);
            _showerRng = new GameRandom(seed ^ 0x51A0);
            _env = env;
            _spells.Add(new Spell { From = 0, To = _rng.Range(400, 700), Kind = WeatherKind.Clear });
            _plannedTo = _spells[0].To;
        }

        /// <summary>Typhoons never come before this distance.</summary>
        public const float TyphoonFrom = 1500f;
        /// <summary>Rain is rarer now, but a run never stays dry longer than this (metres): weather still happens.</summary>
        public const float MaxDryStretch = 2200f;
        private float _dryFrom;

        private void PlanUntil(float distance)
        {
            while (_plannedTo < distance + 400)
            {
                var prev = _spells[_spells.Count - 1].Kind;
                // mostly fair weather: long dry spells, shorter rain, typhoons rare (likelier by the sea)
                float typhoon = _plannedTo < TyphoonFrom ? 0 : _env == EnvironmentKind.Beach ? 0.5f : _env == EnvironmentKind.Mountain ? 0.4f : 0.3f;
                WeatherKind k;
                int guard = 0;
                do k = _rng.Weighted(new (WeatherKind, float)[] { (WeatherKind.Clear, 3.2f), (WeatherKind.Rain, 1.1f), (WeatherKind.Typhoon, typhoon) });
                while (k == prev && k != WeatherKind.Clear && guard++ < 5); // dry spells may follow each other; storms don't
                if (k == WeatherKind.Clear && _plannedTo - _dryFrom > MaxDryStretch - 800f) k = WeatherKind.Rain; // it's been dry long enough
                float len = k == WeatherKind.Clear ? _rng.Range(700, 1200) : k == WeatherKind.Rain ? _rng.Range(350, 600) : _rng.Range(300, 450);
                if (k == WeatherKind.Clear && len >= 900f && _plannedTo > 700f && _showerRng.Chance(0.3))
                {
                    // dry - short sharp shower - dry
                    float a = len * _showerRng.Range(0.3f, 0.45f), shower = _showerRng.Range(ShowerMin, ShowerMax);
                    _spells.Add(new Spell { From = _plannedTo, To = _plannedTo + a, Kind = WeatherKind.Clear });
                    _spells.Add(new Spell { From = _plannedTo + a, To = _plannedTo + a + shower, Kind = WeatherKind.Rain });
                    _spells.Add(new Spell { From = _plannedTo + a + shower, To = _plannedTo + len + shower, Kind = WeatherKind.Clear });
                    _dryFrom = _plannedTo + a + shower;
                    _plannedTo += len + shower;
                    Showers++;
                    continue;
                }
                _spells.Add(new Spell { From = _plannedTo, To = _plannedTo + len, Kind = k });
                if (k != WeatherKind.Clear) _dryFrom = _plannedTo + len;
                _plannedTo += len;
            }
        }

        /// <summary>Share of the first <paramref name="metres"/> that is wet (rain or typhoon).</summary>
        public float WetShare(float metres)
        {
            int wet = 0, n = 0;
            for (float d = 0; d < metres; d += 10f, n++) if (KindAt(d) != WeatherKind.Clear) wet++;
            return n == 0 ? 0 : wet / (float)n;
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
        public static bool InZone(float zoneZ, float playerX, float zoneX, BalanceConfig cfg, float zoneLength = -1f) =>
            Math.Abs(playerX - zoneX) <= cfg.laneSpacing / 2f + cfg.playerHalfWidth &&
            Math.Abs(zoneZ) <= (zoneLength > 0 ? zoneLength : cfg.stopZoneLength) / 2f + cfg.playerHalfLength + cfg.zoneSlack;

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
    /// Rolling fog banks in the highlands (Baguio, Mountain Province): stretches of road where
    /// the mist thickens. Visual only (gameplay is unchanged) but the far view shrinks, so the
    /// fog never closes in below <see cref="MinVisibility"/> metres.
    /// </summary>
    public sealed class FogBanks
    {
        public const float FirstAt = 350f, GapMin = 450f, GapMax = 900f, LengthMin = 160f, LengthMax = 300f, Ramp = 60f;
        public const float MinVisibility = 95f;
        private readonly List<(float from, float to, float strength)> _banks = new List<(float, float, float)>();
        private readonly GameRandom _rng;
        private readonly float _strength;
        private float _plannedTo;

        public static bool Has(EnvironmentKind env) => env == EnvironmentKind.Baguio || env == EnvironmentKind.Mountain;

        public FogBanks(int seed, EnvironmentKind env)
        {
            _rng = new GameRandom(seed ^ 0x0F06);
            _strength = env == EnvironmentKind.Baguio ? 1f : env == EnvironmentKind.Mountain ? 0.7f : 0f;
            _plannedTo = FirstAt;
        }

        public int Count => _banks.Count;

        private void PlanTo(float distance)
        {
            if (_strength <= 0) return;
            while (_plannedTo < distance + 1000f)
            {
                float from = _plannedTo + _rng.Range(0f, 200f), to = from + _rng.Range(LengthMin, LengthMax);
                _banks.Add((from, to, _strength * _rng.Range(0.75f, 1f)));
                _plannedTo = to + _rng.Range(GapMin, GapMax);
            }
        }

        /// <summary>0 = the route's normal haze .. 1 = thick mist.</summary>
        public float DensityAt(float distance)
        {
            if (_strength <= 0) return 0f;
            PlanTo(distance);
            foreach (var (from, to, s) in _banks)
            {
                if (distance < from - Ramp || distance > to + Ramp) continue;
                float t = distance < from ? (distance - (from - Ramp)) / Ramp : distance > to ? ((to + Ramp) - distance) / Ramp : 1f;
                t = Math.Max(0, Math.Min(1, t));
                return s * t * t * (3 - 2 * t);
            }
            return 0f;
        }

        /// <summary>Fog end distance (m) for a route's base value at this density.</summary>
        public static float FogEnd(float baseEnd, float density) => baseEnd + (MinVisibility - baseEnd) * Math.Max(0, Math.Min(1, density));
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

        /// <summary>Steepest grade a route may have (Baguio's city streets are famously steep).</summary>
        public static float MaxGrade(EnvironmentKind env) => env == EnvironmentKind.Baguio ? 0.18f : 0.12f;

        public RoadShape(int seed, EnvironmentKind env)
        {
            var rng = new GameRandom(seed ^ 0x7A11);
            // hill height (m) and bend amplitude (m) per route: Manila has flyovers and gentle
            // street bends, the province rolls, the coastal road winds along the shore
            // the mountain road climbs and winds the most (still ≤ 12% grade, checked by tests)
            float hills = env == EnvironmentKind.Mountain ? 5f : env == EnvironmentKind.Provincial ? 6f : env == EnvironmentKind.Beach ? 4f
                : env == EnvironmentKind.Baguio ? 8f : env == EnvironmentKind.Cebu ? 4f : env == EnvironmentKind.Edsa ? 2.5f : 3f; // Baguio: steep city hills; EDSA: flyovers
            float bends = env == EnvironmentKind.Manila ? 22f : env == EnvironmentKind.Provincial ? 34f : env == EnvironmentKind.Mountain ? 48f
                : env == EnvironmentKind.Edsa ? 14f : env == EnvironmentKind.Cebu ? 26f : env == EnvironmentKind.Baguio ? 52f : 50f; // Baguio switchbacks, winding coastal road
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
