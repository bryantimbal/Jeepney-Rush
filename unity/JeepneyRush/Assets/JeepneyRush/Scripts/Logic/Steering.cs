using System;

namespace JeepneyRush.Logic
{
    /// <summary>How the player steers. Values are stored in the save file – append only.</summary>
    public enum SteerMode { Tap = 0, Tilt = 1, Wheel = 2 }

    /// <summary>
    /// Designer tuning for the jeepney's steering (part of <see cref="BalanceConfig"/>, so it is edited
    /// on the GameBalance ScriptableObject). Player-facing options live in <see cref="SteeringPrefs"/>.
    /// </summary>
    [Serializable]
    public class SteeringTuning
    {
        /// <summary>Seconds for the steering wheel to go from straight to full lock (button / full tilt).</summary>
        public float wheelTime = 0.3f;
        /// <summary>Seconds for the wheel to come back to centre when the input is released.</summary>
        public float returnTime = 0.25f;
        /// <summary>How quickly the jeepney's sideways speed follows the wheel (seconds).</summary>
        public float lateralLag = 0.06f;
        /// <summary>Extra steering at low speed (0.35 = 35% tighter when crawling; never less than the planned lane time).</summary>
        public float lowSpeedBoost = 0.35f;
        /// <summary>How far past the outer lane centres the jeepney may drift onto the shoulder (m).</summary>
        public float shoulder = 0.45f;
        /// <summary>Road-edge assist: 0 = off, 1 = the jeepney eases to a stop at the shoulder and drifts back.</summary>
        public float edgeAssist = 1f;
        /// <summary>Lane-keeping assist (hands off): settle speed per metre of offset and its cap (m/s).</summary>
        public float assistGain = 2.2f, assistMaxSpeed = 1.6f;
    }

    /// <summary>Player steering options (Settings ▸ Controls), persisted in the save.</summary>
    public struct SteeringPrefs
    {
        /// <summary>0.5 .. 1.5 – Low / Medium / High presets are 0.7 / 1 / 1.3.</summary>
        public float Sensitivity;
        /// <summary>0.7 .. 1 – the most steering the input can ask for.</summary>
        public float MaxInput;
        /// <summary>0.5 .. 2 – how fast the wheel returns to centre on release.</summary>
        public float ReturnSpeed;

        public static SteeringPrefs Default => new SteeringPrefs { Sensitivity = 1f, MaxInput = 1f, ReturnSpeed = 1f };
        public SteeringPrefs Clamped() => new SteeringPrefs
        {
            Sensitivity = Clamp(Sensitivity, 0.5f, 1.5f, 1f),
            MaxInput = Clamp(MaxInput, 0.7f, 1f, 1f),
            ReturnSpeed = Clamp(ReturnSpeed, 0.5f, 2f, 1f),
        };
        private static float Clamp(float v, float lo, float hi, float def) => float.IsNaN(v) || v <= 0 ? def : Math.Max(lo, Math.Min(hi, v));
    }

    /// <summary>
    /// One steering system for every input method (buttons, phone tilt, on-screen wheel).
    ///
    /// The input (-1 .. 1) turns a <see cref="Wheel"/> that builds up over
    /// <see cref="SteeringTuning.wheelTime"/> and returns to centre when released. The jeepney's
    /// sideways speed is proportional to the wheel (a little tighter at low speed), so holding a
    /// direction moves it steadily across the lanes – never an ever-increasing turn – and the
    /// road-edge assist eases it to a stop on the shoulder instead of the curb. With no input, the
    /// optional lane-keeping assist settles it into the nearest lane.
    ///
    /// Fairness: at full lock one lane takes <see cref="BalanceConfig.laneChangeDuration"/> at top
    /// speed (wet roads: longer by the wet factor). Settings that slow the steering (lower maximum
    /// input) are reported by <see cref="PlannedLaneTime"/> so the spawner can plan for them.
    /// </summary>
    public sealed class SteeringModel
    {
        /// <summary>Offset from a lane centre (m) beyond which the jeepney counts as being between lanes.</summary>
        public const float BetweenLanes = 0.3f;
        public const float AssistDeadband = 0.03f;
        /// <summary>Default wheel ramp (kept for older callers / tests).</summary>
        public const float WheelTime = 0.3f;

        private readonly BalanceConfig _cfg;
        private SteeringTuning T => _cfg.steering ?? (_cfg.steering = new SteeringTuning());
        public float X { get; private set; }
        public float VelocityX { get; private set; }
        /// <summary>Steering wheel position -1 (full left) .. 1 (full right): what the cockpit wheel and HUD show.</summary>
        public float Wheel { get; private set; }
        /// <summary>Wet / flooded road: steering is slower (multiplies the one-lane time).</summary>
        public float DurationScale = 1f;
        /// <summary>Lane-keeping assist on (default) or off (pure manual driving).</summary>
        public bool Assist = true;
        /// <summary>Forward speed 0 (stopped) .. 1 (top speed) – lower speeds steer a little tighter.</summary>
        public float Speed01 = 1f;
        public SteeringPrefs Prefs = SteeringPrefs.Default;

        public SteeringModel(BalanceConfig cfg) { _cfg = cfg; Reset(); }

        public void Reset() { X = _cfg.LaneX((_cfg.laneCount - 1) / 2); VelocityX = 0; Wheel = 0; _kick = 0; _lastDir = 0; }

        private float Scale => Math.Max(0.5f, DurationScale);
        private float LaneTime => _cfg.laneChangeDuration * Scale;
        private float RampTime => Math.Max(0.05f, T.wheelTime * Scale);
        /// <summary>Sideways speed at full lock and top speed: one lane in exactly the planned time (the wheel ramp included).</summary>
        public float MaxLateralSpeed => _cfg.laneSpacing / Math.Max(0.05f, LaneTime - RampTime * 0.5f - T.lateralLag);
        private float SpeedFactor => 1f + Math.Max(0f, T.lowSpeedBoost) * (1f - Math.Max(0f, Math.Min(1f, Speed01)));

        public float MinX => _cfg.LaneX(0) - T.shoulder;
        public float MaxX => _cfg.LaneX(_cfg.laneCount - 1) + T.shoulder;

        /// <summary>Nearest lane.</summary>
        public int Lane => Math.Max(0, Math.Min(_cfg.laneCount - 1, (int)Math.Round(X / _cfg.laneSpacing + (_cfg.laneCount - 1) * 0.5f)));
        private float Offset => X - _cfg.LaneX(Lane);
        /// <summary>True while the jeepney is between lanes or still moving sideways.</summary>
        public bool Changing => Math.Abs(Offset) > BetweenLanes || Math.Abs(VelocityX) > 0.6f;
        /// <summary>The other lane the jeepney overlaps (the one it is leaving or entering); = Lane when centred.</summary>
        public int FromLane
        {
            get
            {
                float o = Offset;
                int side = Math.Abs(o) > 0.05f ? Math.Sign(o) : Math.Sign(VelocityX);
                int n = Lane + side;
                return n < 0 || n >= _cfg.laneCount ? Lane : n;
            }
        }
        /// <summary>-1 turning left, +1 turning right, 0 straight (turn signals).</summary>
        public int TurnDirection => VelocityX > 1f ? 1 : VelocityX < -1f ? -1 : 0;
        /// <summary>Heading of the jeepney relative to the road (radians, + = right) for a forward speed in m/s.</summary>
        public float Heading(float forwardSpeed) => (float)Math.Atan2(VelocityX, Math.Max(4f, forwardSpeed));

        private float _kick;
        private int _lastDir;
        /// <summary>How far into the next lane (fraction of a lane) a released steer commits to it.</summary>
        public const float CommitFraction = 0.22f;

        private int AssistLane()
        {
            float f = X / _cfg.laneSpacing + (_cfg.laneCount - 1) * 0.5f; // fractional lane index
            int dir = _lastDir != 0 ? _lastDir : Math.Sign(VelocityX);
            int lane;
            if (dir > 0) { int lo = (int)Math.Floor(f); lane = f - lo >= CommitFraction ? lo + 1 : lo; }
            else if (dir < 0) { int hi = (int)Math.Ceiling(f); lane = hi - f >= CommitFraction ? hi - 1 : hi; }
            else lane = (int)Math.Round(f);
            return Math.Max(0, Math.Min(_cfg.laneCount - 1, lane));
        }

        /// <summary>Side-swipe: the jeepney is shoved back the way it came and the wheel straightens.</summary>
        public void BounceBack()
        {
            float dir = Math.Abs(VelocityX) > 0.1f ? -Math.Sign(VelocityX) : -Math.Sign(Offset);
            VelocityX = dir * MaxLateralSpeed * 0.7f;
            Wheel = 0;
            _kick = 0.35f; // the wheel is wrenched back for a moment: input ignored
        }

        /// <param name="input">-1 full left .. +1 full right (0 = hands off).</param>
        public void Tick(float input, float dt)
        {
            if (dt <= 0) return;
            var p = Prefs.Clamped();
            input = Math.Max(-1f, Math.Min(1f, input)) * p.MaxInput;
            if (_kick > 0) { _kick -= dt; input = 0; _lastDir = 0; }
            if (Math.Abs(input) > 0.05f) _lastDir = Math.Sign(input);

            // ---- the steering wheel: builds up toward the input, returns to centre on release
            float toward = (Math.Abs(input) > 1e-4f && (Math.Sign(input) == Math.Sign(Wheel) || Math.Abs(Wheel) < 1e-4f) && Math.Abs(input) >= Math.Abs(Wheel))
                ? dt / RampTime                                           // turning in
                : dt / Math.Max(0.05f, T.returnTime / p.ReturnSpeed);    // easing back (or reversing)
            Wheel += Math.Max(-toward, Math.Min(toward, input - Wheel));

            float vmax = MaxLateralSpeed * SpeedFactor;
            float target = Wheel * vmax;
            if (Math.Abs(input) <= 0.02f && Math.Abs(Wheel) < 0.05f && Assist && _kick <= 0)
            {
                // lane keeping: once the jeepney is a little way (CommitFraction) into the next lane in
                // the direction it was steered, it finishes the lane change (a quick tap = one lane);
                // otherwise it eases back to the lane it came from
                int lane = AssistLane();
                float off = _cfg.LaneX(lane) - X;
                target = Math.Abs(off) < AssistDeadband ? 0f : Math.Max(-T.assistMaxSpeed, Math.Min(T.assistMaxSpeed, off * T.assistGain));
            }

            // ---- road-edge assist: beyond the outer lane centres the outward speed fades out, so
            // holding a direction rolls the jeepney gently onto the shoulder and no further
            if (T.edgeAssist > 0 && T.shoulder > 0)
            {
                float outerL = _cfg.LaneX(0), outerR = _cfg.LaneX(_cfg.laneCount - 1);
                if (target < 0 && X < outerL) target *= Math.Max(0f, 1f - T.edgeAssist * (outerL - X) / T.shoulder);
                if (target > 0 && X > outerR) target *= Math.Max(0f, 1f - T.edgeAssist * (X - outerR) / T.shoulder);
                // hands off on the shoulder: drift back onto the road
                if (Math.Abs(input) <= 0.02f && X < outerL - 0.05f) target = Math.Max(target, 0.8f * T.edgeAssist);
                if (Math.Abs(input) <= 0.02f && X > outerR + 0.05f) target = Math.Min(target, -0.8f * T.edgeAssist);
            }

            float k = 1f - (float)Math.Exp(-dt / Math.Max(0.01f, T.lateralLag));
            VelocityX += (target - VelocityX) * k;
            X += VelocityX * dt;
            if (X < MinX) { X = MinX; if (VelocityX < 0) VelocityX = 0; }
            if (X > MaxX) { X = MaxX; if (VelocityX > 0) VelocityX = 0; }
        }

        /// <summary>Seconds to move one lane from rest at full input (top speed, no assists).</summary>
        public static float OneLaneTime(BalanceConfig cfg, float durationScale = 1f, SteeringPrefs? prefs = null)
        {
            var m = new SteeringModel(cfg) { DurationScale = durationScale, Assist = false, Speed01 = 1f, Prefs = prefs ?? SteeringPrefs.Default };
            float x0 = m.X, t = 0;
            const float dt = 1f / 240f;
            while (m.X - x0 < cfg.laneSpacing && t < 5f) { m.Tick(1f, dt); t += dt; }
            return t;
        }

        /// <summary>
        /// The lane-change time the spawner must plan with for these player settings: never shorter
        /// than <see cref="BalanceConfig.laneChangeDuration"/>, longer when the player caps the steering.
        /// </summary>
        public static float PlannedLaneTime(BalanceConfig cfg, SteeringPrefs prefs) =>
            Math.Max(cfg.laneChangeDuration, OneLaneTime(cfg, 1f, prefs) + 0.01f);
    }

    /// <summary>
    /// Phone tilt → steering input, like a steering wheel: the phone's ROLL angle (degrees) around
    /// the calibrated neutral, with a dead zone, smoothing and a proportional response. A steady
    /// tilt gives a steady input (never a growing one).
    /// </summary>
    public sealed class TiltSteer
    {
        /// <summary>Roll (degrees) for full lock at sensitivity 1.</summary>
        public const float FullLockDeg = 24f;
        public const float DeadZoneDegDefault = 3.5f;
        /// <summary>Neutral roll (degrees).</summary>
        public float Centre;
        /// <summary>Dead zone around the neutral (degrees): 2 small .. 6 large.</summary>
        public float DeadZone = DeadZoneDegDefault;
        /// <summary>Sensor filter speed (1/s): 7 = very smooth, 14 = normal, 24 = quick.</summary>
        public float Smoothing = 14f;
        private float _smooth, _lastRoll;
        private bool _has;

        public bool Calibrated => _has;
        public void Calibrate(float rollDeg) { if (float.IsNaN(rollDeg)) rollDeg = 0; Centre = rollDeg; _lastRoll = rollDeg; _smooth = 0; _has = true; }

        /// <param name="rollDeg">phone roll in degrees (NaN = phone too flat: keep the last reading)</param>
        /// <param name="sensitivity">0.5 .. 1.5 (Settings)</param>
        public float Input(float rollDeg, float sensitivity, float dt)
        {
            if (float.IsNaN(rollDeg)) rollDeg = _lastRoll;
            _lastRoll = rollDeg;
            if (!_has) Calibrate(rollDeg);
            float d = rollDeg - Centre;
            if (d > 180f) d -= 360f;
            if (d < -180f) d += 360f;
            float dz = Math.Max(0f, Math.Min(12f, DeadZone));
            float full = Math.Max(dz + 4f, FullLockDeg / Math.Max(0.5f, Math.Min(1.5f, sensitivity)));
            float v = Math.Abs(d) <= dz ? 0f : Math.Sign(d) * Math.Min(1f, (Math.Abs(d) - dz) / (full - dz));
            v = Math.Sign(v) * v * v * 0.35f + v * 0.65f; // finer control near the centre
            float k = 1f - (float)Math.Exp(-dt * Math.Max(2f, Smoothing)); // filter sensor jitter
            _smooth += (v - _smooth) * k;
            return Math.Abs(_smooth) < 0.01f ? 0f : _smooth;
        }
    }

    /// <summary>Hold-to-steer LEFT / RIGHT buttons.</summary>
    public static class TapSteer
    {
        public static float Input(bool leftHeld, bool rightHeld) => leftHeld == rightHeld ? 0f : leftHeld ? -1f : 1f;
    }

    /// <summary>
    /// On-screen steering wheel: dragging it around its centre turns it (clockwise = right), the
    /// angle is limited to ±<see cref="MaxAngle"/> and it springs back to centre when released.
    /// Engine-free maths; the UI element feeds pointer positions and draws <see cref="Angle"/>.
    /// </summary>
    public sealed class VirtualWheel
    {
        /// <summary>Largest rotation either way (degrees): 90, 135 or 180.</summary>
        public float MaxAngle = 135f;
        /// <summary>Degrees per second back to centre after release.</summary>
        public float ReturnSpeed = 420f;
        /// <summary>0.5 .. 1.5: full steering is reached at MaxAngle / sensitivity.</summary>
        public float Sensitivity = 1f;
        /// <summary>Touches closer to the centre than this (fraction of the radius) turn by horizontal drag instead.</summary>
        public const float InnerFraction = 0.22f;

        public float Angle { get; private set; }
        public bool Held { get; private set; }
        private float _lastAngle, _lastX;

        /// <summary>Steering input -1 .. 1, proportional to the rotation.</summary>
        public float Input => Math.Max(-1f, Math.Min(1f, Angle / Math.Max(10f, MaxAngle / Math.Max(0.5f, Math.Min(1.5f, Sensitivity)))));

        /// <param name="dx">pointer x minus wheel centre x</param><param name="dy">pointer y minus wheel centre y (y DOWN, as on screen)</param>
        public void Press(float dx, float dy, float radius)
        {
            Held = true;
            _lastAngle = PointerAngle(dx, dy);
            _lastX = dx;
        }

        public void Drag(float dx, float dy, float radius)
        {
            if (!Held) return;
            float a = PointerAngle(dx, dy);
            float delta;
            if (Math.Sqrt(dx * dx + dy * dy) < radius * InnerFraction) delta = (dx - _lastX) / Math.Max(1f, radius) * 90f; // near the hub: sideways drag
            else delta = DeltaAngle(_lastAngle, a);
            _lastAngle = a;
            _lastX = dx;
            Angle = Math.Max(-MaxAngle, Math.Min(MaxAngle, Angle + delta));
        }

        public void Release() => Held = false;

        public void Tick(float dt)
        {
            if (Held || dt <= 0) return;
            float step = ReturnSpeed * dt;
            Angle = Math.Abs(Angle) <= step ? 0f : Angle - Math.Sign(Angle) * step;
        }

        /// <summary>Clockwise angle (degrees) of the pointer around the centre, 0 = straight up.</summary>
        public static float PointerAngle(float dx, float dy) => (float)(Math.Atan2(dx, -dy) * 180.0 / Math.PI);

        /// <summary>Shortest signed difference b - a in degrees (-180 .. 180).</summary>
        public static float DeltaAngle(float a, float b)
        {
            float d = (b - a) % 360f;
            if (d > 180f) d -= 360f;
            if (d < -180f) d += 360f;
            return d;
        }
    }
}
