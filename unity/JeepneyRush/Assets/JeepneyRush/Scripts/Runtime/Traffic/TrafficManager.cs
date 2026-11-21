using System.Collections.Generic;
using JeepneyRush.Logic;
using UnityEngine;

namespace JeepneyRush
{
    /// <summary>
    /// Moving traffic: sedans, SUVs, motorcycles, tricycles, buses and trucks. Bikes,
    /// tricycles and sedans occasionally swerve into a neighbouring lane after a visible
    /// wobble, but only when the road stays passable for the player.
    /// </summary>
    public sealed class TrafficManager : HazardManagerBase
    {
        private float _weaveTimer = 2f;
        private readonly List<PredictBlocker> _blockers = new List<PredictBlocker>();
        /// <summary>Supplied by the session: all blockers + player lanes (for safe swerves).</summary>
        public System.Action<List<PredictBlocker>> CollectAll;
        public System.Func<IReadOnlyList<int>> PlayerLanes;
        /// <summary>(lane, unityZ, speed, halfLength, secondsUntilPlayerPasses) → would this vehicle occupy a PARA zone?</summary>
        public System.Func<int, float, float, float, float, bool> ZoneBlocked;

        protected override void Animate(Hazard h, float dt, ref Vector3 pos)
        {
            if (h.Hit) return;
            // suspension bob
            var b = h.Body.localPosition;
            b.y = Mathf.Abs(Mathf.Sin(h.Age * (h.Kind == HazardKind.Motorcycle ? 9f : 6f))) * 0.025f;
            h.Body.localPosition = b;
            if (h.TargetLane < 0) return;
            if (h.SignalTime > 0)
            {
                h.SignalTime -= dt;
                h.Body.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(h.Age * 25f) * 1.2f); // "I'm about to move" wobble
                return;
            }
            h.LaneT = Mathf.Min(1, h.LaneT + dt / 0.9f);
            float e = h.LaneT * h.LaneT * (3 - 2 * h.LaneT);
            pos.x = Mathf.Lerp(Cfg.LaneX(h.Lane), Cfg.LaneX(h.TargetLane), e);
            float dir = h.TargetLane > h.Lane ? 1 : -1;
            h.Body.localRotation = Quaternion.Euler(0, dir * Mathf.Sin(h.LaneT * Mathf.PI) * 10f, 0);
            if (h.LaneT >= 1)
            {
                h.Lane = h.TargetLane;
                h.TargetLane = -1;
                h.Body.localRotation = Quaternion.identity;
            }
        }

        public override void Tick(float dt, float playerSpeed)
        {
            base.Tick(dt, playerSpeed);
            _weaveTimer -= dt;
            if (_weaveTimer > 0) return;
            _weaveTimer = Rng.Range(0.8f, 1.8f);
            Hazard pick = null;
            int seen = 0;
            foreach (var h in Items)
            {
                if (h.Hit || h.TargetLane >= 0) continue;
                if (h.Kind != HazardKind.Motorcycle && h.Kind != HazardKind.Tricycle && h.Kind != HazardKind.Sedan) continue;
                float z = h.transform.localPosition.z;
                if (z < 50 || z > 140) continue;
                if (Rng.Int(0, ++seen - 1) == 0) pick = h; // reservoir sample
            }
            if (pick == null) return;
            int to = pick.Lane == 0 ? 1 : pick.Lane == Cfg.laneCount - 1 ? pick.Lane - 1 : (Rng.Chance(0.5) ? pick.Lane - 1 : pick.Lane + 1);
            TryWeave(pick, to, playerSpeed, 0.7f);
        }

        /// <summary>
        /// The jeepney honks: the nearest vehicle ahead in its lane moves over if that's safe
        /// for the player, otherwise it speeds up a little. Returns the vehicle that reacted.
        /// </summary>
        public Hazard Honk(float playerX, float playerSpeed)
        {
            Hazard best = null;
            foreach (var h in Items)
            {
                if (h.Hit || h.Speed <= 0 || h.TargetLane >= 0) continue;
                float z = h.transform.localPosition.z;
                if (z < 3f || z > 55f || Mathf.Abs(h.transform.localPosition.x - playerX) > Cfg.laneSpacing * 0.6f) continue;
                if (best == null || z < best.transform.localPosition.z) best = h;
            }
            if (best == null) return null;
            int lane = best.Lane;
            var options = lane == 0 ? new[] { 1 } : lane == Cfg.laneCount - 1 ? new[] { lane - 1 } : (Rng.Chance(0.5) ? new[] { lane - 1, lane + 1 } : new[] { lane + 1, lane - 1 });
            foreach (int to in options)
                if (TryWeave(best, to, playerSpeed, 0.25f)) return best;
            best.Speed = Mathf.Min(best.Speed + 2.5f, Mathf.Max(best.Speed, playerSpeed * 0.9f)); // can't move over: hurries up
            return best;
        }

        private bool TryWeave(Hazard pick, int to, float playerSpeed, float signal)
        {
            float pz = pick.transform.localPosition.z;
            float tMeet = pz / Mathf.Max(0.5f, playerSpeed - pick.Speed);
            if (ZoneBlocked != null && ZoneBlocked(to, pz, pick.Speed, pick.Def.HalfLength, tMeet)) return false;
            foreach (var o in Items)
                if (o != pick && (o.Lane == to || o.TargetLane == to) && Mathf.Abs(o.transform.localPosition.z - pick.transform.localPosition.z) < o.Def.HalfLength + pick.Def.HalfLength + 6)
                    return false;
            pick.TargetLane = to;
            pick.SignalTime = signal;
            pick.LaneT = 0;
            _blockers.Clear();
            CollectAll?.Invoke(_blockers);
            if (!LaneOccupancy.IsPassable(_blockers, playerSpeed, 10f, Cfg.playerHalfLength, Cfg.laneCount, Cfg.laneChangeDuration + Cfg.laneChangeSlack, PlayerLanes?.Invoke()))
            {
                pick.TargetLane = -1;
                pick.SignalTime = 0;
                return false;
            }
            return true;
        }
    }
}
