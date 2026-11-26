using System;
using System.Collections.Generic;

namespace JeepneyRush.Logic
{
    /// <summary>A recorded run you can race: where the jeepney was every <see cref="Ghosts.Interval"/> seconds.</summary>
    [Serializable]
    public class GhostData
    {
        public bool found;
        public string name;
        public int seed;
        public int env;
        public string jeepney;
        public int score;
        public float distance;
        /// <summary>Distance driven at each sample, in decimetres (non-decreasing).</summary>
        public int[] d = new int[0];
        /// <summary>Lateral position at each sample, in centimetres.</summary>
        public int[] x = new int[0];
    }

    /// <summary>Records the jeepney's path during a run (for Ghost Race).</summary>
    public sealed class GhostRecorder
    {
        private readonly List<int> _d = new List<int>(), _x = new List<int>();
        private float _t;
        private float _maxD;

        public int Count => _d.Count;

        public void Reset() { _d.Clear(); _x.Clear(); _t = 0; _maxD = 0; }

        /// <summary>Call every frame with the run clock's delta, the distance driven and the lateral position.</summary>
        public void Tick(float dt, float distance, float x)
        {
            if (_d.Count >= Ghosts.MaxSamples) return;
            _maxD = Math.Max(_maxD, distance); // reversing never moves a ghost backwards
            if (_d.Count == 0) { Add(x); return; }
            _t += dt;
            while (_t >= Ghosts.Interval && _d.Count < Ghosts.MaxSamples) { _t -= Ghosts.Interval; Add(x); }
        }

        private void Add(float x)
        {
            _d.Add((int)Math.Round(_maxD * 10f));
            _x.Add((int)Math.Round(Math.Max(-Ghosts.MaxAbsX, Math.Min(Ghosts.MaxAbsX, x)) * 100f));
        }

        /// <summary>The recorded path (the last sample is the final position).</summary>
        public GhostData Finish(float finalDistance, float finalX)
        {
            _maxD = Math.Max(_maxD, finalDistance);
            if (_d.Count < Ghosts.MaxSamples) Add(finalX);
            return new GhostData { found = true, d = _d.ToArray(), x = _x.ToArray(), distance = _maxD };
        }
    }

    /// <summary>
    /// Ghost Race rules (the server's jr_save_ghost checks the same): a ghost is one sample every
    /// <see cref="Interval"/> s, at most <see cref="MaxSamples"/>, distances never go backwards nor
    /// faster than the game allows, and it ends where the (server-checked) run ended.
    /// </summary>
    public static class Ghosts
    {
        public const float Interval = 0.5f;
        public const int MaxSamples = 3600; // 30 minutes
        public const float MaxAbsX = 4f;
        /// <summary>Max metres between two samples (top speed with turbo, rounded up).</summary>
        public const float MaxStep = RunBounds.MaxAverageSpeed * Interval;

        /// <summary>Where the ghost is after <paramref name="t"/> seconds (distance in metres, lateral x in metres).</summary>
        public static void Sample(GhostData g, float t, out float distance, out float x)
        {
            distance = 0; x = 0;
            if (g?.d == null || g.d.Length == 0) return;
            float f = Math.Max(0f, t) / Interval;
            int i = (int)Math.Floor(f);
            if (i >= g.d.Length - 1) { distance = g.d[g.d.Length - 1] / 10f; x = g.x[Math.Min(g.x.Length - 1, g.d.Length - 1)] / 100f; return; }
            float k = f - i;
            distance = (g.d[i] + (g.d[i + 1] - g.d[i]) * k) / 10f;
            x = (g.x[i] + (g.x[i + 1] - g.x[i]) * k) / 100f;
        }

        /// <summary>Seconds the ghost's run lasted.</summary>
        public static float Duration(GhostData g) => g?.d == null ? 0 : Math.Max(0, g.d.Length - 1) * Interval;

        /// <summary>Null if the path is plausible for a run of this distance and duration, else why not.</summary>
        public static string Violation(GhostData g, float runDistance, float runDuration)
        {
            if (g?.d == null || g.x == null || g.d.Length != g.x.Length) return "shape";
            int n = g.d.Length;
            if (n < 2 || n > MaxSamples || n > runDuration / Interval + 10) return "samples";
            for (int i = 0; i < n; i++)
            {
                if (g.d[i] < 0 || Math.Abs(g.x[i]) > MaxAbsX * 100f + 0.5f) return "range";
                if (i > 0 && (g.d[i] < g.d[i - 1] || g.d[i] - g.d[i - 1] > MaxStep * 10f + 0.5f)) return "step";
            }
            if (Math.Abs(g.d[n - 1] / 10f - runDistance) > Math.Max(5f, runDistance * 0.03f)) return "distance";
            return null;
        }

        /// <summary>Metres you are ahead of the ghost (negative = behind).</summary>
        public static float Lead(GhostData g, float t, float yourDistance)
        {
            Sample(g, t, out float gd, out _);
            return yourDistance - gd;
        }

        /// <summary>You beat the ghost if your score is higher.</summary>
        public static bool Beat(GhostData g, int yourScore) => g != null && yourScore > g.score;
    }
}
