using System.Collections.Generic;
using JeepneyRush.Logic;
using UnityEngine;

namespace JeepneyRush
{
    /// <summary>
    /// Visual road shape (hills and bends) for the current run. Gameplay stays in straight
    /// lane space (so the fairness guarantees hold); this only says how far a point
    /// <c>z</c> metres ahead is shifted sideways / up and how it is rotated.
    /// </summary>
    public static class RoadCurve
    {
        public static RoadShape Shape;
        /// <summary>Distance driven this run (metres).</summary>
        public static float Distance;
        public static bool Enabled => Shape != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Shape = null; Distance = 0; }

        public static Vector3 Offset(float z)
        {
            if (Shape == null) return Vector3.zero;
            return new Vector3(Shape.RelLateral(Distance, z), Shape.RelHeight(Distance, z), 0);
        }

        public static Quaternion Rotation(float z)
        {
            if (Shape == null) return Quaternion.identity;
            return Quaternion.Euler(-Shape.Pitch(Distance, z) * Mathf.Rad2Deg, Shape.RelYaw(Distance, z) * Mathf.Rad2Deg, 0);
        }

        /// <summary>Slope under the jeepney (radians).</summary>
        public static float PlayerPitch => Shape == null ? 0 : Shape.Pitch(Distance, 0);
    }

    /// <summary>
    /// Applies <see cref="RoadCurve"/> to everything on the road for rendering, then puts
    /// it back before the game logic runs again: LateUpdate bends (straight → curved),
    /// the next Update restores (curved → straight). Managers therefore keep working in
    /// simple straight coordinates. Near the jeepney the offset is ~0, so collisions are
    /// unaffected.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public sealed class RoadBender : MonoBehaviour
    {
        private struct Saved { public Transform T; public Vector3 P; public Quaternion R; }
        private readonly List<Transform> _roots = new List<Transform>();
        private readonly List<Saved> _saved = new List<Saved>(512);
        public RoadManager Road;

        public void AddRoot(Transform t) { if (t != null && !_roots.Contains(t)) _roots.Add(t); }

        private void Update() => Restore();

        private void Restore()
        {
            for (int i = 0; i < _saved.Count; i++)
            {
                var s = _saved[i];
                if (s.T == null) continue;
                s.T.localPosition = s.P;
                s.T.localRotation = s.R;
            }
            _saved.Clear();
        }

        private void LateUpdate()
        {
            Restore(); // in case Update did not run (e.g. first frame)
            if (Road != null) Road.Bend();
            if (!RoadCurve.Enabled) return;
            foreach (var root in _roots)
            {
                if (root == null) continue;
                for (int i = 0; i < root.childCount; i++)
                {
                    var t = root.GetChild(i);
                    if (!t.gameObject.activeSelf) continue;
                    var p = t.localPosition;
                    _saved.Add(new Saved { T = t, P = p, R = t.localRotation });
                    t.localPosition = p + RoadCurve.Offset(p.z);
                    t.localRotation = RoadCurve.Rotation(p.z) * t.localRotation;
                }
            }
        }

        private void OnDisable() => Restore();
    }
}
