using UnityEngine;

namespace JeepneyRush
{
    /// <summary>
    /// Static obstacles: cones, barriers, potholes, construction signs, road blocks and
    /// parked vehicles (with blinking hazard lights via a gentle pulse).
    /// </summary>
    public sealed class ObstacleManager : HazardManagerBase
    {
        protected override void Animate(Hazard h, float dt, ref Vector3 pos)
        {
            if (h.Hit) return;
            if (h.Kind == JeepneyRush.Logic.HazardKind.ConstructionSign)
                h.Body.localRotation = Quaternion.Euler(Mathf.Sin(h.Age * 3f) * 1.5f, 0, 0); // flapping in the wind
        }
    }
}
