using System;
using System.Collections.Generic;

namespace JeepneyRush.Logic
{
    /// <summary>A vehicle or obstacle near the jeepney this frame (positions relative to the jeepney's centre).</summary>
    public struct PassCandidate
    {
        public int Id;
        /// <summary>Lateral offset (m, + = right of the jeepney).</summary>
        public float X;
        /// <summary>Distance ahead (m, centre to centre; negative = behind).</summary>
        public float Z;
        public float HalfLength, HalfWidth;
        /// <summary>Moving traffic (counts for overtakes); static obstacles only give near misses.</summary>
        public bool Moving;
        /// <summary>False once it hit the jeepney – crashes never score.</summary>
        public bool Eligible;
    }

    public enum PassKind { Overtake, NearMiss }

    public struct PassEvent
    {
        public int Id;
        public PassKind Kind;
        /// <summary>Smallest side gap seen while alongside (m).</summary>
        public float Gap;
    }

    /// <summary>
    /// Detects near misses and overtakes. A near miss is passing something without touching it
    /// after being in its path at close range (a last-second swerve), or brushing past with a
    /// side gap under <see cref="SideGap"/>. Every vehicle can score once.
    /// </summary>
    public sealed class NearMissTracker
    {
        /// <summary>Side gap that counts as "close" while alongside (normal adjacent-lane passing is ~1.2 m).</summary>
        public const float SideGap = 0.6f;
        /// <summary>In-path distance that counts as a last-second swerve (m between bumpers).</summary>
        public const float SwerveDistance = 9f;

        private sealed class Track { public bool InPathClose; public float MinGap = float.MaxValue; public bool Done; public bool Seen; }
        private readonly Dictionary<int, Track> _tracks = new Dictionary<int, Track>();
        private readonly List<int> _stale = new List<int>();

        public void Reset() => _tracks.Clear();

        /// <param name="halfWidth">Jeepney half width (m).</param>
        /// <param name="halfLength">Jeepney half length (m).</param>
        public void Update(float halfWidth, float halfLength, IReadOnlyList<PassCandidate> items, List<PassEvent> output)
        {
            output.Clear();
            foreach (var t in _tracks.Values) t.Seen = false;
            for (int i = 0; i < items.Count; i++)
            {
                var c = items[i];
                if (!_tracks.TryGetValue(c.Id, out var t)) { t = new Track(); _tracks[c.Id] = t; }
                t.Seen = true;
                if (t.Done) continue;
                if (!c.Eligible) { t.Done = true; continue; }
                float sideGap = Math.Abs(c.X) - halfWidth - c.HalfWidth;          // < 0 means overlapping lanes
                float bumperGap = c.Z - c.HalfLength - halfLength;                  // > 0 while it is still ahead
                bool alongside = c.Z + c.HalfLength > -halfLength && c.Z - c.HalfLength < halfLength;
                if (sideGap < 0 && bumperGap > 0 && bumperGap < SwerveDistance) t.InPathClose = true;
                if (alongside) t.MinGap = Math.Min(t.MinGap, Math.Max(0, sideGap));
                // fully behind the jeepney: passed
                if (c.Z + c.HalfLength < -halfLength)
                {
                    t.Done = true;
                    bool near = t.InPathClose || t.MinGap < SideGap;
                    if (near) output.Add(new PassEvent { Id = c.Id, Kind = PassKind.NearMiss, Gap = t.MinGap });
                    if (c.Moving) output.Add(new PassEvent { Id = c.Id, Kind = PassKind.Overtake, Gap = t.MinGap });
                }
            }
            // forget vehicles that despawned
            _stale.Clear();
            foreach (var kv in _tracks) if (!kv.Value.Seen) _stale.Add(kv.Key);
            foreach (var k in _stale) _tracks.Remove(k);
        }
    }

    /// <summary>
    /// Chains near misses and overtakes into a multiplier: x2 after 3 links, x3 after 6,
    /// x4 after 10, x5 after 15. Lapses after <see cref="Window"/> seconds without a link,
    /// and breaks on a crash. Points use the multiplier at the moment they are earned.
    /// </summary>
    public sealed class ComboMeter
    {
        public const float Window = 4f;
        public const int NearMissPoints = 150, OvertakePoints = 40;
        public const int MaxMultiplier = 5;
        private static readonly int[] Steps = { 0, 3, 6, 10, 15 };

        public int Chain { get; private set; }
        public int BestChain { get; private set; }
        public int NearMisses { get; private set; }
        public int Overtakes { get; private set; }
        public float SinceLast { get; private set; } = float.MaxValue;

        public int Multiplier
        {
            get
            {
                int m = 1;
                for (int i = 1; i < Steps.Length; i++) if (Chain >= Steps[i]) m = i + 1;
                return Math.Min(MaxMultiplier, m);
            }
        }

        /// <summary>0..1 time left before the combo lapses (HUD bar).</summary>
        public float Remaining01 => Chain > 0 ? Math.Max(0f, 1f - SinceLast / Window) : 0f;

        public void Reset() { Chain = 0; BestChain = 0; NearMisses = 0; Overtakes = 0; SinceLast = float.MaxValue; }

        /// <summary>Adds a link and returns the points it is worth (with the multiplier, x2 during Turbo).</summary>
        public int Add(PassKind kind, bool turbo)
        {
            Chain = SinceLast <= Window ? Chain + 1 : 1;
            SinceLast = 0;
            BestChain = Math.Max(BestChain, Chain);
            if (kind == PassKind.NearMiss) NearMisses++; else Overtakes++;
            int basePts = kind == PassKind.NearMiss ? NearMissPoints : OvertakePoints;
            return basePts * Multiplier * (turbo ? 2 : 1);
        }

        public void Tick(float dt)
        {
            if (SinceLast < float.MaxValue) SinceLast += dt;
            if (SinceLast > Window) Chain = 0;
        }

        /// <summary>A crash ends the chain immediately.</summary>
        public void Break() { Chain = 0; SinceLast = float.MaxValue; }

        /// <summary>Most points a run could earn from <paramref name="nearMisses"/> and <paramref name="overtakes"/> (server bound).</summary>
        public static long MaxPoints(int nearMisses, int overtakes) =>
            (long)nearMisses * NearMissPoints * MaxMultiplier * 2 + (long)overtakes * OvertakePoints * MaxMultiplier * 2;
    }
}
