using System;

namespace JeepneyRush.Logic
{
    /// <summary>How the player steers: hold the left/right half of the screen, or tilt the phone.</summary>
    public enum SteerMode { Tap = 0, Tilt = 1 }

    /// <summary>
    /// Analog steering: the jeepney has a real sideways position and speed. Steering input
    /// (-1 .. 1) turns the "wheel" – the sideways speed builds up and eases off smoothly instead
    /// of snapping between lanes. With no input a gentle lane-keeping assist settles the
    /// jeepney into the nearest lane (so PARA stops and gas stations line up).
    ///
    /// Fairness: at full lock one lane is crossed in <see cref="BalanceConfig.laneChangeDuration"/>
    /// (times the wet-road factor) – the same time the spawner plans with, so every row it
    /// places stays passable.
    /// </summary>
    public sealed class SteeringModel
    {
        /// <summary>Seconds for the wheel to go from straight to full lock (and back).</summary>
        public const float WheelTime = 0.3f;
        /// <summary>How far past the outer lane centres the jeepney may drift (onto the shoulder).</summary>
        public const float Shoulder = 0.45f;
        /// <summary>Lane-keeping assist: settle speed (m/s per m of offset) and the offset below which it lets go.</summary>
        public const float AssistGain = 2.2f, AssistMaxSpeed = 1.6f, AssistDeadband = 0.03f;
        /// <summary>Offset from a lane centre (m) beyond which the jeepney counts as being between lanes.</summary>
        public const float BetweenLanes = 0.3f;

        private readonly BalanceConfig _cfg;
        public float X { get; private set; }
        public float VelocityX { get; private set; }
        /// <summary>Wet / flooded road: steering is slower (multiplies the one-lane time).</summary>
        public float DurationScale = 1f;
        /// <summary>Lane-keeping assist on (default) or off (pure manual driving).</summary>
        public bool Assist = true;

        public SteeringModel(BalanceConfig cfg) { _cfg = cfg; Reset(); }

        public void Reset() { X = _cfg.LaneX((_cfg.laneCount - 1) / 2); VelocityX = 0; _kick = 0; }

        /// <summary>Top sideways speed at full lock so that one lane takes exactly the planned time (including the wheel ramp).</summary>
        public float MaxLateralSpeed => _cfg.laneSpacing / Math.Max(0.05f, LaneTime - WheelTime * Scale * 0.5f);
        private float Scale => Math.Max(0.5f, DurationScale);
        private float LaneTime => _cfg.laneChangeDuration * Scale;
        private float Accel => MaxLateralSpeed / (WheelTime * Scale);

        public float MinX => _cfg.LaneX(0) - Shoulder;
        public float MaxX => _cfg.LaneX(_cfg.laneCount - 1) + Shoulder;

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

        private float _kick;

        /// <summary>Side-swipe: the jeepney is shoved back the way it came.</summary>
        public void BounceBack()
        {
            float dir = Math.Abs(VelocityX) > 0.1f ? -Math.Sign(VelocityX) : -Math.Sign(Offset);
            VelocityX = dir * MaxLateralSpeed * 0.7f;
            _kick = 0.35f; // the wheel is wrenched back for a moment: input ignored
        }

        /// <param name="input">-1 full left .. +1 full right (0 = hands off).</param>
        public void Tick(float input, float dt)
        {
            if (dt <= 0) return;
            input = Math.Max(-1f, Math.Min(1f, input));
            if (_kick > 0) { _kick -= dt; input = 0; }
            float vmax = MaxLateralSpeed;
            float target;
            if (Math.Abs(input) > 0.02f) target = input * vmax;
            else if (Assist && _kick <= 0)
            {
                // lane keeping: ease toward the nearest lane centre (the lane we're heading into when moving)
                int lane = Lane;
                if (Math.Abs(VelocityX) > 1.5f)
                {
                    int ahead = Lane + Math.Sign(VelocityX);
                    float stop = VelocityX * VelocityX / (2f * Accel);
                    if (ahead >= 0 && ahead < _cfg.laneCount && Math.Abs(_cfg.LaneX(ahead) - X) < stop + _cfg.laneSpacing * 0.5f && Math.Sign(_cfg.LaneX(ahead) - X) == Math.Sign(VelocityX)) lane = ahead;
                }
                float off = _cfg.LaneX(lane) - X;
                target = Math.Abs(off) < AssistDeadband ? 0f : Math.Max(-AssistMaxSpeed, Math.Min(AssistMaxSpeed, off * AssistGain));
            }
            else target = 0f;
            float a = Accel * dt;
            VelocityX = VelocityX + Math.Max(-a, Math.Min(a, target - VelocityX));
            X += VelocityX * dt;
            if (X < MinX) { X = MinX; if (VelocityX < 0) VelocityX = 0; }
            if (X > MaxX) { X = MaxX; if (VelocityX > 0) VelocityX = 0; }
        }

        /// <summary>Seconds to move one lane from rest at full lock (used by tests and the HUD hint).</summary>
        public static float OneLaneTime(BalanceConfig cfg, float durationScale = 1f)
        {
            var m = new SteeringModel(cfg) { DurationScale = durationScale, Assist = false };
            float x0 = m.X, t = 0;
            const float dt = 1f / 240f;
            while (m.X - x0 < cfg.laneSpacing && t < 5f) { m.Tick(1f, dt); t += dt; }
            return t;
        }
    }

    /// <summary>Phone tilt → steering input: calibrated centre, dead zone, smooth response.</summary>
    public sealed class TiltSteer
    {
        /// <summary>Gravity x (g) for full lock at sensitivity 1 – about 20° of tilt.</summary>
        public const float FullLockG = 0.34f;
        public const float DeadZoneG = 0.035f;
        public float Centre;
        private float _smooth;
        private bool _has;

        public void Calibrate(float gx) { Centre = gx; _smooth = 0; _has = true; }

        /// <param name="gx">accelerometer x in g (portrait: + = tilted right).</param>
        /// <param name="sensitivity">0.5 .. 1.5 (Settings).</param>
        public float Input(float gx, float sensitivity, float dt)
        {
            if (!_has) Calibrate(gx);
            float d = gx - Centre;
            float full = FullLockG / Math.Max(0.5f, Math.Min(1.5f, sensitivity));
            float v = Math.Abs(d) <= DeadZoneG ? 0f : Math.Sign(d) * Math.Min(1f, (Math.Abs(d) - DeadZoneG) / (full - DeadZoneG));
            v = Math.Sign(v) * v * v * 0.35f + v * 0.65f; // finer control near the centre
            float k = 1f - (float)Math.Exp(-dt * 14f);     // filter sensor jitter
            _smooth += (v - _smooth) * k;
            return Math.Abs(_smooth) < 0.01f ? 0f : _smooth;
        }
    }

    /// <summary>Hold-to-steer touch zones: left half steers left, right half steers right.</summary>
    public static class TapSteer
    {
        /// <param name="leftHeld">a finger is down on the left half (not on a button)</param>
        /// <param name="rightHeld">a finger is down on the right half (not on a button)</param>
        public static float Input(bool leftHeld, bool rightHeld) => leftHeld == rightHeld ? 0f : leftHeld ? -1f : 1f;
    }
}
