using System;
using JeepneyRush.Logic;
using JeepneyRush.Logic.Audio;
using NUnit.Framework;

namespace JeepneyRush.Tests
{
    public class WeatherTests
    {
        [Test]
        public void RunStartsSunnyAndRainArrivesLater()
        {
            foreach (EnvironmentKind env in Enum.GetValues(typeof(EnvironmentKind)))
                for (int seed = 1; seed <= 10; seed++)
                {
                    var w = new WeatherSchedule(seed, env);
                    Assert.AreEqual(WeatherKind.Clear, w.KindAt(0));
                    Assert.AreEqual(0f, w.WetnessAt(100));
                    bool wet = false;
                    for (float d = 0; d < 4000; d += 20) wet |= w.KindAt(d) != WeatherKind.Clear;
                    Assert.IsTrue(wet, $"no rain in 4 km (seed {seed}, {env})");
                }
        }

        [Test]
        public void TyphoonsNeverInTheFirst900Metres()
        {
            for (int seed = 1; seed <= 30; seed++)
            {
                var w = new WeatherSchedule(seed, EnvironmentKind.Beach);
                for (float d = 0; d < 900; d += 10) Assert.AreNotEqual(WeatherKind.Typhoon, w.KindAt(d));
            }
        }

        [Test]
        public void WetnessIsSmoothAndBounded()
        {
            var w = new WeatherSchedule(3, EnvironmentKind.Manila);
            float prev = w.WetnessAt(0);
            for (float d = 0; d < 6000; d += 1)
            {
                float v = w.WetnessAt(d);
                Assert.That(v, Is.InRange(0f, 1f));
                Assert.Less(Math.Abs(v - prev), 0.03f, $"wetness jumps at {d} m");
                prev = v;
            }
        }

        [Test]
        public void ScheduleIsDeterministic()
        {
            var a = new WeatherSchedule(9, EnvironmentKind.Provincial);
            var b = new WeatherSchedule(9, EnvironmentKind.Provincial);
            for (float d = 0; d < 5000; d += 37) Assert.AreEqual(a.WetnessAt(d), b.WetnessAt(d));
        }

        [Test]
        public void WetRoadSlowsSteering()
        {
            var c = new BalanceConfig();
            Assert.AreEqual(1f, WeatherSchedule.LaneChangeFactor(c, 0));
            Assert.AreEqual(1.3f, WeatherSchedule.LaneChangeFactor(c, 1), 1e-4);
            Assert.AreEqual(1.3f, WeatherSchedule.LaneChangeFactor(c, 5), 1e-4);
        }

        [Test]
        public void HumpIsSmoothOnlyWhenSlow()
        {
            var c = new BalanceConfig();
            Assert.IsTrue(HumpRules.Smooth(c.SpeedAt(400) * 0.45f, c.SpeedAt(400), c));
            Assert.IsFalse(HumpRules.Smooth(c.SpeedAt(400), c.SpeedAt(400), c));
        }

        [Test]
        public void BrakingAtTheHumpIsSmooth()
        {
            var c = new BalanceConfig();
            float b = c.SpeedAt(400);
            Assert.IsTrue(HumpRules.Smooth(b * 0.85f, b, c, braking: true), "braking just before the hump counts");
            Assert.IsFalse(HumpRules.Smooth(b * 0.85f, b, c, braking: false), "not slowing down = complaints");
            Assert.IsFalse(HumpRules.Smooth(b * 1.3f, b, c, braking: true), "a tap of the brake at full gas is not enough");
        }

        [Test]
        public void PassengersOnlyBoardWhenFullyStoppedInTheZone()
        {
            var c = new BalanceConfig();
            Assert.IsTrue(StopRules.Stopped(0f, c));
            Assert.IsFalse(StopRules.Stopped(3f, c));
            Assert.IsTrue(StopRules.InZone(2f, 1, false, 1, c));
            Assert.IsFalse(StopRules.InZone(2f, 0, false, 1, c), "wrong lane");
            Assert.IsFalse(StopRules.InZone(2f, 1, true, 1, c), "still changing lanes");
            Assert.IsFalse(StopRules.InZone(c.stopZoneLength, 1, false, 1, c), "zone still ahead");
            Assert.Greater(StopRules.LoadTime(new PickupResult { Boarded = 3, Alighted = 2 }, c), StopRules.LoadTime(new PickupResult { Boarded = 1 }, c));
        }

        [Test]
        public void JeepneyCanStopBeforeTheNextZone()
        {
            // even at top speed with gas, the brakes stop the jeepney well within the gap between stops
            var c = new BalanceConfig();
            float top = c.maxSpeed * c.gasMultiplier;
            Assert.Less(StopRules.StoppingDistance(top, c), 60f);
            Assert.Less(StopRules.StoppingDistance(top, c) + c.stopZoneLength, c.stopSpacingMin);
        }

        [Test]
        public void RoadIsFlatAtStartAndHillyLater()
        {
            foreach (EnvironmentKind env in Enum.GetValues(typeof(EnvironmentKind)))
            {
                var r = new RoadShape(4, env);
                Assert.AreEqual(0f, r.RelHeight(0, 100), 1e-4);
                Assert.AreEqual(0f, r.RelLateral(0, 100), 1e-4);
                float maxH = 0, maxSlope = 0;
                for (float d = 400; d < 3000; d += 5)
                {
                    maxH = Math.Max(maxH, Math.Abs(r.RelHeight(d, 60)));
                    maxSlope = Math.Max(maxSlope, Math.Abs(r.Slope(d)));
                }
                Assert.Greater(maxH, 0.8f, $"{env} road has no slopes");
                Assert.Less(maxSlope, 0.12f, $"{env} slope too steep for a jeepney");
            }
        }

        [Test]
        public void RoadUnderTheJeepneyIsAlwaysStraightAhead()
        {
            var r = new RoadShape(2, EnvironmentKind.Beach);
            for (float d = 0; d < 3000; d += 50)
            {
                Assert.AreEqual(0f, r.RelLateral(d, 0), 1e-4);
                Assert.AreEqual(0f, r.RelHeight(d, 0), 1e-4);
                Assert.AreEqual(0f, r.RelYaw(d, 0), 1e-4);
                Assert.Less(Math.Abs(r.RelLateral(d, 10)), 0.3f); // near field nearly straight: lanes still readable
            }
        }

        [Test]
        public void NewSoundsAreAudible()
        {
            foreach (var s in new[] { Sfx.Rev, Sfx.Bump, Sfx.Splash, Sfx.Thunder })
            {
                var b = SoundBank.Make(s);
                float peak = 0; foreach (var x in b) { Assert.IsFalse(float.IsNaN(x)); peak = Math.Max(peak, Math.Abs(x)); }
                Assert.Greater(peak, 0.3f, s.ToString());
            }
            var rain = SoundBank.RainLoop();
            Assert.Greater(rain.Length, Synth.Rate);
            Assert.Less(Math.Abs(rain[0] - rain[rain.Length - 1]), 0.35f);
        }
    }
}
