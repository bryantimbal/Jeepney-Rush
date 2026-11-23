using System.Collections.Generic;
using JeepneyRush.Logic;
using JeepneyRush.Logic.Geometry;
using UnityEngine;

namespace JeepneyRush
{
    /// <summary>One roadside gas station: canopy, pumps, refuelling pad and the attendant.</summary>
    public sealed class StationView : MonoBehaviour
    {
        public GasStation Station;
        public bool Done;
        public Transform Attendant;
        /// <summary>Floating "GAS" marker above the pad (hidden once used).</summary>
        public Transform Marker;
        public Vector3 AttendantHome;
    }

    /// <summary>
    /// Spawns and scrolls gas stations ("gasolinahan"). A station stays usable while it
    /// is within reversing reach; refuelling itself is driven by the session.
    /// </summary>
    public sealed class StationManager : MonoBehaviour
    {
        private const string Pool = "gas_station";
        private BalanceConfig _cfg;
        private readonly List<StationView> _stations = new List<StationView>();
        private float _time;
        public IReadOnlyList<StationView> Stations => _stations;
        /// <summary>The station currently being used for a fill-up (attendant walks over), or null.</summary>
        public StationView Serving;

        public void Init(BalanceConfig cfg)
        {
            _cfg = cfg;
            if (!Services.Pools.Has(Pool))
                Services.Pools.Register(Pool, () =>
                {
                    float laneX = cfg.LaneX(cfg.laneCount - 1);
                    var go = ModelLibrary.Instantiate(ModelLibrary.Get("gasstation", () => PropBuilder.GasStation(laneX, cfg.laneSpacing - 0.5f, cfg.stationZoneLength)), "GasStation");
                    var v = go.AddComponent<StationView>();
                    var person = ModelLibrary.Instantiate(ModelLibrary.Person(4 % PropBuilder.PersonVariants), "Attendant", go.transform);
                    v.AttendantHome = new Vector3(SceneryBuilder.RoadHalfWidth + 0.45f, SceneryBuilder.SidewalkHeight, -1.2f);
                    person.transform.localPosition = v.AttendantHome;
                    person.transform.localRotation = Quaternion.Euler(0, -90, 0); // facing the road
                    v.Attendant = person.transform;
                    var marker = ModelLibrary.Instantiate(ModelLibrary.Get("gas_marker", PropBuilder.GasMarker), "GasMarker", go.transform, castShadows: false);
                    marker.transform.localPosition = new Vector3(laneX, 7.4f, 0);
                    v.Marker = marker.transform;
                    return go;
                }, 1);
        }

        public void Spawn(GasStation st, float unityZ)
        {
            var v = Services.Pools.Get(Pool, transform).GetComponent<StationView>();
            v.Station = st;
            v.Done = false;
            v.transform.localPosition = new Vector3(0, 0, unityZ);
            v.transform.localRotation = Quaternion.identity;
            v.Attendant.localPosition = v.AttendantHome;
            if (v.Marker != null) v.Marker.gameObject.SetActive(true);
            _stations.Add(v);
        }

        public void Clear()
        {
            foreach (var s in _stations) Services.Pools.Release(s.gameObject);
            _stations.Clear();
            Serving = null;
        }

        /// <summary>Nearest unused station ahead of (or just behind) the jeepney.</summary>
        public StationView Next()
        {
            StationView best = null;
            foreach (var s in _stations)
                if (!s.Done && (best == null || s.transform.localPosition.z < best.transform.localPosition.z)) best = s;
            return best;
        }

        public void Tick(float dt, float speed, Vector3 playerLocal)
        {
            _time += dt;
            for (int i = _stations.Count - 1; i >= 0; i--)
            {
                var s = _stations[i];
                var p = s.transform.localPosition;
                p.z -= speed * dt;
                s.transform.localPosition = p;
                // attendant: waves at approaching jeepneys, walks to the tank flap while refuelling
                var home = s.AttendantHome;
                Vector3 target = home;
                if (Serving == s) target = new Vector3(playerLocal.x + 1.5f, 0f, -p.z - 1.0f);
                var a = s.Attendant.localPosition;
                a = Vector3.MoveTowards(a, target, dt * 3.5f);
                float wave = s.Done || Serving == s ? 0f : Mathf.Max(0, Mathf.Sin(_time * 5f)) * 0.06f;
                s.Attendant.localPosition = new Vector3(a.x, Serving == s ? 0f : Mathf.Max(home.y, a.y) + wave, a.z);
                s.Attendant.localRotation = Quaternion.Euler(0, Serving == s ? -90f + Mathf.Sin(_time * 3f) * 10f : -90f, 0);
                if (!s.Done && StopRules.OutOfReach(p.z, _cfg)) s.Done = true;
                if (s.Marker != null)
                {
                    bool show = !s.Done && Serving != s;
                    if (s.Marker.gameObject.activeSelf != show) s.Marker.gameObject.SetActive(show);
                    if (show)
                    {
                        var m = s.Marker.localPosition;
                        s.Marker.localPosition = new Vector3(m.x, 7.4f + Mathf.Sin(_time * 2.5f) * 0.35f, m.z);
                        s.Marker.localRotation = Quaternion.Euler(0, Mathf.Sin(_time * 0.8f) * 20f, 0); // sways, always readable
                        float pulse = 1f + Mathf.Max(0f, Mathf.Sin(_time * 4f)) * 0.08f;           // gentle pulse catches the eye
                        s.Marker.localScale = new Vector3(pulse, pulse, pulse);
                    }
                }
                if (p.z < -60f)
                {
                    if (Serving == s) Serving = null;
                    Services.Pools.Release(s.gameObject);
                    _stations.RemoveAt(i);
                }
            }
        }
    }
}
