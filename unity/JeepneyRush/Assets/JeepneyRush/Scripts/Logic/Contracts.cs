using System;

namespace JeepneyRush.Logic
{
    public enum ContractKind { None, Hospital, Pakyaw, Lechon, Wedding }

    /// <summary>
    /// Special passengers ("contracts") offered at some stops, one at a time, a few per run. Each has a
    /// destination <see cref="Distance"/> metres ahead: the contract is done at the first stop you serve
    /// after that.
    ///  • HOSPITAL – a mom in labour: get there before the time runs out
    ///  • PAKYAW – a group charter: don't serve any other stop on the way
    ///  • LECHON – deliver it in one piece: no crash and no fast speed hump
    ///  • WEDDING – the bride can't be late: on time and no crash
    /// Rewards are coins (within the run's server limits) and score.
    /// </summary>
    public sealed class ContractRun
    {
        public const double OfferChance = 0.14;
        public const int MinStopsBeforeFirst = 2, MaxPerRun = 3, ScoreBonus = 500;

        public ContractKind Kind { get; private set; }
        public float TargetDistance { get; private set; }
        public float Deadline { get; private set; }
        public bool Failed { get; private set; }
        public string FailReason { get; private set; }
        public int Completed { get; private set; }
        public int Offered { get; private set; }
        public bool Active => Kind != ContractKind.None;

        public static float Distance(ContractKind k) => k == ContractKind.Hospital ? 500f : k == ContractKind.Pakyaw ? 700f : k == ContractKind.Lechon ? 600f : k == ContractKind.Wedding ? 800f : 0f;
        public static bool Timed(ContractKind k) => k == ContractKind.Hospital || k == ContractKind.Wedding;
        public static float TimeLimit(ContractKind k) => Distance(k) / 11f + 25f;
        public static int Reward(ContractKind k) => k == ContractKind.Hospital ? 45 : k == ContractKind.Pakyaw ? 70 : k == ContractKind.Lechon ? 50 : k == ContractKind.Wedding ? 80 : 0;

        public static string Title(ContractKind k)
        {
            switch (k)
            {
                case ContractKind.Hospital: return "MANGANGANAK NA SI ATE! OSPITAL";
                case ContractKind.Pakyaw: return "PAKYAW! BAWAL HUMINTO SA IBA";
                case ContractKind.Lechon: return "LECHON DELIVERY · INGAT!";
                case ContractKind.Wedding: return "KASAL! BAWAL MA-LATE ANG BRIDE";
                default: return "";
            }
        }

        public static readonly string[][] OfferLines =
        {
            new string[0],
            new[] { "KUYA! MANGANGANAK NA AKO! SA OSPITAL, BILIS!", "AY AY AY! LALABAS NA SI BABY! OSPITAL PO!" },
            new[] { "KUYA, PAKYAW NA NAMIN! DIRETSO NA TAYO!", "BUONG BARKADA KAMI, KUYA! PAKYAW NA!" },
            new[] { "KUYA, INGATAN MO ANG LECHON! PARA SA BINYAG 'YAN!", "BUONG LECHON 'TO, KUYA! WAG MO IPAGPAG!" },
            new[] { "KUYA, KASAL KO NGAYON! BAWAL MA-LATE!", "NAGHIHINTAY NA ANG GROOM, KUYA! BILIS PERO INGAT!" },
        };

        /// <summary>Maybe offer a contract at a stop where riders boarded.</summary>
        public ContractKind Roll(GameRandom rng, int stopsServed, bool night)
        {
            if (Active || Offered >= MaxPerRun || stopsServed < MinStopsBeforeFirst || rng.Next() >= OfferChance) return ContractKind.None;
            double x = rng.Next();
            if (night) return x < 0.4 ? ContractKind.Hospital : x < 0.75 ? ContractKind.Pakyaw : ContractKind.Lechon; // no weddings at night
            return x < 0.3 ? ContractKind.Hospital : x < 0.55 ? ContractKind.Pakyaw : x < 0.78 ? ContractKind.Lechon : ContractKind.Wedding;
        }

        public void Start(ContractKind k, float distance, float clock)
        {
            if (k == ContractKind.None) return;
            Kind = k;
            TargetDistance = distance + Distance(k);
            Deadline = Timed(k) ? clock + TimeLimit(k) : float.PositiveInfinity;
            Failed = false;
            FailReason = null;
            Offered++;
        }

        public void Reset() { Kind = ContractKind.None; Failed = false; Completed = 0; Offered = 0; }

        private void Fail(string why) { if (!Active || Failed) return; Failed = true; FailReason = why; }

        public void OnCrash()
        {
            if (Kind == ContractKind.Lechon) Fail("NABAGSAK ANG LECHON!");
            else if (Kind == ContractKind.Wedding) Fail("NASIRA ANG GOWN NG BRIDE!");
        }

        public void OnRoughHump() { if (Kind == ContractKind.Lechon) Fail("NATAPON ANG LECHON SA HUMP!"); }

        public void Tick(float clock) { if (Active && Timed(Kind) && clock > Deadline) Fail(Kind == ContractKind.Hospital ? "SA JEEP NA NANGANAK SI ATE!" : "LATE ANG BRIDE!"); }

        /// <summary>The player served a stop. Returns the outcome: +1 done (reward), -1 failed, 0 still going.</summary>
        public int OnStopServed(float distance, float clock, out ContractKind kind)
        {
            kind = Kind;
            if (!Active) return 0;
            Tick(clock);
            if (Failed) { Kind = ContractKind.None; return -1; }
            if (distance < TargetDistance)
            {
                if (Kind != ContractKind.Pakyaw) return 0;
                Fail("PAKYAW KAMI, KUYA! BAKIT KA HUMINTO?");
                Kind = ContractKind.None;
                return -1;
            }
            Kind = ContractKind.None;
            Completed++;
            return 1;
        }

        /// <summary>HUD line while a contract is running.</summary>
        public string Hud(float distance, float clock)
        {
            if (!Active) return "";
            if (Failed) return "BIGO: " + FailReason;
            float left = Math.Max(0f, TargetDistance - distance);
            string where = left > 0 ? $"{left:0} m" : "HUMINTO SA SUSUNOD NA PARA!";
            string time = Timed(Kind) ? $" · {Math.Max(0, (int)Math.Ceiling(Deadline - clock))} s" : "";
            return $"{Title(Kind)} · {where}{time}";
        }
    }
}
