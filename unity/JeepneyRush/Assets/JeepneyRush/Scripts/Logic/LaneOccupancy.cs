using System;
using System.Collections.Generic;

namespace JeepneyRush.Logic
{
    /// <summary>A lane-blocking object as seen by the predictor. z &lt; 0 is ahead of the player.</summary>
    public struct PredictBlocker
    {
        public int Lane;
        /// <summary>Second lane occupied while changing lanes, or -1.</summary>
        public int Lane2;
        public float Z;
        public float HalfLength;
        public float Speed;
        public bool Jumpable;

        public PredictBlocker(int lane, float z, float halfLength, float speed, bool jumpable, int lane2 = -1)
        {
            Lane = lane; Z = z; HalfLength = halfLength; Speed = speed; Jumpable = jumpable; Lane2 = lane2;
        }
    }

    public struct LaneInterval
    {
        public int Lane;
        public float T0;
        public float T1;
    }

    /// <summary>
    /// Predicts when each lane is blocked at the player's position and checks that
    /// a human can always find a way through. World convention: the player sits at
    /// z = 0 and the world moves toward +z, so an object with forward speed s
    /// approaches at (playerSpeed - s).
    /// </summary>
    public static class LaneOccupancy
    {
        public const float Pad = 0.14f;

        public static List<LaneInterval> BlockedIntervals(IReadOnlyList<PredictBlocker> blockers, float playerSpeed, float horizon, float playerHalfLength, List<LaneInterval> output = null)
        {
            var result = output ?? new List<LaneInterval>();
            result.Clear();
            foreach (var b in blockers)
            {
                if (b.Jumpable) continue;
                float reach = b.HalfLength + playerHalfLength;
                float closing = playerSpeed - b.Speed;
                float t0, t1;
                if (closing <= 0.25f)
                {
                    if (Math.Abs(b.Z) < reach) { t0 = 0; t1 = horizon; }
                    else continue;
                }
                else
                {
                    t0 = (-reach - b.Z) / closing;
                    t1 = (reach - b.Z) / closing;
                }
                if (t1 < 0 || t0 > horizon) continue;
                t0 = Math.Max(0, t0 - Pad);
                t1 = Math.Min(horizon, t1 + Pad);
                result.Add(new LaneInterval { Lane = b.Lane, T0 = t0, T1 = t1 });
                if (b.Lane2 >= 0 && b.Lane2 != b.Lane) result.Add(new LaneInterval { Lane = b.Lane2, T0 = t0, T1 = t1 });
            }
            return result;
        }

        /// <summary>
        /// True if a player starting in <paramref name="startLanes"/> (null = any) can survive
        /// the next <paramref name="horizon"/> seconds. Staying needs the lane clear; a lane
        /// change takes <paramref name="moveTime"/> during which BOTH lanes must be clear.
        /// </summary>
        public static bool IsPassable(IReadOnlyList<PredictBlocker> blockers, float playerSpeed, float horizon, float playerHalfLength, int laneCount, float moveTime, IReadOnlyList<int> startLanes = null, float step = 0.05f)
        {
            var intervals = BlockedIntervals(blockers, playerSpeed, horizon, playerHalfLength);
            int steps = (int)Math.Ceiling(horizon / step);
            int move = Math.Max(1, (int)Math.Ceiling(moveTime / step));
            int all = (1 << laneCount) - 1;
            var free = new int[steps + move + 1];
            for (int k = 0; k < free.Length; k++) free[k] = all;
            foreach (var iv in intervals)
            {
                int k0 = Math.Max(0, (int)Math.Floor(iv.T0 / step));
                int k1 = Math.Min(free.Length - 1, (int)Math.Ceiling(iv.T1 / step) - 1);
                for (int k = k0; k <= k1; k++) free[k] &= ~(1 << iv.Lane);
            }
            var reach = new int[steps + move + 2];
            if (startLanes != null && startLanes.Count > 0) foreach (var l in startLanes) reach[0] |= 1 << l;
            else reach[0] = all;
            reach[0] &= free[0];
            for (int k = 0; k < steps; k++)
            {
                int r = reach[k];
                if (r == 0) return false;
                reach[k + 1] |= r & free[k + 1];
                int run = all;
                for (int j = k; j < k + move; j++) run &= free[j];
                for (int l = 0; l < laneCount; l++)
                {
                    if ((r & (1 << l)) == 0 || (run & (1 << l)) == 0) continue;
                    if (l > 0 && (run & (1 << (l - 1))) != 0) reach[k + move] |= 1 << (l - 1);
                    if (l < laneCount - 1 && (run & (1 << (l + 1))) != 0) reach[k + move] |= 1 << (l + 1);
                }
            }
            for (int k = steps; k < reach.Length; k++) if (reach[k] != 0) return true;
            return false;
        }

        public static bool LaneClear(IReadOnlyList<LaneInterval> intervals, int lane, float t0, float t1)
        {
            foreach (var iv in intervals) if (iv.Lane == lane && iv.T0 < t1 && iv.T1 > t0) return false;
            return true;
        }
    }
}
