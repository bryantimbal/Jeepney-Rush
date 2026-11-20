using System;
using System.Collections.Generic;
using JeepneyRush.Logic;
using NUnit.Framework;

namespace JeepneyRush.Tests
{
    /// <summary>
    /// A bot with perfect information but real constraints (lane change takes
    /// laneChangeDuration, real jump arc) drives through generated traffic. If the
    /// generator ever produces an unavoidable situation, the bot crashes and the test fails.
    /// </summary>
    public class FairnessTests
    {
        private sealed class Obj { public HazardKind Kind; public int Lane; public float Z; public float Speed; public bool Solid; public bool Hit; }

        private sealed class MockWorld : ISpawnSink
        {
            public readonly List<Obj> Objs = new List<Obj>();
            public readonly List<(PassengerStop stop, float z)> Stops = new List<(PassengerStop, float)>();
            public int CoinFormations;
            public int[] Lanes = { 1 };
            public IReadOnlyList<int> PlayerLanes => Lanes;

            public void CollectBlockers(List<PredictBlocker> output)
            {
                foreach (var o in Objs)
                {
                    if (o.Hit) continue;
                    var d = HazardCatalog.Get(o.Kind);
                    output.Add(new PredictBlocker(o.Lane, o.Z, d.HalfLength, o.Speed, d.Jumpable && !o.Solid));
                }
            }

            public void SpawnHazard(HazardKind kind, int lane, float z, float speed, bool solid) => Objs.Add(new Obj { Kind = kind, Lane = lane, Z = z, Speed = speed, Solid = solid });
            public void SpawnCoins(CoinFormation f, int lane, int lane2, float z, int count, float spacing) => CoinFormations++;
            public void SpawnStop(PassengerStop stop, float z) => Stops.Add((stop, z));
        }

        private static (List<string> hits, int stops, int coins, int passengers) Simulate(int seed, float metres)
        {
            var cfg = new BalanceConfig();
            var rng = new GameRandom(seed);
            var world = new MockWorld();
            var route = new PassengerRoute(cfg, rng);
            var director = new SpawnDirector(cfg, rng, world, route);
            const float dt = 1f / 30f;
            float distance = 0;
            int lane = 1, from = 1;
            float laneT = 1, x = cfg.LaneX(1), jumpT = -1;
            var hits = new List<string>();
            var blockers = new List<PredictBlocker>();
            var ivs = new List<LaneInterval>();
            const float step = 0.05f;
            int move = (int)Math.Ceiling(cfg.laneChangeDuration / step);
            const int horizonSteps = 600; // 30 s, same as the generator

            while (distance < metres)
            {
                float speed = cfg.SpeedAt(distance);
                world.Lanes = laneT < 1 ? new[] { from, lane } : new[] { lane };
                director.Update(distance, speed);
                foreach (var o in world.Objs) o.Z += (speed - o.Speed) * dt;
                world.Objs.RemoveAll(o => o.Z > cfg.despawnBehind + 6);
                for (int i = 0; i < world.Stops.Count; i++)
                {
                    var (s, z) = world.Stops[i];
                    z += speed * dt;
                    world.Stops[i] = (s, z);
                    if (!s.Resolved && z > 10) route.Serve(s);
                }
                distance += speed * dt;

                // ---- plan: backward DP over a fine grid with real steering time
                if (laneT >= 1)
                {
                    int best = -1;
                    foreach (bool cautious in new[] { true, false })
                    {
                        blockers.Clear();
                        world.CollectBlockers(blockers);
                        if (cautious) for (int i = 0; i < blockers.Count; i++) { var b = blockers[i]; b.Jumpable = false; blockers[i] = b; }
                        LaneOccupancy.BlockedIntervals(blockers, speed, 30.5f, cfg.playerHalfLength, ivs);
                        var free = new int[horizonSteps + move + 1];
                        for (int k = 0; k < free.Length; k++) free[k] = 7;
                        foreach (var iv in ivs)
                        {
                            int k0 = Math.Max(0, (int)Math.Floor(iv.T0 / step));
                            int k1 = Math.Min(free.Length - 1, (int)Math.Ceiling(iv.T1 / step) - 1);
                            for (int k = k0; k <= k1; k++) free[k] &= ~(1 << iv.Lane);
                        }
                        bool Clear(int l, int k0, int n) { for (int k = k0; k < k0 + n; k++) if ((free[k] & (1 << l)) == 0) return false; return true; }
                        var alive = new int[horizonSteps + move + 1];
                        for (int k = horizonSteps; k < alive.Length; k++) alive[k] = 7;
                        for (int k = horizonSteps - 1; k >= 0; k--)
                        {
                            int m = 0;
                            for (int l = 0; l < 3; l++)
                            {
                                if (Clear(l, k, 1) && (alive[k + 1] & (1 << l)) != 0) { m |= 1 << l; continue; }
                                foreach (int n in new[] { l - 1, l + 1 })
                                    if (n >= 0 && n <= 2 && Clear(l, k, move) && Clear(n, k, move) && (alive[k + move] & (1 << n)) != 0) m |= 1 << l;
                            }
                            alive[k] = m;
                        }
                        if (Clear(lane, 0, 1) && (alive[1] & (1 << lane)) != 0) best = lane;
                        else foreach (int n in new[] { lane - 1, lane + 1 })
                                if (n >= 0 && n <= 2 && Clear(lane, 0, move) && Clear(n, 0, move) && (alive[move] & (1 << n)) != 0) { best = n; break; }
                        if (best >= 0) break;
                    }
                    if (best >= 0 && best != lane) { from = lane; lane = best; laneT = 0; }
                }
                if (laneT < 1) laneT = Math.Min(1, laneT + dt / cfg.laneChangeDuration);
                float e = 1 - (float)Math.Pow(1 - laneT, 3);
                x = cfg.LaneX(from) + (cfg.LaneX(lane) - cfg.LaneX(from)) * e;

                // ---- jump over jumpables ahead in our lane
                if (jumpT < 0)
                {
                    foreach (var o in world.Objs)
                    {
                        var d = HazardCatalog.Get(o.Kind);
                        if (o.Hit || !d.Jumpable || o.Solid || Math.Abs(cfg.LaneX(o.Lane) - x) > 1.6f) continue;
                        float gap = -o.Z - d.HalfLength - cfg.playerHalfLength;
                        if (gap > 0 && gap < speed * 0.12f) { jumpT = 0; break; }
                    }
                }
                float y = 0;
                if (jumpT >= 0)
                {
                    jumpT += dt;
                    y = cfg.JumpHeightAt(jumpT);
                    if (jumpT >= cfg.jumpDuration) jumpT = -1;
                }

                foreach (var o in world.Objs)
                {
                    if (o.Hit) continue;
                    if (CollisionMath.Overlaps(cfg, x, y, cfg.LaneX(o.Lane), o.Z, HazardCatalog.Get(o.Kind)))
                    {
                        o.Hit = true;
                        hits.Add($"{o.Kind}@{distance:0}m");
                        if (Environment.GetEnvironmentVariable("DEBUG_SIM") == "1")
                        {
                            Console.WriteLine($"HIT {o.Kind} L{o.Lane} z={o.Z:0.0} | bot lane={lane} from={from} laneT={laneT:0.00} y={y:0.00} v={speed:0.0}");
                            foreach (var q in world.Objs) if (q.Z > -60 && q.Z < 12) Console.WriteLine($"   {q.Kind} L{q.Lane} z={q.Z:0.0} v={q.Speed:0.0} solid={q.Solid}");
                        }
                    }
                }
            }
            return (hits, world.Stops.Count, world.CoinFormations, route.PickedUp);
        }

        [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
        [TestCase(5)] [TestCase(6)] [TestCase(7)] [TestCase(8)]
        public void SkilledBotSurvivesFourKilometres(int seed)
        {
            var (hits, stops, coins, passengers) = Simulate(seed, 4000);
            CollectionAssert.IsEmpty(hits, string.Join(", ", hits));
            Assert.Greater(stops, 8);
            Assert.Greater(coins, 40);
            Assert.Greater(passengers, 10);
        }
    }
}
