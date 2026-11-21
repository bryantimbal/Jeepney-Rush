using JeepneyRush.Logic;
using JeepneyRush.Logic.Geometry;
using UnityEngine;

namespace JeepneyRush
{
    /// <summary>
    /// The player's jeepney: lane movement, brake ("special maneuver" on swipe down),
    /// speed modifiers and all the juicy body motion (lean, pitch on slopes and humps,
    /// diesel shake, wet-road fishtail, spinning wheels, damage blink).
    /// Physics: kinematic Rigidbody + box collider.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
    public sealed class PlayerController : MonoBehaviour
    {
        private BalanceConfig _cfg;
        public LaneController Lanes { get; private set; }
        public CollisionHandler Collisions { get; private set; }
        public Transform Body { get; private set; }
        private Transform[] _wheels = new Transform[0];
        private GameObject _bodyModel;
        private float _lean, _pitch, _wheelSpin, _time, _brakeT, _brakeCooldown, _crashSlow = 1f;
        private float _bumpY, _bumpV, _floodSlowT, _slip;
        // turn signals: [0,1] = left front/rear, [2,3] = right front/rear
        private readonly GameObject[] _blinkers = new GameObject[4];
        private int _signalDir;
        private float _signalT, _signalHold;
        private bool _signalOn;
        /// <summary>Plays the relay "tik" (only the gameplay jeepney, not menu previews).</summary>
        public bool SignalSound = true;
        /// <summary>Road slope under the jeepney (radians, + = uphill), set by the session.</summary>
        public float SlopePitch;
        private string _livery;

        public bool Braking => _brakeT > 0 || BrakeHeld;
        /// <summary>Set every frame by the session from the GAS / BRAKE hold controls.</summary>
        public bool GasHeld, BrakeHeld;
        /// <summary>Multiplier applied to the difficulty speed: brake pulse / hold, gas, crash slow-down.</summary>
        public float SpeedMultiplier
        {
            get
            {
                float m = _crashSlow;
                // holding BRAKE is real deceleration to a full stop (handled by the session)
                if (BrakeHeld) { }
                else if (_brakeT > 0) m *= _cfg.brakeMultiplier;
                else if (GasHeld) m *= _cfg.gasMultiplier;
                if (_floodSlowT > 0) m *= _cfg.floodSlowdown;
                return m;
            }
        }

        public void Init(BalanceConfig cfg, string liveryId)
        {
            Configure(cfg);
            if (Body == null)
            {
                Body = new GameObject("Body").transform;
                Body.SetParent(transform, false);
            }
            SetLivery(liveryId);
        }

        /// <summary>Applies a run's effective balance (jeepney stats + garage upgrades).</summary>
        public void Configure(BalanceConfig cfg)
        {
            _cfg = cfg;
            Lanes = new LaneController(cfg);
            var rb = GetComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.None;
            var box = GetComponent<BoxCollider>();
            box.isTrigger = false;
            box.size = new Vector3(cfg.playerHalfWidth * 2, 2.2f, cfg.playerHalfLength * 2);
            box.center = new Vector3(0, 1.1f, 0);
            Collisions = GetComponent<CollisionHandler>();
            if (Collisions == null) Collisions = gameObject.AddComponent<CollisionHandler>();
            Collisions.Init(cfg);
        }

        /// <summary>Swap the paint job (garage) without rebuilding gameplay state.</summary>
        public void SetLivery(string liveryId)
        {
            if (_livery == liveryId && _bodyModel != null) return;
            _livery = liveryId;
            if (_bodyModel != null) Destroy(_bodyModel);
            foreach (var w in _wheels) if (w != null) Destroy(w.gameObject);
            _bodyModel = ModelLibrary.Instantiate(ModelLibrary.Jeepney(liveryId), "Jeepney_" + liveryId, Body);
            BuildBlinkers();
            var wp = JeepneyBuilder.WheelPositions;
            _wheels = new Transform[wp.Length];
            for (int i = 0; i < wp.Length; i++)
            {
                var w = ModelLibrary.Instantiate(ModelLibrary.WheelFor(liveryId), "Wheel" + i, transform);
                w.transform.localPosition = new Vector3(wp[i].X, wp[i].Y, wp[i].Z);
                _wheels[i] = w.transform;
            }
        }

        /// <summary>Amber turn-signal lamps at the four corners of the current body model.</summary>
        private void BuildBlinkers()
        {
            foreach (var bl in _blinkers) if (bl != null) Destroy(bl);
            var bounds = new Bounds();
            bool any = false;
            foreach (var mf in _bodyModel.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;
                var bb = mf.sharedMesh.bounds;
                if (!any) { bounds = bb; any = true; } else bounds.Encapsulate(bb);
            }
            if (!any) bounds = new Bounds(new Vector3(0, 1, 0.2f), new Vector3(2.1f, 2.2f, 6.8f));
            var model = ModelLibrary.Get("blinker", () =>
            {
                var b = new MeshBuilder();
                b.Box("glow", "#FFA000", 0.2f, 0.14f, 0.12f, 0, 0, 0);
                b.Sphere("bubble", "#FFC040", 0.22f, 0, 0, 0, 8); // soft glow halo
                return b.Data;
            });
            float hx = Mathf.Min(bounds.extents.x, 1.15f) - 0.05f;
            var spots = new[]
            {
                new Vector3(-hx, 0.95f, bounds.max.z - 0.35f), new Vector3(-hx, 0.9f, bounds.min.z + 0.25f),
                new Vector3(hx, 0.95f, bounds.max.z - 0.35f), new Vector3(hx, 0.9f, bounds.min.z + 0.25f),
            };
            for (int i = 0; i < 4; i++)
            {
                _blinkers[i] = ModelLibrary.Instantiate(model, "TurnSignal" + i, Body, castShadows: false);
                _blinkers[i].transform.localPosition = spots[i];
                _blinkers[i].SetActive(false);
            }
        }

        /// <summary>Left/right turn signal while changing lanes (and briefly after), ~2.5 blinks per second.</summary>
        private void UpdateSignals(float dt)
        {
            int dir = Lanes.TurnDirection;
            if (dir != 0)
            {
                if (dir != _signalDir) { _signalDir = dir; _signalT = 0; }
                _signalHold = 0.45f;
            }
            else if (_signalHold > 0) _signalHold -= dt;
            else _signalDir = 0;
            bool on = false;
            if (_signalDir != 0)
            {
                _signalT += dt;
                on = (_signalT % 0.4f) < 0.22f;
            }
            if (on != _signalOn)
            {
                _signalOn = on;
                if (SignalSound && Services.Audio != null) Services.Audio.Play(Logic.Audio.Sfx.Blinker, on ? 0.35f : 0.25f, on ? 1f : 0.85f);
            }
            for (int i = 0; i < 4; i++)
            {
                if (_blinkers[i] == null) continue;
                bool side = _signalDir < 0 ? i < 2 : _signalDir > 0 && i >= 2;
                bool show = on && side;
                if (_blinkers[i].activeSelf != show) _blinkers[i].SetActive(show);
            }
        }

        public void ResetRun()
        {
            _signalDir = 0; _signalHold = 0;
            Lanes.Reset();
            _bumpY = _bumpV = _floodSlowT = _slip = SlopePitch = 0;
            Collisions.ResetRun();
            _brakeT = _brakeCooldown = 0;
            _crashSlow = 1f;
            transform.localPosition = new Vector3(Lanes.X, 0, 0);
            transform.localRotation = Quaternion.identity;
            Body.localRotation = Quaternion.identity;
            Body.gameObject.SetActive(true);
        }

        public bool MoveLane(int dir) => Lanes.Move(dir);
        /// <summary>Speed hump / pothole: kick the suspension (0.1 = gentle, 0.4 = hard).</summary>
        public void Bump(float strength)
        {
            _bumpV = Mathf.Max(_bumpV, strength * 9f);
            _pitch -= strength * 10f;
        }

        /// <summary>Driving into flood water: drag slows the jeepney down for a moment.</summary>
        public void Flooded() => _floodSlowT = _cfg.floodSlowTime;
        public bool InFlood => _floodSlowT > 0;

        /// <summary>Road wetness 0..1: slower, slidier lane changes.</summary>
        public void SetWetness(float wetness)
        {
            _slip = wetness;
            Lanes.DurationScale = Logic.WeatherSchedule.LaneChangeFactor(_cfg, wetness) * (_floodSlowT > 0 ? 1.15f : 1f);
        }

        /// <summary>Swipe down: emergency brake.</summary>
        public bool Down()
        {
            if (_brakeCooldown > 0) return false;
            _brakeT = _cfg.brakeDuration;
            _brakeCooldown = _cfg.brakeCooldown;
            return true;
        }

        public void OnCrash(bool sideSwipe)
        {
            if (sideSwipe) Lanes.BounceBack();
            _crashSlow = _cfg.crashSlowdown;
        }

        public void Tick(float dt, float speed)
        {
            _time += dt;
            if (_brakeT > 0) _brakeT -= dt;
            if (_brakeCooldown > 0) _brakeCooldown -= dt;
            _crashSlow = Mathf.MoveTowards(_crashSlow, 1f, dt * 0.6f);
            if (_floodSlowT > 0) _floodSlowT -= dt;
            Lanes.Tick(dt);
            Collisions.Tick(dt);

            transform.localPosition = new Vector3(Lanes.X, 0, 0);
            // whole jeepney follows the slope of the road (nose up uphill)
            transform.localRotation = Quaternion.Euler(-SlopePitch * Mathf.Rad2Deg, 0, 0);
            // visuals ---------------------------------------------------------------
            // suspension spring for humps / potholes
            _bumpV -= (_bumpY * 140f + _bumpV * 9f) * dt;
            _bumpY += _bumpV * dt;
            float targetLean = Mathf.Clamp(-Lanes.VelocityX * 0.35f, -6f, 6f);
            _lean = Mathf.Lerp(_lean, targetLean, 1f - Mathf.Exp(-dt * 14f));
            float targetPitch = Braking && speed > 0.5f ? 2.5f : GasHeld ? -1.5f : 0f; // nose dips while braking, settles when stopped
            _pitch = Mathf.Lerp(_pitch, targetPitch, 1f - Mathf.Exp(-dt * 8f));
            float shake = Mathf.Sin(_time * 9f) * 0.008f + Mathf.Max(0, Mathf.Sin(_time * 3.1f)) * 0.015f;
            // wet road: the rear steps out a little during lane changes (fishtail)
            float fishtail = _slip * Mathf.Clamp(Lanes.VelocityX, -12f, 12f) * 0.55f;
            Body.localPosition = new Vector3(0, shake + Mathf.Max(-0.05f, _bumpY), 0);
            // the nose points into the turn (steering), the body leans gently outward
            Body.localRotation = Quaternion.Euler(_pitch, Lanes.VelocityX * 0.7f + fishtail, _lean);
            UpdateSignals(dt);
            _wheelSpin += speed * dt / JeepneyBuilder.WheelRadius * Mathf.Rad2Deg;
            foreach (var w in _wheels) w.localRotation = Quaternion.Euler(_wheelSpin, 0, 0);
            // invulnerability blink
            // gentle blink: mostly visible, short off-beats at 5 Hz (avoids a strobe/flicker look)
            bool visible = !Collisions.Invulnerable || (Collisions.InvulnerableTime * 5f) % 1f > 0.3f;
            if (_bodyModel.activeSelf != visible) _bodyModel.SetActive(visible);
        }
    }
}
