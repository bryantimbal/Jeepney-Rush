using System;

namespace JeepneyRush.Logic
{
    public enum EventRewardKind { Coins, Gems, Cosmetic, Jeepney }

    public sealed class EventReward
    {
        /// <summary>Parol tokens needed (the Golden Parol needs all Simbang Gabi nights instead).</summary>
        public int Need;
        public EventRewardKind Kind;
        public string Id;
        public int Amount;
        public string Name;
    }

    /// <summary>
    /// "Paskong Pasada" Christmas event (PH time, 1 December – 6 January):
    ///  • parols and Christmas lights over every road
    ///  • parol tokens on the road (capped per run by distance and per day), carolers at some stops
    ///    (+<see cref="CarolerTokens"/> each, at most <see cref="MaxCarolers"/> a run)
    ///  • tokens unlock the event rewards in order; the Pasko Jeepney is event-only
    ///  • Simbang Gabi (16–24 December): a Night Shift run of <see cref="SimbangGabiDistance"/> m or more
    ///    lights that night's star; all nine win the Golden Parol roof sign
    /// The server (jr_event_*) applies the same rules; offline the save file.
    /// </summary>
    public static class ChristmasEvent
    {
        public const string Title = "PASKONG PASADA";
        public const int MaxParolsPerRun = 25, MaxCarolers = 3, CarolerTokens = 2, DailyCap = 120, Nights = 9;
        public const float FirstParolAt = 260f, ParolSpacingMin = 170f, ParolSpacingMax = 300f;
        public const float CarolerChance = 0.2f, SimbangGabiDistance = 1500f;
        public const int AllNights = (1 << Nights) - 1;

        public static bool Active(DateTime ph) => ph.Month == 12 || (ph.Month == 1 && ph.Day <= 6);

        /// <summary>The event's year (December's year): rewards and tokens are per season.</summary>
        public static int Season(DateTime ph) => ph.Month == 12 ? ph.Year : ph.Year - 1;

        public static int DaysLeft(DateTime ph) => Active(ph) ? (int)Math.Ceiling((new DateTime(Season(ph) + 1, 1, 7) - ph).TotalDays) : 0;

        /// <summary>Simbang Gabi night 1..9 (16–24 December), 0 otherwise.</summary>
        public static int SimbangGabiNight(DateTime ph) => ph.Month == 12 && ph.Day >= 16 && ph.Day <= 24 ? ph.Day - 15 : 0;

        /// <summary>Most parols a run of this length can bring (server clamp).</summary>
        public static int MaxParolsFor(float distance) => distance < 100f ? 0 : Math.Min(MaxParolsPerRun, (int)(distance / 120f) + 1);

        /// <summary>Tokens a run earns (parols collected + carolers), within the run caps.</summary>
        public static int RunTokens(int parols, int carolers, float distance) =>
            Math.Max(0, Math.Min(parols, MaxParolsFor(distance))) + Math.Max(0, Math.Min(carolers, MaxCarolers)) * CarolerTokens;

        /// <summary>Does this run light a Simbang Gabi star? (a long enough Night Shift run on a Simbang Gabi date)</summary>
        public static bool LightsNight(DateTime ph, string mode, float distance) => SimbangGabiNight(ph) > 0 && mode == "night" && distance >= SimbangGabiDistance;

        public static readonly EventReward[] Rewards =
        {
            new EventReward { Need = 50, Kind = EventRewardKind.Coins, Amount = 500, Name = "500 COINS" },
            new EventReward { Need = 120, Kind = EventRewardKind.Cosmetic, Id = "horn_jingle", Name = "JINGLE BUSINA horn" },
            new EventReward { Need = 200, Kind = EventRewardKind.Cosmetic, Id = "orn_parol", Name = "PAROL hood ornament" },
            new EventReward { Need = 300, Kind = EventRewardKind.Cosmetic, Id = "rear_belen", Name = "BELEN rear sticker" },
            new EventReward { Need = 450, Kind = EventRewardKind.Gems, Amount = 50, Name = "50 GEMS" },
            new EventReward { Need = 600, Kind = EventRewardKind.Jeepney, Id = "pasko", Name = "PASKO JEEPNEY" },
        };

        /// <summary>Simbang Gabi prize (all nine nights).</summary>
        public static readonly EventReward GoldenParol = new EventReward { Need = 0, Kind = EventRewardKind.Cosmetic, Id = "sign_goldenparol", Name = "GOLDEN PAROL roof sign" };

        /// <summary>What carolers sing when they board (original lines, not song lyrics).</summary>
        public static readonly string[] CarolerLines =
        {
            "NAMAMASKO PO! KUYA, PASAKAY KAMING CAROLERS!", "MALIGAYANG PASKO, KUYA DRIVER!", "TATLONG KANTA LANG, LIBRE NA PAMASAHE? JOKE!",
            "SIMBANG GABI TAYO MAMAYA, KUYA!", "MAY BIBINGKA AT PUTO BUMBONG SA SIMBAHAN!", "ANG SAYA-SAYA NG PASKO SA JEEP!",
        };

        public static int CountNights(int mask) { int n = 0; for (int i = 0; i < Nights; i++) if ((mask & (1 << i)) != 0) n++; return n; }

        /// <summary>Adds a run's tokens within today's cap. Returns how many were added.</summary>
        public static int AddTokens(ref int tokens, ref int dayKey, ref int dayTokens, int today, int earned)
        {
            if (dayKey != today) { dayKey = today; dayTokens = 0; }
            int add = Math.Max(0, Math.Min(earned, DailyCap - dayTokens));
            tokens += add; dayTokens += add;
            return add;
        }
    }
}
