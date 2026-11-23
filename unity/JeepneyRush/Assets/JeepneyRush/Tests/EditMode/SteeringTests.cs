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
            Assert.Less(maxJump, m.MaxLateralSpeed * dt / SteeringModel.WheelTime + 1e-3f, "sideways speed never jumps");
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
            t.Calibrate(0.1f); // the phone is held slightly tilted: that's "straight"
            float v = 0;
            for (int i = 0; i < 60; i++) v = t.Input(0.12f, 1f, 1f / 60f);
            Assert.AreEqual(0f, v, "small wobbles inside the dead zone don't steer");
            for (int i = 0; i < 60; i++) v = t.Input(0.1f + TiltSteer.FullLockG + 0.05f, 1f, 1f / 60f);
            Assert.AreEqual(1f, v, 0.02f);
            for (int i = 0; i < 60; i++) v = t.Input(0.1f - 0.2f, 1f, 1f / 60f);
            Assert.That(v, Is.InRange(-0.9f, -0.2f), "half tilt left = partial steering");
            float hi = 0;
            var t2 = new TiltSteer(); t2.Calibrate(0);
            for (int i = 0; i < 60; i++) hi = t2.Input(-0.2f, 1.5f, 1f / 60f);
            Assert.Less(hi, v, "higher sensitivity steers harder for the same tilt");
        }

        [Test]
        public void TapZones()
        {
            Assert.AreEqual(-1f, TapSteer.Input(true, false));
            Assert.AreEqual(1f, TapSteer.Input(false, true));
            Assert.AreEqual(0f, TapSteer.Input(true, true));
            Assert.AreEqual(0f, TapSteer.Input(false, false));
        }
    }
}
