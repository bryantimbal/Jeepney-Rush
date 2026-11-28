using System;

namespace JeepneyRush.Logic
{
    /// <summary>
    /// The jeepney radio: station 0 is the route's own music (or the Patok sound trip), then three
    /// original stations. While a station plays, the DJ's lines (jokes, shout-outs and traffic reports
    /// made from the route, time and weather) scroll on the HUD every <see cref="TalkEvery"/> seconds.
    /// All music is original; the DJ speaks in text (no recorded voices).
    /// </summary>
    public static class Radio
    {
        public const int Stations = 4;
        public const float TalkEvery = 24f;

        public static readonly string[] Names = { "MUSIKA NG RUTA", "DZJR 99.1 · OPM", "JEEP FM 101.5", "RADYO PASADA 1020 AM" };

        public static int Next(int station) => (station + 1) % Stations;

        private static readonly string[] Opm =
        {
            "Para sa lahat ng tsuper diyan sa kalsada, ito ang kanta para sa inyo!",
            "Request mula kay Aling Nena ng Tondo: \"Para sa asawa kong driver, ingat lagi!\"",
            "Kung may hugot ka ngayong gabi, nasa tamang istasyon ka. Pakinggan natin.",
            "Shout-out sa mga estudyante: kaya n'yo 'yan, malapit na ang Christmas break!",
            "Kung mahal mo siya, ihatid mo... pero bayad muna sa pamasahe!",
        };
        private static readonly string[] Jams =
        {
            "JEEP FM! Ang istasyon na umaandar kahit traffic!",
            "Taas ang kamay ng mga naka-sabit diyan! Ingat lang, ha!",
            "Bagong tugtog, bagong biyahe! Lakasan ang volume, kuya!",
            "Ang tunay na budol? Yung \"isang sakay na lang\" pero apat pa ang sumakay!",
            "Paalala: ang busina ay para sa pag-iingat, hindi para sa pagkanta. Pero sige, isa pa!",
        };
        private static readonly string[] Jokes =
        {
            "Bakit malungkot ang jeep? Kasi lagi siyang iniiwan sa terminal.",
            "Anong sabi ng gulong sa kalsada? \"Ikaw ang daan ko.\"",
            "Kuya, bayad po! ... Saan po ito? ... Sa puso mo po.",
            "Bakit hindi nagre-retire ang mga tsuper? Kasi hindi sila marunong huminto... maliban sa PARA!",
        };

        /// <summary>A DJ line for the station (traffic reports on the AM station use the route, time and weather).</summary>
        public static string Line(int station, EnvironmentKind env, float hour, bool raining, float jam, GameRandom rng)
        {
            switch (station)
            {
                case 1: return Lines.Pick(Opm, rng);
                case 2: return rng.Next() < 0.35 ? Lines.Pick(Jokes, rng) : Lines.Pick(Jams, rng);
                case 3: return rng.Next() < 0.6 ? Traffic(env, hour, raining, jam, rng) : Lines.Pick(Jokes, rng);
                default: return null;
            }
        }

        public static string Traffic(EnvironmentKind env, float hour, bool raining, float jam, GameRandom rng)
        {
            string place = env == EnvironmentKind.Edsa ? (rng.Next() < 0.5 ? "EDSA southbound sa Guadalupe" : "EDSA northbound sa Cubao")
                : env == EnvironmentKind.Manila ? "Taft Avenue at España" : env == EnvironmentKind.Cebu ? "Colon at Osmeña Boulevard"
                : env == EnvironmentKind.Baguio ? "Session Road at Marcos Highway" : env == EnvironmentKind.Mountain ? "Halsema Highway"
                : env == EnvironmentKind.Beach ? "coastal road" : env == EnvironmentKind.Fiesta ? "plaza dahil sa prusisyon" : "national highway";
            string flow = jam > 0.5f ? "mabigat at halos hindi gumagalaw" : jam > 0.1f ? "mabagal" : (hour >= 7 && hour <= 9) || (hour >= 17 && hour <= 19) ? "medyo mabigat, rush hour na" : "maluwag";
            string weather = raining ? " May ulan, kaya dahan-dahan at buksan ang ilaw." : hour >= 18 || hour < 5 ? " Gabi na, ingat sa mga tumatawid." : " Maaraw, mainit, kaya uminom ng tubig, mga tsuper!";
            return $"TRAFFIC UPDATE: ang daloy sa {place} ay {flow}.{weather}";
        }
    }
}
