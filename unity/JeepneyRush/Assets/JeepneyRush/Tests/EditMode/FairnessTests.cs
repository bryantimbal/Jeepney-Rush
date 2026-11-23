using System;
using System.Collections.Generic;
using JeepneyRush.Logic;
using NUnit.Framework;

namespace JeepneyRush.Tests
{
    /// <summary>
    /// A bot with perfect information but real constraints (lane change takes
    /// laneChangeDuration, longer on wet roads) drives through generated traffic. If the
    /// generator ever produces an unavoidable situation, the bot crashes and the test fails.
    /// </summary>
    public class FairnessTests
    {
        private sealed class Obj { public float W; public HazardKind Kind; public int Lane; public float Z; public float Speed; public bool Solid; public bool Hit; }

        private sealed class MockWorld : ISpawnSink
        {
            public readonly List<Obj> Objs = new List<Obj>();
            public readonly List<(PassengerStop stop, float z)> Stops = new List<(PassengerStop, float)>();
            public int CoinFormations;
            public float Distance;
            public int[] Lanes = { 1 };
            public IReadOnlyList<int> PlayerLanes => Lanes;

            public void CollectBlockers(List<PredictBlocker> output)
            {
                foreach (var o in Objs)
                {
                    if (o.Hit) continue;
                    var d = HazardCatalog.Get(o.Kind);
                    output.Add(new PredictBlocker(o.Lane, o.Z, d.HalfLength, o.Speed, d.Soft && !o.Solid));
                }
            }

            public readonly List<(HazardKind kind, int lane)> Oncoming = new List<(HazardKind, int)>();
            public readonly Dictionary<HazardKind, int> Spawned = new Dictionary<HazardKind, int>();
            public void SpawnHazard(HazardKind kind, int lane, float z, float speed, bool solid)
            {
                Objs.Add(new Obj { W = Distance - z, Kind = kind, Lane = lane, Z = z, Speed = speed, Solid = solid });
                if (Environment.GetEnvironmentVariable("DEBUG_SPAWN") is string ds && Distance > float.Parse(ds) && Distance < float.Parse(ds) + 40) Console.WriteLine($"SPAWN @{Distance:0.0} {kind} L{lane} z={z:0.0} v={speed:0.0} lanes={string.Join("/", Lanes)}");
                Spawned[kind] = Spawned.TryGetValue(kind, out var n) ? n + 1 : 1;
                if (speed < 0) Oncoming.Add((kind, lane));
            }
            public void SpawnCoins(CoinFormation f, int lane, int lane2, float z, int count, float spacing) => CoinFormations++;
            public void SpawnStop(PassengerStop stop, float z) => Stops.Add((stop, z));
            public int PowerUps;
            public readonly List<(PowerUpKind kind, float w)> PowerUpList = new List<(PowerUpKind, float)>();
            public void SpawnPowerUp(PowerUpKind kind, int lane, float z) { PowerUps++; PowerUpList.Add((kind, Distance - z)); }
            public int Gems;
            public int Brakes;
            public void SpawnGem(int lane, float z) { Gems++; }
            public readonly List<(GasStation station, float z)> Stations = new List<(GasStation, float)>();
            public void SpawnStation(GasStation station, float z) => Stations.Add((station, z));
            public readonly List<(Intersection x, float z)> Crossings = new List<(Intersection, float)>();
            public void SpawnIntersection(Intersection x, float z) => Crossings.Add((x, z));
        }

        private static (List<string> hits, int stops, int coins, int passengers, int powerUps, List<string> zoneIntrusions, MockWorld world) Simulate(int seed, float metres, EnvironmentKind env = EnvironmentKind.Manila)
        {
            var cfg = new BalanceConfig();
            var rng = new GameRandom(seed);
            var world = new MockWorld();
            var route = new PassengerRoute(cfg, rng, StopNames.ForEnvironment(env));
            var weather = new WeatherSchedule(seed, env);
            var jams = new JamSchedule(seed, env);
            var director = new SpawnDirector(cfg, rng, world, route, env, weather.WetnessAt, jams.At);
            const float dt = 1f / 30f;
            float distance = 0, speed = -1, brakeHold = 0;
            int lane = 1, from = 1;
            float laneT = 1, x = cfg.LaneX(1), laneDur = cfg.laneChangeDuration;
            var hits = new List<string>();
            var intrusions = new List<string>();
            var blockers = new List<PredictBlocker>();
            var ivs = new List<LaneInterval>();
            const float step = 0.05f;
            int maxMove = (int)Math.Ceiling((cfg.laneChangeDuration * WeatherSchedule.LaneChangeFactor(cfg, 1) + cfg.laneChangeSlack) / step) + 1;
            const int horizonSteps = 600; // 30 s, same as the generator

            while (distance < metres)
            {
                float target = cfg.SpeedAt(distance) * JamRules.SpeedCap(jams.At(distance)); // the jeepney crawls in a jam
                speed = speed < 0 ? target : speed > target ? Math.Max(target, speed - JamRules.CapDecel * dt) : Math.Min(target, speed + JamRules.CapAccel * dt);
                // a real driver brakes when no lane is safe (the game's guarantee: braking always avoids a crash)
                if (brakeHold > 0) { speed = Math.Max(1.5f, speed - cfg.brakeDecel * dt); brakeHold -= dt; world.Brakes++; }
                world.Lanes = laneT < 1 ? new[] { from, lane } : new[] { lane };
                world.Distance = distance;
                // like the game (GameplaySession → LevelSpawner.SetPlayerLanes): the director plans from the player's real lane(s)
                world.Lanes = laneT < 1 && from != lane ? new[] { lane, from } : new[] { lane };
                director.Update(distance, speed);
                foreach (var o in world.Objs) o.Z += (speed - (o.Z > 0 && o.Speed > 0 ? Math.Min(o.Speed, speed) : o.Speed)) * dt; // like the game: traffic behind waits
                world.Objs.RemoveAll(o => o.Z > cfg.despawnBehind + 6);
                for (int i = 0; i < world.Stops.Count; i++)
                {
                    var (s, z) = world.Stops[i];
                    z += speed * dt;
                    world.Stops[i] = (s, z);
                    if (!s.Resolved && z > 10) route.Serve(s);
                    // nothing may stand or drive in the PARA zone lane while the jeepney approaches it
                    if (s.Resolved || z < -110 || z > cfg.stopZoneLength / 2) continue;
                    foreach (var o in world.Objs)
                    {
                        var d = HazardCatalog.Get(o.Kind);
                        bool fullWidth = d.HalfWidth >= HazardCatalog.FullRoadHalfWidth - 0.01f;
                        if ((o.Lane == s.Lane || fullWidth) && Math.Abs(o.Z - z) < cfg.stopZoneLength / 2 + d.HalfLength)
                        {
                            string tag = $"{o.Kind}@stop{s.Id}";
                            if (!intrusions.Contains(tag)) intrusions.Add(tag);
                        }
                    }
                }
                for (int i = 0; i < world.Stations.Count; i++)
                {
                    var (st, z) = world.Stations[i];
                    z += speed * dt;
                    world.Stations[i] = (st, z);
                    if (z < -110 || z > cfg.stationZoneLength / 2) continue;
                    foreach (var o in world.Objs)
                    {
                        var d = HazardCatalog.Get(o.Kind);
                        bool fullWidth = d.HalfWidth >= HazardCatalog.FullRoadHalfWidth - 0.01f;
                        if ((o.Lane == st.Lane || fullWidth) && Math.Abs(o.Z - z) < cfg.stationZoneLength / 2 + d.HalfLength)
                        {
                            string tag = $"{o.Kind}@station{st.Id}";
                            if (!intrusions.Contains(tag)) intrusions.Add(tag);
                        }
                    }
                }
                for (int i = 0; i < world.Crossings.Count; i++)
                {
                    var (xs, z) = world.Crossings[i];
                    z += speed * dt;
                    world.Crossings[i] = (xs, z);
                    if (z < -110 || z > TrafficLightRules.HalfBox + 2) continue;
                    // nothing may stand or drive inside the intersection or on its approach while the jeepney comes
                    foreach (var o in world.Objs)
                        if (o.Z > z - TrafficLightRules.HalfBox - HazardCatalog.Get(o.Kind).HalfLength && o.Z < z + TrafficLightRules.StopLineOffset + HazardCatalog.Get(o.Kind).HalfLength)
                        {
                            string tag = $"{o.Kind}@crossing{xs.Id}";
                            if (!intrusions.Contains(tag)) intrusions.Add(tag);
                        }
                }
                distance += speed * dt;

                // ---- plan: backward DP over a fine grid with real steering time
                if (laneT >= 1)
                {
                    // slippery road: steering takes longer when wet
                    laneDur = cfg.laneChangeDuration * WeatherSchedule.LaneChangeFactor(cfg, Math.Max(weather.WetnessAt(distance), weather.WetnessAt(distance + 60))); // plan for the rain that's coming
                    // a careful driver steers early: plan with the generator's slower lane change first
                    int quick = (int)Math.Ceiling(laneDur / step), careful = (int)Math.Ceiling((laneDur + cfg.laneChangeSlack) / step);
                    int best = -1;
                    foreach (int move in new[] { careful, quick }) {
                    // speeding up after a jam: a sensible driver plans for the speed it's about to have
                    var plans = target > speed + 0.5f ? new[] { (true, Math.Min(target, speed + 3f)), (true, speed), (false, Math.Min(target, speed + 3f)), (false, speed) } : new[] { (true, speed), (false, speed) };
                    foreach (var (cautious, pv) in plans)
                    {
                        blockers.Clear();
                        world.CollectBlockers(blockers);
                        if (cautious) for (int i = 0; i < blockers.Count; i++) { var b = blockers[i]; b.Soft = false; blockers[i] = b; }
                        LaneOccupancy.BlockedIntervals(blockers, pv, 30.5f, cfg.playerHalfLength, ivs);
                        var free = new int[horizonSteps + maxMove + 1];
                        for (int k = 0; k < free.Length; k++) free[k] = 7;
                        foreach (var iv in ivs)
                        {
                            int k0 = Math.Max(0, (int)Math.Floor(iv.T0 / step));
                            int k1 = Math.Min(free.Length - 1, (int)Math.Ceiling(iv.T1 / step) - 1);
                            for (int k = k0; k <= k1; k++) free[k] &= ~(1 << iv.Lane);
                        }
                        bool Clear(int l, int k0, int n) { for (int k = k0; k < k0 + n; k++) if ((free[k] & (1 << l)) == 0) return false; return true; }
                        var alive = new int[horizonSteps + maxMove + 1];
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
                    if (best >= 0) break; }
                    if (Environment.GetEnvironmentVariable("DEBUG_FROM") is string df && Environment.GetEnvironmentVariable("DEBUG_SEED") == seed.ToString() && distance > float.Parse(df) && distance < float.Parse(df) + 40)
                    {
                        var dbg = new List<PredictBlocker>(); world.CollectBlockers(dbg);
                        Console.WriteLine($"@{distance:0.0} v={speed:0.0} lane={lane} best={best} passable={LaneOccupancy.IsPassable(dbg, speed, 12f, cfg.playerHalfLength, 3, cfg.laneChangeDuration + cfg.laneChangeSlack, new[] { lane })} bot-dur={laneDur:0.00} dir-dur={cfg.laneChangeDuration + cfg.laneChangeSlack:0.00}");
                        foreach (var q in world.Objs) if (q.Z > -400 && q.Z < 15) Console.WriteLine($"     {q.Kind} L{q.Lane} z={q.Z:0.0} v={q.Speed:0.0}");
                    }
                    if (best < 0) brakeHold = 0.35f;
                    if (best >= 0 && best != lane) { from = lane; lane = best; laneT = 0; }
                }
                if (laneT < 1) laneT = Math.Min(1, laneT + dt / laneDur);
                float e = laneT * laneT * (3 - 2 * laneT); // same smooth ease as the game
                x = cfg.LaneX(from) + (cfg.LaneX(lane) - cfg.LaneX(from)) * e;

                const float y = 0; // jeepneys don't jump: humps/potholes/floods are driven over

                foreach (var o in world.Objs)
                {
                    if (o.Hit || HazardCatalog.Get(o.Kind).Soft) continue;
                    if (CollisionMath.Overlaps(cfg, x, y, cfg.LaneX(o.Lane), o.Z, HazardCatalog.Get(o.Kind)))
                    {
                        o.Hit = true;
                        hits.Add($"{o.Kind}@{distance:0}m");
                        if (Environment.GetEnvironmentVariable("DEBUG_SIM") == "1")
                        {
                            Console.WriteLine($"HIT {o.Kind} L{o.Lane} z={o.Z:0.0} | bot lane={lane} from={from} laneT={laneT:0.00} y={y:0.00} v={speed:0.0} target={target:0.0} jam={jams.At(distance):0.00} at={distance:0}");
                            foreach (var q in world.Objs) if (q.Z > -60 && q.Z < 40) Console.WriteLine($"   {q.Kind} L{q.Lane} z={q.Z:0.0} v={q.Speed:0.0} solid={q.Solid}");
                        }
                    }
                }
            }
            return (hits, world.Stops.Count, world.CoinFormations, route.PickedUp, world.PowerUps, intrusions, world);
        }

        [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
        [TestCase(5)] [TestCase(6)] [TestCase(7)] [TestCase(8)]
        [TestCase(11)] [TestCase(12)] [TestCase(13)] [TestCase(18)] [TestCase(19)] [TestCase(20)]
        public void SkilledBotSurvivesFourKilometres(int seed)
        {
            var env = (EnvironmentKind)(seed % Environments.All.Length);
            var (hits, stops, coins, passengers, powerUps, intrusions, world) = Simulate(seed, 4000, env);
            CollectionAssert.IsEmpty(hits, string.Join(", ", hits));
            Assert.Greater(powerUps, 4);
            Assert.Greater(stops, env == EnvironmentKind.Mountain ? 5 : 8); // mountain stops are far apart
            Assert.Greater(coins, 40);
            Assert.Greater(passengers, env == EnvironmentKind.Mountain ? 6 : 10);
            CollectionAssert.IsEmpty(intrusions, "vehicles/obstacles in PARA zones: " + string.Join(", ", intrusions));
            int Count(HazardKind k) => world.Spawned.TryGetValue(k, out var n) ? n : 0;
            if (Environments.Paved(env)) Assert.Greater(Count(HazardKind.SpeedHump), 0, "no speed humps in 4 km");
            else
            {
                // unpaved mountain road: mud and fallen rocks, no painted humps
                Assert.AreEqual(0, Count(HazardKind.SpeedHump));
                Assert.Greater(Count(HazardKind.MudPatch), 3, "mud patches on the dirt road");
                Assert.Greater(Count(HazardKind.Rocks), 0, "fallen rocks");
            }
            // Turbo pickups are stored (BoostBank) and can't be USED near a stop, station or intersection,
            // so they may lie anywhere; check a run still offers some boosters and a few gems
            Assert.Greater(world.PowerUpList.FindAll(p => p.kind == PowerUpKind.Turbo).Count, 2, "too few TURBO boosters in 4 km");
            Assert.That(world.Gems, Is.InRange(1, RoadGems.MaxPerRun), "road gems in 4 km");
            // gas stations: regular, never closer than the tank allows, never on top of a PARA stop
            Assert.GreaterOrEqual(world.Stations.Count, 3, "too few gas stations in 4 km");
            Assert.GreaterOrEqual(world.Crossings.Count, env == EnvironmentKind.Mountain ? 1 : env == EnvironmentKind.Edsa ? 2 : env == EnvironmentKind.Baguio ? 3 : 4, "too few intersections in 4 km");
            var cfg = new BalanceConfig();
            for (int i = 0; i < world.Stations.Count; i++)
            {
                float prev = i == 0 ? 0 : world.Stations[i - 1].station.At;
                Assert.Less(world.Stations[i].station.At - prev, FuelTank.RangeOnGas(cfg) - 100f, "stations too far apart for a full tank");
            }
        }

        [TestCase(EnvironmentKind.Provincial, 3)]
        [TestCase(EnvironmentKind.Beach, 5)]
        [TestCase(EnvironmentKind.Manila, 7)]
        [TestCase(EnvironmentKind.Edsa, 9)]
        public void TwoWayRoadsHaveOncomingTrafficInTheLeftLaneOnly(EnvironmentKind env, int seed)
        {
            var (hits, _, _, _, _, intrusions, world) = Simulate(seed, 3000, env);
            CollectionAssert.IsEmpty(hits, string.Join(", ", hits));
            CollectionAssert.IsEmpty(intrusions);
            int oncoming = world.Oncoming.Count;
            foreach (var (kind, lane) in world.Oncoming)
            {
                Assert.AreEqual(Environments.OncomingLane, lane, $"{kind} drives against traffic outside the oncoming lane");
                Assert.IsTrue(HazardCatalog.IsMoving(kind));
            }
            if (Environments.TwoWay(env))
            {
                Assert.Greater(oncoming, 25, "two-way road without oncoming traffic");
                foreach (var (stop, _) in world.Stops) Assert.AreEqual(2, stop.Lane, "PARA stops are on the right on a two-way road");
            }
            else Assert.AreEqual(0, oncoming);
        }

        /// <summary>Long sweep over many seeds and every route (run explicitly: JR_SWEEP=1).</summary>
        [Test]
        public void SweepManySeeds()
        {
            if (Environment.GetEnvironmentVariable("JR_SWEEP") != "1") Assert.Ignore("set JR_SWEEP=1 to run the long sweep");
            var bad = new List<string>();
            var only = Environment.GetEnvironmentVariable("JR_SWEEP_SEED");
            for (int seed = 21; seed <= 160; seed++)
            {
                if (only != null && only != seed.ToString()) continue;
                var env = (EnvironmentKind)(seed % Environments.All.Length);
                var r = Simulate(seed, 4000, env);
                if (r.hits.Count > 0 || r.zoneIntrusions.Count > 0) bad.Add($"{seed}/{env}: {string.Join(",", r.hits)} {string.Join(",", r.zoneIntrusions)}");
            }
            Console.WriteLine("SWEEPBAD " + bad.Count + " :: " + string.Join(" | ", bad));
            CollectionAssert.IsEmpty(bad, string.Join("\n", bad));
        }

        [Test]
        public void FloodsOnlyOnWetRoadsAndAppearInRain()
        {
            int floods = 0;
            for (int seed = 1; seed <= 6; seed++)
            {
                var (_, _, _, _, _, _, world) = Simulate(seed, 4000, (EnvironmentKind)(seed % Environments.All.Length));
                floods += (world.Spawned.TryGetValue(HazardKind.FloodPuddle, out var a) ? a : 0) + (world.Spawned.TryGetValue(HazardKind.FloodWide, out var b) ? b : 0);
            }
            Assert.Greater(floods, 0);
        }

        [Test]
        public void MountainHasMuchLessTraffic()
        {
            int Vehicles(EnvironmentKind env)
            {
                int n = 0;
                for (int seed = 1; seed <= 3; seed++)
                {
                    var (_, _, _, _, _, _, w) = Simulate(seed, 3000, env);
                    foreach (var kv in w.Spawned) if (HazardCatalog.IsMoving(kv.Key)) n += kv.Value;
                }
                return n;
            }
            int manila = Vehicles(EnvironmentKind.Manila), mountain = Vehicles(EnvironmentKind.Mountain);
            Assert.Less(mountain, manila * 0.6f, $"mountain {mountain} vs manila {manila} vehicles");
            Assert.Greater(mountain, 0);
        }

        [Test]
        public void NoRowOfConesAcrossAllLanes()
        {
            var cfg = new BalanceConfig();
            var world = new MockWorld();
            var rng = new GameRandom(42);
            var director = new SpawnDirector(cfg, rng, world, new PassengerRoute(cfg, rng));
            for (float d = 0; d < 5000; d += 0.5f) { world.Distance = d; director.Update(d, cfg.SpeedAt(d)); }
            var byZ = new Dictionary<int, HashSet<int>>();
            foreach (var o in world.Objs)
            {
                if (HazardCatalog.Get(o.Kind).Soft || o.Speed > 0) continue;
                int key = (int)Math.Round(o.W / 2f);
                if (!byZ.TryGetValue(key, out var set)) byZ[key] = set = new HashSet<int>();
                set.Add(o.Lane);
            }
            foreach (var kv in byZ) Assert.Less(kv.Value.Count, cfg.laneCount, $"solid wall at z={kv.Key}");
        }
    }
}
