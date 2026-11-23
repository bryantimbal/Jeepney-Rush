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
        /// <summary>A gem pickup (premium currency) floating over a lane.</summary>
        void SpawnGem(int lane, float z);
        void SpawnStation(GasStation station, float z);
        void SpawnIntersection(Intersection intersection, float z);
    }

    /// <summary>
    /// Procedural level generation in "world distance" (metres since the run
    /// started). Every candidate row is validated with LaneOccupancy so the road is
    /// never impossible, and passenger stops reserve their curb lane.
    /// </summary>
    public sealed class SpawnDirector
    {
        private enum Pattern { Breather, Single, Vehicle, Double, HumpRow, Convoy, Roadwork, Parked, Weaver, Flood, Mud, Jam }

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
        private readonly List<Intersection> _pendingCrossings = new List<Intersection>();
        private float _nextCrossingAt;
        private int _crossingIds;
        public int IntersectionsPlaced { get; private set; }
        private float _nextPowerUpAt;
        // road gems use their own random stream so adding them didn't change any existing layout
        private readonly GameRandom _gemRng;
        private float _nextGemAt, _nextBoostAt;
        /// <summary>Extra TURBO booster pickups (stored, used on demand) – own schedule on the same side stream.</summary>
        public const float BoostSpacingMin = 380f, BoostSpacingMax = 650f;
        public int BoostsPlaced { get; private set; }
        // road events: extra validated rows between normal rows, on their own random stream
        private readonly GameRandom _eventRng;
        /// <summary>Side stream for the extra oncoming traffic on two-way roads (keeps the main sequence unchanged).</summary>
        private readonly GameRandom _oncomingRng;
        public int OncomingPlaced { get; private set; }
        public const float OncomingChance = 0.6f;
        private float _nextEventAt;
        private readonly List<RoadEvents.Piece> _pieces = new List<RoadEvents.Piece>();
        public int EventsPlaced { get; private set; }
        public readonly List<RoadEventKind> EventLog = new List<RoadEventKind>();
        /// <summary>Gems this run may still place (per-run and daily caps; set by the session).</summary>
        public int GemBudget = RoadGems.MaxPerRun;
        public int GemsPlaced { get; private set; }
        private readonly float[] _traffic;
        private readonly Func<float, float> _wetness;
        private readonly Func<float, float> _jam;
        private float _rowJam, _trafficRef;
        private readonly bool _jamAware;
        private readonly EnvironmentKind _env;
        private readonly bool _twoWay;
        private readonly float _density;
        private float _planWet;
        /// <summary>Slow traffic can take a long time to reach the player; plan that far ahead.</summary>
        public const float MaxHorizon = 30f;

        public float Cursor = 50f;
        public int Rejected { get; private set; }
        public int RowsPlaced { get; private set; }

        public int PowerUpsPlaced { get; private set; }

        /// <param name="wetness">Weather wetness (0..1) at a world distance; floods only appear on wet roads.</param>
        /// <param name="jam">Traffic-jam strength (0..1) at a world distance (EDSA rush hour).</param>
        public SpawnDirector(BalanceConfig cfg, GameRandom rng, ISpawnSink sink, PassengerRoute route, EnvironmentKind environment = EnvironmentKind.Manila, Func<float, float> wetness = null, Func<float, float> jam = null)
        {
            _cfg = cfg; _rng = rng; _sink = sink; _route = route;
            _gemRng = rng.Fork(0x6E3D5A11u);
            _nextGemAt = RoadGems.FirstAt + _gemRng.Range(0, 250);
            _nextBoostAt = 300f + _gemRng.Range(0, 200);
            _eventRng = rng.Fork(0x3E7A1C55u);
            _oncomingRng = rng.Fork(0x0C0A1B2Du);
            _nextEventAt = RoadEvents.FirstAt + _eventRng.Range(0, 120);
            _wetness = wetness ?? (_ => 0f);
            _jam = jam ?? (_ => 0f);
            _jamAware = jam != null && Environments.JamIntensity(environment) > 0;
            _traffic = Environments.TrafficWeights(environment);
            _env = environment;
            _twoWay = Environments.TwoWay(environment);
            _density = Environments.TrafficDensity(environment);
            _nextPowerUpAt = 200f + _rng.Range(0, 150);
            NextStationAt = cfg.firstStationAt + _rng.Range(0, 150);
            _nextCrossingAt = cfg.firstIntersectionAt + _rng.Range(0, 120);
            PlanStops();
            PlanStation(NextStationAt);
            PlanCrossings(0);
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
            PlanCrossings(distance);
            for (int i = _pendingCrossings.Count - 1; i >= 0; i--)
            {
                var x = _pendingCrossings[i];
                if (x.At - 20 < distance + _cfg.spawnAhead)
                {
                    _sink.SpawnIntersection(x, -(x.At - distance));
                    _pendingCrossings.RemoveAt(i);
                    IntersectionsPlaced++;
                }
            }
            // once the jeepney is past the current station, plan the next one
            if (distance > NextStationAt + 20f)
            {
                NextStationAt += _rng.Range(_cfg.stationSpacingMin, _cfg.stationSpacingMax) * Environments.StationSpacingScale(_env);
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

        private bool OverlapsAny(float from, float to)
        {
            for (int l = 0; l < _cfg.laneCount; l++) if (Overlaps(l, from, to)) return true;
            return false;
        }

        /// <summary>Signalised intersections: every lane is kept clear from the approach to the far side.</summary>
        private void PlanCrossings(float distance)
        {
            while (_nextCrossingAt < distance + _cfg.spawnAhead + 600f)
            {
                float at = _nextCrossingAt;
                int guard = 0;
                while (OverlapsAny(at - TrafficLightRules.StopLineOffset - 40, at + TrafficLightRules.HalfBox + 15) && guard++ < 20) at += 40f;
                var x = new Intersection { Id = ++_crossingIds, At = at, Phase = _rng.Range(0, 30f) };
                _pendingCrossings.Add(x);
                for (int l = 0; l < _cfg.laneCount; l++)
                    _reservations.Add(new Reservation { Lane = l, From = at - TrafficLightRules.StopLineOffset - 22, To = at + TrafficLightRules.HalfBox + 6 });
                _nextCrossingAt = at + _rng.Range(_cfg.intersectionSpacingMin, _cfg.intersectionSpacingMax) * Environments.IntersectionSpacingScale(_env);
            }
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
                if (Environments.TwoWay(_env)) side = 1; // the left lane is oncoming traffic: stops are on the right
                int lane = side < 0 ? 0 : _cfg.laneCount - 1;
                int g = 0;
                while (Overlaps(lane, _nextStopAt - _cfg.stopZoneLength / 2 - 30, _nextStopAt + _cfg.stopZoneLength / 2 + 12) && g++ < 20) _nextStopAt += 40f;
                var stop = _route.Plan(_nextStopAt, side, lane, _rng.Int(1, 4));
                _pendingStops.Add(stop);
                _reservations.Add(new Reservation { Lane = lane, From = stop.At - _cfg.stopZoneLength / 2 - 22, To = stop.At + _cfg.stopZoneLength / 2 + 6 });
                _nextStopAt += _rng.Range(_cfg.stopSpacingMin, _cfg.stopSpacingMax) * Environments.StopSpacingScale(_env);
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
                if (Math.Abs(s) < 0.01f) continue;
                // oncoming traffic (s < 0) enters at the far end of the zone
                float ta = (r.From - halfLength - w0) / s;
                float tb = (r.To + halfLength - w0) / s;
                if (Math.Min(ta, tb) < tMeet + 0.5f && Math.Max(ta, tb) > 0) return true;
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
            _rowJam = _jam(rowAt);
            // traffic must stay clearly slower than the jeepney even after it slows for a jam
            _trafficRef = speed;
            if (_jamAware)
                for (float w = distance; w <= rowAt + 700f; w += 25f)
                    _trafficRef = Math.Min(_trafficRef, _cfg.SpeedAt(w) * JamRules.SpeedCap(_jam(w)));
            float gap = Math.Max(speed * _cfg.minReactionTime + _cfg.playerHalfLength * 2,
                (_cfg.maxRowGap + (_cfg.minRowGap - _cfg.maxRowGap) * d) * _rng.Range(0.85f, 1.2f) * JamRules.RowGapScale(_rowJam));
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
            if (Cursor >= _nextEventAt && rowAt > RoadEvents.FirstAt)
            {
                // halfway between this row and the next; a failed attempt simply waits for the next gap
                if (TryEventRow(z - gap * 0.5f, distance, speed)) _nextEventAt = Cursor + _eventRng.Range(RoadEvents.SpacingMin, RoadEvents.SpacingMax);
            }
            if (_twoWay) TryOncoming(z - gap * (_oncomingRng.Chance(0.5) ? 0.25f : 0.75f), distance, speed);
            PlaceCoins(z, distance, speed, gap, placed && lastPattern == Pattern.HumpRow);
        }

        private static readonly HazardKind[] OncomingKinds =
            { HazardKind.Motorcycle, HazardKind.Motorcycle, HazardKind.Sedan, HazardKind.Truck, HazardKind.Bus, HazardKind.Tricycle, HazardKind.Suv };

        /// <summary>Two-way road: an oncoming vehicle in the left lane between two rows, only if it stays fair.</summary>
        private void TryOncoming(float z, float distance, float speed)
        {
            if (!_oncomingRng.Chance(OncomingChance)) return;
            var k = OncomingKinds[_oncomingRng.Int(0, OncomingKinds.Length - 1)];
            _cands.Clear();
            _cands.Add(new Candidate { Kind = k, Lane = Environments.OncomingLane, Speed = -OncomingSpeed(k) });
            if (Validate(z, distance, speed))
            {
                _sink.SpawnHazard(k, Environments.OncomingLane, z, -OncomingSpeed(k), false);
                OncomingPlaced++;
            }
            _cands.Clear();
        }

        private Pattern PickPattern(float d, float at)
        {
            bool early = at < 300;
            bool humps = at >= _cfg.humpMinDistance && Environments.Paved(_env); // no painted humps on a dirt road
            bool mountain = _env == EnvironmentKind.Mountain;
            float k = _density;
            float wet = _wetness(at);
            float jam = _rowJam;
            if (jam > 0.15f) k *= 1f - 0.5f * jam; // the jam pattern takes over
            return _rng.Weighted(new (Pattern, float)[]
            {
                (Pattern.Jam, jam > 0.15f ? 9f * jam : 0f),
                (Pattern.Breather, (0.9f - d * 0.6f) * (mountain ? 1.8f : 1f)),
                (Pattern.Single, mountain ? 2.6f : 2.2f),
                (Pattern.Vehicle, 2.6f * k),
                (Pattern.Double, (early ? 0.2f : 0.6f + 2.2f * d) * k),
                (Pattern.HumpRow, humps ? 0.45f + 0.5f * d : 0f),
                (Pattern.Flood, wet > 0.25f ? 1.0f + 2.0f * wet : 0f),
                (Pattern.Mud, mountain ? 1.6f + 1.4f * wet : 0f),
                (Pattern.Convoy, (early ? 0.3f : 0.9f + 0.5f * d) * k),
                (Pattern.Roadwork, mountain ? 0.35f : 0.7f),
                (Pattern.Parked, 0.8f * k),
                (Pattern.Weaver, (early ? 0f : 0.4f + 0.9f * d) * k),
            });
        }

        /// <summary>Oncoming traffic speed (m/s) – a fixed table so the main random stream is unchanged.</summary>
        public static float OncomingSpeed(HazardKind k) =>
            k == HazardKind.Motorcycle ? 8f : k == HazardKind.Bus || k == HazardKind.Truck ? 5.5f : k == HazardKind.Tricycle ? 4.5f : 7f;

        private float TrafficSpeed(HazardKind k, float playerSpeed)
        {
            var def = HazardCatalog.Get(k);
            // in a jam everything crawls
            if (_rowJam > 0.15f && k != HazardKind.Motorcycle)
                return Math.Min(_rng.Range(JamRules.CrawlMin, JamRules.CrawlMax), Math.Max(1.2f, playerSpeed * 0.55f));
            // traffic is always clearly slower than the jeepney so it can be overtaken
            return Math.Max(1.5f, Math.Min(_rng.Range(def.MinSpeed, def.MaxSpeed), playerSpeed * 0.55f));
        }

        private HazardKind RandomVehicle(float d) => _rng.Weighted(new (HazardKind, float)[]
        {
            (HazardKind.Sedan, _traffic[0]), (HazardKind.Suv, _traffic[1]), (HazardKind.Tricycle, _traffic[2]), (HazardKind.Motorcycle, _traffic[3]),
            (HazardKind.Bus, _traffic[4] * (0.6f + d)), (HazardKind.Truck, _traffic[5] * (0.6f + d * 0.8f)),
        });

        private HazardKind RandomStatic() => _env == EnvironmentKind.Mountain
            ? _rng.Weighted(new (HazardKind, float)[]
            {
                // landslide rocks and deep potholes; the odd road-works sign
                (HazardKind.Rocks, 2.2f), (HazardKind.Pothole, 2.4f), (HazardKind.ConstructionSign, 0.6f), (HazardKind.Barrier, 0.4f),
            })
            : _rng.Weighted(new (HazardKind, float)[]
            {
                (HazardKind.Cone, 1.6f), (HazardKind.Barrier, 1.6f), (HazardKind.Pothole, 1.4f), (HazardKind.ConstructionSign, 0.9f), (HazardKind.Roadblock, 0.9f),
            });

        private void BuildPattern(Pattern p, float d, float speed)
        {
            _cands.Clear();
            int lanes = _cfg.laneCount;
            int lane = _rng.Int(0, lanes - 1);
            int Other(int l) => (l + _rng.Int(1, lanes - 1)) % lanes;
            void Veh(HazardKind k, int l, float dz = 0)
            {
                float v = TrafficSpeed(k, _jamAware ? _trafficRef : speed);
                // two-way road: vehicles in the left lane come toward the jeepney
                if (_twoWay && l == Environments.OncomingLane) v = -OncomingSpeed(k);
                _cands.Add(new Candidate { Kind = k, Lane = l, Dz = dz, Speed = v });
            }
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
                case Pattern.Mud:
                    // muddy patches across one or two lanes (soft: they slow you down)
                    Stat(HazardKind.MudPatch, lane);
                    if (_rng.Chance(0.45)) Stat(HazardKind.MudPatch, Other(lane), -_rng.Range(3, 10));
                    break;
                case Pattern.Convoy: Veh(_env == EnvironmentKind.Mountain || !_rng.Chance(0.55) ? HazardKind.Truck : HazardKind.Bus, lane); break;
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
                case Pattern.Jam:
                {
                    // bumper-to-bumper: two lanes packed, the third sometimes filled a little further on
                    int l2 = Other(lane);
                    Veh(RandomVehicle(d), lane);
                    Veh(RandomVehicle(d), l2, -_rng.Range(0, 4));
                    if (_rng.Chance(0.5)) Veh(RandomVehicle(d), lane, -_rng.Range(9, 13));
                    if (_rng.Chance(0.2))
                    {
                        int l3 = 3 - lane - l2; // the remaining lane (3-lane road)
                        if (l3 >= 0 && l3 < lanes) Veh(RandomVehicle(d), l3, -_rng.Range(14, 22));
                    }
                    if (_rng.Chance(0.25)) Veh(HazardKind.Motorcycle, _rng.Int(0, lanes - 1), -_rng.Range(5, 18)); // motorbikes squeezing through
                    break;
                }
                case Pattern.Weaver:
                    Veh(HazardKind.Motorcycle, lane);
                    Veh(HazardKind.Motorcycle, Other(lane), -_rng.Range(6, 14));
                    break;
            }
        }

        private bool Validate(float rowZ, float distance, float speed)
        {
            // slowest speed the jeepney will have before it reaches this row (jams): traffic then
            // has longer to drive on, so zone checks must also hold at that speed
            float slow = speed;
            if (_jamAware)
                for (int i = 1; i <= 8; i++) { float w = distance + (-rowZ + 300f) * i / 8f; slow = Math.Min(slow, _cfg.SpeedAt(w) * JamRules.SpeedCap(_jam(w))); }
            foreach (var c in _cands)
            {
                var def = HazardCatalog.Get(c.Kind);
                bool fullWidth = def.HalfWidth >= HazardCatalog.FullRoadHalfWidth - 0.01f;
                foreach (float v in slow < speed - 0.25f ? new[] { speed, slow } : new[] { speed })
                {
                    float closing = Math.Max(0.5f, v - c.Speed);
                    float t = -(rowZ + c.Dz) / closing;
                    float meetAt = distance + v * t;
                    for (int l = 0; l < _cfg.laneCount; l++)
                    {
                        if (!fullWidth && l != c.Lane) continue;
                        if (Reserved(l, meetAt, def.HalfLength + _cfg.playerHalfLength)) return false;
                        // moving traffic must not even drive THROUGH a PARA zone before the jeepney passes it
                        if (Math.Abs(c.Speed) > 0.01f && CrossesReservation(l, distance - (rowZ + c.Dz), c.Speed, def.HalfLength, t)) return false;
                    }
                }
            }
            _strict = false;
            foreach (var c in _cands) if (c.Speed < 0) _strict = true;
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
            float arriveAt = distance - rowZ;
            float later = Math.Max(speed, _cfg.SpeedAt(arriveAt + _cfg.spawnAhead * 0.5f) * JamRules.SpeedCap(_jam(arriveAt + _cfg.spawnAhead * 0.5f)));
            if (!PassableAt(speed) || (later - speed >= 0.25f && !PassableAt(later))) return false;
            // entering or leaving a jam changes the jeepney's speed a lot: the row must also be
            // survivable at the speeds it will have on the way there
            if (_jamAware)
            {
                float lo = speed, hi = speed;
                for (int i = 0; i <= 4; i++)
                {
                    float w = distance + (arriveAt - distance + _cfg.spawnAhead * 0.5f) * i / 4f;
                    float v = _cfg.SpeedAt(w) * JamRules.SpeedCap(_jam(w));
                    lo = Math.Min(lo, v); hi = Math.Max(hi, v);
                }
                if (speed - lo >= 0.25f && !PassableAt(lo)) return false;
                if (hi - Math.Max(speed, later) >= 0.25f && !PassableAt(hi)) return false;
                float mid = (lo + hi) / 2;
                if (hi - lo >= 1f && !PassableAt(mid)) return false;
            }
            return true;
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
                var kind = PowerUpState.RandomKind(_rng);
                // no speed boost right before a PARA stop, gas station or intersection: you'd fly past it
                if (kind == PowerUpKind.Turbo && TurboUnsafe(distance - z)) kind = l % 2 == 0 ? PowerUpKind.Magnet : PowerUpKind.DoubleCoins;
                _sink.SpawnPowerUp(kind, l, z);
                PowerUpsPlaced++;
                return true;
            }
            return false;
        }

        private bool TryEventRow(float z, float distance, float speed)
        {
            if (_rowJam > 0.15f) return false; // the jam is event enough
            // never close a lane just before a jam: slow traffic would fill the other lanes
            if (_jamAware) for (float w = distance - z; w <= distance - z + 500f; w += 50f) if (_jam(w) > 0.05f) return false;
            var kind = _eventRng.Weighted(RoadEvents.For(_env));
            RoadEvents.Build(kind, _eventRng.Int(0, _cfg.laneCount - 1), _cfg.laneCount, _pieces);
            _cands.Clear();
            foreach (var p in _pieces)
            {
                var def = HazardCatalog.Get(p.Kind);
                float v = def.IsMoving ? Math.Max(0.8f, Math.Min(_eventRng.Range(def.MinSpeed, def.MaxSpeed), speed * 0.5f)) : 0f;
                _cands.Add(new Candidate { Kind = p.Kind, Lane = p.Lane, Dz = p.Dz, Speed = v });
            }
            if (!Validate(z, distance, speed)) { _cands.Clear(); return false; }
            foreach (var c in _cands) _sink.SpawnHazard(c.Kind, c.Lane, z + c.Dz, c.Speed, c.Solid);
            _cands.Clear();
            EventsPlaced++;
            EventLog.Add(kind);
            return true;
        }

        /// <summary>A gem or a TURBO booster in a clear lane (like power-ups: never behind a blocker or in a stop's lane).</summary>
        private bool TryPlaceExtra(float z, float distance, float speed, bool gem)
        {
            _blockers.Clear();
            _sink.CollectBlockers(_blockers);
            LaneOccupancy.BlockedIntervals(_blockers, speed, MaxHorizon, _cfg.playerHalfLength, _intervals);
            float t = -z / Math.Max(1f, speed);
            int start = _gemRng.Int(0, _cfg.laneCount - 1);
            for (int i = 0; i < _cfg.laneCount; i++)
            {
                int l = (start + i) % _cfg.laneCount;
                if (!LaneOccupancy.LaneClear(_intervals, l, t - 0.5f, t + 0.5f) || Reserved(l, distance - z, 3)) continue;
                if (gem) { _sink.SpawnGem(l, z); GemsPlaced++; }
                else { _sink.SpawnPowerUp(PowerUpKind.Turbo, l, z); BoostsPlaced++; }
                return true;
            }
            return false;
        }

        /// <summary>Distance a Turbo must stay away from any stop, gas station or intersection (it lasts several seconds at high speed).</summary>
        public const float TurboClearance = 220f; // ~5 s of Turbo at top speed, plus room to brake

        /// <summary>True if a reserved zone (PARA stop, gas station, intersection) starts within reach of a Turbo picked up at <paramref name="w"/>.</summary>
        public bool TurboUnsafe(float w)
        {
            foreach (var r in _reservations) if (r.To > w - 10f && r.From < w + TurboClearance) return true;
            foreach (var st in _pendingStops) if (st.At > w - 10f && st.At < w + TurboClearance) return true;
            foreach (var x in _pendingCrossings) if (x.At > w - 10f && x.At < w + TurboClearance) return true;
            return false;
        }

        public const float OncomingSteerFactor = 1.25f;
        private bool _strict;
        private readonly int[] _oneLane = new int[1];

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
            float move = _cfg.laneChangeDuration * WeatherSchedule.LaneChangeFactor(_cfg, _planWet) + _cfg.laneChangeSlack;
            if (!_strict) return LaneOccupancy.IsPassable(_blockers, speed, horizon, _cfg.playerHalfLength, _cfg.laneCount, move, _sink.PlayerLanes);
            // oncoming traffic closes very fast and can shut the only escape lane: the row must be survivable
            // from EACH lane the player may be in (mid lane change) and with slower steering
            var lanes = _sink.PlayerLanes;
            if (lanes == null || lanes.Count == 0) return LaneOccupancy.IsPassable(_blockers, speed, horizon, _cfg.playerHalfLength, _cfg.laneCount, move * OncomingSteerFactor);
            for (int i = 0; i < lanes.Count; i++)
            {
                _oneLane[0] = lanes[i];
                if (!LaneOccupancy.IsPassable(_blockers, speed, horizon, _cfg.playerHalfLength, _cfg.laneCount, move * OncomingSteerFactor, _oneLane)) return false;
            }
            return true;
        }

        private void PlaceCoins(float rowZ, float distance, float speed, float gap, bool hump)
        {
            // gems and extra boosters sit a quarter-gap ahead of the row; they never change what the row itself gets
            if (GemsPlaced < GemBudget && Cursor - gap >= _nextGemAt)
            {
                if (TryPlaceExtra(rowZ - gap * 0.25f, distance, speed, true)) _nextGemAt = Cursor + _gemRng.Range(RoadGems.SpacingMin, RoadGems.SpacingMax);
            }
            else if (Cursor - gap >= _nextBoostAt && TryPlaceExtra(rowZ - gap * 0.25f, distance, speed, false))
                _nextBoostAt = Cursor + _gemRng.Range(BoostSpacingMin, BoostSpacingMax);
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
