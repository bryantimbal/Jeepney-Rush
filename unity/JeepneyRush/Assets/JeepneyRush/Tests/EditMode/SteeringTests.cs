using System;
using JeepneyRush.Logic;
using NUnit.Framework;

namespace JeepneyRush.Tests
{
    public class SteeringTests
    {
        private static readonly BalanceConfig Cfg = new BalanceConfig();

        [Test]
        public void FullLockCrossesOneLaneInThePlannedTime()
        {
            // the spawner's fairness guarantee assumes exactly this lane-change time
            Assert.AreEqual(Cfg.laneChangeDuration, SteeringModel.OneLaneTime(Cfg), 0.03f);
            // wet road: slower steering, by the same factor the spawner plans with
            float wet = WeatherSchedule.LaneChangeFactor(Cfg, 1f);
            Assert.AreEqual(Cfg.laneChangeDuration * wet, SteeringModel.OneLaneTime(Cfg, wet), 0.04f);
            Assert.Less(SteeringModel.OneLaneTime(Cfg), Cfg.laneChangeDuration + Cfg.laneChangeSlack);
        }

        [Test]
        public void SteeringBuildsUpAndEasesOffSmoothly()
        {
            var m = new SteeringModel(Cfg) { Assist = false };
            const float dt = 1f / 60f;
            float prevV = 0, maxJump = 0;
            for (int i = 0; i < 12; i++) { m.Tick(1f, dt); maxJump = Math.Max(maxJump, Math.Abs(m.VelocityX - prevV)); prevV = m.VelocityX; }
            Assert.Greater(m.VelocityX, 0f);
            Assert.Less(m.VelocityX, m.MaxLateralSpeed, "the wheel takes time to reach full lock (no instant snap)");
            for (int i = 0; i < 60; i++) { m.Tick(0f, dt); maxJump = Math.Max(maxJump, Math.Abs(m.VelocityX - prevV)); prevV = m.VelocityX; }
            Assert.AreEqual(0f, m.VelocityX, 1e-3, "hands off: the turn eases out and the jeepney goes straight");
            Assert.Less(maxJump, m.MaxLateralSpeed * dt / Math.Min(Cfg.steering.wheelTime, Cfg.steering.returnTime) + 1e-3f, "sideways speed never jumps");
        }

        [Test]
        public void ShortTapMovesOnlyALittle()
        {
            var m = new SteeringModel(Cfg) { Assist = false };
            float x0 = m.X;
            for (int i = 0; i < 6; i++) m.Tick(-1f, 1f / 60f);   // a 0.1 s tap
            for (int i = 0; i < 60; i++) m.Tick(0f, 1f / 60f);
            Assert.Less(m.X, x0);
            Assert.Greater(m.X, x0 - Cfg.laneSpacing * 0.5f, "a tap is a nudge, not a full lane");
        }

        [Test]
        public void LaneKeepingAssistSettlesIntoTheNearestLane()
        {
            var m = new SteeringModel(Cfg);
            for (int i = 0; i < 20; i++) m.Tick(1f, 1f / 60f);  // steer right a bit, then let go
            for (int i = 0; i < 240; i++) m.Tick(0f, 1f / 60f);
            Assert.AreEqual(Cfg.LaneX(m.Lane), m.X, 0.05f);
            Assert.IsFalse(m.Changing);
            var manual = new SteeringModel(Cfg) { Assist = false };
            for (int i = 0; i < 20; i++) manual.Tick(1f, 1f / 60f);
            for (int i = 0; i < 240; i++) manual.Tick(0f, 1f / 60f);
            Assert.Greater(Math.Abs(manual.X - Cfg.LaneX(manual.Lane)), 0.1f, "assist off: the jeepney stays where it was steered");
        }

        [Test]
        public void TheJeepneyStaysOnTheRoad()
        {
            var m = new SteeringModel(Cfg);
            for (int i = 0; i < 600; i++) m.Tick(-1f, 1f / 60f);
            Assert.AreEqual(m.MinX, m.X, 1e-4);
            Assert.AreEqual(0, m.Lane);
            for (int i = 0; i < 600; i++) m.Tick(1f, 1f / 60f);
            Assert.AreEqual(m.MaxX, m.X, 1e-4);
            Assert.AreEqual(Cfg.laneCount - 1, m.Lane);
        }

        [Test]
        public void BetweenLanesReportsBothLanesToTheSpawner()
        {
            var m = new SteeringModel(Cfg) { Assist = false };
            while (m.X < Cfg.LaneX(1) + Cfg.laneSpacing * 0.45f) m.Tick(1f, 1f / 120f);
            Assert.IsTrue(m.Changing);
            Assert.AreEqual(1, m.Lane);
            Assert.AreEqual(2, m.FromLane);
            Assert.AreEqual(1, m.TurnDirection);
        }

        [Test]
        public void SideSwipePushesTheJeepneyBack()
        {
            var m = new SteeringModel(Cfg);
            for (int i = 0; i < 15; i++) m.Tick(1f, 1f / 60f);
            m.BounceBack();
            Assert.Less(m.VelocityX, 0f);
            float x = m.X;
            for (int i = 0; i < 6; i++) m.Tick(1f, 1f / 60f); // the wheel is wrenched back: input ignored briefly
            Assert.Less(m.X, x);
        }

        [Test]
        public void TiltHasADeadZoneAndFullLock()
        {
            var t = new TiltSteer();
            t.Calibrate(8f); // the phone is held slightly turned: that's "straight"
            float v = 0;
            for (int i = 0; i < 60; i++) v = t.Input(10f, 1f, 1f / 60f);
            Assert.AreEqual(0f, v, "small wobbles inside the dead zone don't steer");
            for (int i = 0; i < 60; i++) v = t.Input(8f + TiltSteer.FullLockDeg + 3f, 1f, 1f / 60f);
            Assert.AreEqual(1f, v, 0.02f);
            float held = v;
            for (int i = 0; i < 300; i++) v = t.Input(8f + TiltSteer.FullLockDeg + 3f, 1f, 1f / 60f);
            Assert.AreEqual(held, v, 1e-4, "holding the tilt keeps the SAME input (it never grows)");
            for (int i = 0; i < 60; i++) v = t.Input(8f - 12f, 1f, 1f / 60f);
            Assert.That(v, Is.InRange(-0.7f, -0.15f), "half tilt left = partial steering");
            float hi = 0;
            var t2 = new TiltSteer(); t2.Calibrate(0);
            for (int i = 0; i < 60; i++) hi = t2.Input(-12f, 1.5f, 1f / 60f);
            Assert.Less(hi, v, "higher sensitivity steers harder for the same tilt");
            // phone laid flat for a moment (NaN): keeps the last reading instead of jumping
            float before = t2.Input(float.NaN, 1.5f, 1f / 60f);
            Assert.AreEqual(hi, before, 0.05f);
        }

        [Test]
        public void ATapFinishesTheLaneChangeASmallNudgeDoesNot()
        {
            float Run(float tapSeconds)
            {
                var m = new SteeringModel(Cfg);
                for (float t = 0; t < tapSeconds; t += 1f / 60f) m.Tick(1f, 1f / 60f);
                for (int i = 0; i < 300; i++) m.Tick(0f, 1f / 60f);
                return m.X;
            }
            Assert.AreEqual(Cfg.LaneX(1), Run(0.05f), 0.05f, "a brush of the button: back to the lane");
            Assert.AreEqual(Cfg.LaneX(2), Run(0.25f), 0.05f, "a quick tap: one full lane over");
            Assert.AreEqual(Cfg.LaneX(2), Run(0.45f), 0.05f);
        }

        [Test]
        public void TapZones()
        {
            Assert.AreEqual(-1f, TapSteer.Input(true, false));
            Assert.AreEqual(1f, TapSteer.Input(false, true));
            Assert.AreEqual(0f, TapSteer.Input(true, true));
            Assert.AreEqual(0f, TapSteer.Input(false, false));
        }

        [Test]
        public void HoldingADirectionForLongNeverRunsOffTheRoad()
        {
            foreach (int dir in new[] { -1, 1 })
                foreach (float hold in new[] { 0.3f, 1.5f, 10f })
                {
                    var m = new SteeringModel(Cfg);
                    float maxSide = 0, maxV = 0;
                    for (float t = 0; t < hold; t += 1f / 60f)
                    {
                        m.Tick(dir, 1f / 60f);
                        maxSide = Math.Max(maxSide, Math.Abs(m.X));
                        maxV = Math.Max(maxV, Math.Abs(m.VelocityX));
                    }
                    // the jeepney's body (half width) stays on the asphalt: never onto the curb / sidewalk
                    Assert.LessOrEqual(maxSide + Cfg.playerHalfWidth, Logic.Geometry.SceneryBuilder.RoadHalfWidth - 0.2f, $"dir {dir} hold {hold}s");
                    Assert.LessOrEqual(maxV, m.MaxLateralSpeed * (1 + Cfg.steering.lowSpeedBoost) + 1e-3f, "steady, bounded sideways speed (no ever-increasing turn)");
                    if (hold >= 10f) Assert.AreEqual(0f, m.VelocityX, 0.05f, "held at the edge: the jeepney has stopped moving sideways");
                    // let go: it drifts back onto the road and settles in the outer lane
                    for (int i = 0; i < 300; i++) m.Tick(0f, 1f / 60f);
                    if (hold >= 1.5f)
                    {
                        Assert.AreEqual(dir < 0 ? 0 : Cfg.laneCount - 1, m.Lane);
                        Assert.AreEqual(Cfg.LaneX(m.Lane), m.X, 0.08f);
                    }
                }
        }

        [Test]
        public void SteeringIsTighterAtLowSpeedButNeverSlowerThanPlanned()
        {
            float Lane(float speed01)
            {
                var m = new SteeringModel(Cfg) { Assist = false, Speed01 = speed01 };
                float x0 = m.X, t = 0;
                while (m.X - x0 < Cfg.laneSpacing && t < 5) { m.Tick(1f, 1f / 240f); t += 1f / 240f; }
                return t;
            }
            float slow = Lane(0.1f), fast = Lane(1f);
            Assert.Less(slow, fast, "crawling into a stop: tighter steering");
            Assert.LessOrEqual(fast, Cfg.laneChangeDuration + 0.03f, "top speed: exactly the lane time the spawner plans with");
        }

        [Test]
        public void CappedSteeringIsPlannedForBySpawner()
        {
            var capped = new SteeringPrefs { Sensitivity = 1f, MaxInput = 0.8f, ReturnSpeed = 1f };
            float t = SteeringModel.OneLaneTime(Cfg, 1f, capped);
            Assert.Greater(t, SteeringModel.OneLaneTime(Cfg));
            Assert.GreaterOrEqual(SteeringModel.PlannedLaneTime(Cfg, capped), t);
            Assert.AreEqual(Cfg.laneChangeDuration, SteeringModel.PlannedLaneTime(Cfg, SteeringPrefs.Default), 0.04f);
        }

        [Test]
        public void ReturnSpeedSettingChangesHowFastTheWheelStraightens()
        {
            float Settle(float ret)
            {
                var m = new SteeringModel(Cfg) { Assist = false, Prefs = new SteeringPrefs { Sensitivity = 1, MaxInput = 1, ReturnSpeed = ret } };
                for (int i = 0; i < 30; i++) m.Tick(1f, 1f / 60f);
                float t = 0;
                while (Math.Abs(m.Wheel) > 0.01f && t < 3) { m.Tick(0f, 1f / 60f); t += 1f / 60f; }
                return t;
            }
            Assert.Less(Settle(2f), Settle(0.5f));
        }

        [Test]
        public void VirtualWheelTurnsProportionallyAndSpringsBack()
        {
            var w = new VirtualWheel { MaxAngle = 135f, ReturnSpeed = 420f, Sensitivity = 1f };
            const float r = 150f;
            // grab the top of the rim and drag a quarter turn clockwise (screen y grows downward)
            w.Press(0, -r, r);
            for (int i = 1; i <= 30; i++) { double a = i / 30.0 * Math.PI / 2; w.Drag((float)(Math.Sin(a) * r), (float)(-Math.Cos(a) * r), r); }
            Assert.AreEqual(90f, w.Angle, 1f);
            Assert.AreEqual(90f / 135f, w.Input, 0.02f, "steering proportional to the rotation");
            // keep turning: limited at MaxAngle, never unlimited
            for (int i = 1; i <= 60; i++) { double a = Math.PI / 2 + i / 60.0 * Math.PI; w.Drag((float)(Math.Sin(a) * r), (float)(-Math.Cos(a) * r), r); }
            Assert.AreEqual(135f, w.Angle, 0.01f);
            Assert.AreEqual(1f, w.Input, 1e-4);
            // counter-clockwise past centre steers left, without lifting the finger
            for (int i = 1; i <= 90; i++) { double a = Math.PI * 1.5 - i / 90.0 * Math.PI * 1.25; w.Drag((float)(Math.Sin(a) * r), (float)(-Math.Cos(a) * r), r); }
            Assert.Less(w.Input, 0f);
            // held: it stays where it is; released: it eases back to centre
            float held = w.Angle;
            w.Tick(0.2f);
            Assert.AreEqual(held, w.Angle);
            w.Release();
            w.Tick(0.1f);
            Assert.AreEqual(held + 42f, w.Angle, 0.5f, "returns at ReturnSpeed, smoothly");
            for (int i = 0; i < 60; i++) w.Tick(1f / 60f);
            Assert.AreEqual(0f, w.Angle);
            // higher sensitivity: full steering with less rotation
            var s = new VirtualWheel { MaxAngle = 135f, Sensitivity = 1.5f };
            s.Press(0, -r, r);
            for (int i = 1; i <= 30; i++) { double a = i / 30.0 * Math.PI / 2; s.Drag((float)(Math.Sin(a) * r), (float)(-Math.Cos(a) * r), r); }
            Assert.AreEqual(1f, s.Input, 1e-4);
            Assert.AreEqual(-20f, VirtualWheel.DeltaAngle(10f, -10f), 1e-4);
            Assert.AreEqual(20f, VirtualWheel.DeltaAngle(170f, -170f), 1e-4);
        }

        private static readonly SteeringPrefs Buttons = new SteeringPrefs { Sensitivity = 1, MaxInput = 1, ReturnSpeed = 1, LaneStep = true };

        [Test]
        public void StoppedJeepneyOnlyTurnsItsWheels()
        {
            foreach (var prefs in new[] { SteeringPrefs.Default, Buttons })
            {
                var m = new SteeringModel(Cfg) { ForwardSpeed = 0f, Prefs = prefs };
                float x0 = m.X;
                for (int i = 0; i < 120; i++) m.Tick(-1f, 1f / 60f);       // hold LEFT for 2 s at the stop
                Assert.AreEqual(x0, m.X, 1e-4, "no lane change while stopped");
                Assert.Less(m.Wheel, -0.9f, "but the steering wheel / front wheels are turned");
                for (int i = 0; i < 60; i++) m.Tick(0f, 1f / 60f);
                Assert.AreEqual(0f, m.Wheel, 0.02f, "released: wheels straighten");
                Assert.AreEqual(x0, m.X, 1e-4);
                // creeping forward: only a little sideways movement
                var c = new SteeringModel(Cfg) { ForwardSpeed = 1.0f, Prefs = SteeringPrefs.Default };
                for (int i = 0; i < 60; i++) c.Tick(1f, 1f / 60f);
                Assert.Less(c.X - x0, 1.2f);
            }
        }

        [Test]
        public void ButtonsOnePressIsOneLane()
        {
            float Hold(float seconds, int presses)
            {
                var m = new SteeringModel(Cfg) { ForwardSpeed = 12f, Prefs = Buttons };
                for (int p = 0; p < presses; p++)
                {
                    for (float t = 0; t < seconds; t += 1f / 60f) m.Tick(1f, 1f / 60f);
                    for (int i = 0; i < 6; i++) m.Tick(0f, 1f / 60f);       // lift the finger
                }
                for (int i = 0; i < 240; i++) m.Tick(0f, 1f / 60f);
                return m.X;
            }
            Assert.AreEqual(Cfg.LaneX(2), Hold(0.08f, 1), 0.06f, "a quick press: one lane");
            Assert.AreEqual(Cfg.LaneX(2), Hold(3f, 1), 0.06f, "holding: still just one lane");
            Assert.AreEqual(Cfg.LaneX(2), Hold(0.1f, 2), 0.06f, "two presses from the middle: stops at the last lane");
            // never overshoots the lane centre by much (straightens smoothly)
            var m2 = new SteeringModel(Cfg) { ForwardSpeed = 12f, Prefs = Buttons };
            float maxX = m2.X;
            for (int i = 0; i < 240; i++) { m2.Tick(i < 5 ? 1f : 0f, 1f / 60f); maxX = Math.Max(maxX, m2.X); }
            Assert.Less(maxX, Cfg.LaneX(2) + 0.25f);
            // a press while stopped does nothing to the lane, even after driving off
            var s = new SteeringModel(Cfg) { ForwardSpeed = 0f, Prefs = Buttons };
            for (int i = 0; i < 10; i++) s.Tick(1f, 1f / 60f);
            for (int i = 0; i < 5; i++) s.Tick(0f, 1f / 60f);
            s.ForwardSpeed = 10f;
            for (int i = 0; i < 240; i++) s.Tick(0f, 1f / 60f);
            Assert.AreEqual(Cfg.LaneX(1), s.X, 0.05f);
            // the spawner plans with the real one-press lane time
            Assert.GreaterOrEqual(SteeringModel.PlannedLaneTime(Cfg, Buttons), SteeringModel.OneLaneTime(Cfg, 1f, Buttons));
            Assert.Less(SteeringModel.OneLaneTime(Cfg, 1f, Buttons), Cfg.laneChangeDuration + 0.25f, "one press still changes lane briskly");
        }

        [Test]
        public void SlightTiltDoesNotCreepSideways()
        {
            float Drift(bool soft)
            {
                var m = new SteeringModel(Cfg) { ForwardSpeed = 12f, Prefs = new SteeringPrefs { Sensitivity = 1, MaxInput = 1, ReturnSpeed = 1, SoftAssist = soft } };
                for (int i = 0; i < 180; i++) m.Tick(0.12f, 1f / 60f);       // hand wobble just past the dead zone, 3 s
                return Math.Abs(m.X - Cfg.LaneX(1));
            }
            Assert.Less(Drift(true), 0.5f, "tilt: a slight tilt stays in the lane");
            Assert.Greater(Drift(false), Drift(true));
            // a real tilt still changes lanes
            var r = new SteeringModel(Cfg) { ForwardSpeed = 12f, Prefs = new SteeringPrefs { Sensitivity = 1, MaxInput = 1, ReturnSpeed = 1, SoftAssist = true } };
            for (int i = 0; i < 60; i++) r.Tick(0.7f, 1f / 60f);
            Assert.Greater(r.X - Cfg.LaneX(1), 2f);
        }
    }
}
