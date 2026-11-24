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
                for (float d = 0; d < WeatherSchedule.TyphoonFrom; d += 10) Assert.AreNotEqual(WeatherKind.Typhoon, w.KindAt(d));
            }
        }

        [Test]
        public void MostlyFairWeatherButRainStillComes()
        {
            float wetSum = 0; int runs = 0, typhoons = 0, typhoonRuns = 0;
            foreach (EnvironmentKind env in Enum.GetValues(typeof(EnvironmentKind)))
                for (int seed = 1; seed <= 12; seed++)
                {
                    var w = new WeatherSchedule(seed, env);
                    wetSum += w.WetShare(8000); runs++;
                    bool t = false;
                    float dry = 0, longestDry = 0;
                    for (float d = 0; d < 8000; d += 10)
                    {
                        var k = w.KindAt(d);
                        if (k == WeatherKind.Typhoon) { typhoons++; t = true; }
                        dry = k == WeatherKind.Clear ? dry + 10 : 0;
                        longestDry = Math.Max(longestDry, dry);
                    }
                    if (t) typhoonRuns++;
                    Assert.LessOrEqual(longestDry, WeatherSchedule.MaxDryStretch + 1200f, $"{env} seed {seed}: dry for {longestDry:0} m");
                }
            float share = wetSum / runs;
            Assert.That(share, Is.InRange(0.12f, 0.35f), $"wet share {share:P0}: rain should be occasional");
            Assert.Greater(typhoonRuns, 0, "typhoons still happen sometimes");
            Assert.Less(typhoonRuns, runs / 2, "typhoons are rare");
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
            float zx = c.LaneX(2);
            Assert.IsTrue(StopRules.InZone(2f, zx, zx, c), "parked in the box");
            Assert.IsTrue(StopRules.InZone(0f, (c.LaneX(1) + zx) / 2f, zx, c), "straddling the lane line still counts");
            Assert.IsTrue(StopRules.InZone(c.stopZoneLength / 2f + c.playerHalfLength, zx, zx, c), "front bumper just on the line");
            Assert.IsTrue(StopRules.InZone(-(c.stopZoneLength / 2f + c.playerHalfLength), zx, zx, c), "rolled a bit past");
            Assert.IsFalse(StopRules.InZone(0f, c.LaneX(1), zx, c), "fully in the next lane");
            Assert.IsFalse(StopRules.InZone(c.stopZoneLength + 6f, zx, zx, c), "zone still well ahead");
            Assert.IsFalse(StopRules.OutOfReach(-20f, c), "can still reverse back");
            Assert.IsTrue(StopRules.OutOfReach(-60f, c));
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
                Assert.Less(maxSlope, RoadShape.MaxGrade(env), $"{env} slope too steep for a jeepney");
            }
        }

        [TestCase(EnvironmentKind.Beach)]
        [TestCase(EnvironmentKind.Baguio)]
        [TestCase(EnvironmentKind.Mountain)]
        public void RoadUnderTheJeepneyIsAlwaysStraightAhead(EnvironmentKind env)
        {
            var r = new RoadShape(2, env);
            for (float d = 0; d < 3000; d += 50)
            {
                Assert.AreEqual(0f, r.RelLateral(d, 0), 1e-4);
                Assert.AreEqual(0f, r.RelHeight(d, 0), 1e-4);
                Assert.AreEqual(0f, r.RelYaw(d, 0), 1e-4);
                Assert.Less(Math.Abs(r.RelLateral(d, 10)), 0.3f); // near field nearly straight: lanes still readable
            }
        }

        [Test]
        public void WindingRoutesBendMoreThanTheCity()
        {
            float Bend(EnvironmentKind env) { var r = new RoadShape(3, env); float m = 0; for (float d = 400; d < 3000; d += 10) m = Math.Max(m, Math.Abs(r.RelLateral(d, 80))); return m; }
            Assert.Greater(Bend(EnvironmentKind.Baguio), Bend(EnvironmentKind.Manila));
            Assert.Greater(Bend(EnvironmentKind.Beach), Bend(EnvironmentKind.Edsa));
            float Climb(EnvironmentKind env) { var r = new RoadShape(3, env); float m = 0; for (float d = 400; d < 3000; d += 5) m = Math.Max(m, Math.Abs(r.Slope(d))); return m; }
            Assert.Greater(Climb(EnvironmentKind.Baguio), Math.Max(0.09f, Climb(EnvironmentKind.Manila) * 1.8f), "Baguio should have properly steep streets");
        }

        [Test]
        public void HighlandFogBanksRollInAndOut()
        {
            var f = new FogBanks(5, EnvironmentKind.Baguio);
            Assert.AreEqual(0f, f.DensityAt(0));
            Assert.AreEqual(0f, f.DensityAt(FogBanks.FirstAt - FogBanks.Ramp - 1));
            float max = 0; int entries = 0; bool inside = false;
            for (float d = 0; d < 5000; d += 2)
            {
                float v = f.DensityAt(d);
                Assert.That(v, Is.InRange(0f, 1f));
                max = Math.Max(max, v);
                if (!inside && v > 0.2f) { inside = true; entries++; }
                else if (inside && v < 0.05f) inside = false;
                Assert.Less(Math.Abs(f.DensityAt(d + 2) - v), 0.08f, "fog must roll in smoothly");
            }
            Assert.Greater(max, 0.7f);
            Assert.That(entries, Is.InRange(4, 9), "fog banks in 5 km");
            Assert.GreaterOrEqual(FogBanks.FogEnd(160f, 1f), FogBanks.MinVisibility, "the road ahead stays visible");
            var city = new FogBanks(5, EnvironmentKind.Manila);
            for (float d = 0; d < 3000; d += 25) Assert.AreEqual(0f, city.DensityAt(d));
            Assert.IsTrue(FogBanks.Has(EnvironmentKind.Baguio));
            Assert.IsFalse(FogBanks.Has(EnvironmentKind.Edsa));
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
