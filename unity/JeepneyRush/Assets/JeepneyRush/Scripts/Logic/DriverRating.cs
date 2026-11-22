using System;

namespace JeepneyRush.Logic
{
    /// <summary>What the riders think of the driver this run (shown as stars, 1.0–5.0).</summary>
    public enum RatingEvent { Served, Smooth, PoliteCheckpoint, SkippedStop, Crash, HumpFast, RedLight, Ticket, AngryWalkOff }

    /// <summary>
    /// Per-run driver rating, like a ride-hailing app. Starts at ★4.0. Serving stops and smooth
    /// driving raise it; skipping stops where people were waiting (or wanted to get off), crashing,
    /// flying over humps and running red lights lower it. It scales fares and tips. Skip 3 stops
    /// in a row and the riders panic; hit ★1.0 and the police come after you (see <see cref="PoliceChase"/>).
    /// </summary>
    public sealed class DriverRating
    {
        public const float Start = 4f, Min = 1f, Max = 5f;
        public const int PanicAfterSkips = 3;
        /// <summary>Rating after being let off with a warning by the police.</summary>
        public const float AfterPardon = 2f;

        public float Value { get; private set; } = Start;
        /// <summary>Stops with business skipped in a row (reset by serving a stop).</summary>
        public int SkippedInARow { get; private set; }
        public bool Panicking => SkippedInARow >= PanicAfterSkips;
        public bool AtBottom => Value <= Min + 0.0001f;
        /// <summary>Rounded to one decimal for display ("3.7").</summary>
        public string Display => (Math.Round(Value * 10) / 10).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);

        public static float Delta(RatingEvent e)
        {
            switch (e)
            {
                case RatingEvent.Served: return 0.1f;
                case RatingEvent.Smooth: return 0.05f;
                case RatingEvent.PoliteCheckpoint: return 0.1f;
                case RatingEvent.SkippedStop: return -0.4f;
                case RatingEvent.Crash: return -0.2f;
                case RatingEvent.HumpFast: return -0.1f;
                case RatingEvent.RedLight: return -0.3f;
                case RatingEvent.Ticket: return -0.2f;
                case RatingEvent.AngryWalkOff: return -0.2f;
                default: return 0f;
            }
        }

        /// <summary>Applies an event and returns the change actually made (clamped to 1..5).</summary>
        public float Apply(RatingEvent e)
        {
            if (e == RatingEvent.Served) SkippedInARow = 0;
            if (e == RatingEvent.SkippedStop) SkippedInARow++;
            float before = Value;
            Value = Math.Max(Min, Math.Min(Max, Value + Delta(e)));
            return Value - before;
        }

        /// <summary>The police let you go with a warning: back to ★2.0, riders calm down.</summary>
        public void Pardon()
        {
            Value = Math.Max(Value, AfterPardon);
            SkippedInARow = 0;
        }

        /// <summary>Fares and tips: ★5 = +20 %, ★3.5 = normal, ★2 = −20 % (never below −30 %).</summary>
        public float FareMultiplier => Math.Max(0.7f, 1f + (Value - 3.5f) * (0.2f / 1.5f));
    }

    /// <summary>
    /// The police chase that starts when the rating hits ★1.0. The patrol car starts
    /// <see cref="StartGap"/> metres behind and closes in at <see cref="ClosingSpeed"/>. One last
    /// chance: come to a full stop (pull over, or stop at a PARA) for <see cref="PullOverSeconds"/>
    /// before it reaches you and you're let off with a warning and a fine. Otherwise: BUSTED.
    /// </summary>
    public sealed class PoliceChase
    {
        public const float StartGap = 75f, ClosingSpeed = 3.2f, PullOverSeconds = 1.5f;

        public bool Active { get; private set; }
        public float Gap { get; private set; }
        public float StoppedFor { get; private set; }
        /// <summary>0 = just started, 1 = caught.</summary>
        public float Progress => Active ? 1f - Math.Max(0, Gap) / StartGap : 0f;

        public void Start() { Active = true; Gap = StartGap; StoppedFor = 0; }
        public void End() { Active = false; StoppedFor = 0; }

        public enum Outcome { Chasing, PulledOver, Busted }

        /// <param name="stopped">The jeepney is at a full stop this frame.</param>
        public Outcome Tick(float dt, bool stopped)
        {
            if (!Active) return Outcome.Chasing;
            Gap -= ClosingSpeed * dt;
            StoppedFor = stopped ? StoppedFor + dt : 0f;
            if (StoppedFor >= PullOverSeconds) { End(); return Outcome.PulledOver; }
            if (Gap <= 0f) { Active = false; return Outcome.Busted; }
            return Outcome.Chasing;
        }

        /// <summary>Seconds the driver has to pull over (from a fresh start).</summary>
        public static float TimeToCatch => StartGap / ClosingSpeed;
    }
}
