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
        // ---- passenger personalities (see Riders)
        /// <summary>Tips included in <see cref="Coins"/> (lola, on-time hurry, tindera cargo, balikbayan, drunk).</summary>
        public int Tips;
        /// <summary>Riders who left a tip here.</summary>
        public int Tippers;
        /// <summary>Fare dodgers who jumped off here without paying (honk to collect <see cref="DodgerFare"/>).</summary>
        public int Dodgers;
        public int DodgerFare;
        /// <summary>Hurried riders delivered late (they complain) / on time.</summary>
        public int Late, OnTime;
        /// <summary>The most notable rider who got on here (Regular if none).</summary>
        public RiderKind Boarder;
        /// <summary>The most notable rider who got off here (Regular if none).</summary>
        public RiderKind Leaver;
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
        private readonly List<Rider> _aboard = new List<Rider>();
        /// <summary>Personalities come from their own stream so stop names / destinations stay the same for a seed.</summary>
        private readonly GameRandom _moodRng;
        /// <summary>Route and time of day (who boards). Set by the session.</summary>
        public EnvironmentKind Env = EnvironmentKind.Manila;
        public bool Night;
        /// <summary>Run clock in seconds (hurried riders' deadlines). Set by the session.</summary>
        public float Clock;
        /// <summary>Forces the personality of the next riders to board (tests, tutorials). Null = random.</summary>
        public RiderKind? NextKind;
        public int TipsEarned { get; private set; }
        public int DodgersCaught { get; private set; }
        public int DodgersEscaped { get; private set; }
        private int _nextId = 1;
        private int _lastName = -1;

        public readonly List<PassengerStop> Stops = new List<PassengerStop>();
        public int Capacity;
        /// <summary>Extra riders allowed hanging on the back ("sabit", provincial roads). Included in <see cref="Capacity"/>.</summary>
        public int SabitCapacity { get; private set; }
        /// <summary>Seats inside; anyone beyond this rides "sabit" on the rear step.</summary>
        public int SeatCapacity => Capacity - SabitCapacity;
        /// <summary>Riders currently hanging on the back.</summary>
        public int Hanging => Sabit.Hanging(Count, SeatCapacity);

        /// <summary>Allows <paramref name="extra"/> riders to hang on the back on top of the seats.</summary>
        public void AllowSabit(int extra)
        {
            Capacity = SeatCapacity + Math.Max(0, extra);
            SabitCapacity = Math.Max(0, extra);
        }
        /// <summary>Set to 2 while the Passenger Bonus power-up is active.</summary>
        public float FareMultiplier = 1f;
        public int PickedUp { get; private set; }
        public int Delivered { get; private set; }
        public int Missed { get; private set; }
        public int Streak { get; private set; }
        public int BestStreak { get; private set; }
        public int Count => _aboard.Count;
        /// <summary>Seats taken (a tindera's bayong takes two).</summary>
        public int SeatsUsed { get { int n = 0; foreach (var r in _aboard) n += Riders.Seats(r.Kind); return n; } }
        /// <summary>Free seats (including sabit spots).</summary>
        public int Room => Math.Max(0, Capacity - SeatsUsed);
        public IReadOnlyList<Rider> Aboard => _aboard;

        public PassengerRoute(BalanceConfig cfg, GameRandom rng, IReadOnlyList<int> nameIndices = null, GameRandom moodRng = null)
        {
            _cfg = cfg;
            _rng = rng;
            _moodRng = moodRng;
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
            foreach (var r in _aboard) if (r.Dest == stopId) n++;
            return n;
        }

        public float StreakMultiplier => Math.Min(_cfg.streakMax, 1 + Math.Max(0, Streak - 1) * _cfg.streakStep);

        public PickupResult Serve(PassengerStop stop)
        {
            stop.Resolved = true;
            var r = new PickupResult();
            var off = Remove(stop.Id);
            r.Alighted = off.Count;
            if (r.Alighted > 0)
            {
                Streak++;
                BestStreak = Math.Max(BestStreak, Streak);
                float mult = StreakMultiplier * FareMultiplier;
                // fares: the student discount, and nothing from a fare dodger (until you honk)
                float fares = 0f;
                int payers = 0;
                foreach (var p in off)
                {
                    if (p.Kind == RiderKind.Dodger) { r.Dodgers++; continue; }
                    fares += p.Kind == RiderKind.Student ? Riders.StudentFare : 1f;
                    payers++;
                }
                r.Coins += (int)Math.Round((fares * _cfg.fareCoins + Math.Max(0, payers - 1) * _cfg.groupBonusCoins) * mult);
                if (r.Dodgers > 0) r.DodgerFare = (int)Math.Round(r.Dodgers * _cfg.fareCoins * mult);
                r.Score += (int)Math.Round(r.Alighted * _cfg.scorePerDelivery * mult);
                // tips (scaled by the rating / passenger bonus, not by the streak)
                foreach (var p in off)
                {
                    int tip = Tip(p);
                    if (tip > 0) { r.Tips += (int)Math.Round(tip * FareMultiplier); r.Tippers++; }
                    if (p.Kind == RiderKind.Hurry) { if (Clock <= p.Deadline) r.OnTime++; else r.Late++; }
                    if (Notable(p.Kind) > Notable(r.Leaver)) r.Leaver = p.Kind;
                }
                r.Coins += r.Tips;
                TipsEarned += r.Tips;
                Delivered += r.Alighted;
            }
            // destinations: one of the next few planned stops (nearer stops more likely)
            var futures = Stops.FindAll(s => !s.Resolved && s.At > stop.At);
            futures.Sort((a, b) => a.At.CompareTo(b.At));
            int maxAhead = Math.Min(futures.Count, Math.Max(1, _cfg.maxRideStops));
            int space = Room;
            int boarding = futures.Count == 0 ? 0 : Math.Max(0, Math.Min(space, stop.Waiting));
            for (int i = 0; i < boarding; i++)
            {
                int minAhead = Math.Min(Math.Max(0, _cfg.minRideStops - 1), maxAhead - 1);
                int pick = minAhead + (int)Math.Floor(_rng.Next() * _rng.Next() * (maxAhead - minAhead));
                var dest = futures[Math.Min(pick, maxAhead - 1)];
                var kind = NextKind.HasValue && Riders.Seats(NextKind.Value) <= Room ? NextKind.Value
                    : _moodRng == null ? RiderKind.Regular : Riders.Pick(_moodRng, Env, Night, Room, HasAboard(RiderKind.Balikbayan));
                var rider = new Rider { Dest = dest.Id, Kind = kind };
                if (kind == RiderKind.Hurry) rider.Deadline = Clock + Math.Max(0f, dest.At - stop.At) / Riders.HurrySpeed + Riders.HurrySlack;
                _aboard.Add(rider);
                r.Boarded++;
                if (Notable(kind) > Notable(r.Boarder)) r.Boarder = kind;
                if (Room <= 0) break;
            }
            r.LeftBehind = stop.Waiting - r.Boarded;
            r.Full = r.LeftBehind > 0 && Room <= 0;
            stop.Waiting = r.LeftBehind;
            r.Coins += r.Boarded * _cfg.pickupCoins;
            r.Score += r.Boarded * _cfg.scorePerPassenger;
            r.Streak = Streak;
            PickedUp += r.Boarded;
            Prune();
            return r;
        }

        private int Tip(Rider p)
        {
            switch (p.Kind)
            {
                case RiderKind.Lola: return p.Upset ? 0 : Riders.LolaTip;
                case RiderKind.Hurry: return Clock <= p.Deadline ? Riders.HurryTip : 0;
                case RiderKind.Vendor: return Riders.VendorCargo;
                case RiderKind.Balikbayan: return p.Upset ? 0 : Riders.BalikbayanTip;
                case RiderKind.Drunk: return _moodRng != null && _moodRng.Next() < Riders.DrunkDoubleChance ? _cfg.fareCoins : 0;
                default: return 0;
            }
        }

        /// <summary>Which rider the UI talks about when several got on / off at once.</summary>
        private static int Notable(RiderKind k)
        {
            switch (k)
            {
                case RiderKind.Balikbayan: return 9;
                case RiderKind.Dodger: return 8;
                case RiderKind.Drunk: return 7;
                case RiderKind.Lola: return 6;
                case RiderKind.Hurry: return 5;
                case RiderKind.Vendor: return 4;
                case RiderKind.Chismosa: return 3;
                case RiderKind.Student: return 2;
                default: return 0;
            }
        }

        public bool HasAboard(RiderKind k)
        {
            foreach (var p in _aboard) if (p.Kind == k) return true;
            return false;
        }

        /// <summary>
        /// Rough driving (crash, flying over a hump or pothole). Lolas get upset by any jolt, the
        /// balikbayan only by a crash. Returns how many riders were newly upset.
        /// </summary>
        public int Jolt(bool crash)
        {
            int n = 0;
            foreach (var p in _aboard)
                if (!p.Upset && (p.Kind == RiderKind.Lola || (crash && p.Kind == RiderKind.Balikbayan))) { p.Upset = true; n++; }
            return n;
        }

        /// <summary>You honked in time: the fare dodger pays up.</summary>
        public void CaughtDodger(int count) => DodgersCaught += count;
        public void DodgerEscaped(int count) => DodgersEscaped += count;

        /// <summary>A random rider of this kind on board, or null (for their speech bubbles).</summary>
        public RiderKind? RandomTalker(GameRandom rng)
        {
            int n = 0;
            foreach (var p in _aboard) if (p.Kind != RiderKind.Regular) n++;
            if (n == 0) return null;
            int pick = rng.Int(0, n - 1);
            foreach (var p in _aboard) if (p.Kind != RiderKind.Regular && pick-- == 0) return p.Kind;
            return null;
        }

        /// <summary>The zone passed without the jeepney in it. Returns riders who missed their stop.</summary>
        public int Miss(PassengerStop stop)
        {
            stop.Resolved = true;
            int lost = Remove(stop.Id).Count;
            if (lost > 0)
            {
                Streak = 0;
                Missed += lost;
            }
            Prune();
            return lost;
        }

        /// <summary>
        /// A rider fed up with rough driving gets off immediately (no fare). Returns 1 if
        /// someone left, 0 if the jeepney is empty. Breaks the delivery streak.
        /// </summary>
        public int EjectAngry()
        {
            if (_aboard.Count == 0) return 0;
            _aboard.RemoveAt(_rng.Int(0, _aboard.Count - 1));
            Streak = 0;
            Missed++;
            return 1;
        }

        private readonly List<Rider> _off = new List<Rider>();

        /// <summary>Takes everyone for this stop off the jeepney (the returned list is reused).</summary>
        private List<Rider> Remove(int stopId)
        {
            _off.Clear();
            for (int i = _aboard.Count - 1; i >= 0; i--)
                if (_aboard[i].Dest == stopId) { _off.Add(_aboard[i]); _aboard.RemoveAt(i); }
            return _off;
        }

        private void Prune() => Stops.RemoveAll(s => s.Resolved);
    }
}
