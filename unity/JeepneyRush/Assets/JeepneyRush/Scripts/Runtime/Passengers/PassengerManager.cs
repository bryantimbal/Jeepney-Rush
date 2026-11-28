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
        private readonly List<GameObject> _hangers = new List<GameObject>();
        private Transform _player;
        private float _time;
        // ---- fare passing ("Bayad po!"): money travels hand to hand along the bench to the driver
        private sealed class Fare { public Transform T; public readonly List<Vector3> Path = new List<Vector3>(); public readonly List<int> Passers = new List<int>(); public float Time; public float Delay; }
        private readonly List<Fare> _fares = new List<Fare>();
        private float[] _bob = new float[0];
        private GameObject _driver, _konduktor;
        private float _driverBob;
        private const float HopTime = 0.3f;
        /// <summary>Driver sits on the left, behind the steering wheel.</summary>
        public static readonly Vector3 DriverSeat = new Vector3(-0.45f, 1.05f, 0.62f);
        private static readonly Vector3 DriverHand = new Vector3(-0.2f, 1.55f, 0.5f);
        /// <summary>Raised when a fare reaches the driver (world position of the money).</summary>
        public System.Action<Vector3> FarePaid;
        /// <summary>Raised when riders miss their stop (count, stop).</summary>
        public System.Action<int, PassengerStop> Missed;
        /// <summary>Drove past a stop where someone was waiting or wanted to get off.</summary>
        public System.Action<PassengerStop> Skipped;

        public PassengerRoute Route => _route;
        /// <summary>Current route's environment (barker lines name its stops).</summary>
        public EnvironmentKind Env;
        /// <summary>Retro-Modern Sarao: barkers are likelier at each stop.</summary>
        public float BarkerBonus;
        private readonly GameRandom _lineRng = new GameRandom(7);
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
            foreach (RiderKind k in System.Enum.GetValues(typeof(RiderKind)))
            {
                if (k == RiderKind.Regular) continue;
                var kind = k;
                string key = RiderPool(k);
                if (!Services.Pools.Has(key)) Services.Pools.Register(key, () => ModelLibrary.Instantiate(ModelLibrary.Rider(kind, false), key));
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
            _bob = new float[_seated.Count];
            _seatLooks.Clear();
            _seatKind = new RiderKind[_seated.Count];
            // "sabit" riders standing on the rear step, holding the grab bar (provincial roads)
            foreach (var hg in _hangers) if (hg != null) Destroy(hg);
            _hangers.Clear();
            float[] hx = { -0.55f, 0.55f, 0f };
            for (int i = 0; i < Sabit.ProvinceExtra; i++)
            {
                var go = ModelLibrary.Instantiate(ModelLibrary.Person((i + 5) % PropBuilder.PersonVariants), "Sabit" + i, _player, castShadows: false);
                go.transform.localPosition = new Vector3(hx[i], 0.42f, JeepneyBuilder.BodyRearZ - 0.32f - (i == 2 ? 0.08f : 0f));
                go.transform.localRotation = Quaternion.Euler(-6f, i == 0 ? -20f : i == 1 ? 20f : 0f, i == 0 ? 6f : i == 1 ? -6f : 0f); // leaning out, holding on
                go.SetActive(false);
                _hangers.Add(go);
            }
            if (_driver != null) Destroy(_driver);
            // the driver wears the player's look; the konduktor (if hired) rides on the rear step
            var sd = Services.Save.Data;
            var shirt = DriverStyle.Worn(sd, DriverPart.Shirt); var hat = DriverStyle.Worn(sd, DriverPart.Hat); var extra = DriverStyle.Worn(sd, DriverPart.Extra);
            _driver = ModelLibrary.Instantiate(ModelLibrary.Get(DriverStyle.LookKey(sd), () => PropBuilder.Driver(shirt.Id, hat.Id, hat.Color, extra.Id)), "Driver", _player, castShadows: false);
            _driver.transform.localPosition = DriverSeat;
            _driver.transform.localRotation = Quaternion.identity; // facing the road
            if (_konduktor != null) Destroy(_konduktor);
            _konduktor = null;
            if (DriverStyle.HasKonduktor(sd))
            {
                _konduktor = ModelLibrary.Instantiate(ModelLibrary.Get("konduktor", PropBuilder.Konduktor), "Konduktor", _player, castShadows: false);
                _konduktor.transform.localPosition = new Vector3(-0.62f, 0.5f, JeepneyBuilder.BodyRearZ - 0.28f);
                _konduktor.transform.localRotation = Quaternion.Euler(0, 160f, 0); // half turned to the street, holding the rail
            }
            if (!Services.Pools.Has("fare")) Services.Pools.Register("fare", () => ModelLibrary.Instantiate(ModelLibrary.Get("fare", () =>
            {
                var b = new MeshBuilder();
                b.Box("paint", "#6DBE6D", 0.2f, 0.012f, 0.1f, 0, 0, 0);          // folded peso bill
                b.Box("paint", "#3E8E4A", 0.08f, 0.014f, 0.05f, 0.03f, 0.001f, 0);
                b.Cylinder("gold", "#F2C230", 0.045f, 0.045f, 0.016f, 12, -0.06f, 0.012f, 0.02f); // coins
                b.Cylinder("gold", "#E0B020", 0.04f, 0.04f, 0.016f, 12, -0.05f, 0.03f, 0.0f);
                return b.Data;
            }), "Fare", null, castShadows: false), 4);
        }

        private Vector3 HandOf(int seat)
        {
            var s = JeepneyBuilder.Seats[seat];
            return new Vector3(s.X * 0.55f, 1.5f, s.Z + 0.05f); // hand held out over the aisle
        }

        /// <summary>A new rider hands their fare forward ("Bayad po!"); others pass it on ("Pakiabot").</summary>
        private void StartFare(int seat, float delay)
        {
            if (seat < 0 || seat >= JeepneyBuilder.Seats.Length) return;
            var f = new Fare { Delay = delay };
            f.Path.Add(HandOf(seat));
            float z = JeepneyBuilder.Seats[seat].Z;
            int n = _route != null ? Mathf.Min(Mathf.Min(_route.Count, _route.SeatCapacity), _seated.Count) : 0;
            var ahead = new List<int>();
            for (int j = 0; j < n; j++) if (j != seat && JeepneyBuilder.Seats[j].Z > z + 0.2f) ahead.Add(j);
            ahead.Sort((a, b) => JeepneyBuilder.Seats[a].Z.CompareTo(JeepneyBuilder.Seats[b].Z));
            for (int k = 0; k < ahead.Count && k < 3; k++) { f.Path.Add(HandOf(ahead[k])); f.Passers.Add(ahead[k]); }
            f.Path.Add(DriverHand);
            f.T = Services.Pools.Get("fare", _player).transform;
            f.T.localPosition = f.Path[0];
            f.T.localRotation = Quaternion.identity;
            f.T.gameObject.SetActive(delay <= 0);
            _fares.Add(f);
        }

        private void TickFares(float dt)
        {
            for (int i = _fares.Count - 1; i >= 0; i--)
            {
                var f = _fares[i];
                if (f.Delay > 0)
                {
                    f.Delay -= dt;
                    if (f.Delay <= 0) f.T.gameObject.SetActive(true);
                    continue;
                }
                float before = f.Time;
                f.Time += dt;
                int seg = Mathf.Min(f.Path.Count - 2, Mathf.FloorToInt(f.Time / HopTime));
                float k = Mathf.Clamp01(f.Time / HopTime - seg);
                // each hand-over is a little arc
                var pos = Vector3.Lerp(f.Path[seg], f.Path[seg + 1], k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.22f;
                f.T.localPosition = pos;
                f.T.localRotation = Quaternion.Euler(0, f.Time * 400f, Mathf.Sin(f.Time * 20f) * 15f);
                // the person receiving it reaches up
                int prevSeg = Mathf.FloorToInt(before / HopTime);
                if (seg != prevSeg && seg - 1 < f.Passers.Count && seg >= 1) _bob[f.Passers[seg - 1]] = 0.3f;
                if (f.Time >= HopTime * (f.Path.Count - 1))
                {
                    _driverBob = 0.35f;
                    FarePaid?.Invoke(f.T.position);
                    Services.Audio?.Play(Logic.Audio.Sfx.Fare, 0.8f, Random.Range(0.95f, 1.1f));
                    Services.Pools.Release(f.T.gameObject);
                    _fares.RemoveAt(i);
                }
            }
            for (int j = 0; j < _bob.Length; j++)
            {
                if (_seated[j] == null) continue;
                var s = JeepneyBuilder.Seats[j];
                float b = _bob[j] > 0 ? Mathf.Sin((0.3f - _bob[j]) / 0.3f * Mathf.PI) : 0f;
                if (_bob[j] > 0) _bob[j] -= dt;
                _seated[j].transform.localPosition = new Vector3(s.X * (1f - 0.12f * b), s.Y + 0.06f * b, s.Z);
            }
            if (_driver != null)
            {
                float d = _driverBob > 0 ? Mathf.Sin((0.35f - _driverBob) / 0.35f * Mathf.PI) : 0f;
                if (_driverBob > 0) _driverBob -= dt;
                // driver turns to take the money without taking their eyes off the road for long
                _driver.transform.localRotation = Quaternion.Euler(0, 25f * d, 0); // turns right, toward the aisle
                _driver.transform.localPosition = DriverSeat + new Vector3(0.05f * d, 0.03f * d, 0);
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
            foreach (var f in _fares) if (f.T != null) Services.Pools.Release(f.T.gameObject);
            _fares.Clear();
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
            // A stop returned by Pools.ReleaseAll (quit / restart / new mode) skipped ReleaseStop: it still
            // carries its old sign (e.g. GUADALUPE from an EDSA run, showing on every Beach stop) and
            // references to commuters that now belong to someone else. Start clean.
            for (int c = v.transform.childCount - 1; c >= 0; c--)
            {
                var child = v.transform.GetChild(c).gameObject;
                if (child.name == "Sign") { child.SetActive(false); Destroy(child); }
            }
            v.Sign = null;
            v.Waiting.Clear();
            v.WaitingVariants.Clear();
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
            // a barker at the curb before the stop, turned toward the oncoming jeepney
            v.HasBarker = Random.value < Barkers.Chance(Env) + BarkerBonus;
            v.Barker.gameObject.SetActive(v.HasBarker);
            v.Barker.localPosition = new Vector3(stop.Side * (SceneryBuilder.RoadHalfWidth + 0.45f), SceneryBuilder.SidewalkHeight, -5.5f);
            v.Barker.localRotation = Quaternion.Euler(0, 180 + (stop.Side < 0 ? 55 : -55), 0);
            v.Called = false;
            v.BarkT = Random.Range(0f, 0.8f);
            v.SendOffT = 0;
            v.BarkerCalls = 0;
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
            TickFares(dt);
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
                TickBarker(s, dt, p.z);
                if (!s.Done && StopRules.OutOfReach(p.z, _cfg)) // still reachable by reversing until then
                {
                    s.Done = true;
                    s.Arrow.gameObject.SetActive(false);
                    s.DropArrow.gameObject.SetActive(false);
                    // someone was waiting (and there was room) or wanted to get off here: a skipped stop
                    bool business = (s.Stop.Waiting > 0 && _route.Count < _route.Capacity) || _route.AlightingAt(s.Stop.Id) > 0;
                    int lost = _route.Miss(s.Stop);
                    if (lost > 0) { Missed?.Invoke(lost, s.Stop); UpdateSeats(); }
                    if (business) Skipped?.Invoke(s.Stop);
                    if (business && s.HasBarker && s.Stop.Waiting > 0) Shout(s, Lines.Pick(Lines.BarkerSkipped, _lineRng), true);
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

        private void Shout(PassengerStopView s, string text, bool angry = false)
        {
            var head = s.Barker;
            Services.UI?.Say(text, () => head != null ? head.position + Vector3.up * 2.1f : Vector3.zero, angry, 1.9f);
        }

        /// <summary>
        /// The barker waves the jeepney in and shouts the route while it approaches, then slaps
        /// its side and sends it off once the riders are aboard.
        /// </summary>
        private void TickBarker(PassengerStopView s, float dt, float z)
        {
            if (!s.HasBarker) return;
            // calling arm: fast beckoning while the stop is open, lazy wave afterwards
            // (the arm mesh points +Y; −25° raises it up and out to the road side, −165° hangs it down)
            float cur = s.BarkerArm.localEulerAngles.z > 180 ? s.BarkerArm.localEulerAngles.z - 360 : s.BarkerArm.localEulerAngles.z;
            float wave = s.Done && s.SendOffT <= 0 ? Mathf.Lerp(cur, -165f, dt * 2.5f)
                                                   : -25f + Mathf.Sin(_time * 9f + z) * 25f;
            s.BarkerArm.localRotation = Quaternion.Euler(s.Done ? 0 : Mathf.Sin(_time * 4.5f) * 20f, 0, wave);
            var bp = s.Barker.localPosition;
            bp.y = SceneryBuilder.SidewalkHeight + (s.Done ? 0 : Mathf.Max(0, Mathf.Sin(_time * 7f)) * 0.05f);
            s.Barker.localPosition = bp;
            if (s.SendOffT > 0)
            {
                s.SendOffT -= dt;
                if (s.SendOffT <= 0)
                {
                    Services.Audio?.Play(Logic.Audio.Sfx.Tapik, 0.9f, Random.Range(0.95f, 1.08f));
                    Shout(s, Lines.Pick(Lines.BarkerThanks, _lineRng));
                }
                return;
            }
            if (s.Done || s.BarkerCalls >= Barkers.MaxCalls || z <= 0f || z > Barkers.CallRange) return;
            s.BarkT -= dt;
            if (s.BarkT > 0) return;
            s.BarkT = Barkers.CallEvery * Random.Range(0.85f, 1.25f);
            s.BarkerCalls++;
            int left = _route != null ? _route.Capacity - _route.Count + _route.AlightingAt(s.Stop.Id) : 1;
            Shout(s, Lines.BarkerCall(Env, s.Stop.Name, left, _lineRng));
        }

        /// <summary>
        /// Horn: someone on the sidewalk hears the busina and joins the queue at this stop
        /// (once per stop, only if there is room). Returns the new rider's transform, or null.
        /// </summary>
        public Transform CallPassenger(PassengerStopView s)
        {
            if (s == null || s.Done || s.Called || s.Stop.Waiting >= 6 || _route == null || _route.Count + s.Stop.Waiting >= _route.Capacity) return null;
            s.Called = true;
            int i = s.Stop.Waiting;
            s.Stop.Waiting++;
            int variant = Random.Range(0, PropBuilder.PersonVariants);
            var p = Services.Pools.Get("person" + variant, s.transform).transform;
            p.localPosition = new Vector3(s.Stop.Side * (SceneryBuilder.RoadHalfWidth + 0.7f + Random.value * 0.5f), SceneryBuilder.SidewalkHeight, (i - s.Stop.Waiting / 2f) * 1.1f);
            p.localRotation = Quaternion.Euler(0, 180 + (s.Stop.Side < 0 ? 35 : -35), 0);
            s.Waiting.Add(p);
            s.WaitingVariants.Add(variant);
            return p;
        }

        /// <summary>The jeepney drove into the PARA zone.</summary>
        public PickupResult Serve(PassengerStopView s)
        {
            if (s.Done) return default;
            s.Done = true;
            s.Arrow.gameObject.SetActive(false);
            s.DropArrow.gameObject.SetActive(false);
            var r = _route.Serve(s.Stop);
            if (s.HasBarker && r.Boarded > 0) s.SendOffT = Barkers.SendOffDelay + r.Boarded * 0.15f;
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
                // the one everybody notices gets off first: the fare dodger running, the lola, the balikbayan with his box…
                var look = a == 0 ? (r.Dodgers > 0 ? RiderKind.Dodger : r.Leaver) : RiderKind.Regular;
                var t = Services.Pools.Get(look == RiderKind.Regular ? "person" + variant : RiderPool(look), transform).transform;
                var from = new Vector3(_player.position.x, 0.8f, -3.2f - a * 0.4f);
                t.position = from;
                t.localRotation = Quaternion.identity;
                _flying.Add((t, variant, from, 0f, false, s.Stop.Side * (SceneryBuilder.RoadHalfWidth + 0.8f + a * 0.4f)));
            }
            UpdateSeats();
            // every new rider pays: the fare is passed forward to the driver once they sit down
            int seatedNow = Mathf.Min(Mathf.Min(_route.Count, _route.SeatCapacity), _seated.Count);
            int firstNew = Mathf.Min(_route.Count, _seated.Count) - r.Boarded;
            for (int b = 0; b < r.Boarded; b++) if (firstNew + b < seatedNow) StartFare(firstNew + b, 0.6f + b * 0.45f); // sabit riders pay at the step
            if (_route.Hanging > 0 && r.Boarded > 0) Services.UI?.Float(_route.Hanging == 1 ? "SUMABIT SA LIKOD!" : $"{_route.Hanging} NAKASABIT!", UIManager.FloatKind.Info);
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

        private readonly Dictionary<int, float> _speakingUntil = new Dictionary<int, float>();

        /// <summary>
        /// Speech-bubble anchor for "a passenger": picks ONE seated rider (preferring one who isn't
        /// talking already) and follows that rider's head for the whole bubble. (Picking a random
        /// rider every frame made the bubble jump between heads – the flicker.)
        /// </summary>
        public System.Func<Vector3> RiderHead(float seconds = 2.4f)
        {
            int n = Mathf.Min(_route != null ? Mathf.Min(_route.Count, _route.SeatCapacity) : 0, _seated.Count);
            if (n == 0)
            {
                if (_route != null && _route.Hanging > 0 && _hangers.Count > 0 && _hangers[0] != null)
                {
                    var h = _hangers[0].transform;
                    return () => h != null ? h.position + new Vector3(0, 2.0f, 0) : _player.position + new Vector3(0, 2.6f, -1f);
                }
                return () => _player.position + new Vector3(0, 2.6f, -1f);
            }
            int start = Random.Range(0, n), seat = start;
            for (int i = 0; i < n; i++)
            {
                int s = (start + i) % n;
                if (!_speakingUntil.TryGetValue(s, out float until) || until < Time.time) { seat = s; break; }
            }
            _speakingUntil[seat] = Time.time + seconds;
            var t = _seated[seat] != null ? _seated[seat].transform : null;
            Vector3 last = t != null ? t.position + new Vector3(0, 1.1f, 0) : _player.position + Vector3.up * 2.6f;
            return () =>
            {
                if (t != null && t.gameObject.activeInHierarchy) last = t.position + new Vector3(0, 1.1f, 0);
                return last; // the rider got off: the bubble stays where they were
            };
        }

        /// <summary>World position of a random seated passenger's head (one-off; bubbles use <see cref="RiderHead"/>).</summary>
        public Vector3 RandomRiderHead()
        {
            int n = Mathf.Min(_route != null ? Mathf.Min(_route.Count, _route.SeatCapacity) : 0, _seated.Count);
            if (n == 0 && _route != null && _route.Hanging > 0 && _hangers.Count > 0 && _hangers[0] != null) return _hangers[0].transform.position + new Vector3(0, 2.0f, 0);
            if (n == 0) return _player.position + new Vector3(0, 2.6f, -1f);
            var s = _seated[Random.Range(0, n)];
            return s != null ? s.transform.position + new Vector3(0, 1.1f, 0) : _player.position + Vector3.up * 2.6f;
        }

        public void UpdateSeats()
        {
            int n = _route != null ? Mathf.Min(_route.Count, _route.SeatCapacity) : 0;
            for (int i = 0; i < _seated.Count; i++)
            {
                if (_seated[i] == null) continue;
                _seated[i].SetActive(i < n);
                if (i < n) ShowLook(i, i < _route.Aboard.Count ? _route.Aboard[i].Kind : RiderKind.Regular);
            }
            int h = _route != null ? _route.Hanging : 0;
            for (int i = 0; i < _hangers.Count; i++) if (_hangers[i] != null) _hangers[i].SetActive(i < h);
        }

        private static string RiderPool(RiderKind k) => "rider_" + k;

        // seat i shows rider i: the generic commuter, or a child model for a personality (bobs with the seat)
        private readonly Dictionary<(int seat, RiderKind kind), GameObject> _seatLooks = new Dictionary<(int, RiderKind), GameObject>();
        private RiderKind[] _seatKind = new RiderKind[0];

        private void ShowLook(int seat, RiderKind kind)
        {
            if (seat >= _seatKind.Length) return;
            var generic = _seated[seat].GetComponent<MeshRenderer>();
            if (generic != null) generic.enabled = kind == RiderKind.Regular;
            if (_seatKind[seat] == kind) return;
            if (_seatLooks.TryGetValue((seat, _seatKind[seat]), out var old) && old != null) old.SetActive(false);
            _seatKind[seat] = kind;
            if (kind == RiderKind.Regular) return;
            if (!_seatLooks.TryGetValue((seat, kind), out var look) || look == null)
            {
                look = ModelLibrary.Instantiate(ModelLibrary.Rider(kind, true), "Seated" + seat + "_" + kind, _seated[seat].transform, castShadows: false);
                _seatLooks[(seat, kind)] = look;
            }
            look.SetActive(true);
        }

        /// <summary>The seated driver is hidden in the driver's-seat view (the camera is in his place).</summary>
        public void SetDriverVisible(bool visible) { if (_driver != null) _driver.SetActive(visible); }

        /// <summary>A rider hanging on the back loses grip (fast hump, crash) and drops off. Returns true if someone fell.</summary>
        public bool SabitFallsOff()
        {
            if (_route == null || _route.Hanging <= 0 || _route.EjectAngry() <= 0) return false;
            int variant = Random.Range(0, PropBuilder.PersonVariants);
            var t = Services.Pools.Get("person" + variant, transform).transform;
            var from = new Vector3(_player.position.x, 0.6f, -3.6f);
            t.position = from;
            t.localRotation = Quaternion.identity;
            float side = _player.position.x >= 0 ? 1f : -1f;
            _flying.Add((t, variant, from, 0f, false, side * (SceneryBuilder.RoadHalfWidth + 1.2f)));
            UpdateSeats();
            return true;
        }
    }
}
