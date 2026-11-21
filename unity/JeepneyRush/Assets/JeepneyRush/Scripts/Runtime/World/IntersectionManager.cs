using System.Collections.Generic;
using JeepneyRush.Logic;
using JeepneyRush.Logic.Geometry;
using UnityEngine;

namespace JeepneyRush
{
    /// <summary>One signalised intersection: lamps, light cycle and crossing pedestrians.</summary>
    public sealed class IntersectionView : MonoBehaviour
    {
        public Intersection Data;
        public TrafficLightCycle Light;
        public GameObject[] Lamps; // per head: red, yellow, green
        public readonly List<Transform> Walkers = new List<Transform>();
        public readonly List<int> WalkerVariants = new List<int>();
        public readonly List<float> WalkerDir = new List<float>();
        public bool Crossed, Ticketed;
        public float LineZBefore;

        /// <summary>Stop line ahead of the jeepney (Unity z, + = ahead).</summary>
        public float StopLineZ => transform.localPosition.z - TrafficLightRules.StopLineOffset;
    }

    /// <summary>
    /// Spawns and runs intersections with traffic lights. When the light is red for the
    /// jeepney, people walk across the zebra crossing in front of it. Crossing the stop
    /// line on red raises <see cref="RanRed"/> (the session issues an MMDA ticket).
    /// </summary>
    public sealed class IntersectionManager : MonoBehaviour
    {
        private const string Pool = "intersection";
        private BalanceConfig _cfg;
        private readonly List<IntersectionView> _items = new List<IntersectionView>();
        private float _time;
        public IReadOnlyList<IntersectionView> Items => _items;
        public System.Action<IntersectionView> RanRed;

        public void Init(BalanceConfig cfg)
        {
            _cfg = cfg;
            if (Services.Pools.Has(Pool)) return;
            var colors = new[] { "#FF2A2A", "#FFC400", "#2BFF6A" };
            Services.Pools.Register(Pool, () =>
            {
                var go = ModelLibrary.Instantiate(ModelLibrary.Get("intersection", PropBuilder.Intersection), "Intersection");
                var v = go.AddComponent<IntersectionView>();
                var lamps = PropBuilder.SignalLamps();
                v.Lamps = new GameObject[lamps.Length];
                for (int i = 0; i < lamps.Length; i++)
                {
                    string c = colors[i % 3];
                    var lamp = ModelLibrary.Instantiate(ModelLibrary.Get("lamp" + c, () => PropBuilder.SignalLamp(c)), "Lamp" + i, go.transform, castShadows: false);
                    lamp.transform.localPosition = new Vector3(lamps[i].X, lamps[i].Y, lamps[i].Z - 0.01f);
                    lamp.SetActive(false);
                    v.Lamps[i] = lamp;
                }
                return go;
            }, 1);
        }

        public void Spawn(Intersection data, float unityZ)
        {
            var v = Services.Pools.Get(Pool, transform).GetComponent<IntersectionView>();
            v.Data = data;
            v.Light = new TrafficLightCycle(_cfg, data.Phase);
            v.Crossed = v.Ticketed = false;
            v.transform.localPosition = new Vector3(0, 0, unityZ);
            v.transform.localRotation = Quaternion.identity;
            v.LineZBefore = v.StopLineZ;
            _items.Add(v);
        }

        public void Clear()
        {
            foreach (var v in _items) ReleaseWalkers(v);
            foreach (var v in _items) Services.Pools.Release(v.gameObject);
            _items.Clear();
        }

        private void ReleaseWalkers(IntersectionView v)
        {
            foreach (var w in v.Walkers) if (w != null) Services.Pools.Release(w.gameObject);
            v.Walkers.Clear();
            v.WalkerVariants.Clear();
            v.WalkerDir.Clear();
        }

        /// <summary>Nearest intersection whose stop line is still ahead (or the one the jeepney is waiting at).</summary>
        public IntersectionView Next()
        {
            IntersectionView best = null;
            foreach (var v in _items)
                if (!v.Crossed && (best == null || v.StopLineZ < best.StopLineZ)) best = v;
            return best;
        }

        public void Tick(float dt, float speed)
        {
            _time += dt;
            float rw = SceneryBuilder.RoadHalfWidth;
            for (int i = _items.Count - 1; i >= 0; i--)
            {
                var v = _items[i];
                var p = v.transform.localPosition;
                p.z -= speed * dt;
                v.transform.localPosition = p;
                v.Light.Tick(dt);
                var c = v.Light.Color;
                for (int k = 0; k < v.Lamps.Length; k++)
                {
                    bool on = (int)c == k % 3;
                    if (c == LightColor.Yellow && k % 3 == 1) on = (_time * 3f) % 1f < 0.8f; // slight flicker so it reads as "changing"
                    if (v.Lamps[k].activeSelf != on) v.Lamps[k].SetActive(on);
                }

                // the jeepney's front bumper crossing the stop line
                float line = v.StopLineZ;
                if (!v.Crossed && TrafficLightRules.CrossedLine(v.LineZBefore, line, _cfg))
                {
                    v.Crossed = true;
                    if (TrafficLightRules.IsViolation(c) && !v.Ticketed) { v.Ticketed = true; RanRed?.Invoke(v); }
                }
                if (v.Crossed && line - _cfg.playerHalfLength > 1f) v.Crossed = false; // reversed back behind the line
                v.LineZBefore = line;

                // pedestrians use the zebra crossing in front of the jeepney while it has red
                bool walk = c == LightColor.Red && v.Light.Remaining > 1.2f && !v.Ticketed;
                if (walk && v.Walkers.Count < 4 && p.z < 150f && Random.value < dt * 1.6f)
                {
                    int variant = Random.Range(0, PropBuilder.PersonVariants);
                    float dir = Random.value < 0.5f ? 1f : -1f;
                    var t = Services.Pools.Get("person" + variant, v.transform).transform;
                    t.localPosition = new Vector3(-dir * (rw + 0.6f), SceneryBuilder.SidewalkHeight, -7.4f + Random.Range(-1f, 1f));
                    t.localRotation = Quaternion.Euler(0, dir > 0 ? 90 : -90, 0);
                    v.Walkers.Add(t);
                    v.WalkerVariants.Add(variant);
                    v.WalkerDir.Add(dir);
                }
                for (int w = v.Walkers.Count - 1; w >= 0; w--)
                {
                    var t = v.Walkers[w];
                    var lp = t.localPosition;
                    // someone ran the red: everyone hurries back to the curb
                    float sp = v.Ticketed ? -4.5f : (c == LightColor.Red ? 1.5f : 2.6f);
                    lp.x += v.WalkerDir[w] * sp * dt;
                    bool onRoad = Mathf.Abs(lp.x) < rw;
                    lp.y = (onRoad ? 0f : SceneryBuilder.SidewalkHeight) + Mathf.Abs(Mathf.Sin(_time * 9f + w)) * 0.05f;
                    t.localPosition = lp;
                    if (Mathf.Abs(lp.x) > rw + 1.2f && (v.WalkerDir[w] * lp.x > 0 || v.Ticketed))
                    {
                        Services.Pools.Release(t.gameObject);
                        v.Walkers.RemoveAt(w);
                        v.WalkerVariants.RemoveAt(w);
                        v.WalkerDir.RemoveAt(w);
                    }
                }

                if (p.z < -60f)
                {
                    ReleaseWalkers(v);
                    Services.Pools.Release(v.gameObject);
                    _items.RemoveAt(i);
                }
            }
        }
    }
}
