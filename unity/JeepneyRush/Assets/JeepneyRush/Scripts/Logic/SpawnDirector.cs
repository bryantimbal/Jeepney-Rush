using System;
using System.Collections.Generic;

namespace JeepneyRush.Logic
{
    public enum CoinFormation { Line, Arc, Zigzag }

    /// <summary>What the director needs from the world. Implemented by Unity managers and by test mocks.</summary>
    public interface ISpawnSink
    {
        void CollectBlockers(List<PredictBlocker> output);
        /// <summary>Lanes the player currently occupies (two while changing lanes).</summary>
        IReadOnlyList<int> PlayerLanes { get; }
        /// <param name="solid">Jumpable, but planned as lane-blocking (only jump walls are not solid).</param>
        void SpawnHazard(HazardKind kind, int lane, float z, float speed, bool solid);
        /// <param name="lane">Start lane; Zigzag alternates toward <paramref name="lane2"/>.</param>
        void SpawnCoins(CoinFormation formation, int lane, int lane2, float z, int count, float spacing);
        void SpawnStop(PassengerStop stop, float z);
    }

    /// <summary>
    /// Procedural level generation in "world distance" (metres since the run
    /// started). Every candidate row is validated with LaneOccupancy so the road is
    /// never impossible, and passenger stops reserve their curb lane.
    /// </summary>
    public sealed class SpawnDirector
    {
        private enum Pattern { Breather, Single, Vehicle, Double, JumpWall, Convoy, Roadwork, Parked, Weaver }

        private struct Candidate
        {
            public HazardKind Kind;
            public int Lane;
            public float Dz; // offset from the row (negative = further ahead)
            public float Speed;
            public bool Solid;
        }

        private struct Reservation { public int Lane; public float From, To; }

        private readonly BalanceConfig _cfg;
        private readonly GameRandom _rng;
        private readonly ISpawnSink _sink;
        private readonly PassengerRoute _route;
        private readonly List<Reservation> _reservations = new List<Reservation>();
        private readonly List<PassengerStop> _pendingStops = new List<PassengerStop>();
        private readonly List<PredictBlocker> _blockers = new List<PredictBlocker>();
        private readonly List<LaneInterval> _intervals = new List<LaneInterval>();
        private readonly List<Candidate> _cands = new List<Candidate>();
        private float _nextStopAt = 120f;
        /// <summary>Slow traffic can take a long time to reach the player; plan that far ahead.</summary>
        public const float MaxHorizon = 30f;

        public float Cursor = 50f;
        public int Rejected { get; private set; }
        public int RowsPlaced { get; private set; }

        public SpawnDirector(BalanceConfig cfg, GameRandom rng, ISpawnSink sink, PassengerRoute route)
        {
            _cfg = cfg; _rng = rng; _sink = sink; _route = route;
            PlanStops();
        }

        public void Update(float distance, float speed)
        {
            PlanStops();
            for (int i = _pendingStops.Count - 1; i >= 0; i--)
            {
                var s = _pendingStops[i];
                if (s.At - _cfg.stopZoneLength / 2 - 10 < distance + _cfg.spawnAhead)
                {
                    _sink.SpawnStop(s, -(s.At - distance));
                    _pendingStops.RemoveAt(i);
                }
            }
            int guard = 0;
            while (Cursor < distance + _cfg.spawnAhead && guard++ < 8) GenerateRow(distance, speed);
            _reservations.RemoveAll(r => r.To < distance - 20);
        }

        private void PlanStops()
        {
            while (_route.UpcomingCount() < 3)
            {
                int side = _rng.Chance(0.5) ? -1 : 1;
                int lane = side < 0 ? 0 : _cfg.laneCount - 1;
                var stop = _route.Plan(_nextStopAt, side, lane, _rng.Int(1, 4));
                _pendingStops.Add(stop);
                _reservations.Add(new Reservation { Lane = lane, From = stop.At - _cfg.stopZoneLength / 2 - 22, To = stop.At + _cfg.stopZoneLength / 2 + 6 });
                _nextStopAt += _rng.Range(_cfg.stopSpacingMin, _cfg.stopSpacingMax);
            }
        }

        private bool Reserved(int lane, float at, float halfLength)
        {
            foreach (var r in _reservations) if (r.Lane == lane && at + halfLength > r.From && at - halfLength < r.To) return true;
            return false;
        }

        private void GenerateRow(float distance, float speed)
        {
            float d = _cfg.DifficultyAt(Cursor);
            float rowAt = Cursor;
            float z = -(rowAt - distance);
            float gap = Math.Max(speed * _cfg.minReactionTime + _cfg.playerHalfLength * 2,
                (_cfg.maxRowGap + (_cfg.minRowGap - _cfg.maxRowGap) * d) * _rng.Range(0.85f, 1.2f));
            Cursor += gap;

            bool placed = false;
            Pattern lastPattern = Pattern.Breather;
            for (int attempt = 0; attempt < 4 && !placed; attempt++)
            {
                var pattern = attempt == 3 ? Pattern.Single : PickPattern(d, rowAt);
                lastPattern = pattern;
                BuildPattern(pattern, d, speed);
                if (_cands.Count == 0) break;
                if (pattern != Pattern.JumpWall)
                    for (int i = 0; i < _cands.Count; i++)
                        if (HazardCatalog.Get(_cands[i].Kind).Jumpable) { var c = _cands[i]; c.Solid = true; _cands[i] = c; }
                if (Validate(z, distance, speed)) placed = true;
                else Rejected++;
            }
            if (placed)
            {
                RowsPlaced++;
                foreach (var c in _cands) _sink.SpawnHazard(c.Kind, c.Lane, z + c.Dz, c.Speed, c.Solid);
            }
            else _cands.Clear();
            PlaceCoins(z, distance, speed, gap, placed && lastPattern == Pattern.JumpWall);
        }

        private Pattern PickPattern(float d, float at)
        {
            bool early = at < 300;
            bool walls = at >= _cfg.jumpWallMinDistance;
            return _rng.Weighted(new (Pattern, float)[]
            {
                (Pattern.Breather, 0.9f - d * 0.6f),
                (Pattern.Single, 2.2f),
                (Pattern.Vehicle, 2.6f),
                (Pattern.Double, early ? 0.2f : 0.6f + 2.2f * d),
                (Pattern.JumpWall, walls ? 0.35f + 0.8f * d : 0f),
                (Pattern.Convoy, early ? 0.3f : 0.9f + 0.5f * d),
                (Pattern.Roadwork, 0.7f),
                (Pattern.Parked, 0.8f),
                (Pattern.Weaver, early ? 0f : 0.4f + 0.9f * d),
            });
        }

        private float TrafficSpeed(HazardKind k, float playerSpeed)
        {
            var def = HazardCatalog.Get(k);
            // traffic is always clearly slower than the jeepney so it can be overtaken
            return Math.Max(1.5f, Math.Min(_rng.Range(def.MinSpeed, def.MaxSpeed), playerSpeed * 0.55f));
        }

        private HazardKind RandomVehicle(float d) => _rng.Weighted(new (HazardKind, float)[]
        {
            (HazardKind.Sedan, 3f), (HazardKind.Suv, 1.6f), (HazardKind.Tricycle, 1.5f), (HazardKind.Motorcycle, 1.1f),
            (HazardKind.Bus, 0.6f + d), (HazardKind.Truck, 0.6f + d * 0.8f),
        });

        private HazardKind RandomStatic() => _rng.Weighted(new (HazardKind, float)[]
        {
            (HazardKind.Cone, 2f), (HazardKind.Barrier, 1.6f), (HazardKind.Pothole, 1.5f), (HazardKind.ConstructionSign, 0.8f), (HazardKind.Roadblock, 0.8f),
        });

        private void BuildPattern(Pattern p, float d, float speed)
        {
            _cands.Clear();
            int lanes = _cfg.laneCount;
            int lane = _rng.Int(0, lanes - 1);
            int Other(int l) => (l + _rng.Int(1, lanes - 1)) % lanes;
            void Veh(HazardKind k, int l, float dz = 0) => _cands.Add(new Candidate { Kind = k, Lane = l, Dz = dz, Speed = TrafficSpeed(k, speed) });
            void Stat(HazardKind k, int l, float dz = 0, bool solid = false) => _cands.Add(new Candidate { Kind = k, Lane = l, Dz = dz, Solid = solid });
            switch (p)
            {
                case Pattern.Breather: break;
                case Pattern.Single: Stat(RandomStatic(), lane); break;
                case Pattern.Vehicle: Veh(RandomVehicle(d), lane); break;
                case Pattern.Double:
                {
                    int l2 = Other(lane);
                    Veh(RandomVehicle(d), lane);
                    if (_rng.Chance(0.5)) Veh(RandomVehicle(d), l2, -_rng.Range(0, 8)); else Stat(RandomStatic(), l2);
                    break;
                }
                case Pattern.JumpWall:
                {
                    var k = _rng.Pick(new[] { HazardKind.Cone, HazardKind.Barrier, HazardKind.Pothole });
                    for (int l = 0; l < lanes; l++) Stat(k, l);
                    break;
                }
                case Pattern.Convoy: Veh(_rng.Chance(0.55) ? HazardKind.Bus : HazardKind.Truck, lane); break;
                case Pattern.Roadwork:
                    // a cone line leading to a road block: too long to jump, so it blocks the lane
                    Stat(HazardKind.Roadblock, lane, -6f);
                    Stat(HazardKind.Cone, lane, 0f, true);
                    Stat(HazardKind.Cone, lane, 3.2f, true);
                    Stat(HazardKind.Cone, lane, 6.4f, true);
                    break;
                case Pattern.Parked:
                    Stat(_rng.Chance(0.5) ? HazardKind.ParkedSedan : HazardKind.ParkedJeepney, _rng.Chance(0.5) ? 0 : lanes - 1);
                    break;
                case Pattern.Weaver:
                    Veh(HazardKind.Motorcycle, lane);
                    Veh(HazardKind.Motorcycle, Other(lane), -_rng.Range(6, 14));
                    break;
            }
        }

        private bool Validate(float rowZ, float distance, float speed)
        {
            foreach (var c in _cands)
            {
                var def = HazardCatalog.Get(c.Kind);
                float closing = Math.Max(0.5f, speed - c.Speed);
                float t = -(rowZ + c.Dz) / closing;
                float meetAt = distance + speed * t;
                if (Reserved(c.Lane, meetAt, def.HalfLength + _cfg.playerHalfLength)) return false;
            }
            _blockers.Clear();
            _sink.CollectBlockers(_blockers);
            foreach (var c in _cands)
            {
                var def = HazardCatalog.Get(c.Kind);
                _blockers.Add(new PredictBlocker(c.Lane, rowZ + c.Dz, def.HalfLength, c.Speed, def.Jumpable && !c.Solid));
            }
            // The player keeps accelerating, which changes when slow traffic arrives relative to
            // static obstacles. Validate at the current speed and at the speed reached by the
            // time the row arrives; both must be survivable.
            float later = Math.Max(speed, _cfg.SpeedAt(distance - rowZ + _cfg.spawnAhead * 0.5f));
            return PassableAt(speed) && (later - speed < 0.25f || PassableAt(later));
        }

        private bool PassableAt(float speed)
        {
            // horizon covers every known blocker: a new row can push the player into a lane an older row closes later
            float horizon = 1;
            foreach (var b in _blockers)
            {
                if (b.Jumpable) continue;
                float closing = Math.Max(0.5f, speed - b.Speed);
                horizon = Math.Max(horizon, (b.HalfLength + _cfg.playerHalfLength - b.Z) / closing + 0.6f);
            }
            horizon = Math.Min(horizon, MaxHorizon);
            return LaneOccupancy.IsPassable(_blockers, speed, horizon, _cfg.playerHalfLength, _cfg.laneCount, _cfg.laneChangeDuration + _cfg.laneChangeSlack, _sink.PlayerLanes);
        }

        private void PlaceCoins(float rowZ, float distance, float speed, float gap, bool jumpWall)
        {
            if (jumpWall && _rng.Chance(0.8))
            {
                // special formation: an arc over the wall
                int l = _rng.Int(0, _cfg.laneCount - 1);
                _sink.SpawnCoins(CoinFormation.Arc, l, l, rowZ + 6f, 5, 3f);
                return;
            }
            if (!_rng.Chance(0.75)) return;
            _blockers.Clear();
            _sink.CollectBlockers(_blockers);
            float horizon = Math.Min(MaxHorizon, (-rowZ + gap + 20) / Math.Max(1f, speed));
            LaneOccupancy.BlockedIntervals(_blockers, speed, horizon, _cfg.playerHalfLength, _intervals);
            // jumpables are not in the intervals; treat them as blocking for coin placement
            foreach (var b in _blockers)
                if (b.Jumpable)
                {
                    float t = -b.Z / Math.Max(1f, speed);
                    _intervals.Add(new LaneInterval { Lane = b.Lane, T0 = t - 0.4f, T1 = t + 0.4f });
                }
            float startZ = rowZ - gap * 0.25f;
            bool big = _rng.Chance(0.25f + 0.2f * _cfg.DifficultyAt(distance));
            int count = big ? _rng.Int(8, 10) : _rng.Int(5, 7);
            const float spacing = 3f;
            float t0 = -startZ / Math.Max(1f, speed) - 0.3f;
            float dur = count * spacing / Math.Max(1f, speed) + 0.6f;
            var order = new List<int>();
            for (int l = 0; l < _cfg.laneCount; l++) order.Insert(_rng.Int(0, order.Count), l);
            foreach (int l in order)
            {
                if (!LaneOccupancy.LaneClear(_intervals, l, t0, t0 + dur)) continue;
                if (Reserved(l, distance - startZ + count * spacing / 2, count * spacing / 2 + 2)) continue;
                int l2 = l;
                if (big)
                {
                    // zigzag into a neighbouring clear lane for the bigger formation
                    int n = l == 0 ? 1 : l == _cfg.laneCount - 1 ? l - 1 : (_rng.Chance(0.5) ? l - 1 : l + 1);
                    if (LaneOccupancy.LaneClear(_intervals, n, t0, t0 + dur) && !Reserved(n, distance - startZ + count * spacing / 2, count * spacing / 2 + 2)) l2 = n;
                }
                _sink.SpawnCoins(l2 != l ? CoinFormation.Zigzag : CoinFormation.Line, l, l2, startZ, count, spacing);
                return;
            }
        }
    }
}
