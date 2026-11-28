using System.Collections.Generic;
using JeepneyRush.Logic.Geometry;
using UnityEngine;

namespace JeepneyRush
{
    /// <summary>
    /// Christmas decorations over the road (Paskong Pasada): wires with hanging parols and strings of
    /// lights every <see cref="Spacing"/> metres, scrolling with the road (and bent by the RoadBender).
    /// Visual only.
    /// </summary>
    public sealed class FestiveDecor : MonoBehaviour
    {
        public const float Spacing = 42f, Ahead = 230f, Behind = 20f;
        private readonly List<Transform> _arches = new List<Transform>();
        private float _nextZ;
        private int _variant;
        public bool On { get; private set; }

        public void Begin(bool on)
        {
            Clear();
            On = on;
            _nextZ = 12f;
        }

        public void Clear()
        {
            foreach (var a in _arches) if (a != null) Destroy(a.gameObject);
            _arches.Clear();
        }

        public void Tick(float dt, float speed)
        {
            if (!On) return;
            float move = speed * dt;
            for (int i = _arches.Count - 1; i >= 0; i--)
            {
                var t = _arches[i];
                var p = t.localPosition;
                p.z -= move;
                t.localPosition = p;
                if (p.z < -Behind) { Destroy(t.gameObject); _arches.RemoveAt(i); }
            }
            _nextZ -= move;
            while (_nextZ < Ahead)
            {
                int v = _variant++ % 4;
                var go = ModelLibrary.Instantiate(ModelLibrary.Get("parol_arch" + v, () => PropBuilder.ParolArch(SceneryBuilder.RoadHalfWidth + 1.2f, v)), "ParolArch", transform, castShadows: false);
                go.transform.localPosition = new Vector3(0, 0, _nextZ);
                _arches.Add(go.transform);
                _nextZ += Spacing;
            }
        }
    }
}
