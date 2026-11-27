using System;

namespace JeepneyRush.Logic
{
    /// <summary>Extra goal for the third star of a career shift.</summary>
    public enum ShiftGoal { Deliveries, MaxHits, NearMisses, Overtakes, Streak, CleanDistance, Passengers, SmoothHumps }

    /// <summary>One career shift (biyahe): drive from one terminal to the next on a set route.</summary>
    public sealed class CareerShift
    {
        public string Id;
        /// <summary>Chapter = the license you are working towards (index into <see cref="Career.Licenses"/>).</summary>
        public int Chapter;
        public string Title;
        public EnvironmentKind Env;
        public string From, To;
        /// <summary>Metres to the terminal: reaching it ends the shift.</summary>
        public int Length;
        /// <summary>Coins to collect for the operator (the daily "boundary").</summary>
        public int Boundary;
        public ShiftGoal Goal;
        public int Target;
        /// <summary>What Mang Kanor, the operator, says before the shift.</summary>
        public string Intro;
    }

    /// <summary>A driver's license rank, earned with career stars.</summary>
    public sealed class DriverLicense
    {
        public string Name;
        public string Tagline;
        public int StarsNeeded;
        public int RewardCoins;
        public int RewardGems;
    }

    /// <summary>Result of a career shift (stars, rewards, promotions).</summary>
    public sealed class ShiftResult
    {
        public CareerShift Shift;
        public bool Finished, MadeBoundary, GoalMet;
        /// <summary>Fares collected, diesel paid out of them, the boundary handed to the operator, and what the driver keeps.</summary>
        public int Fares, Gas, BoundaryPaid;
        public int TakeHome => Fares - Gas - BoundaryPaid;
        /// <summary>Stars this run earned (bit 1 finish, 2 boundary, 4 goal).</summary>
        public int Mask;
        /// <summary>Stars earned for the first time (bits).</summary>
        public int NewMask;
        public int StarCoins;
        public int NewRank = -1;
        public int RankCoins, RankGems;
        public int Stars => Career.Count(Mask);
    }

    /// <summary>
    /// Career mode (BUHAY TSUPER): from a student permit to Hari ng Kalsada, driving Mang Kanor's
    /// jeepney. Each shift is a biyahe to a terminal on a set route with three stars:
    /// ★ reach the terminal, ★ also collect the boundary in FARES (passenger money, not road coins),
    /// ★ also meet the shift's extra goal. Stars unlock the next shift and, in total, the next
    /// license (each license pays a one-time reward). Every NEW star pays coins.
    /// The server (jr_submit_run) applies exactly these rules from the generated catalog.
    /// </summary>
    public static class Career
    {
        public const int StarFinish = 1, StarBoundary = 2, StarGoal = 4;

        public static readonly DriverLicense[] Licenses =
        {
            new DriverLicense { Name = "STUDENT PERMIT", Tagline = "Mang Kanor lends you his jeepney. Huwag mong gagasgasan!", StarsNeeded = 0 },
            new DriverLicense { Name = "NON-PRO LICENSE", Tagline = "City routes and your first EDSA rush hour.", StarsNeeded = 6, RewardCoins = 500, RewardGems = 10 },
            new DriverLicense { Name = "PROFESSIONAL LICENSE", Tagline = "Long trips: the coast and Cebu.", StarsNeeded = 15, RewardCoins = 1000, RewardGems = 20 },
            new DriverLicense { Name = "OPERATOR – PRANGKISA", Tagline = "Your own franchise. Now the boundary is your income.", StarsNeeded = 25, RewardCoins = 2000, RewardGems = 30 },
            new DriverLicense { Name = "HARI NG KALSADA", Tagline = "The hardest roads in the country.", StarsNeeded = 36, RewardCoins = 4000, RewardGems = 50 },
        };

        public static readonly CareerShift[] Shifts =
        {
            // ---- Student permit
            S("c1s1", 0, "Unang Biyahe", EnvironmentKind.Manila, "QUIAPO", "CUBAO", 800, 25, ShiftGoal.Deliveries, 3,
              "O, ito ang susi. Quiapo hanggang Cubao lang. Ingatan mo ang jeep ko, ha?"),
            S("c1s2", 0, "Pasada sa Divisoria", EnvironmentKind.Manila, "DIVISORIA", "RECTO", 1000, 35, ShiftGoal.MaxHits, 1,
              "Masikip sa Divisoria. Dahan-dahan lang, wag kang bubunggo."),
            S("c1s3", 0, "Probinsya Run", EnvironmentKind.Provincial, "BAYAN", "PALENGKE", 1200, 40, ShiftGoal.Passengers, 10,
              "Sa probinsya, lahat kumakaway. Isakay mo silang lahat!"),
            S("c1s4", 0, "Tricycle Territory", EnvironmentKind.Provincial, "MUNISIPYO", "SIMBAHAN", 1400, 55, ShiftGoal.NearMisses, 5,
              "Ang daming tricycle diyan. Dikit-dikit pero wag tatama!"),
            // ---- Non-pro license
            S("c2s1", 1, "EDSA Baptism", EnvironmentKind.Edsa, "MONUMENTO", "BALINTAWAK", 1300, 55, ShiftGoal.MaxHits, 2,
              "EDSA na 'to, iho. Bus, kotse, motor... Kalma lang."),
            S("c2s2", 1, "Fiesta sa Bayan", EnvironmentKind.Fiesta, "PLAZUELA", "SIMBAHAN", 1500, 65, ShiftGoal.Deliveries, 8,
              "Pista ngayon! Maraming pasahero, maraming barya."),
            S("c2s3", 1, "Rush Hour", EnvironmentKind.Edsa, "CUBAO", "ORTIGAS", 1700, 80, ShiftGoal.Overtakes, 10,
              "Alas-singko na. Lahat nagmamadali. Ikaw rin!"),
            S("c2s4", 1, "Pista Express", EnvironmentKind.Fiesta, "PALENGKE", "PLAZUELA", 1900, 90, ShiftGoal.Streak, 4,
              "Sunod-sunod na baba, sunod-sunod na bayad. Galingan mo!"),
            // ---- Professional license
            S("c3s1", 2, "Beach Highway", EnvironmentKind.Beach, "RESORT", "PIER", 2000, 95, ShiftGoal.CleanDistance, 1200,
              "Maganda ang tanawin, pero sa daan ang mata mo."),
            S("c3s2", 2, "Colon to IT Park", EnvironmentKind.Cebu, "COLON", "IT PARK", 2100, 110, ShiftGoal.Deliveries, 12,
              "Cebu na tayo, 'Day! Maraming estudyante at call center agent."),
            S("c3s3", 2, "Sunset Biyahe", EnvironmentKind.Beach, "PIER", "WHITE BEACH", 2400, 120, ShiftGoal.NearMisses, 15,
              "Pagabi na. Bilisan pero wag pabaya."),
            S("c3s4", 2, "Sugbo Hustle", EnvironmentKind.Cebu, "FUENTE OSMENA", "CARBON", 2600, 140, ShiftGoal.MaxHits, 1,
              "Malinis na biyahe, buong boundary. Kaya mo 'yan."),
            // ---- Operator (prangkisa)
            S("c4s1", 3, "Rice Terraces", EnvironmentKind.Mountain, "BONTOC", "SAGADA", 2600, 130, ShiftGoal.SmoothHumps, 6,
              "May sarili ka nang prangkisa! Pero mabato dito, dahan-dahan sa lubak."),
            S("c4s2", 3, "Prangkisa Day", EnvironmentKind.Edsa, "QUEZON AVE", "GUADALUPE", 2800, 155, ShiftGoal.Deliveries, 16,
              "Unang araw ng sarili mong ruta sa EDSA. Patunayan mo!"),
            S("c4s3", 3, "Mud & Pine", EnvironmentKind.Mountain, "SAGADA", "BAUKO", 3000, 160, ShiftGoal.MaxHits, 1,
              "Maputik at madulas. Isang gasgas lang ang pwede."),
            S("c4s4", 3, "Boundary King", EnvironmentKind.Manila, "BACLARAN", "MONUMENTO", 3200, 190, ShiftGoal.Streak, 5,
              "Buong Maynila, dulo hanggang dulo. Hari ng boundary ang uuwi."),
            // ---- Hari ng Kalsada
            S("c5s1", 4, "Session Road", EnvironmentKind.Baguio, "BURNHAM PARK", "SESSION RD", 3000, 180, ShiftGoal.CleanDistance, 2000,
              "Malamig sa Baguio, pero mainit ang kalsada. Matarik at paliko-liko."),
            S("c5s2", 4, "Hamog sa Marcos Highway", EnvironmentKind.Baguio, "MARCOS HWY", "MINES VIEW", 3300, 200, ShiftGoal.MaxHits, 0,
              "Makapal ang hamog. Walang tatama. Wala."),
            S("c5s3", 4, "EDSA Legend", EnvironmentKind.Edsa, "BALINTAWAK", "TAFT AVE", 3600, 230, ShiftGoal.Overtakes, 30,
              "Ang buong EDSA. Ipakita mo kung sino ang hari."),
            S("c5s4", 4, "Hari ng Kalsada", EnvironmentKind.Fiesta, "SIMBAHAN", "PLAZUELA", 4000, 250, ShiftGoal.Deliveries, 25,
              "Huling biyahe. Buong bayan nanonood. Arangkada, Hari!"),
        };

        private static CareerShift S(string id, int ch, string title, EnvironmentKind env, string from, string to, int len, int boundary, ShiftGoal goal, int target, string intro) =>
            new CareerShift { Id = id, Chapter = ch, Title = title, Env = env, From = from, To = to, Length = len, Boundary = boundary, Goal = goal, Target = target, Intro = intro };

        public static int MaxStars => Shifts.Length * 3;

        public static int IndexOf(string id)
        {
            if (string.IsNullOrEmpty(id)) return -1;
            for (int i = 0; i < Shifts.Length; i++) if (Shifts[i].Id == id) return i;
            return -1;
        }

        public static CareerShift Get(string id) { int i = IndexOf(id); return i < 0 ? null : Shifts[i]; }

        /// <summary>Coins paid for each star earned for the first time (more in later chapters).</summary>
        public static int StarReward(int chapter) => 40 + 30 * chapter;

        public static int Count(int mask) => (mask & 1) + ((mask >> 1) & 1) + ((mask >> 2) & 1);

        public static int TotalStars(int[] masks)
        {
            int n = 0;
            if (masks != null) foreach (int m in masks) n += Count(m);
            return n;
        }

        /// <summary>Highest license reached with this many stars (0 = student permit).</summary>
        public static int RankFor(int stars)
        {
            int r = 0;
            for (int i = 0; i < Licenses.Length; i++) if (stars >= Licenses[i].StarsNeeded) r = i;
            return r;
        }

        /// <summary>A shift can be played when its chapter's license is reached and the shift before it has a star.</summary>
        public static bool IsUnlocked(int index, int[] masks)
        {
            if (index < 0 || index >= Shifts.Length) return false;
            var s = Shifts[index];
            if (RankFor(TotalStars(masks)) < s.Chapter) return false;
            if (index == 0 || Shifts[index - 1].Chapter != s.Chapter) return true;
            return Mask(masks, index - 1) != 0;
        }

        public static int Mask(int[] masks, int index) => masks != null && index >= 0 && index < masks.Length ? masks[index] & 7 : 0;

        public static string GoalText(ShiftGoal g, int target)
        {
            switch (g)
            {
                case ShiftGoal.Deliveries: return $"Deliver {target} passengers";
                case ShiftGoal.MaxHits: return target == 0 ? "Don't hit anything" : $"Hit at most {target} {(target == 1 ? "thing" : "things")}";
                case ShiftGoal.NearMisses: return $"{target} near misses";
                case ShiftGoal.Overtakes: return $"Overtake {target} vehicles";
                case ShiftGoal.Streak: return $"Reach a x{target} delivery streak";
                case ShiftGoal.CleanDistance: return $"Drive {target:N0} m without crashing";
                case ShiftGoal.Passengers: return $"Pick up {target} passengers";
                default: return $"Cross {target} speed humps slowly";
            }
        }

        public static int GoalValue(ShiftGoal g, RunSummary r)
        {
            switch (g)
            {
                case ShiftGoal.Deliveries: return r.Deliveries;
                case ShiftGoal.MaxHits: return r.Hits;
                case ShiftGoal.NearMisses: return r.NearMisses;
                case ShiftGoal.Overtakes: return r.Overtakes;
                case ShiftGoal.Streak: return r.BestStreak;
                case ShiftGoal.CleanDistance: return (int)r.CleanDistance;
                case ShiftGoal.Passengers: return r.Passengers;
                default: return r.SmoothHumps;
            }
        }

        public static bool GoalMet(ShiftGoal g, int target, int value) => g == ShiftGoal.MaxHits ? value <= target : value >= target;

        /// <summary>Stars a run earned on a shift. Nothing counts without reaching the terminal.</summary>
        public static int Evaluate(CareerShift s, RunSummary r)
        {
            if (s == null || r.Environment != s.Env || r.Distance < s.Length - 0.5f) return 0;
            int m = StarFinish;
            if (r.Fares >= s.Boundary) m |= StarBoundary;
            if (GoalMet(s.Goal, s.Target, GoalValue(s.Goal, r))) m |= StarGoal;
            return m;
        }

        /// <summary>
        /// What the operator gets: the boundary, out of the fares left after the diesel (never more
        /// than that, never more than the run's coins – the driver doesn't go into debt).
        /// </summary>
        public static int BoundaryPaid(int boundary, int fares, int gas, int coins) =>
            Math.Max(0, Math.Min(Math.Min(boundary, fares - gas), coins));

        /// <summary>Records a career run in the save and pays new stars / a new license. Null for free-ride runs.</summary>
        public static ShiftResult Apply(SaveData d, RunSummary r)
        {
            int i = IndexOf(r.Career);
            if (i < 0) return null;
            var s = Shifts[i];
            if (d.careerMasks == null || d.careerMasks.Length != Shifts.Length)
            {
                var m = new int[Shifts.Length];
                if (d.careerMasks != null) Array.Copy(d.careerMasks, m, Math.Min(m.Length, d.careerMasks.Length));
                d.careerMasks = m;
            }
            var res = new ShiftResult { Shift = s };
            // a locked shift (old save, edited save) records nothing
            int mask = IsUnlocked(i, d.careerMasks) ? Evaluate(s, r) : 0;
            res.Mask = mask;
            res.Finished = (mask & StarFinish) != 0;
            res.MadeBoundary = (mask & StarBoundary) != 0;
            res.GoalMet = (mask & StarGoal) != 0;
            // the operator takes the boundary out of the fares (all of them if they fall short);
            // road coins are never touched
            if (IsUnlocked(i, d.careerMasks))
            {
                res.Fares = Math.Max(0, r.Fares);
                res.Gas = Math.Max(0, r.Gas);
                res.BoundaryPaid = BoundaryPaid(s.Boundary, res.Fares, res.Gas, r.Coins);
                d.coins = Math.Max(0, d.coins - res.BoundaryPaid);
            }
            res.NewMask = mask & ~d.careerMasks[i];
            d.careerMasks[i] |= mask;
            res.StarCoins = Count(res.NewMask) * StarReward(s.Chapter);
            d.coins += res.StarCoins;
            int rank = RankFor(TotalStars(d.careerMasks));
            if (rank > d.careerRank)
            {
                for (int k = d.careerRank + 1; k <= rank; k++) { res.RankCoins += Licenses[k].RewardCoins; res.RankGems += Licenses[k].RewardGems; }
                res.NewRank = rank;
                d.careerRank = rank;
                d.coins += res.RankCoins;
                d.gems += res.RankGems;
            }
            return res;
        }
    }
}
