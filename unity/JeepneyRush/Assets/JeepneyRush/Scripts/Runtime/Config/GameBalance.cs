using JeepneyRush.Logic;
using UnityEngine;

namespace JeepneyRush
{
    /// <summary>
    /// Designer-facing balance asset (player, speed curve, spawning, passengers, rewards).
    /// The values live in a plain <see cref="BalanceConfig"/> so the same numbers drive the
    /// unit-tested logic. Create via Assets ▸ Create ▸ Jeepney Rush ▸ Game Balance, or run
    /// Jeepney Rush ▸ Setup Project which creates Resources/GameBalance.asset.
    /// </summary>
    [CreateAssetMenu(menuName = "Jeepney Rush/Game Balance", fileName = "GameBalance")]
    public sealed class GameBalance : ScriptableObject
    {
        public BalanceConfig config = new BalanceConfig();

        private static GameBalance _active;

        /// <summary>The balance asset from Resources, or built-in defaults if none exists.</summary>
        public static BalanceConfig Active
        {
            get
            {
                if (_active == null)
                {
                    _active = Resources.Load<GameBalance>("GameBalance");
                    if (_active == null)
                    {
                        _active = CreateInstance<GameBalance>();
                        _active.name = "GameBalance (defaults)";
                    }
                }
                return _active.config;
            }
        }

        private void OnValidate()
        {
            var c = config;
            c.laneSpacing = Mathf.Max(1.5f, c.laneSpacing);
            c.laneChangeDuration = Mathf.Clamp(c.laneChangeDuration, 0.05f, 1f);
            c.jumpDuration = Mathf.Clamp(c.jumpDuration, 0.2f, 2f);
            c.jumpHeight = Mathf.Clamp(c.jumpHeight, 0.3f, 5f);
            c.startSpeed = Mathf.Max(1f, c.startSpeed);
            c.maxSpeed = Mathf.Max(c.startSpeed, c.maxSpeed);
            c.startHealth = Mathf.Max(1, c.startHealth);
            c.passengerCapacity = Mathf.Clamp(c.passengerCapacity, 1, 15);
        }
    }
}
