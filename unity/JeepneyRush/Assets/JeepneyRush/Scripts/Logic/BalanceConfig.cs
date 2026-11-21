using System;

namespace JeepneyRush.Logic
{
    /// <summary>
    /// Every gameplay tuning value. Wrapped by the GameBalance ScriptableObject in
    /// Unity so designers edit it in the Inspector; plain C# so it is unit-testable.
    /// </summary>
    [Serializable]
    public class BalanceConfig
    {
        // --- lanes & player
        public int laneCount = 3;
        public float laneSpacing = 3.2f;
        public float laneChangeDuration = 0.22f;
        public float jumpDuration = 0.65f;
        public float jumpHeight = 1.5f;
        public int startHealth = 3;
        public float invulnerabilityTime = 1.5f;
        /// <summary>Collision half extents of the jeepney (slightly smaller than the mesh = forgiving).</summary>
        public float playerHalfWidth = 0.85f;
        public float playerHalfLength = 2.4f;
        public float brakeMultiplier = 0.6f;
        public float brakeDuration = 0.8f;
        public float brakeCooldown = 1.5f;
        public float crashSlowdown = 0.5f;

        // --- speed & difficulty curve (piecewise linear over distance)
        public float startSpeed = 12f;
        public float maxSpeed = 24f;
        public float[] difficultyDistances = { 0f, 500f, 1000f, 2000f, 3000f };
        public float[] difficultyValues = { 0f, 0.2f, 0.45f, 0.75f, 1f };

        // --- spawning
        public float spawnAhead = 150f;
        public float despawnBehind = 15f;
        public float minRowGap = 16f;
        public float maxRowGap = 34f;
        /// <summary>Minimum seconds between two blocking rows.</summary>
        public float minReactionTime = 0.9f;
        /// <summary>Extra seconds the level generator budgets per lane change (human slack).</summary>
        public float laneChangeSlack = 0.12f;
        public float jumpWallMinDistance = 500f;
        public float stopSpacingMin = 220f;
        public float stopSpacingMax = 320f;
        public float stopZoneLength = 16f;

        // --- player controls (Phase 2: hold-to-accelerate / hold-to-brake buttons)
        public float gasMultiplier = 1.3f;
        public float gasScoreMultiplier = 1.5f;
        public float brakeHoldMultiplier = 0.45f;

        // --- passengers (Phase 2: destinations, fares, delivery streaks)
        public int passengerCapacity = 5;
        public int minRideStops = 1;
        public int maxRideStops = 3;
        public int fareCoins = 5;
        public int groupBonusCoins = 2;
        public float streakStep = 0.25f;
        public float streakMax = 3f;
        public int scorePerDelivery = 100;

        // --- power-ups
        public float powerUpSpacingMin = 320f;
        public float powerUpSpacingMax = 560f;
        public float magnetDuration = 10f;
        public float magnetRadius = 9f;
        public float shieldDuration = 20f;
        public float turboDuration = 5f;
        public float turboSpeedMultiplier = 1.6f;
        public float doubleCoinsDuration = 15f;
        public float passengerBonusDuration = 20f;
        public float passengerBonusMultiplier = 2f;
        public int repairKitFallbackCoins = 25;

        // --- rewards & score
        public int coinValue = 1;
        public int pickupCoins = 2;
        public float scorePerMetre = 1f;
        public int scorePerCoin = 10;
        public int scorePerPassenger = 50;

        /// <summary>Deep copy (upgrades and jeepney stats are applied to a copy, never the asset).</summary>
        public BalanceConfig Clone()
        {
            var c = (BalanceConfig)MemberwiseClone();
            c.difficultyDistances = (float[])difficultyDistances?.Clone();
            c.difficultyValues = (float[])difficultyValues?.Clone();
            return c;
        }

        public float LaneX(int lane) => (lane - (laneCount - 1) * 0.5f) * laneSpacing;

        public float JumpVelocity => 4f * jumpHeight / jumpDuration;
        public float Gravity => 8f * jumpHeight / (jumpDuration * jumpDuration);

        /// <summary>Difficulty 0..1 at a distance, from the configurable curve.</summary>
        public float DifficultyAt(float distance)
        {
            var d = difficultyDistances;
            var v = difficultyValues;
            if (d == null || v == null || d.Length == 0 || d.Length != v.Length) return Clamp01(distance / 3000f);
            if (distance <= d[0]) return v[0];
            for (int i = 1; i < d.Length; i++)
            {
                if (distance <= d[i])
                {
                    float t = (distance - d[i - 1]) / Math.Max(1e-3f, d[i] - d[i - 1]);
                    return v[i - 1] + (v[i] - v[i - 1]) * t;
                }
            }
            return v[v.Length - 1];
        }

        public float SpeedAt(float distance) => startSpeed + (maxSpeed - startSpeed) * DifficultyAt(distance);

        /// <summary>Height of the jeepney's jump arc after t seconds (0 outside the jump).</summary>
        public float JumpHeightAt(float t)
        {
            if (t <= 0 || t >= jumpDuration) return 0;
            float x = t / jumpDuration;
            return 4f * jumpHeight * x * (1 - x);
        }

        private static float Clamp01(float v) => v < 0 ? 0 : v > 1 ? 1 : v;
    }
}
