using JeepneyRush.Logic;
using JeepneyRush.Logic.Geometry;
using UnityEngine;

namespace JeepneyRush
{
    /// <summary>
    /// The player's jeepney: lane movement, jump, brake ("special maneuver" on swipe
    /// down), speed modifiers and all the juicy body motion (lean, pitch, diesel shake,
    /// spinning wheels, damage blink). Physics: kinematic Rigidbody + box collider.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
    public sealed class PlayerController : MonoBehaviour
    {
        private BalanceConfig _cfg;
        public LaneController Lanes { get; private set; }
        public JumpController Jumper { get; private set; }
        public CollisionHandler Collisions { get; private set; }
        public Transform Body { get; private set; }
        private Transform[] _wheels = new Transform[0];
        private GameObject _bodyModel;
        private float _lean, _pitch, _wheelSpin, _time, _brakeT, _brakeCooldown, _crashSlow = 1f;
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
                if (BrakeHeld) m *= _cfg.brakeHoldMultiplier;
                else if (_brakeT > 0) m *= _cfg.brakeMultiplier;
                else if (GasHeld) m *= _cfg.gasMultiplier;
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
            Jumper = new JumpController(cfg);
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
            var wp = JeepneyBuilder.WheelPositions;
            _wheels = new Transform[wp.Length];
            for (int i = 0; i < wp.Length; i++)
            {
                var w = ModelLibrary.Instantiate(ModelLibrary.WheelFor(liveryId), "Wheel" + i, transform);
                w.transform.localPosition = new Vector3(wp[i].X, wp[i].Y, wp[i].Z);
                _wheels[i] = w.transform;
            }
        }

        public void ResetRun()
        {
            Lanes.Reset();
            Jumper.Reset();
            Collisions.ResetRun();
            _brakeT = _brakeCooldown = 0;
            _crashSlow = 1f;
            transform.localPosition = new Vector3(Lanes.X, 0, 0);
            transform.localRotation = Quaternion.identity;
            Body.localRotation = Quaternion.identity;
            Body.gameObject.SetActive(true);
        }

        public bool MoveLane(int dir) => Lanes.Move(dir);
        public bool Jump() => Jumper.Jump();

        /// <summary>Swipe down: emergency brake on the ground, slam down in the air.</summary>
        public bool Down()
        {
            if (!Jumper.Grounded) return Jumper.FastFall();
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

        /// <summary>Returns true on the landing frame.</summary>
        public bool Tick(float dt, float speed)
        {
            _time += dt;
            if (_brakeT > 0) _brakeT -= dt;
            if (_brakeCooldown > 0) _brakeCooldown -= dt;
            _crashSlow = Mathf.MoveTowards(_crashSlow, 1f, dt * 0.6f);
            Lanes.Tick(dt);
            bool landed = Jumper.Tick(dt);
            Collisions.Tick(dt);

            transform.localPosition = new Vector3(Lanes.X, Jumper.Y, 0);
            // visuals ---------------------------------------------------------------
            float targetLean = Mathf.Clamp(-Lanes.VelocityX * 0.7f, -12f, 12f);
            _lean = Mathf.Lerp(_lean, targetLean, 1f - Mathf.Exp(-dt * 14f));
            float targetPitch = Jumper.Grounded ? (Braking ? 2.5f : GasHeld ? -1.5f : 0f) : -Jumper.Phase * 6f;
            if (landed) _pitch = 3f;
            _pitch = Mathf.Lerp(_pitch, targetPitch, 1f - Mathf.Exp(-dt * 8f));
            float shake = Jumper.Grounded ? Mathf.Sin(_time * 38f) * 0.012f + Mathf.Max(0, Mathf.Sin(_time * 7.3f)) * 0.02f : 0f;
            Body.localPosition = new Vector3(0, shake, 0);
            Body.localRotation = Quaternion.Euler(_pitch, Lanes.VelocityX * 0.25f, _lean);
            _wheelSpin += speed * dt / JeepneyBuilder.WheelRadius * Mathf.Rad2Deg;
            foreach (var w in _wheels) w.localRotation = Quaternion.Euler(_wheelSpin, 0, 0);
            // invulnerability blink
            bool visible = !Collisions.Invulnerable || Mathf.FloorToInt(Collisions.InvulnerableTime * 12f) % 2 == 1;
            if (_bodyModel.activeSelf != visible) _bodyModel.SetActive(visible);
            return landed;
        }
    }
}
