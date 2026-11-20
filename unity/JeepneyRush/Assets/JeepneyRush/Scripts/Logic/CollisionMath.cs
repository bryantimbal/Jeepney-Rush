using System;

namespace JeepneyRush.Logic
{
    /// <summary>
    /// Lane-space AABB overlap used by the fairness simulation (the Unity build uses
    /// trigger colliders sized from the same numbers, see HazardColliderSizing).
    /// </summary>
    public static class CollisionMath
    {
        public const float Forgive = 0.15f;

        public static bool Overlaps(BalanceConfig cfg, float px, float py, float hx, float hz, HazardDef d)
        {
            if (Math.Abs(px - hx) >= cfg.playerHalfWidth + d.HalfWidth - Forgive) return false;
            if (Math.Abs(hz) >= cfg.playerHalfLength + d.HalfLength - Forgive) return false;
            return py < d.Height - 0.05f;
        }
    }
}
