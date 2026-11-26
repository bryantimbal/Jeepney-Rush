using System;

namespace JeepneyRush.Logic
{
    /// <summary>Who's riding: every passenger has a personality that changes how they pay and what they want.</summary>
    public enum RiderKind { Regular, Student, Lola, Chismosa, Hurry, Drunk, Dodger, Vendor, Balikbayan }

    /// <summary>One passenger on board.</summary>
    public sealed class Rider
    {
        /// <summary>Stop id where they get off.</summary>
        public int Dest;
        public RiderKind Kind;
        /// <summary>Hurry: run clock (s) by which they want to be there.</summary>
        public float Deadline;
        /// <summary>Lola / balikbayan: rough driving while they were aboard (no tip).</summary>
        public bool Upset;
    }

    /// <summary>
    /// Passenger personalities (rules shared by the route and the UI):
    ///  • Estudyante – student discount (80 % fare), mostly in the daytime
    ///  • Lola – slow and easily shaken: a smooth ride (no crash / fast hump while aboard) earns a tip
    ///  • Chismosa – talks the whole way; pays normally
    ///  • Nagmamadali – in a hurry: on time = tip, late = complaint
    ///  • Lasing – night only; sometimes pays double ("sa'yo na ang sukli!")
    ///  • Hindi nagbayad – jumps off without paying: honk within <see cref="DodgerWindow"/> s to get the fare
    ///  • Tindera – brings a big bayong that takes two seats, pays for the cargo
    ///  • Balikbayan – rare, generous: big tip if the ride had no crash
    /// Tips are scaled by the rating / passenger-bonus fare multiplier, not by the delivery streak.
    /// </summary>
    public static class Riders
    {
        public const float DodgerWindow = 2.5f;
        /// <summary>Speed (m/s) a hurried rider expects on average, plus slack, for the deadline.</summary>
        public const float HurrySpeed = 8f, HurrySlack = 8f;
        public const int LolaTip = 3, HurryTip = 4, VendorCargo = 3, BalikbayanTip = 10;
        public const float StudentFare = 0.8f, DrunkDoubleChance = 0.25f;

        public static string Tag(RiderKind k)
        {
            switch (k)
            {
                case RiderKind.Student: return "ESTUDYANTE";
                case RiderKind.Lola: return "LOLA";
                case RiderKind.Chismosa: return "CHISMOSA";
                case RiderKind.Hurry: return "NAGMAMADALI";
                case RiderKind.Drunk: return "LASING";
                case RiderKind.Dodger: return "KAHINA-HINALA";
                case RiderKind.Vendor: return "TINDERA";
                case RiderKind.Balikbayan: return "BALIKBAYAN";
                default: return "";
            }
        }

        /// <summary>Seats a rider takes (the tindera's bayong takes one more).</summary>
        public static int Seats(RiderKind k) => k == RiderKind.Vendor ? 2 : 1;

        /// <summary>Relative chance of each kind boarding (by route and time of day).</summary>
        public static float Weight(RiderKind k, EnvironmentKind env, bool night)
        {
            bool city = env == EnvironmentKind.Manila || env == EnvironmentKind.Edsa || env == EnvironmentKind.Cebu;
            bool town = env == EnvironmentKind.Provincial || env == EnvironmentKind.Fiesta || env == EnvironmentKind.Mountain || env == EnvironmentKind.Baguio;
            switch (k)
            {
                case RiderKind.Regular: return 50f;
                case RiderKind.Student: return night ? 3f : 12f;
                case RiderKind.Lola: return night ? 3f : 8f;
                case RiderKind.Chismosa: return 7f;
                case RiderKind.Hurry: return city ? 12f : 6f;
                case RiderKind.Drunk: return night ? 10f : 0f;
                case RiderKind.Dodger: return 5f;
                case RiderKind.Vendor: return town ? 9f : 5f;
                case RiderKind.Balikbayan: return 2f;
                default: return 0f;
            }
        }

        private static readonly RiderKind[] Kinds = (RiderKind[])Enum.GetValues(typeof(RiderKind));

        /// <summary>Picks a kind. <paramref name="room"/> = free seats (a tindera needs two); one balikbayan at a time.</summary>
        public static RiderKind Pick(GameRandom rng, EnvironmentKind env, bool night, int room, bool balikbayanAboard)
        {
            float total = 0;
            foreach (var k in Kinds) if (Allowed(k, room, balikbayanAboard)) total += Weight(k, env, night);
            float x = (float)rng.Next() * total;
            foreach (var k in Kinds)
            {
                if (!Allowed(k, room, balikbayanAboard)) continue;
                x -= Weight(k, env, night);
                if (x < 0) return k;
            }
            return RiderKind.Regular;
        }

        private static bool Allowed(RiderKind k, int room, bool balikbayanAboard) =>
            Seats(k) <= room && !(k == RiderKind.Balikbayan && balikbayanAboard);

        // ------------------------------------------------------------------ lines
        public static readonly string[] Board =
        {
            "", // regular: the usual boarding lines
            "ESTUDYANTE PO, MAY DISCOUNT!",
            "DAHAN-DAHAN LANG, IHO HA?",
            "UY, ALAM MO BA 'YUNG KAPITBAHAY NAMIN?",
            "KUYA, BILISAN MO, LATE NA 'KO!",
            "HIK! SA... SA DULO LANG... HIK!",
            "BAYAD DAW... MAMAYA NA.",
            "KUYA, PASAKAY NG BAYONG KO HA!",
            "GRABE, NAMISS KO ANG JEEP!",
        };

        public static readonly string[][] Ride =
        {
            new string[0],
            new[] { "MAY QUIZ PA KAMI MAMAYA!", "PAKOPYA NG ASSIGNMENT MO!", "PAREHO TAYO NG SCHOOL?", "NAG-ALL NIGHTER AKO, WALA RIN NAMANG NAPALA!",
                    "PROMISSORY NOTE NA NAMAN ANG TUITION!", "KUYA, MAY WIFI BA DITO?", "SI CRUSH, NASA KABILANG JEEP!", "GROUP PROJECT, AKO LANG GUMAWA!" },
            new[] { "NAPAKABILIS NAMAN NIYAN!", "NOONG PANAHON NAMIN...", "SANA KASAMA KO ANG APO KO.", "IHO, KUMAIN KA NA BA?",
                    "NOONG PANAHON KO, PISO LANG ANG PASAHE!", "MAY BAON AKONG TURON, GUSTO MO?", "HUWAG KANG MAGPUPUYAT, IHO!", "SENIOR AKO, MAY DISCOUNT HA!" },
            new[] { "TAPOS ALAM MO BA, SI MARITES...", "ANG ARTE NIYA, PROMISE!", "HUWAG MO NANG IKALAT HA... PERO...", "NAKITA KO SILANG MAGKASAMA!",
                    "BUNTIS DAW SI ANO, SINO ANG AMA?!", "MAY BAGONG KOTSE, PERO MAY UTANG SA'KIN!", "UMUWI SI JUN-JUN GALING ABROAD, MAY BAGONG GF!", "AKIN LANG 'TO HA, SECRET!" },
            new[] { "KUYA, MALAPIT NA BA?", "TATAGA NA AKO NG BOSS KO!", "OVERTAKE MO NA 'YAN!", "NAKA-MUTE AKO SA MEETING, SHH!",
                    "LIMANG MINUTO NA LANG, KUYA!", "SIGE, LIPAD, BAYARAN KO GAS!", "MAY PRESENTATION AKO, WALA PA AKONG SLIDES!" },
            new[] { "PARA! ...AY HINDI PA PALA.", "KUYA, IKAW ANG BEST FRIEND KO!", "MY WAAAY... HIK!", "ISANG BOTE PA, PROMISE HULI NA!",
                    "KUYA, MAHAL KITA... BILANG KAIBIGAN!", "HINDI AKO LASING, NAHIHILO LANG!", "SA'N NA BA BAHAY KO? HIK!" },
            new[] { "...", "SAAN NA BA TAYO?", "MAY BARYA KA BA SA ISANG LIBO?", "SINO NAGBAYAD? AKO? SIGURADO KA?" },
            new[] { "MAY TUYO AKO DITO, GUSTO MO?", "MAHAL NA ANG GULAY NGAYON!", "KUYA, YUNG BAYONG KO, BAKA MAHULOG!",
                    "SUKI KITA, MAY DISKWENTO KA SA'KIN!", "ANG KAMATIS, PARANG GINTO NA ANG PRESYO!", "SINO GUSTONG BUMILI NG KAKANIN?" },
            new[] { "MAS MAINIT PALA DITO KAYSA SA DUBAI!", "PASALUBONG PARA SA LAHAT!", "MISS KO ANG SINIGANG NI NANAY!",
                    "NAMISS KO ANG TRAFFIC... JOKE LANG!", "MAY TOBLERONE AKO PARA SA'YO, KUYA!", "BALIKBAYAN BOX, TATLONG BUWAN BAGO DUMATING!" },
        };

        public static string RideLine(RiderKind k, GameRandom rng)
        {
            var a = Ride[(int)k];
            return a.Length == 0 ? null : a[rng.Int(0, a.Length - 1)];
        }
    }
}
