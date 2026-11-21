using System.Collections.Generic;
using JeepneyRush.Logic;
using JeepneyRush.Logic.Geometry;
using UnityEngine;

namespace JeepneyRush
{
    /// <summary>
    /// Passenger pickup: stop spawning, boarding animations, capacity enforcement and
    /// seated passengers that are visible inside the jeepney.
    /// </summary>
    public sealed class PassengerManager : MonoBehaviour
    {
        private const string StopPool = "stop";
        private BalanceConfig _cfg;
        private PassengerRoute _route;
        private readonly List<PassengerStopView> _stops = new List<PassengerStopView>();
        private readonly List<(Transform t, int variant, Vector3 from, float time, bool boarding, float targetX)> _flying = new List<(Transform, int, Vector3, float, bool, float)>();
        private readonly List<GameObject> _seated = new List<GameObject>();
        private Transform _player;
        private float _time;
        /// <summary>Raised when riders miss their stop (count, stop).</summary>
        public System.Action<int, PassengerStop> Missed;

        public PassengerRoute Route => _route;
        public IReadOnlyList<PassengerStopView> Stops => _stops;

        public void Init(BalanceConfig cfg, Transform playerBody)
        {
            _cfg = cfg;
            _player = playerBody;
            if (!Services.Pools.Has(StopPool)) Services.Pools.Register(StopPool, () => PassengerStopView.Create(cfg.laneSpacing - 0.5f, cfg.stopZoneLength), 3);
            for (int i = 0; i < PropBuilder.PersonVariants; i++)
            {
                int v = i;
                string key = "person" + v;
                if (!Services.Pools.Has(key)) Services.Pools.Register(key, () => ModelLibrary.Instantiate(ModelLibrary.Person(v), key));
            }
            BuildSeats();
        }

        /// <summary>Seated commuters parented to the jeepney body (shown as they board).</summary>
        private void BuildSeats()
        {
            foreach (var s in _seated) if (s != null) Destroy(s);
            _seated.Clear();
            for (int i = 0; i < JeepneyBuilder.Seats.Length; i++)
            {
                var seat = JeepneyBuilder.Seats[i];
                var go = ModelLibrary.Instantiate(ModelLibrary.SeatedPerson(i % PropBuilder.PersonVariants), "Seated" + i, _player, castShadows: false);
                go.transform.localPosition = new Vector3(seat.X, seat.Y, seat.Z);
                go.transform.localRotation = Quaternion.Euler(0, seat.X < 0 ? 90 : -90, 0); // face the aisle
                go.SetActive(false);
                _seated.Add(go);
            }
        }

        public void AttachTo(Transform playerBody)
        {
            _player = playerBody;
            BuildSeats();
        }

        public void ResetRun(PassengerRoute route)
        {
            Clear();
            _route = route;
            UpdateSeats();
        }

        public void Clear()
        {
            foreach (var s in _stops) ReleaseStop(s);
            _stops.Clear();
            foreach (var f in _flying) Services.Pools.Release(f.t.gameObject);
            _flying.Clear();
        }

        private void ReleaseStop(PassengerStopView s)
        {
            foreach (var w in s.Waiting) Services.Pools.Release(w.gameObject);
            s.Waiting.Clear();
            s.WaitingVariants.Clear();
            if (s.Sign != null) Destroy(s.Sign);
            Services.Pools.Release(s.gameObject);
        }

        public void SpawnStop(PassengerStop stop, float unityZ)
        {
            var v = Services.Pools.Get(StopPool, transform).GetComponent<PassengerStopView>();
            v.Stop = stop;
            v.Done = false;
            v.transform.localPosition = new Vector3(0, 0, unityZ);
            float laneX = _cfg.LaneX(stop.Lane);
            v.Zone.localPosition = new Vector3(laneX, 0.03f, 0);
            v.Trigger.transform.localPosition = new Vector3(laneX, 0, 0);
            float shedX = stop.Side * (SceneryBuilder.RoadHalfWidth + 1.8f);
            v.Shed.localPosition = new Vector3(shedX, SceneryBuilder.SidewalkHeight, 0);
            v.Shed.localRotation = Quaternion.Euler(0, stop.Side < 0 ? 0 : 180, 0);
            v.Sign = ModelLibrary.Instantiate(ModelLibrary.StopSign(Mathf.Max(0, stop.NameIndex)), "Sign", v.transform);
            v.Sign.transform.localPosition = new Vector3(stop.Side * (SceneryBuilder.RoadHalfWidth + 0.5f), SceneryBuilder.SidewalkHeight, -3f);
            v.Sign.transform.localRotation = Quaternion.Euler(0, 180, 0); // face the oncoming jeepney (-Z)
            v.Arrow.gameObject.SetActive(true);
            v.DropArrow.gameObject.SetActive(false);
            for (int i = 0; i < stop.Waiting; i++)
            {
                int variant = Random.Range(0, PropBuilder.PersonVariants);
                var p = Services.Pools.Get("person" + variant, v.transform).transform;
                p.localPosition = new Vector3(stop.Side * (SceneryBuilder.RoadHalfWidth + 0.7f + Random.value * 0.5f), SceneryBuilder.SidewalkHeight, (i - stop.Waiting / 2f) * 1.1f);
                p.localRotation = Quaternion.Euler(0, 180 + (stop.Side < 0 ? 35 : -35), 0); // facing the traffic
                v.Waiting.Add(p);
                v.WaitingVariants.Add(variant);
            }
            _stops.Add(v);
        }

        /// <summary>Distance info for the HUD (null when no stop is ahead).</summary>
        public PassengerStopView NextStop()
        {
            PassengerStopView best = null;
            foreach (var s in _stops)
                if (!s.Done && (best == null || s.transform.localPosition.z < best.transform.localPosition.z)) best = s;
            return best;
        }

        public void Tick(float dt, float speed)
        {
            _time += dt;
            float half = _cfg.stopZoneLength / 2f;
            for (int i = _stops.Count - 1; i >= 0; i--)
            {
                var s = _stops[i];
                var p = s.transform.localPosition;
                p.z -= speed * dt;
                s.transform.localPosition = p;
                // green arrow when someone aboard gets off here, yellow when it is a pickup only
                bool drop = !s.Done && _route.AlightingAt(s.Stop.Id) > 0;
                if (s.DropArrow.gameObject.activeSelf != drop) s.DropArrow.gameObject.SetActive(drop);
                if (s.Arrow.gameObject.activeSelf != (!s.Done && !drop)) s.Arrow.gameObject.SetActive(!s.Done && !drop);
                var arrowPos = new Vector3(_cfg.LaneX(s.Stop.Lane), 3.2f + Mathf.Sin(_time * 5f) * 0.3f, -half + 2f);
                s.Arrow.localPosition = s.DropArrow.localPosition = arrowPos;
                s.Arrow.localRotation = s.DropArrow.localRotation = Quaternion.Euler(0, _time * 120f, 0);
                for (int w = 0; w < s.Waiting.Count; w++)
                {
                    var t = s.Waiting[w];
                    var lp = t.localPosition;
                    lp.y = SceneryBuilder.SidewalkHeight + (s.Done ? 0 : Mathf.Max(0, Mathf.Sin(_time * 6f + w)) * 0.12f); // hop & wave "Para!"
                    t.localPosition = lp;
                }
                if (!s.Done && p.z + half < -_cfg.playerHalfLength)
                {
                    s.Done = true;
                    s.Arrow.gameObject.SetActive(false);
                    s.DropArrow.gameObject.SetActive(false);
                    int lost = _route.Miss(s.Stop);
                    if (lost > 0) { Missed?.Invoke(lost, s.Stop); UpdateSeats(); }
                }
                if (p.z < -40f)
                {
                    ReleaseStop(s);
                    _stops.RemoveAt(i);
                }
            }
            for (int i = _flying.Count - 1; i >= 0; i--)
            {
                var f = _flying[i];
                f.time += dt;
                float k = Mathf.Clamp01(f.time / 0.5f);
                if (f.boarding)
                {
                    // hop into the rear doorway of the jeepney
                    var target = new Vector3(_player.position.x, 0.6f, -3.1f);
                    var pos = Vector3.Lerp(f.from, target, k);
                    pos.y += Mathf.Sin(k * Mathf.PI) * 2f;
                    f.t.position = pos;
                    f.t.localScale = Vector3.one * (1f - k * 0.5f);
                }
                else
                {
                    var pos = f.from;
                    pos.x = Mathf.Lerp(f.from.x, f.targetX, k);
                    pos.y = Mathf.Max(SceneryBuilder.SidewalkHeight, 0.9f + Mathf.Sin(k * Mathf.PI) * 1.4f - k * 0.7f);
                    pos.z = f.from.z - speed * f.time;
                    f.t.position = pos;
                }
                _flying[i] = f;
                if (k >= 1 && f.boarding || !f.boarding && f.t.position.z < -25f)
                {
                    f.t.localScale = Vector3.one;
                    Services.Pools.Release(f.t.gameObject);
                    _flying.RemoveAt(i);
                }
            }
        }

        /// <summary>The jeepney drove into the PARA zone.</summary>
        public PickupResult Serve(PassengerStopView s)
        {
            if (s.Done) return default;
            s.Done = true;
            s.Arrow.gameObject.SetActive(false);
            s.DropArrow.gameObject.SetActive(false);
            var r = _route.Serve(s.Stop);
            for (int b = 0; b < r.Boarded && s.Waiting.Count > 0; b++)
            {
                var t = s.Waiting[0];
                s.Waiting.RemoveAt(0);
                s.WaitingVariants.RemoveAt(0);
                var world = t.position;
                t.SetParent(transform, true);
                _flying.Add((t, 0, world, 0f, true, 0f));
            }
            for (int a = 0; a < r.Alighted; a++)
            {
                int variant = Random.Range(0, PropBuilder.PersonVariants);
                var t = Services.Pools.Get("person" + variant, transform).transform;
                var from = new Vector3(_player.position.x, 0.8f, -3.2f - a * 0.4f);
                t.position = from;
                t.localRotation = Quaternion.identity;
                _flying.Add((t, variant, from, 0f, false, s.Stop.Side * (SceneryBuilder.RoadHalfWidth + 0.8f + a * 0.4f)));
            }
            UpdateSeats();
            return r;
        }

        /// <summary>An angry rider jumps out of the rear doorway and stomps off to the curb.</summary>
        public void AngryHopOut()
        {
            int variant = Random.Range(0, PropBuilder.PersonVariants);
            var t = Services.Pools.Get("person" + variant, transform).transform;
            var from = new Vector3(_player.position.x, 0.8f, -3.2f);
            t.position = from;
            t.localRotation = Quaternion.identity;
            float side = _player.position.x >= 0 ? 1f : -1f;
            _flying.Add((t, variant, from, 0f, false, side * (SceneryBuilder.RoadHalfWidth + 1.2f)));
            UpdateSeats();
        }

        /// <summary>World position of a random seated passenger's head (speech-bubble anchor).</summary>
        public Vector3 RandomRiderHead()
        {
            int n = Mathf.Min(_route != null ? _route.Count : 0, _seated.Count);
            if (n == 0) return _player.position + new Vector3(0, 2.6f, -1f);
            var s = _seated[Random.Range(0, n)];
            return s != null ? s.transform.position + new Vector3(0, 1.1f, 0) : _player.position + Vector3.up * 2.6f;
        }

        public void UpdateSeats()
        {
            int n = _route != null ? _route.Count : 0;
            for (int i = 0; i < _seated.Count; i++) if (_seated[i] != null) _seated[i].SetActive(i < n);
        }
    }
}
