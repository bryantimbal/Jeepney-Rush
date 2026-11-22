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
        private bool _soundSystem;
        private readonly System.Collections.Generic.List<GameObject> _disco = new System.Collections.Generic.List<GameObject>();
        private int _discoBeat = int.MinValue;
        // headlights: a soft beam on the road (all phones) + a real spot light on medium/high quality
        private GameObject _beam;
        // driver's-seat view: cockpit extras, steering wheel, dash speedometer, rosary
        private GameObject _cockpit;
        private Transform _wheelT, _needleT, _rosaryT;
        private readonly System.Collections.Generic.List<(Renderer r, Material[] mats)> _glassSwap = new System.Collections.Generic.List<(Renderer, Material[])>();
        private float _gaugeKmh, _gaugeShown;
        // rain on the windshield + wipers (driver view)
        private Transform _wiperL, _wiperR;
        private float _wet, _wipePhase, _dropsL, _dropsR, _dropSlide;
        private bool _wipersOn;
        public bool DriverView { get; private set; }
        private CockpitLayout _layout;
        private int _mirrorRiders = -1;
        /// <summary>The driver's eye for this jeepney (body coordinates) – the camera sits here in driver view.</summary>
        public Vector3 DriverEye => new Vector3(_layout.Eye.X, _layout.Eye.Y, _layout.Eye.Z);
        private Light _headlight;

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
            BuildDisco();
            BuildHeadlights();
            BuildCockpit();
            var wp = JeepneyBuilder.WheelPositions;
            _wheels = new Transform[wp.Length];
            for (int i = 0; i < wp.Length; i++)
            {
                var w = ModelLibrary.Instantiate(ModelLibrary.WheelFor(liveryId), "Wheel" + i, transform);
                w.transform.localPosition = new Vector3(wp[i].X, wp[i].Y, wp[i].Z);
                _wheels[i] = w.transform;
            }
        }

        private void BuildCockpit()
        {
            if (_cockpit != null) Destroy(_cockpit);
            _glassSwap.Clear();
            var livery = JeepneyCatalog.Get(_livery);
            _layout = CockpitBuilder.Layout(livery);
            _mirrorRiders = -1;
            _cockpit = ModelLibrary.Instantiate(ModelLibrary.Get("cockpit_" + _livery, () => CockpitBuilder.Extras(livery)), "Cockpit", Body, castShadows: false);
            // the clean open cab (hood, thin pillars, low dash) replaces the full body in this view
            ModelLibrary.Instantiate(ModelLibrary.Get("cockpit_shell_" + _livery, () => CockpitBuilder.Shell(livery)), "CabShell", _cockpit.transform, castShadows: false);
            var wheelPivot = new GameObject("WheelPivot").transform;
            wheelPivot.SetParent(_cockpit.transform, false);
            var wp = _layout.WheelPos;
            wheelPivot.localPosition = new Vector3(wp.X, wp.Y, wp.Z);
            wheelPivot.localRotation = Quaternion.Euler(CockpitBuilder.WheelTiltDeg, 0, 0);
            _wheelT = ModelLibrary.Instantiate(ModelLibrary.Get("steering_wheel", CockpitBuilder.SteeringWheel), "SteeringWheel", wheelPivot, castShadows: false).transform;
            var gp = _layout.GaugePos;
            var gauge = ModelLibrary.Instantiate(ModelLibrary.Get("dash_gauge", CockpitBuilder.GaugeFace), "DashGauge", _cockpit.transform, castShadows: false).transform;
            gauge.localPosition = new Vector3(gp.X, gp.Y, gp.Z);
            gauge.localRotation = Quaternion.Euler(CockpitBuilder.GaugeTiltDeg, 0, 0);
            _needleT = ModelLibrary.Instantiate(ModelLibrary.Get("dash_needle", CockpitBuilder.GaugeNeedle), "Needle", gauge, castShadows: false).transform;
            ModelLibrary.Instantiate(ModelLibrary.Get("windshield_drops_" + (_layout.Modern ? "modern" : "classic"), () => CockpitBuilder.WindshieldDrops(livery)), "WindshieldRain", _cockpit.transform, castShadows: false);
            _wiperL = AddWiper(true);
            _wiperR = AddWiper(false);
            var mp = _layout.MirrorPos;
            _rosaryT = ModelLibrary.Instantiate(ModelLibrary.Get("rosary", CockpitBuilder.Rosary), "Rosary", _cockpit.transform, castShadows: false).transform;
            _rosaryT.localPosition = new Vector3(mp.X - 0.05f, mp.Y - 0.035f, mp.Z - 0.01f);
            // remember the windshield materials so the glass can turn see-through from inside
            foreach (var r in _bodyModel.GetComponentsInChildren<Renderer>()) _glassSwap.Add((r, r.sharedMaterials));
            ApplyDriverView();
        }

        private Transform AddWiper(bool left)
        {
            var p = CockpitBuilder.WiperPivot(_layout, left);
            var w = ModelLibrary.Instantiate(ModelLibrary.Get("wiper", CockpitBuilder.Wiper), left ? "WiperL" : "WiperR", _cockpit.transform, castShadows: false).transform;
            w.localPosition = new Vector3(p.X, p.Y, p.Z);
            return w;
        }

        /// <summary>Rain on the windshield (0 dry .. 1 typhoon): drops build up and the wipers run.</summary>
        public void SetRain(float wetness) => _wet = Mathf.Clamp01(wetness);

        private void UpdateRainAndWipers(float dt)
        {
            // drops gather on the glass (faster in a storm) and slowly creep down
            float rate = _wet < 0.05f ? -0.6f : 0.25f + 1.1f * _wet;
            _dropsL = Mathf.Clamp(_dropsL + rate * dt, 0f, 0.95f);
            _dropsR = Mathf.Clamp(_dropsR + rate * dt * 0.93f, 0f, 0.95f);
            // wipers: on in rain, faster in a typhoon, park at rest when it stops
            _wipersOn = _wet > 0.08f || (_wipersOn && _wipePhase % 1f > 0.02f && _wipePhase % 1f < 0.98f);
            float period = _wet > 0.7f ? 0.95f : 1.7f;
            float before = _wipePhase % 1f;
            if (_wipersOn) _wipePhase += dt / period;
            else _wipePhase = 0f;
            float k = _wipePhase % 1f;
            float sweep = 0.5f - 0.5f * Mathf.Cos(k * Mathf.PI * 2f);      // 0 → 1 → 0 over one cycle
            float angle = CockpitBuilder.WiperSweepDeg * sweep;
            // seen from the driver's seat a positive Z rotation is counter-clockwise: up and over to the left
            if (_wiperL != null) _wiperL.localRotation = Quaternion.Euler(0, 0, angle);
            if (_wiperR != null) _wiperR.localRotation = Quaternion.Euler(0, 0, angle);
            // the blades clear the glass as they pass the top of the stroke
            if (_wipersOn && before < 0.5f && k >= 0.5f)
            {
                _dropsL = 0.05f; _dropsR = 0.08f;
                if (DriverView && SignalSound && Services.Audio != null) Services.Audio.Play(Logic.Audio.Sfx.Wiper, 0.35f, Random.Range(0.95f, 1.05f));
            }
            _dropSlide += dt * (0.015f + 0.03f * _wet);
            // unlit drops would glow at night: dim them with the darkness
            float light = Mathf.Lerp(1f, 0.38f, MaterialLibrary.Night);
            MaterialLibrary.SetTint("utexa:windshield_drops_a", new Color(light, light, light, _dropsL));
            MaterialLibrary.SetTint("utexa:windshield_drops_b", new Color(light, light, light, _dropsR));
            MaterialLibrary.SetOffset("utexa:windshield_drops_a", new Vector2(0, _dropSlide));
            MaterialLibrary.SetOffset("utexa:windshield_drops_b", new Vector2(0.37f, _dropSlide * 1.13f));
        }

        /// <summary>Driver's-seat view on/off: cockpit visible, windshield see-through from inside.</summary>
        public void SetDriverView(bool on)
        {
            DriverView = on;
            ApplyDriverView();
        }

        private void ApplyDriverView()
        {
            if (_cockpit != null && _cockpit.activeSelf != DriverView) _cockpit.SetActive(DriverView);
            if (_bodyModel != null && _bodyModel.activeSelf == DriverView) _bodyModel.SetActive(!DriverView); // the full body would crowd the view
            var clear = MaterialLibrary.ForKey("glass_clear");
            foreach (var (r, mats) in _glassSwap)
            {
                if (r == null) continue;
                if (!DriverView) { r.sharedMaterials = mats; continue; }
                var swapped = (Material[])mats.Clone();
                for (int i = 0; i < swapped.Length; i++) if (swapped[i] != null && swapped[i].name == "JR_glass") swapped[i] = clear;
                r.sharedMaterials = swapped;
            }
        }

        /// <summary>The rear-view mirror shows as many riders as are aboard (0–10).</summary>
        public void SetMirrorRiders(int n)
        {
            n = Mathf.Clamp(n, 0, 10);
            if (n == _mirrorRiders || !DriverView) return;
            _mirrorRiders = n;
            MaterialLibrary.SetTexture("utex:mirror_view_0", "mirror_view_" + n);
        }

        /// <summary>Speed for the dashboard speedometer (km/h).</summary>
        public void SetGauge(float kmh) => _gaugeKmh = kmh;

        private void UpdateCockpit(float dt)
        {
            if (!DriverView || _cockpit == null) return;
            UpdateRainAndWipers(dt);
            // steering wheel follows the lane change. Seen from the driver's seat (looking +Z), a positive
            // Z rotation turns counter-clockwise, so steering right (+X) needs a negative angle.
            float steer = Mathf.Clamp(-Lanes.VelocityX * 12f, -110f, 110f);
            _wheelT.localRotation = Quaternion.Euler(0, 0, steer);
            // old gauges lag and shake a little
            _gaugeShown = Mathf.Lerp(_gaugeShown, _gaugeKmh, 1f - Mathf.Exp(-dt * 4f));
            float wobble = _gaugeShown > 2f ? Mathf.Sin(_time * 23f) * 0.9f + Mathf.Sin(_time * 7.3f) * 0.6f : 0f;
            // NeedleAngle is clockwise-positive (as on the HUD); the 3D needle needs the opposite sign
            _needleT.localRotation = Quaternion.Euler(0, 0, -Logic.Speedometer.NeedleAngle(_gaugeShown + wobble));
            // rosary swings with turns, braking and bumps
            float swayZ = Mathf.Clamp(Lanes.VelocityX * 3.5f, -25f, 25f) + Mathf.Sin(_time * 2.1f) * 3f;
            float swayX = Mathf.Clamp(_pitch * 4f + _bumpV * 20f, -30f, 30f);
            _rosaryT.localRotation = Quaternion.Slerp(_rosaryT.localRotation, Quaternion.Euler(swayX, 0, swayZ), 1f - Mathf.Exp(-dt * 5f));
        }

        private void BuildHeadlights()
        {
            if (_beam == null)
            {
                var model = ModelLibrary.Get("headlight_beam", () =>
                {
                    var b = new MeshBuilder();
                    // 9 m wide, from 3.5 m to 27 m ahead of the jeepney, just above the road
                    b.Quad("beam", new V3(-4.5f, 0.06f, 3.5f), new V3(-4.5f, 0.06f, 27f), new V3(4.5f, 0.06f, 27f), new V3(4.5f, 0.06f, 3.5f), V3.Up,
                        new V2(0, 0), new V2(0, 1), new V2(1, 1), new V2(1, 0));
                    return b.Data;
                });
                _beam = ModelLibrary.Instantiate(model, "HeadlightBeam", transform, castShadows: false);
            }
            if (_headlight == null && GraphicsQuality.Level >= 1)
            {
                var go = new GameObject("Headlight");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(0, 1.3f, 3.2f);
                go.transform.localRotation = Quaternion.Euler(14f, 0, 0);
                _headlight = go.AddComponent<Light>();
                _headlight.type = LightType.Spot;
                _headlight.spotAngle = 70f;
                _headlight.range = 34f;
                _headlight.color = new Color(1f, 0.95f, 0.82f);
                _headlight.shadows = LightShadows.None;
                _headlight.intensity = 0;
                _headlight.enabled = false;
            }
        }

        private void UpdateHeadlights()
        {
            float n = MaterialLibrary.Night;
            if (_beam != null && _beam.activeSelf != n > 0.02f) _beam.SetActive(n > 0.02f);
            if (_headlight != null)
            {
                bool on = n > 0.05f;
                if (_headlight.enabled != on) _headlight.enabled = on;
                _headlight.intensity = n * 3.2f;
            }
        }

        /// <summary>Patok jeepneys: coloured disco lights under the roof that flash to the sound trip.</summary>
        private void BuildDisco()
        {
            foreach (var g in _disco) if (g != null) Destroy(g);
            _disco.Clear();
            _soundSystem = JeepneyCatalog.Get(_livery).SoundSystem;
            if (!_soundSystem) return;
            string[] cols = { "#FF2E4D", "#00E5FF", "#FFC300", "#7B2FF7", "#18FF6D", "#FF4FD8" };
            float[] zs = { 0.3f, -0.6f, -1.5f, -2.4f };
            int k = 0;
            foreach (float z in zs)
                foreach (float x in new[] { -0.45f, 0.45f })
                {
                    string col = cols[k % cols.Length];
                    var model = ModelLibrary.Get("disco_" + col, () =>
                    {
                        var b = new MeshBuilder();
                        b.Sphere("glow", col, 0.07f, 0, 0, 0, 8);
                        b.Sphere("bubble", col, 0.16f, 0, 0, 0, 8);
                        return b.Data;
                    });
                    var g = ModelLibrary.Instantiate(model, "Disco" + k, Body, castShadows: false);
                    g.transform.localPosition = new Vector3(x, JeepneyBuilder.RoofY - 0.12f, z);
                    _disco.Add(g);
                    k++;
                }
        }

        private void UpdateDisco()
        {
            if (_disco.Count == 0) return;
            int beat = Services.Audio != null ? Services.Audio.SoundTripBeat : -1;
            // no music (menu preview, sound trip off): slow colour chase
            if (beat < 0) beat = Mathf.FloorToInt(_time * 2f);
            if (beat == _discoBeat) return;
            _discoBeat = beat;
            for (int i = 0; i < _disco.Count; i++)
            {
                bool on = ((i + beat) % 3) != 0;
                if (_disco[i].activeSelf != on) _disco[i].SetActive(on);
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
            // patok sound trip: the whole body thumps with the bass
            float bass = _soundSystem && Services.Audio != null ? Services.Audio.SoundTripPulse * 0.035f : 0f;
            Body.localPosition = new Vector3(0, shake + bass + Mathf.Max(-0.05f, _bumpY), 0);
            UpdateDisco();
            UpdateHeadlights();
            UpdateCockpit(dt);
            // the nose points into the turn (steering), the body leans gently outward
            Body.localRotation = Quaternion.Euler(_pitch, Lanes.VelocityX * 0.7f + fishtail, _lean);
            UpdateSignals(dt);
            _wheelSpin += speed * dt / JeepneyBuilder.WheelRadius * Mathf.Rad2Deg;
            foreach (var w in _wheels) w.localRotation = Quaternion.Euler(_wheelSpin, 0, 0);
            // invulnerability blink
            // gentle blink: mostly visible, short off-beats at 5 Hz (avoids a strobe/flicker look)
            bool visible = !Collisions.Invulnerable || (Collisions.InvulnerableTime * 5f) % 1f > 0.3f;
            visible &= !DriverView; // in the driver's seat the body is replaced by the cab shell
            if (_bodyModel.activeSelf != visible) _bodyModel.SetActive(visible);
        }
    }
}
