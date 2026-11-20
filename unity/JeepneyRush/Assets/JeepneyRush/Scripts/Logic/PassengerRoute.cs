using System;
using System.Collections.Generic;

namespace JeepneyRush.Logic
{
    public sealed class PassengerStop
    {
        public int Id;
        public string Name;
        /// <summary>-1 = left curb, +1 = right curb.</summary>
        public int Side;
        public int Lane;
        /// <summary>World distance (metres since the run started) of the zone centre.</summary>
        public float At;
        public int Waiting;
        public bool Resolved;
    }

    public struct PickupResult
    {
        public int Boarded;
        public int Alighted;
        public int LeftBehind;
        public int Coins;
        public int Score;
        public bool Full;
    }

    /// <summary>
    /// Phase 1 passenger model: pick up commuters at stops, capacity enforced.
    /// Riders hop off on their own after a few stops (freeing seats); destination
    /// choice and fares are the Phase 2 delivery system.
    /// </summary>
    public sealed class PassengerRoute
    {
        public static readonly string[] StopNames =
        {
            "QUIAPO", "CUBAO", "DIVISORIA", "RECTO", "LAWTON", "ESPANA", "TAFT", "MONUMENTO", "BACLARAN",
            "BLUMENTRITT", "KALAW", "PACO", "STA. MESA", "PEDRO GIL", "VITO CRUZ", "LIBERTAD", "SAMPALOC",
            "ERMITA", "MALATE", "TONDO",
        };

        private readonly BalanceConfig _cfg;
        private readonly GameRandom _rng;
        private readonly List<int> _ridesLeft = new List<int>();
        private int _nextId = 1;
        private int _lastNameIndex = -1;

        public readonly List<PassengerStop> Stops = new List<PassengerStop>();
        public int Capacity;
        public int PickedUp { get; private set; }
        public int Count => _ridesLeft.Count;

        public PassengerRoute(BalanceConfig cfg, GameRandom rng)
        {
            _cfg = cfg;
            _rng = rng;
            Capacity = cfg.passengerCapacity;
        }

        public PassengerStop Plan(float at, int side, int lane, int waiting)
        {
            int idx;
            int guard = 0;
            do idx = _rng.Int(0, StopNames.Length - 1);
            while ((idx == _lastNameIndex || Stops.Exists(s => !s.Resolved && s.Name == StopNames[idx])) && guard++ < 20);
            _lastNameIndex = idx;
            var stop = new PassengerStop { Id = _nextId++, Name = StopNames[idx], Side = side, Lane = lane, At = at, Waiting = waiting };
            Stops.Add(stop);
            return stop;
        }

        public int UpcomingCount()
        {
            int n = 0;
            foreach (var s in Stops) if (!s.Resolved) n++;
            return n;
        }

        /// <summary>Next unresolved stop at or after <paramref name="distance"/>-10 m.</summary>
        public PassengerStop NextStop(float distance)
        {
            PassengerStop best = null;
            foreach (var s in Stops)
                if (!s.Resolved && s.At > distance - 10 && (best == null || s.At < best.At)) best = s;
            return best;
        }

        /// <summary>The jeepney drove through the stop zone.</summary>
        public PickupResult Serve(PassengerStop stop)
        {
            stop.Resolved = true;
            var r = new PickupResult();
            // riders whose trip ends get off on their own
            for (int i = _ridesLeft.Count - 1; i >= 0; i--)
            {
                _ridesLeft[i]--;
                if (_ridesLeft[i] <= 0)
                {
                    _ridesLeft.RemoveAt(i);
                    r.Alighted++;
                }
            }
            int space = Capacity - _ridesLeft.Count;
            r.Boarded = Math.Max(0, Math.Min(space, stop.Waiting));
            for (int i = 0; i < r.Boarded; i++) _ridesLeft.Add(_rng.Int(_cfg.minRideStops, _cfg.maxRideStops));
            r.LeftBehind = stop.Waiting - r.Boarded;
            r.Full = r.LeftBehind > 0;
            stop.Waiting = r.LeftBehind;
            r.Coins = r.Boarded * _cfg.pickupCoins;
            r.Score = r.Boarded * _cfg.scorePerPassenger;
            PickedUp += r.Boarded;
            Prune();
            return r;
        }

        /// <summary>The zone passed without the jeepney in it: commuters keep waiting, riders stay aboard.</summary>
        public void Miss(PassengerStop stop)
        {
            stop.Resolved = true;
            Prune();
        }

        private void Prune() => Stops.RemoveAll(s => s.Resolved);
    }
}
