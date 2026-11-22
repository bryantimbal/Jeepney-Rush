using System;

namespace JeepneyRush.Logic
{
    /// <summary>
    /// Coin combo: coins collected in quick succession build a combo. Every
    /// <see cref="MilestoneEvery"/> coins in one combo pays a "COIN RUSH" bonus that grows
    /// with the combo, which makes chasing coin trails feel rewarding.
    /// </summary>
    public sealed class CoinCombo
    {
        public const float Window = 0.6f;     // seconds between coins to keep the combo
        public const int MilestoneEvery = 10;

        private float _sinceLast = float.MaxValue;
        public int Count { get; private set; }
        public int Best { get; private set; }

        /// <summary>Registers a coin. Returns bonus coins if a milestone was reached (else 0).</summary>
        public int Add()
        {
            Count = _sinceLast <= Window ? Count + 1 : 1;
            _sinceLast = 0;
            Best = Math.Max(Best, Count);
            return Count % MilestoneEvery == 0 ? MilestoneBonus(Count) : 0;
        }

        public static int MilestoneBonus(int combo) => 5 * (combo / MilestoneEvery);

        public void Tick(float dt)
        {
            if (_sinceLast < float.MaxValue) _sinceLast += dt;
            if (_sinceLast > Window) Count = 0;
        }

        public void Reset()
        {
            Count = 0;
            Best = 0;
            _sinceLast = float.MaxValue;
        }

        /// <summary>Pitch for the coin sound: rises a semitone per combo step, capped.</summary>
        public float Pitch => (float)Math.Pow(2, Math.Min(Count - 1, 12) / 12.0);
    }

    /// <summary>
    /// MMDA checkpoint: after reckless driving (a crash) a traffic enforcer sets up ahead.
    /// Slow down (hold BRAKE) when passing to be waved through with a small bonus;
    /// speed past and you get a ticket (coins deducted from the run).
    /// </summary>
    /// <summary>Who runs the checkpoints: the MMDA only in Metro Manila (Manila streets, EDSA).</summary>
    public enum EnforcerKind { Mmda, Police, CityTraffic }

    public static class Enforcers
    {
        public static EnforcerKind For(EnvironmentKind env) =>
            env == EnvironmentKind.Manila || env == EnvironmentKind.Edsa ? EnforcerKind.Mmda
            : env == EnvironmentKind.Cebu || env == EnvironmentKind.Baguio ? EnforcerKind.CityTraffic
            : EnforcerKind.Police; // provincial, beach and mountain roads: police checkpoints

        public static string Checkpoint(EnforcerKind k) => k == EnforcerKind.Mmda ? "MMDA CHECKPOINT" : k == EnforcerKind.Police ? "POLICE CHECKPOINT" : "TRAFFIC CHECKPOINT";
        public static string SignTexture(EnforcerKind k) => k == EnforcerKind.Mmda ? "mmda_sign" : k == EnforcerKind.Police ? "police_sign" : "enforcer_sign";
    }

    public static class EnforcerRules
    {
        /// <summary>Passing speed must be at or below this fraction of the current base speed.</summary>
        public const float SlowFraction = 0.62f;
        public const float SpawnAhead = 140f;
        public const float Cooldown = 25f;      // seconds between checkpoints
        public const int PoliteBonusScore = 150;

        public static bool PassedSafely(float passingSpeed, float baseSpeed) => passingSpeed <= baseSpeed * SlowFraction + 0.01f;

        /// <summary>Fine: 15% of the coins earned this run, at least 10, never more than you have.</summary>
        public static int Fine(int runCoins) => Math.Min(runCoins, Math.Max(10, (int)Math.Round(runCoins * 0.15)));
    }

    /// <summary>Lines spoken by passengers and enforcers (Filipino / Taglish flavour).</summary>
    /// <summary>Stop barkers: who has one and how often they call.</summary>
    public static class Barkers
    {
        /// <summary>Busy city stops nearly always have a barker; quiet mountain stops rarely.</summary>
        public static float Chance(EnvironmentKind env) =>
            env == EnvironmentKind.Mountain ? 0.3f : env == EnvironmentKind.Provincial || env == EnvironmentKind.Beach ? 0.5f : 0.8f;
        /// <summary>Starts calling when the stop is this close ahead (m).</summary>
        public const float CallRange = 55f;
        public const float CallEvery = 2.4f;
        public const int MaxCalls = 3;
        /// <summary>Delay after boarding before the slap on the side and the send-off.</summary>
        public const float SendOffDelay = 0.7f;
    }

    public static class Lines
    {
        public static readonly string[] Bump = { "ARAY KO!", "DAHAN-DAHAN PO!", "KUYA, INGAT!", "ANO BA 'YAN?!", "NAKU PO!", "SAN KA NATUTONG MAGMANEHO?!" };
        public static readonly string[] Leave = { "BABABA NA AKO!", "LALAKAD NA LANG AKO!", "IBANG JEEP NA LANG!" };
        public static readonly string[] Landing = { "WHOA!", "SUSMARYOSEP!", "PARANG RIDE!" };
        public static readonly string[] Hump = { "ARAY! ANG HUMP!", "KUYA, MAY HUMP!", "TUMALBOG AKO!", "ANG SAKIT NG PWET KO!" };
        public static readonly string[] Smooth = { "AYOS, KUYA!", "SMOOTH!", "GALING MAG-DRIVE!" };
        public static readonly string[] Flood = { "BAHA! BAHA!", "BASA PAA KO!", "LUSONG NA, KUYA!", "LUBOG NA TAYO!" };
        public static readonly string[] Board = { "BAYAD PO!", "PAKIABOT PO!", "SAKAY PO!" };

        /// <summary>Boarding line; "ISANG ___ PO!" always names a stop on the current route (no Quiapo on the beach).</summary>
        public static string BoardFor(EnvironmentKind env, GameRandom rng)
        {
            if (rng.Chance(0.5)) return Pick(Board, rng);
            var names = StopNames.ForEnvironment(env);
            return $"ISANG {StopNames.All[names[rng.Int(0, names.Count - 1)]]} PO!";
        }
        public static readonly string[] Alight = { "PARA PO!", "SALAMAT PO!", "DITO NA LANG PO!" };
        public static readonly string[] Ticket = { "HULI KA, BOSS!", "LISENSYA, KUYA!", "OVERSPEEDING YAN!" };
        public static readonly string[] Polite = { "INGAT PO!", "SALAMAT SA PAGSUNOD!", "GOOD JOB, DRIVER!" };

        // stuck in traffic -------------------------------------------------------------
        public static readonly string[] LateForWork = { "MALE-LATE NA AKO SA TRABAHO!", "KUYA, MAY MEETING AKO!", "PAPAGALITAN AKO NG BOSS KO!", "LATE NA NAMAN AKO!", "MAKAKALTASAN NA NAMAN AKO!", "MAY EXAM PA AKO, KUYA!" };
        public static readonly string[] GoingHome = { "GUSTO KO NANG UMUWI!", "GUTOM NA AKO, KUYA!", "NAGHIHINTAY ANG MGA ANAK KO!", "ANONG ORAS NA KAMI MAKAKAUWI?", "PAGOD NA AKO SA TRABAHO!", "LUMALAMIG NA ANG ULAM SA BAHAY!" };
        public static readonly string[] Traffic = { "GRABE ANG TRAFFIC!", "WALANG GALAWAN!", "PARKING LOT NA 'TO!", "KUYA, SINGIT KA DOON!", "LALAKARIN KO NA LANG KAYA?", "BUONG ARAW NA TAYO DITO!" };
        public static readonly string[] Edsa = { "GRABE ANG EDSA!", "EDSA NA NAMAN!", "SANA NAG-MRT NA LANG AKO!", "ISANG ORAS NA TAYO SA CUBAO!" };
        public static readonly string[] JamClear = { "SA WAKAS!", "LUMUWAG NA!", "BILIS NA, KUYA!", "SALAMAT, UMANDAR NA!" };

        /// <summary>A commuter's complaint while stuck: late for work in the morning, tired and hungry going home at night.</summary>
        public static string Stuck(float hour, EnvironmentKind env, GameRandom rng)
        {
            double r = rng.Next();
            if (env == EnvironmentKind.Edsa && r < 0.22) return Pick(Edsa, rng);
            if (r < 0.62) return Pick(DayNight.GoingToWork(hour) ? LateForWork : GoingHome, rng);
            return Pick(Traffic, rng);
        }

        // the driver at the gas station
        public static readonly string[] GasPrice = { "ANG MAHAL NG GAS!", "TUMAAS NA NAMAN ANG DIESEL!", "WALA NANG KITA SA BOUNDARY!", "KAILAN BA ANG ROLLBACK?!", "PUHUNAN LANG 'TO, WALANG TUBO!", "PANG-ULAM KO NA SANA 'YAN!" };

        // Patok jeepney: the music is too loud to hear "PARA!", so riders pull the cord to ring the bell
        public static readonly string[] PullCord = { "*HILA* DING-DING!", "*HILA* BABABA PO!", "DING! DITO LANG PO!" };

        // riders after 3 skipped stops in a row
        public static readonly string[] Panic = { "KUYA, LAMPAS NA AKO!", "SAAN MO KAMI DADALHIN?!", "PARA! PARA! PARA!", "BABABA NA KAMI!", "TULONG! AYAW HUMINTO!", "HINDI KA BA MARUNONG HUMINTO?!" };
        public static readonly string[] JumpOff = { "BABABA NA KAMI DITO!", "DI NA AKO MAGBABAYAD!", "BAHALA KA SA BUHAY MO!" };
        public static readonly string[] Police = { "PULL OVER! TABI!", "HOY! HUMINTO KA!", "LISENSYA MO, BOSS!" };

        // ---- jeepney barkers ("barker" / "dispatcher") calling riders at the stops
        public static readonly string[] BarkerFill = { "ISA NA LANG, LARGA NA!", "KASYA PA SA KANAN!", "DALAWA PA SA KALIWA!", "USOG-USOG PO, KASYA PA!", "SAKAY NA, ALIS NA!", "MAY BAKANTE PA, SAKAY NA!" };
        public static readonly string[] BarkerThanks = { "SIGE, LARGA!", "O, ANDAR NA!", "SALAMAT, BOSS!", "PANG-KAPE LANG, BOSS!", "INGAT, PARE!" };
        public static readonly string[] BarkerFull = { "PUNO NA 'YAN! SUSUNOD NA LANG!", "WALA NANG UPUAN, NEXT JEEP!", "PUNO NA, BOSS!" };
        public static readonly string[] BarkerSkipped = { "HOY! MAY SASAKAY DITO!", "BOSS! BAKIT DI KA HUMINTO?!", "SAYANG! MAY PASAHERO DITO!" };

        /// <summary>
        /// What a barker shouts while the jeepney approaches: a destination further down this
        /// route said twice ("CUBAO! CUBAO!"), or a hurry-up line about the seats left.
        /// </summary>
        public static string BarkerCall(EnvironmentKind env, string here, int seatsLeft, GameRandom rng)
        {
            if (seatsLeft <= 0) return Pick(BarkerFull, rng);
            if (seatsLeft == 1 && rng.Chance(0.5)) return "ISA NA LANG! ISA NA LANG!";
            if (rng.Chance(0.35)) return Pick(BarkerFill, rng);
            var names = StopNames.ForEnvironment(env);
            string dest = StopNames.All[names[rng.Int(0, names.Count - 1)]];
            for (int i = 0; i < 4 && dest == here; i++) dest = StopNames.All[names[rng.Int(0, names.Count - 1)]];
            switch (rng.Int(0, 3))
            {
                case 0: return $"{dest}! {dest}! SAKAY NA!";
                case 1: return $"O, {dest}! {dest}!";
                case 2: return $"{dest}, LARGA NA!";
                default: return $"{dest}! MAY BAKANTE PA!";
            }
        }

        public static string Pick(string[] lines, GameRandom rng) => lines[rng.Int(0, lines.Length - 1)];
    }
}
