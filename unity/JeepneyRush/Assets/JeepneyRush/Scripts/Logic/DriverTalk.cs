using System;
using System.Collections.Generic;

namespace JeepneyRush.Logic
{
    /// <summary>What the driver (tsuper) is reacting to.</summary>
    public enum DriverMoment { Boarding, AskFare, FareReceived, VehicleFull, PickupDone, Depart, NearMiss, Pothole, GameOver }

    /// <summary>
    /// The driver's lines: natural, polite everyday jeepney Tagalog ("po", "paki-abot"), a few
    /// variations per moment so it doesn't repeat. Passengers' own lines stay in <see cref="Lines"/>.
    /// </summary>
    public static class DriverLines
    {
        public static string[] For(DriverMoment m)
        {
            switch (m)
            {
                case DriverMoment.Boarding: return new[] { "SAKAY NA PO KAYO!", "SAKAY NA PO, MAY BAKANTE PA!", "DAHAN-DAHAN PO SA PAGSAKAY!", "USOG-USOG LANG PO, KASYA PA!" };
                case DriverMoment.AskFare: return new[] { "PAKI-ABOT NA LANG PO NG BAYAD.", "BAYAD PO MUNA, SALAMAT!", "PAKIABOT PO ANG BAYAD, SALAMAT PO!" };
                case DriverMoment.FareReceived: return new[] { "SALAMAT PO!", "SALAMAT PO SA BAYAD!", "OKAY NA PO, SALAMAT!", "SUKLI PO, SALAMAT!" };
                case DriverMoment.VehicleFull: return new[] { "PASENSYA NA PO, PUNO NA!", "PUNO NA PO, SA SUSUNOD NA LANG!", "WALA NA PONG UPUAN, PASENSYA!" };
                case DriverMoment.PickupDone: return new[] { "OKAY NA PO, BIYAHE NA!", "KUMPLETO NA PO? TARA NA!", "AYOS NA PO, ANDAR NA TAYO!" };
                case DriverMoment.Depart: return new[] { "ALIS NA TAYO!", "LARGA NA!", "TARA, BIYAHE NA!", "KAPIT PO, AANDAR NA!" };
                case DriverMoment.NearMiss: return new[] { "AY, MUNTIK NA!", "MUNTIK NA 'YON, AH!", "HAY, BUTI NA LANG!", "SUSMARYOSEP, MUNTIK NA!" };
                case DriverMoment.Pothole: return new[] { "INGAT PO, MAY LUBAK!", "KAPIT PO, MAY LUBAK!", "PASENSYA NA PO SA LUBAK!" };
                default: return new[] { "AY, PAHINGA MUNA TAYO!", "HAY, BUKAS NA LANG ULIT!", "TAMA NA MUNA ANG BIYAHE!" };
            }
        }

        /// <summary>How important a line is: warnings may cut in, small talk waits its turn.</summary>
        public static int Priority(DriverMoment m) =>
            m == DriverMoment.NearMiss || m == DriverMoment.Pothole || m == DriverMoment.GameOver ? 3
            : m == DriverMoment.VehicleFull || m == DriverMoment.AskFare || m == DriverMoment.FareReceived ? 2 : 1;
    }

    /// <summary>
    /// Picks the driver's lines without repeating the last one used for the same moment, and keeps
    /// him from talking over himself: one line at a time, a short pause between lines, and only a
    /// more important line (a warning) may cut in.
    /// </summary>
    public sealed class DriverTalk
    {
        /// <summary>Seconds a line occupies the driver before the next ordinary one may start.</summary>
        public const float LineTime = 1.7f;
        private readonly GameRandom _rng;
        private readonly Dictionary<DriverMoment, int> _last = new Dictionary<DriverMoment, int>();
        private float _busyUntil = -1f;
        private int _busyPriority;

        public DriverTalk(GameRandom rng) { _rng = rng; }

        /// <summary>A line for the moment (never the same as the previous one for that moment).</summary>
        public string Pick(DriverMoment m)
        {
            var lines = DriverLines.For(m);
            int i = _rng.Int(0, lines.Length - 1);
            if (lines.Length > 1 && _last.TryGetValue(m, out int prev) && prev == i) i = (i + 1 + _rng.Int(0, lines.Length - 2)) % lines.Length;
            _last[m] = i;
            return lines[i];
        }

        /// <summary>Returns the line to say now, or null if the driver is busy with something at least as important.</summary>
        public string TrySay(DriverMoment m, float now)
        {
            int p = DriverLines.Priority(m);
            if (now < _busyUntil && p <= _busyPriority) return null;
            _busyUntil = now + LineTime;
            _busyPriority = p;
            return Pick(m);
        }

        public void Reset() { _busyUntil = -1f; _busyPriority = 0; _last.Clear(); }
    }
}
