using System;

namespace JeepneyRush.Logic
{
    public enum PowerUpKind { Magnet, Shield, Turbo, DoubleCoins, PassengerBonus, RepairKit }

    /// <summary>
    /// Timers for active power-ups. Picking up one that is already active refreshes it
    /// (no stacking). The Shield absorbs one collision or expires; Repair Kit is instant.
    /// Durations scale with the Power-up Duration upgrade.
    /// </summary>
    public sealed class PowerUpState
    {
        public const int KindCount = 6;
        private readonly float[] _remaining = new float[KindCount];
        private readonly float[] _duration = new float[KindCount];
        private readonly BalanceConfig _cfg;
        public int UsedThisRun { get; private set; }

        public PowerUpState(BalanceConfig cfg) { _cfg = cfg; }

        public static string DisplayName(PowerUpKind k)
        {
            switch (k)
            {
                case PowerUpKind.Magnet: return "Coin Magnet";
                case PowerUpKind.Shield: return "Shield";
                case PowerUpKind.Turbo: return "Turbo Boost";
                case PowerUpKind.DoubleCoins: return "Double Coins";
                case PowerUpKind.PassengerBonus: return "Passenger Bonus";
                default: return "Repair Kit";
            }
        }

        public float BaseDuration(PowerUpKind k)
        {
            switch (k)
            {
                case PowerUpKind.Magnet: return _cfg.magnetDuration;
                case PowerUpKind.Shield: return _cfg.shieldDuration;
                case PowerUpKind.Turbo: return _cfg.turboDuration;
                case PowerUpKind.DoubleCoins: return _cfg.doubleCoinsDuration;
                case PowerUpKind.PassengerBonus: return _cfg.passengerBonusDuration;
                default: return 0f;
            }
        }

        public void Reset()
        {
            Array.Clear(_remaining, 0, KindCount);
            Array.Clear(_duration, 0, KindCount);
            UsedThisRun = 0;
        }

        /// <param name="durationScale">1 + power-up duration upgrade bonus.</param>
        public void Activate(PowerUpKind k, float durationScale = 1f)
        {
            UsedThisRun++;
            if (k == PowerUpKind.RepairKit) return; // instant, handled by the game
            float d = BaseDuration(k) * Math.Max(0.1f, durationScale);
            _remaining[(int)k] = d;
            _duration[(int)k] = d;
        }

        public bool IsActive(PowerUpKind k) => _remaining[(int)k] > 0;
        /// <summary>Ends a power-up early (Turbo switches off before a stop or gas station).</summary>
        public void End(PowerUpKind k) => _remaining[(int)k] = 0;
        public float Remaining(PowerUpKind k) => _remaining[(int)k];
        /// <summary>0..1 fraction left (for the HUD timer bars).</summary>
        public float Fraction(PowerUpKind k) => _duration[(int)k] > 0 ? Math.Max(0, _remaining[(int)k] / _duration[(int)k]) : 0;

        /// <summary>Uses up the shield. Returns true if a hit was blocked.</summary>
        public bool ConsumeShield()
        {
            if (!IsActive(PowerUpKind.Shield)) return false;
            _remaining[(int)PowerUpKind.Shield] = 0;
            return true;
        }

        public void Tick(float dt)
        {
            for (int i = 0; i < KindCount; i++) if (_remaining[i] > 0) _remaining[i] = Math.Max(0, _remaining[i] - dt);
        }

        public float SpeedMultiplier => IsActive(PowerUpKind.Turbo) ? _cfg.turboSpeedMultiplier : 1f;
        public int CoinMultiplier => IsActive(PowerUpKind.DoubleCoins) ? 2 : 1;
        public float FareMultiplier => IsActive(PowerUpKind.PassengerBonus) ? _cfg.passengerBonusMultiplier : 1f;
        /// <summary>Turbo makes the jeepney smash through traffic unharmed.</summary>
        public bool Invulnerable => IsActive(PowerUpKind.Turbo);

        /// <summary>Weighted random kind for a spawned pickup.</summary>
        public static PowerUpKind RandomKind(GameRandom rng) => rng.Weighted(new (PowerUpKind, float)[]
        {
            (PowerUpKind.Magnet, 2.2f), (PowerUpKind.Shield, 1.6f), (PowerUpKind.Turbo, 1.2f),
            (PowerUpKind.DoubleCoins, 1.6f), (PowerUpKind.PassengerBonus, 1.2f), (PowerUpKind.RepairKit, 1.0f),
        });
    }
}
