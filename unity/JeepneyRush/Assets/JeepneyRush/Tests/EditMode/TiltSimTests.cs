using System;
using JeepneyRush.Logic;
using NUnit.Framework;

namespace JeepneyRush.Tests
{
    /// <summary>
    /// Tilt steering, tested with a simulated phone: realistic accelerometer readings (Unity's
    /// convention: g units, upright portrait = (0,-1,0), face-up flat = z -1) for a phone held in
    /// portrait / landscape left / landscape right, upright or leaned back or nearly flat, for both
    /// sensor backends (readings already rotated to the screen, or raw phone axes), with sensor
    /// noise, hand tremor and slow posture drift – fed through the game's real tilt and steering code.
    /// </summary>
    public class TiltSimTests
    {
        private const float Dt = 1f / 60f;
        private static readonly BalanceConfig Cfg = new BalanceConfig();

        private static readonly ScreenLayout.Rotation[] Holds = { ScreenLayout.Rotation.Portrait, ScreenLayout.Rotation.LandscapeLeft, ScreenLayout.Rotation.LandscapeRight };

        /// <summary>
        /// Accelerometer reading for a phone whose screen faces the player, leaned back by
        /// <paramref name="leanDeg"/> and turned like a steering wheel by <paramref name="steerDeg"/>
        /// (+ = right side down). Raw = in the phone's own portrait axes (backend not rotating).
        /// </summary>
        private static (float ax, float ay, float az) Reading(float steerDeg, float leanDeg, ScreenLayout.Rotation r, bool rotated)
        {
            double t = steerDeg * Math.PI / 180, p = leanDeg * Math.PI / 180;
            float sx = (float)(Math.Sin(t) * Math.Cos(p)), sy = (float)(-Math.Cos(t) * Math.Cos(p)), sz = (float)(-Math.Sin(p));
            if (rotated) return (sx, sy, sz);
            switch (r) // invert ScreenLayout.ToScreen (= Unity's CompensateDirection rotation)
            {
                case ScreenLayout.Rotation.LandscapeLeft: return (sy, -sx, sz);
                case ScreenLayout.Rotation.LandscapeRight: return (-sy, sx, sz);
                default: return (sx, sy, sz);
            }
        }

        /// <summary>A phone lying nearly flat, tilted left / right about its long side (how some people play landscape).</summary>
        private static (float ax, float ay, float az) FlatReading(float tiltDeg, float pitchDeg)
        {
            double t = tiltDeg * Math.PI / 180, p = pitchDeg * Math.PI / 180;
            return ((float)(Math.Sin(t)), (float)(-Math.Sin(p) * Math.Cos(t)), (float)(-Math.Cos(p) * Math.Cos(t)));
        }

        private static float Angle((float ax, float ay, float az) a, ScreenLayout.Rotation r, bool rotated) =>
            ScreenLayout.TiltAngleDeg(a.ax, a.ay, a.az, r, rotated);

        private sealed class Noise
        {
            private readonly Random _r;
            public Noise(int seed) { _r = new Random(seed); }
            public float Gauss(float sigma) => (float)(Math.Sqrt(-2 * Math.Log(1 - _r.NextDouble())) * Math.Cos(2 * Math.PI * _r.NextDouble())) * sigma;
        }

        /// <summary>Realistic phone sample: sensor noise ±0.012 g, hand tremor 0.4° at 8 Hz.</summary>
        private static float Sample(float steerDeg, float leanDeg, ScreenLayout.Rotation r, bool rotated, float t, Noise n)
        {
            float tremor = 0.4f * (float)Math.Sin(2 * Math.PI * 8 * t) + n.Gauss(0.15f);
            var a = Reading(steerDeg + tremor, leanDeg, r, rotated);
            return Angle((a.ax + n.Gauss(0.012f), a.ay + n.Gauss(0.012f), a.az + n.Gauss(0.012f)), r, rotated);
        }

        // ------------------------------------------------------------------ the angle itself

        [Test]
        public void AngleIsRightInEveryHoldLeanAndBackend()
        {
            foreach (var r in Holds)
                foreach (bool rotated in new[] { true, false })
                    foreach (float lean in new[] { 0f, 25f, 45f, 60f })
                        foreach (float steer in new[] { -20f, -8f, 0f, 8f, 20f })
                        {
                            float a = Angle(Reading(steer, lean, r, rotated), r, rotated);
                            Assert.AreEqual(steer, a, 0.6f, $"{r} rotated={rotated} lean={lean} steer={steer}");
                        }
        }

        [Test]
        public void NearlyFlatStillWorksAndNeverBlowsUp()
        {
            for (float pitch = 0; pitch <= 30; pitch += 10)
                foreach (float tilt in new[] { -15f, -5f, 0f, 5f, 15f })
                {
                    float a = Angle(FlatReading(tilt, pitch), ScreenLayout.Rotation.Portrait, true);
                    Assert.IsFalse(float.IsNaN(a), "flat phone gives a reading");
                    Assert.AreEqual(Math.Sign(tilt), Math.Sign(Math.Round(a, 3)), $"right direction flat pitch={pitch} tilt={tilt}");
                    Assert.LessOrEqual(Math.Abs(a), Math.Abs(tilt) * 2.05f + 0.5f, "bounded gain when flat (no blow-up)");
                }
        }

        [Test]
        public void ReadingIsContinuousFromUprightToFlat()
        {
            // leaning the phone back (no steering) must never look like a turn
            float prev = Angle(Reading(5, 0, ScreenLayout.Rotation.Portrait, true), ScreenLayout.Rotation.Portrait, true);
            for (float lean = 1; lean <= 89; lean += 1)
            {
                float a = Angle(Reading(5, lean, ScreenLayout.Rotation.Portrait, true), ScreenLayout.Rotation.Portrait, true);
                Assert.Less(Math.Abs(a - prev), 0.6f, $"no jump at lean {lean}");
                prev = a;
            }
        }

        // ------------------------------------------------------------------ the filter + response

        [Test]
        public void HeldStillTheJeepneyDoesNotWander()
        {
            foreach (var r in Holds)
                foreach (bool rotated in new[] { true, false })
                    foreach (float lean in new[] { 10f, 40f, 65f })
                    {
                        var n = new Noise(7);
                        var tilt = new TiltSteer();
                        tilt.Calibrate(Angle(Reading(0, lean, r, rotated), r, rotated));
                        var m = new SteeringModel(Cfg) { Prefs = TiltPrefs, Assist = true, ForwardSpeed = 16f, Speed01 = 0.6f };
                        float x0 = m.X, maxAbs = 0;
                        int nonZero = 0;
                        for (int i = 0; i < 60 * 30; i++)
                        {
                            float s = tilt.Input(Sample(0, lean, r, rotated, i * Dt, n), 1f, Dt);
                            if (s != 0) nonZero++;
                            m.Tick(s, Dt);
                            maxAbs = Math.Max(maxAbs, Math.Abs(m.X - x0));
                        }
                        Assert.AreEqual(0, nonZero, $"{r} rot={rotated} lean={lean}: no steering from sensor noise / tremor");
                        Assert.Less(maxAbs, 0.05f, "stays in its lane for 30 s");
                    }
        }

        [Test]
        public void TurnsQuicklyWithoutOvershoot()
        {
            foreach (var r in Holds)
            {
                var tilt = new TiltSteer();
                tilt.Calibrate(0);
                float target = 0, t90 = -1, peak = 0;
                for (int i = 0; i < 90; i++)
                {
                    float steer = i < 10 ? 0 : 15f;
                    var a = Reading(steer, 30, r, true);
                    float s = tilt.Input(Angle(a, r, true), 1f, Dt);
                    if (i == 89) target = s;
                    peak = Math.Max(peak, s);
                }
                tilt = new TiltSteer();
                tilt.Calibrate(0);
                for (int i = 0; i < 90; i++)
                {
                    float steer = i < 10 ? 0 : 15f;
                    float s = tilt.Input(Angle(Reading(steer, 30, r, true), r, true), 1f, Dt);
                    if (t90 < 0 && s >= target * 0.9f) t90 = (i - 10) * Dt;
                }
                Assert.Greater(target, 0.4f, "a 15° tilt is a solid turn");
                Assert.That(t90, Is.InRange(0f, 0.22f), $"{r}: reaches 90% in {t90:0.00}s (snappy)");
                Assert.LessOrEqual(peak, target + 0.01f, "no overshoot");
            }
        }

        [Test]
        public void SlowSweepIsSmoothAndProportional()
        {
            var tilt = new TiltSteer();
            tilt.Calibrate(0);
            float prev = 0, maxStep = 0;
            float atTen = 0, atTwenty = 0;
            for (int i = 0; i < 60 * 4; i++)
            {
                float steer = 25f * (float)Math.Sin(i * Dt * Math.PI / 4); // 0 → 25° over 2 s, back
                float s = tilt.Input(Angle(Reading(steer, 35, ScreenLayout.Rotation.LandscapeLeft, false), ScreenLayout.Rotation.LandscapeLeft, false), 1f, Dt);
                maxStep = Math.Max(maxStep, Math.Abs(s - prev));
                prev = s;
                if (Math.Abs(steer - 10f) < 0.4f && atTen == 0) atTen = s;
                if (Math.Abs(steer - 20f) < 0.4f && atTwenty == 0) atTwenty = s;
            }
            Assert.Less(maxStep, 0.04f, "no jumps in the steering");
            Assert.Greater(atTwenty, atTen * 1.6f, "more tilt = more steering");
            Assert.Greater(atTen, 0.1f);
        }

        [Test]
        public void LeftIsLeftAndRightIsRightInEveryHold()
        {
            foreach (var r in Holds)
                foreach (bool rotated in new[] { true, false })
                {
                    var tilt = new TiltSteer();
                    tilt.Calibrate(Angle(Reading(0, 30, r, rotated), r, rotated));
                    float s = 0;
                    for (int i = 0; i < 30; i++) s = tilt.Input(Angle(Reading(12, 30, r, rotated), r, rotated), 1f, Dt);
                    Assert.Greater(s, 0.2f, $"{r} rotated={rotated}: right side down steers right");
                    for (int i = 0; i < 60; i++) s = tilt.Input(Angle(Reading(-12, 30, r, rotated), r, rotated), 1f, Dt);
                    Assert.Less(s, -0.2f, $"{r} rotated={rotated}: left side down steers left");
                }
        }

        [Test]
        public void PostureDriftIsAbsorbedButRealTurnsAreNot()
        {
            var tilt = new TiltSteer { DeadZone = 3f };
            tilt.Calibrate(0);
            var n = new Noise(3);
            int nonZero = 0;
            // the hands sink 2.5° over 10 s (inside the dead zone): absorbed, never steers
            for (int i = 0; i < 600; i++)
            {
                float drift = 2.5f * Math.Min(1f, i / 600f);
                if (tilt.Input(Sample(drift, 35, ScreenLayout.Rotation.Portrait, true, i * Dt, n), 1f, Dt) != 0) nonZero++;
            }
            Assert.AreEqual(0, nonZero);
            Assert.AreEqual(2.5f, tilt.Centre, 1.0f, "straight-ahead followed the hands");
            // a real turn held for 3 s keeps steering the whole time
            float s = 0;
            for (int i = 0; i < 180; i++) s = tilt.Input(Sample(2.5f + 14f, 35, ScreenLayout.Rotation.Portrait, true, i * Dt, n), 1f, Dt);
            Assert.Greater(s, 0.35f, "a held turn is never re-centred away");
        }

        // ------------------------------------------------------------------ driving

        private static readonly SteeringPrefs TiltPrefs = new SteeringPrefs { Sensitivity = 1, MaxInput = 1, ReturnSpeed = 1, SoftAssist = true };

        [Test]
        public void TiltAndBackChangesExactlyOneLane()
        {
            foreach (var r in Holds)
                foreach (bool rotated in new[] { true, false })
                {
                    var n = new Noise(11);
                    var tilt = new TiltSteer();
                    tilt.Calibrate(Angle(Reading(0, 35, r, rotated), r, rotated));
                    var m = new SteeringModel(Cfg) { Prefs = TiltPrefs, Assist = true, ForwardSpeed = 16f, Speed01 = 0.6f };
                    int lane0 = m.Lane;
                    for (int i = 0; i < 60 * 4; i++)
                    {
                        float t = i * Dt;
                        float steer = t < 0.5f ? 0 : t < 1.05f ? 14f : 0f; // tilt right ~half a second, then straight
                        m.Tick(tilt.Input(Sample(steer, 35, r, rotated, t, n), 1f, Dt), Dt);
                    }
                    Assert.AreEqual(lane0 + 1, m.Lane, $"{r} rotated={rotated}: one lane to the right");
                    Assert.AreEqual(Cfg.LaneX(lane0 + 1), m.X, 0.12f, "settles in the lane centre");
                    Assert.Less(Math.Abs(m.VelocityX), 0.1f, "and stays there");
                }
        }
    }
}
