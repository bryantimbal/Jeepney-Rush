using JeepneyRush.Logic;
using UnityEngine;

namespace JeepneyRush
{
    /// <summary>
    /// Trigger-based collisions for the jeepney: hazards cost health (with temporary
    /// invulnerability and one hit per object), coins and PARA zones are collected.
    /// </summary>
    public sealed class CollisionHandler : MonoBehaviour
    {
        private BalanceConfig _cfg;
        public int Health { get; private set; }
        public int MaxHealth { get; private set; }
        public float InvulnerableTime { get; private set; }
        public bool Invulnerable => InvulnerableTime > 0;
        public bool Dead => Health <= 0;

        /// <summary>Set by the GameplaySession.</summary>
        public System.Action<Hazard> HazardHit;
        public System.Action<Coin> CoinTouched;
        public System.Action<PassengerStopView> ZoneEntered;
        public System.Action<PowerUpPickup> PowerUpTouched;

        /// <summary>Repair Kit: restore one health point. Returns false at full health.</summary>
        public bool Heal()
        {
            if (Health >= MaxHealth) return false;
            Health++;
            return true;
        }

        public void Init(BalanceConfig cfg)
        {
            _cfg = cfg;
            ResetRun();
        }

        public void ResetRun()
        {
            MaxHealth = Health = _cfg.startHealth;
            InvulnerableTime = 0;
        }

        public void Tick(float dt)
        {
            if (InvulnerableTime > 0) InvulnerableTime -= dt;
        }

        /// <summary>Applies damage once. Returns false if ignored (invulnerable / already hit).</summary>
        public bool TryDamage()
        {
            if (Invulnerable || Dead) return false;
            Health--;
            InvulnerableTime = _cfg.invulnerabilityTime;
            return true;
        }

        private void OnTriggerEnter(Collider other)
        {
            var hazard = other.GetComponent<Hazard>();
            if (hazard != null)
            {
                if (!hazard.Hit) HazardHit?.Invoke(hazard);
                return;
            }
            var coin = other.GetComponent<Coin>();
            if (coin != null) { CoinTouched?.Invoke(coin); return; }
            var pu = other.GetComponent<PowerUpPickup>();
            if (pu != null) { PowerUpTouched?.Invoke(pu); return; }
            var zone = other.GetComponent<StopZoneTrigger>();
            if (zone != null && zone.View != null) ZoneEntered?.Invoke(zone.View);
        }
    }
}
