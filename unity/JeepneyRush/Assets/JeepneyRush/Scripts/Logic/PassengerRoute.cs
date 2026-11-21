using System;
using System.Collections.Generic;

namespace JeepneyRush.Logic
{
    public sealed class PassengerStop
    {
        public int Id;
        public string Name;
        /// <summary>Index into <see cref="StopNames.All"/> (used for the pre-rendered sign).</summary>
        public int NameIndex;
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
        /// <summary>Passengers who reached their destination here (paid fares).</summary>
        public int Alighted;
        public int LeftBehind;
        public int Coins;
        public int Score;
        public int Streak;
        public bool Full;
    }

    /// <summary>
    /// Phase 2 passenger model. Every rider has a destination among the next stops.
    /// Drive through their stop to deliver them: fare + group bonus × delivery-streak
    /// multiplier (× passenger-bonus power-up). Skip it and they are lost and the
    /// streak resets ("LAGPAS!"). Capacity comes from the jeepney + upgrades.
    /// </summary>
    public sealed class PassengerRoute
    {
        private readonly BalanceConfig _cfg;
        private readonly GameRandom _rng;
        private readonly IReadOnlyList<int> _names;
        private readonly List<int> _aboard = new List<int>(); // destination stop ids
        private int _nextId = 1;
        private int _lastName = -1;

        public readonly List<PassengerStop> Stops = new List<PassengerStop>();
        public int Capacity;
        /// <summary>Set to 2 while the Passenger Bonus power-up is active.</summary>
        public float FareMultiplier = 1f;
        public int PickedUp { get; private set; }
        public int Delivered { get; private set; }
        public int Missed { get; private set; }
        public int Streak { get; private set; }
        public int BestStreak { get; private set; }
        public int Count => _aboard.Count;

        public PassengerRoute(BalanceConfig cfg, GameRandom rng, IReadOnlyList<int> nameIndices = null)
        {
            _cfg = cfg;
            _rng = rng;
            Capacity = cfg.passengerCapacity;
            _names = nameIndices ?? StopNames.ForEnvironment(EnvironmentKind.Manila);
        }

        public PassengerStop Plan(float at, int side, int lane, int waiting)
        {
            int idx, guard = 0;
            do idx = _names[_rng.Int(0, _names.Count - 1)];
            while ((idx == _lastName || Stops.Exists(s => !s.Resolved && s.NameIndex == idx)) && guard++ < 30);
            _lastName = idx;
            var stop = new PassengerStop { Id = _nextId++, NameIndex = idx, Name = StopNames.All[idx], Side = side, Lane = lane, At = at, Waiting = waiting };
            Stops.Add(stop);
            return stop;
        }

        public int UpcomingCount()
        {
            int n = 0;
            foreach (var s in Stops) if (!s.Resolved) n++;
            return n;
        }

        public PassengerStop NextStop(float distance)
        {
            PassengerStop best = null;
            foreach (var s in Stops)
                if (!s.Resolved && s.At > distance - 10 && (best == null || s.At < best.At)) best = s;
            return best;
        }

        public int AlightingAt(int stopId)
        {
            int n = 0;
            foreach (int d in _aboard) if (d == stopId) n++;
            return n;
        }

        public float StreakMultiplier => Math.Min(_cfg.streakMax, 1 + Math.Max(0, Streak - 1) * _cfg.streakStep);

        public PickupResult Serve(PassengerStop stop)
        {
            stop.Resolved = true;
            var r = new PickupResult();
            r.Alighted = Remove(stop.Id);
            if (r.Alighted > 0)
            {
                Streak++;
                BestStreak = Math.Max(BestStreak, Streak);
                float mult = StreakMultiplier * FareMultiplier;
                r.Coins += (int)Math.Round((r.Alighted * _cfg.fareCoins + (r.Alighted - 1) * _cfg.groupBonusCoins) * mult);
                r.Score += (int)Math.Round(r.Alighted * _cfg.scorePerDelivery * mult);
                Delivered += r.Alighted;
            }
            // destinations: one of the next few planned stops (nearer stops more likely)
            var futures = Stops.FindAll(s => !s.Resolved && s.At > stop.At);
            futures.Sort((a, b) => a.At.CompareTo(b.At));
            int maxAhead = Math.Min(futures.Count, Math.Max(1, _cfg.maxRideStops));
            int space = Capacity - _aboard.Count;
            r.Boarded = futures.Count == 0 ? 0 : Math.Max(0, Math.Min(space, stop.Waiting));
            for (int i = 0; i < r.Boarded; i++)
            {
                int minAhead = Math.Min(Math.Max(0, _cfg.minRideStops - 1), maxAhead - 1);
                int pick = minAhead + (int)Math.Floor(_rng.Next() * _rng.Next() * (maxAhead - minAhead));
                _aboard.Add(futures[Math.Min(pick, maxAhead - 1)].Id);
            }
            r.LeftBehind = stop.Waiting - r.Boarded;
            r.Full = r.LeftBehind > 0 && _aboard.Count >= Capacity;
            stop.Waiting = r.LeftBehind;
            r.Coins += r.Boarded * _cfg.pickupCoins;
            r.Score += r.Boarded * _cfg.scorePerPassenger;
            r.Streak = Streak;
            PickedUp += r.Boarded;
            Prune();
            return r;
        }

        /// <summary>The zone passed without the jeepney in it. Returns riders who missed their stop.</summary>
        public int Miss(PassengerStop stop)
        {
            stop.Resolved = true;
            int lost = Remove(stop.Id);
            if (lost > 0)
            {
                Streak = 0;
                Missed += lost;
            }
            Prune();
            return lost;
        }

        private int Remove(int stopId)
        {
            int n = 0;
            for (int i = _aboard.Count - 1; i >= 0; i--)
                if (_aboard[i] == stopId) { _aboard.RemoveAt(i); n++; }
            return n;
        }

        private void Prune() => Stops.RemoveAll(s => s.Resolved);
    }
}
