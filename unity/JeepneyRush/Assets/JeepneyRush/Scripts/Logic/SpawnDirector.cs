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
        /// <param name="solid">Soft hazard that should still be planned as lane-blocking (unused for now).</param>
        void SpawnHazard(HazardKind kind, int lane, float z, float speed, bool solid);
        /// <param name="lane">Start lane; Zigzag alternates toward <paramref name="lane2"/>.</param>
        void SpawnCoins(CoinFormation formation, int lane, int lane2, float z, int count, float spacing);
        void SpawnStop(PassengerStop stop, float z);
        void SpawnPowerUp(PowerUpKind kind, int lane, float z);
        void SpawnStation(GasStation station, float z);
    }

    /// <summary>
    /// Procedural level generation in "world distance" (metres since the run
    /// started). Every candidate row is validated with LaneOccupancy so the road is
    /// never impossible, and passenger stops reserve their curb lane.
    /// </summary>
    public sealed class SpawnDirector
    {
        private enum Pattern { Breather, Single, Vehicle, Double, HumpRow, Convoy, Roadwork, Parked, Weaver, Flood }

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
        private readonly List<GasStation> _pendingStations = new List<GasStation>();
        private int _stationIds;
        /// <summary>World distance of the next gas station that has not been spawned or is still ahead.</summary>
        public float NextStationAt { get; private set; }
        public int StationsPlaced { get; private set; }
        private float _nextPowerUpAt;
        private readonly float[] _traffic;
        private readonly Func<float, float> _wetness;
        private float _planWet;
        /// <summary>Slow traffic can take a long time to reach the player; plan that far ahead.</summary>
        public const float MaxHorizon = 30f;

        public float Cursor = 50f;
        public int Rejected { get; private set; }
        public int RowsPlaced { get; private set; }

        public int PowerUpsPlaced { get; private set; }

        /// <param name="wetness">Weather wetness (0..1) at a world distance; floods only appear on wet roads.</param>
        public SpawnDirector(BalanceConfig cfg, GameRandom rng, ISpawnSink sink, PassengerRoute route, EnvironmentKind environment = EnvironmentKind.Manila, Func<float, float> wetness = null)
        {
            _cfg = cfg; _rng = rng; _sink = sink; _route = route;
            _wetness = wetness ?? (_ => 0f);
            _traffic = Environments.TrafficWeights(environment);
            _nextPowerUpAt = 200f + _rng.Range(0, 150);
            NextStationAt = cfg.firstStationAt + _rng.Range(0, 150);
            PlanStops();
            PlanStation(NextStationAt);
        }

        public void Update(float distance, float speed)
        {
            // wet roads make steering slower: plan with the worst wetness in the look-ahead window
            _planWet = Math.Max(_wetness(distance), Math.Max(_wetness(distance + 150), _wetness(distance + 400)));
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
            for (int i = _pendingStations.Count - 1; i >= 0; i--)
            {
                var st = _pendingStations[i];
                if (st.At - _cfg.stationZoneLength / 2 - 10 < distance + _cfg.spawnAhead)
                {
                    _sink.SpawnStation(st, -(st.At - distance));
                    _pendingStations.RemoveAt(i);
                    StationsPlaced++;
                }
            }
            // once the jeepney is past the current station, plan the next one
            if (distance > NextStationAt + 20f)
            {
                NextStationAt += _rng.Range(_cfg.stationSpacingMin, _cfg.stationSpacingMax);
                PlanStation(NextStationAt);
            }
            int guard = 0;
            while (Cursor < distance + _cfg.spawnAhead && guard++ < 8) GenerateRow(distance, speed);
            _reservations.RemoveAll(r => r.To < distance - 20);
        }

        private bool Overlaps(int lane, float from, float to)
        {
            foreach (var r in _reservations) if (r.Lane == lane && from < r.To && to > r.From) return true;
            return false;
        }

        /// <summary>Gas station in the right curb lane, shifted forward if a PARA stop is there.</summary>
        private void PlanStation(float at)
        {
            int lane = _cfg.laneCount - 1;
            float half = _cfg.stationZoneLength / 2;
            int guard = 0;
            while (Overlaps(lane, at - half - 30, at + half + 12) && guard++ < 20) at += 40f;
            NextStationAt = at;
            var st = new GasStation { Id = ++_stationIds, At = at, Lane = lane };
            _pendingStations.Add(st);
            _reservations.Add(new Reservation { Lane = lane, From = at - half - 22, To = at + half + 6 });
        }

        private void PlanStops()
        {
            while (_route.UpcomingCount() < 4) // passengers need future stops as destinations
            {
                int side = _rng.Chance(0.5) ? -1 : 1;
                int lane = side < 0 ? 0 : _cfg.laneCount - 1;
                int g = 0;
                while (Overlaps(lane, _nextStopAt - _cfg.stopZoneLength / 2 - 30, _nextStopAt + _cfg.stopZoneLength / 2 + 12) && g++ < 20) _nextStopAt += 40f;
                var stop = _route.Plan(_nextStopAt, side, lane, _rng.Int(1, 4));
                _pendingStops.Add(stop);
                _reservations.Add(new Reservation { Lane = lane, From = stop.At - _cfg.stopZoneLength / 2 - 22, To = stop.At + _cfg.stopZoneLength / 2 + 6 });
                _nextStopAt += _rng.Range(_cfg.stopSpacingMin, _cfg.stopSpacingMax);
            }
        }

        /// <summary>
        /// True if a vehicle starting at world position <paramref name="w0"/> with forward speed
        /// <paramref name="s"/> occupies a stop zone in <paramref name="lane"/> at any moment before
        /// the jeepney catches up with it (time <paramref name="tMeet"/>).
        /// </summary>
        public bool CrossesReservation(int lane, float w0, float s, float halfLength, float tMeet)
        {
            foreach (var r in _reservations)
            {
                if (r.Lane != lane) continue;
                float ta = (r.From - halfLength - w0) / s;
                float tb = (r.To + halfLength - w0) / s;
                if (ta < tMeet + 0.5f && tb > 0) return true;
            }
            return false;
        }

        /// <summary>For traffic lane changes: is a stop zone in this lane near world position w?</summary>
        public bool ZoneNear(int lane, float w, float margin)
        {
            foreach (var r in _reservations) if (r.Lane == lane && w + margin > r.From && w - margin < r.To) return true;
            return false;
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
                if (Validate(z, distance, speed)) placed = true;
                else Rejected++;
            }
            if (placed)
            {
                RowsPlaced++;
                foreach (var c in _cands) _sink.SpawnHazard(c.Kind, c.Lane, z + c.Dz, c.Speed, c.Solid);
            }
            else _cands.Clear();
            PlaceCoins(z, distance, speed, gap, placed && lastPattern == Pattern.HumpRow);
        }

        private Pattern PickPattern(float d, float at)
        {
            bool early = at < 300;
            bool humps = at >= _cfg.humpMinDistance;
            float wet = _wetness(at);
            return _rng.Weighted(new (Pattern, float)[]
            {
                (Pattern.Breather, 0.9f - d * 0.6f),
                (Pattern.Single, 2.2f),
                (Pattern.Vehicle, 2.6f),
                (Pattern.Double, early ? 0.2f : 0.6f + 2.2f * d),
                (Pattern.HumpRow, humps ? 0.45f + 0.5f * d : 0f),
                (Pattern.Flood, wet > 0.25f ? 1.0f + 2.0f * wet : 0f),
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
            (HazardKind.Sedan, _traffic[0]), (HazardKind.Suv, _traffic[1]), (HazardKind.Tricycle, _traffic[2]), (HazardKind.Motorcycle, _traffic[3]),
            (HazardKind.Bus, _traffic[4] * (0.6f + d)), (HazardKind.Truck, _traffic[5] * (0.6f + d * 0.8f)),
        });

        private HazardKind RandomStatic() => _rng.Weighted(new (HazardKind, float)[]
        {
            (HazardKind.Cone, 1.6f), (HazardKind.Barrier, 1.6f), (HazardKind.Pothole, 1.4f), (HazardKind.ConstructionSign, 0.9f), (HazardKind.Roadblock, 0.9f),
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
                case Pattern.HumpRow:
                    // one speed hump across the whole road (centre lane holds the object)
                    Stat(HazardKind.SpeedHump, (lanes - 1) / 2);
                    break;
                case Pattern.Flood:
                    if (_wetness(Cursor) > 0.8f && _rng.Chance(0.35)) Stat(HazardKind.FloodWide, (lanes - 1) / 2);
                    else
                    {
                        Stat(HazardKind.FloodPuddle, lane);
                        if (_rng.Chance(0.4)) Stat(HazardKind.FloodPuddle, Other(lane), -_rng.Range(4, 12));
                    }
                    break;
                case Pattern.Convoy: Veh(_rng.Chance(0.55) ? HazardKind.Bus : HazardKind.Truck, lane); break;
                case Pattern.Roadwork:
                    // a cone line leading to a road block
                    Stat(HazardKind.Roadblock, lane, -6f);
                    Stat(HazardKind.Cone, lane, 0f);
                    Stat(HazardKind.Cone, lane, 3.2f);
                    Stat(HazardKind.Cone, lane, 6.4f);
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
                bool fullWidth = def.HalfWidth >= HazardCatalog.FullRoadHalfWidth - 0.01f;
                for (int l = 0; l < _cfg.laneCount; l++)
                {
                    if (!fullWidth && l != c.Lane) continue;
                    if (Reserved(l, meetAt, def.HalfLength + _cfg.playerHalfLength)) return false;
                    // moving traffic must not even drive THROUGH a PARA zone before the jeepney passes it
                    if (c.Speed > 0 && CrossesReservation(l, distance - (rowZ + c.Dz), c.Speed, def.HalfLength, t)) return false;
                }
            }
            _blockers.Clear();
            _sink.CollectBlockers(_blockers);
            foreach (var c in _cands)
            {
                var def = HazardCatalog.Get(c.Kind);
                _blockers.Add(new PredictBlocker(c.Lane, rowZ + c.Dz, def.HalfLength, c.Speed, def.Soft && !c.Solid));
            }
            // The player keeps accelerating, which changes when slow traffic arrives relative to
            // static obstacles. Validate at the current speed and at the speed reached by the
            // time the row arrives; both must be survivable.
            float later = Math.Max(speed, _cfg.SpeedAt(distance - rowZ + _cfg.spawnAhead * 0.5f));
            return PassableAt(speed) && (later - speed < 0.25f || PassableAt(later));
        }

        private bool TryPlacePowerUp(float z, float distance, float speed)
        {
            _blockers.Clear();
            _sink.CollectBlockers(_blockers);
            LaneOccupancy.BlockedIntervals(_blockers, speed, MaxHorizon, _cfg.playerHalfLength, _intervals);
            foreach (var b in _blockers)
                if (b.Soft)
                {
                    float tj = -b.Z / Math.Max(1f, speed);
                    _intervals.Add(new LaneInterval { Lane = b.Lane, T0 = tj - 0.4f, T1 = tj + 0.4f });
                }
            float t = -z / Math.Max(1f, speed);
            int start = _rng.Int(0, _cfg.laneCount - 1);
            for (int i = 0; i < _cfg.laneCount; i++)
            {
                int l = (start + i) % _cfg.laneCount;
                if (!LaneOccupancy.LaneClear(_intervals, l, t - 0.5f, t + 0.5f) || Reserved(l, distance - z, 3)) continue;
                _sink.SpawnPowerUp(PowerUpState.RandomKind(_rng), l, z);
                PowerUpsPlaced++;
                return true;
            }
            return false;
        }

        private bool PassableAt(float speed)
        {
            // horizon covers every known blocker: a new row can push the player into a lane an older row closes later
            float horizon = 1;
            foreach (var b in _blockers)
            {
                if (b.Soft) continue;
                float closing = Math.Max(0.5f, speed - b.Speed);
                horizon = Math.Max(horizon, (b.HalfLength + _cfg.playerHalfLength - b.Z) / closing + 0.6f);
            }
            horizon = Math.Min(horizon, MaxHorizon);
            return LaneOccupancy.IsPassable(_blockers, speed, horizon, _cfg.playerHalfLength, _cfg.laneCount, _cfg.laneChangeDuration * WeatherSchedule.LaneChangeFactor(_cfg, _planWet) + _cfg.laneChangeSlack, _sink.PlayerLanes);
        }

        private void PlaceCoins(float rowZ, float distance, float speed, float gap, bool hump)
        {
            if (Cursor - gap >= _nextPowerUpAt && TryPlacePowerUp(rowZ - gap * 0.5f, distance, speed))
            {
                _nextPowerUpAt = Cursor + _rng.Range(_cfg.powerUpSpacingMin, _cfg.powerUpSpacingMax);
                return;
            }
            if (hump && _rng.Chance(0.6))
            {
                // reward for slowing down: a short coin line just after the hump
                int l = _rng.Int(0, _cfg.laneCount - 1);
                if (!Reserved(l, distance - rowZ + 10, 10)) _sink.SpawnCoins(CoinFormation.Line, l, l, rowZ - 4f, 5, 2.5f);
                return;
            }
            if (!_rng.Chance(0.75)) return;
            _blockers.Clear();
            _sink.CollectBlockers(_blockers);
            float horizon = Math.Min(MaxHorizon, (-rowZ + gap + 20) / Math.Max(1f, speed));
            LaneOccupancy.BlockedIntervals(_blockers, speed, horizon, _cfg.playerHalfLength, _intervals);
            // jumpables are not in the intervals; treat them as blocking for coin placement
            foreach (var b in _blockers)
                if (b.Soft)
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
