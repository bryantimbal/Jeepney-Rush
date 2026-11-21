using System;
using System.Collections.Generic;

namespace JeepneyRush.Logic
{
    /// <summary>
    /// Where the traffic jams are on a route ("RUSH HOUR!" on EDSA). Like the weather, jams
    /// are planned along the road from the run seed: a jam ramps in over ~60 m, holds, and
    /// clears. Inside a jam the traffic crawls, rows are packed closer, and the jeepney's
    /// top speed is capped – but every row is still validated by the fairness planner, and
    /// braking to a stop is always safe, so a jam slows you down without trapping you.
    /// </summary>
    public sealed class JamSchedule
    {
        private struct Spell { public float From, To, Strength; }

        public const float Ramp = 110f;
        private readonly List<Spell> _spells = new List<Spell>();
        private readonly GameRandom _rng;
        private readonly float _intensity;
        private readonly bool _edsa;
        private float _plannedTo;

        public JamSchedule(int seed, EnvironmentKind env)
        {
            _rng = new GameRandom(seed ^ 0x3A11);
            _intensity = Environments.JamIntensity(env);
            _edsa = env == EnvironmentKind.Edsa;
            _plannedTo = _edsa ? _rng.Range(220f, 380f) : _rng.Range(500f, 800f);
        }

        public bool HasJams => _intensity > 0;

        private void PlanUntil(float distance)
        {
            while (_intensity > 0 && _plannedTo < distance + 1200f)
            {
                float len = _edsa ? _rng.Range(200f, 340f) : _rng.Range(120f, 200f);
                float strength = _intensity * _rng.Range(0.75f, 1f);
                _spells.Add(new Spell { From = _plannedTo, To = _plannedTo + len, Strength = strength });
                _plannedTo += len + (_edsa ? _rng.Range(380f, 650f) : _rng.Range(700f, 1100f));
            }
        }

        /// <summary>Jam strength 0..1 at a world distance.</summary>
        public float At(float distance)
        {
            if (_intensity <= 0) return 0f;
            PlanUntil(distance);
            foreach (var s in _spells)
            {
                if (distance < s.From - 1 || distance > s.To + 1) continue;
                float a = Math.Min(1f, (distance - s.From) / Ramp), b = Math.Min(1f, (s.To - distance) / Ramp);
                return Math.Max(0f, Math.Min(a, b)) * s.Strength;
            }
            return 0f;
        }

        /// <summary>Start of the next jam at or after <paramref name="distance"/> (for the "traffic ahead" warning).</summary>
        public float NextJamFrom(float distance)
        {
            if (_intensity <= 0) return float.MaxValue;
            PlanUntil(distance);
            foreach (var s in _spells) if (s.From >= distance) return s.From;
            return float.MaxValue;
        }
    }

    /// <summary>Gameplay numbers for jams (tested).</summary>
    public static class JamRules
    {
        /// <summary>Top-speed multiplier in a jam: a crawl at full strength.</summary>
        public static float SpeedCap(float jam) => 1f - 0.58f * Math.Max(0, Math.Min(1, jam));
        /// <summary>Rows are packed closer together in a jam.</summary>
        public static float RowGapScale(float jam) => 1f - 0.4f * Math.Max(0, Math.Min(1, jam));
        /// <summary>How fast the jeepney's speed follows the jam cap (m/s²): it eases into a jam and picks up speed gradually after.</summary>
        public const float CapAccel = 0.8f, CapDecel = 1.6f;
        /// <summary>Crawling traffic speed range (m/s).</summary>
        public const float CrawlMin = 1.2f, CrawlMax = 3.2f;
        /// <summary>Below this speed with riders aboard, you're "stuck in traffic" and they start complaining.</summary>
        public const float StuckSpeed = 6.5f;
        /// <summary>Seconds between complaints while stuck (a bit faster the longer it lasts).</summary>
        public static float ComplaintInterval(float stuckSeconds) => Math.Max(2.6f, 5.5f - stuckSeconds * 0.08f);
        /// <summary>After this many complaints in one jam, fed-up riders may get off and walk.</summary>
        public const int PatienceComplaints = 4;
        /// <summary>Chance per complaint (after patience runs out) that someone gets off.</summary>
        public const float WalkOffChance = 0.22f;
    }
}
