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
        /// <summary>Cross-street traffic (moves along X through the box).</summary>
        public readonly List<CrossCar> Cars = new List<CrossCar>();
        public readonly float[] SpawnTimer = new float[2];
        public float LineZBefore;

        /// <summary>Stop line ahead of the jeepney (Unity z, + = ahead).</summary>
        public float StopLineZ => transform.localPosition.z - TrafficLightRules.StopLineOffset;
    }

    public sealed class CrossCar
    {
        public Hazard H;
        public float Dir;
        public float Speed;
    }

    /// <summary>
    /// Spawns and runs intersections with traffic lights. When the light is red for the
    /// jeepney, people walk across the zebra crossing in front of it. Crossing the stop
    /// line on red raises <see cref="RanRed"/> (the session issues an MMDA ticket).
    /// </summary>
    public sealed class IntersectionManager : MonoBehaviour
    {
        private string Pool = "intersection";
        private BalanceConfig _cfg;
        private readonly List<IntersectionView> _items = new List<IntersectionView>();
        private float _time;
        public IReadOnlyList<IntersectionView> Items => _items;
        public System.Action<IntersectionView> RanRed;

        public void Init(BalanceConfig cfg, EnvironmentKind env)
        {
            _cfg = cfg;
            Pool = "intersection_" + env;
            if (Services.Pools.Has(Pool)) return;
            var colors = new[] { "#FF2A2A", "#FFC400", "#2BFF6A" };
            Services.Pools.Register(Pool, () =>
            {
                var go = ModelLibrary.Instantiate(ModelLibrary.Get(Pool, () => PropBuilder.Intersection(env)), "Intersection");
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
            v.SpawnTimer[0] = Random.Range(0f, 1.5f);
            v.SpawnTimer[1] = Random.Range(0f, 1.5f);
            _items.Add(v);
        }

        public void Clear()
        {
            foreach (var v in _items) { ReleaseWalkers(v); ReleaseCars(v); }
            foreach (var v in _items) Services.Pools.Release(v.gameObject);
            _items.Clear();
        }

        private static readonly (HazardKind kind, float weight)[] CrossKinds =
        {
            (HazardKind.Sedan, 3f), (HazardKind.Suv, 1.5f), (HazardKind.Tricycle, 1.6f), (HazardKind.Motorcycle, 1.4f), (HazardKind.ParkedJeepney, 1.2f),
        };
        private const float CrossCruise = 8f;

        private void ReleaseCars(IntersectionView v)
        {
            foreach (var c in v.Cars) if (c.H != null) Services.Pools.Release(c.H.gameObject);
            v.Cars.Clear();
        }

        private void SpawnCar(IntersectionView v, float dir)
        {
            float total = 0;
            foreach (var k in CrossKinds) total += k.weight;
            float r = Random.value * total;
            var kind = CrossKinds[0].kind;
            foreach (var k in CrossKinds) { if (r < k.weight) { kind = k.kind; break; } r -= k.weight; }
            int variant = Random.Range(0, ModelLibrary.VariantCount(kind));
            string key = $"xc:{kind}:{variant}";
            if (!Services.Pools.Has(key)) Services.Pools.Register(key, () => Hazard.Create(kind, variant, key));
            var h = Services.Pools.Get(key, v.transform).GetComponent<Hazard>();
            h.ResetState(0, 0, true);
            float so = SceneryBuilder.SidewalkOuter;
            h.transform.localPosition = new Vector3(-dir * (so + 4f), 0, CrossTrafficRules.LaneFor(dir));
            h.transform.localRotation = Quaternion.Euler(0, dir > 0 ? 90 : -90, 0); // models face +Z
            v.Cars.Add(new CrossCar { H = h, Dir = dir, Speed = CrossCruise });
        }

        /// <summary>
        /// Side-street cars: they queue at their stop line while the jeepney has green/yellow,
        /// drive through on the jeepney's red (after an all-red second) and never pull into a
        /// jeepney that is still in the box.
        /// </summary>
        private void TickCars(IntersectionView v, float dt, float viewZ)
        {
            float rw = SceneryBuilder.RoadHalfWidth, so = SceneryBuilder.SidewalkOuter;
            bool go = CrossTrafficRules.CrossGreen(v.Light.Color, v.Light.Remaining, _cfg) && !CrossTrafficRules.JeepneyInBox(-viewZ, _cfg);
            // new arrivals from both side streets
            if (viewZ < 220f && viewZ > -20f)
                for (int d = 0; d < 2; d++)
                {
                    float dir = d == 0 ? 1f : -1f;
                    v.SpawnTimer[d] -= dt;
                    if (v.SpawnTimer[d] > 0) continue;
                    v.SpawnTimer[d] = Random.Range(go ? 1.2f : 2.2f, go ? 2.6f : 4.5f);
                    int queued = 0;
                    bool clear = true;
                    foreach (var c in v.Cars)
                    {
                        if (c.Dir != dir) continue;
                        queued++;
                        if (dir * c.H.transform.localPosition.x < -(so + 4f) + 6f) clear = false;
                    }
                    if (queued < 4 && clear) SpawnCar(v, dir);
                }
            for (int i = v.Cars.Count - 1; i >= 0; i--)
            {
                var c = v.Cars[i];
                var t = c.H.transform;
                var p = t.localPosition;
                if (c.H.Hit) { c.Speed = 0; continue; } // got hit by a red-light runner: stays put
                float hl = c.H.Def.HalfLength;
                float x = p.x * c.Dir;                       // progress along its own direction
                float stopX = -(rw + 1.2f + hl);             // its stop line (front bumper at the curb)
                float limit = float.MaxValue;
                if (!go && x < stopX + 0.3f) limit = stopX;  // wait at the line
                // keep a gap to the car in front (same direction)
                foreach (var o in v.Cars)
                {
                    if (o == c || o.Dir != c.Dir) continue;
                    float ox = o.H.transform.localPosition.x * c.Dir;
                    if (ox > x) limit = Mathf.Min(limit, ox - o.H.Def.HalfLength - hl - 1.5f);
                }
                float target = CrossCruise;
                if (limit < float.MaxValue)
                {
                    float gap = Mathf.Max(0, limit - x);
                    target = Mathf.Min(target, Mathf.Sqrt(2f * 6f * gap));
                    if (gap < 0.05f) target = 0;
                }
                c.Speed = Mathf.MoveTowards(c.Speed, target, (target < c.Speed ? 12f : 4f) * dt);
                x = Mathf.Min(x + c.Speed * dt, limit < float.MaxValue ? Mathf.Max(x, limit) : float.MaxValue);
                p.x = x * c.Dir;
                t.localPosition = p;
                // a little suspension bob so waiting cars look alive
                c.H.Body.localPosition = new Vector3(0, Mathf.Abs(Mathf.Sin((_time + i) * 6f)) * 0.02f, 0);
                if (x > so + 4f)
                {
                    Services.Pools.Release(c.H.gameObject);
                    v.Cars.RemoveAt(i);
                }
            }
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
                    bool on = TrafficLightRules.LampIndex(c) == k % 3; // lamps are red/yellow/green top to bottom
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

                TickCars(v, dt, p.z);

                if (p.z < -60f)
                {
                    ReleaseWalkers(v);
                    ReleaseCars(v);
                    Services.Pools.Release(v.gameObject);
                    _items.RemoveAt(i);
                }
            }
        }
    }
}
