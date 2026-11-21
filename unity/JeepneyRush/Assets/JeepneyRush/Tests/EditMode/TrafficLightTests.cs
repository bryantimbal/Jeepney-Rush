using JeepneyRush.Logic;
using NUnit.Framework;

namespace JeepneyRush.Tests
{
    public class TrafficLightTests
    {
        [Test]
        public void CycleGoesGreenYellowRed()
        {
            var c = new BalanceConfig();
            var l = new TrafficLightCycle(c, 0);
            Assert.AreEqual(LightColor.Green, l.Color);
            l.Tick(c.lightGreen + 0.1f);
            Assert.AreEqual(LightColor.Yellow, l.Color);
            l.Tick(c.lightYellow);
            Assert.AreEqual(LightColor.Red, l.Color);
            Assert.AreEqual(c.lightRed - 0.1f, l.Remaining, 1e-3);
            l.Tick(c.lightRed);
            Assert.AreEqual(LightColor.Green, l.Color);
        }

        [Test]
        public void PhaseWrapsAround()
        {
            var c = new BalanceConfig();
            var a = new TrafficLightCycle(c, 3f);
            var b = new TrafficLightCycle(c, 3f + 2 * a.Period);
            Assert.AreEqual(a.Color, b.Color);
            Assert.AreEqual(a.Remaining, b.Remaining, 1e-3);
        }

        [Test]
        public void YellowGivesEnoughTimeEvenAtTopSpeed()
        {
            var c = new BalanceConfig();
            for (float v = 1; v <= c.maxSpeed * c.gasMultiplier * 1.6f; v += 0.5f)
                Assert.IsTrue(TrafficLightRules.YellowIsFair(v, c), $"unfair yellow at {v} m/s");
        }

        [Test]
        public void OnlyRedIsAViolationAndOnlyWhenTheBumperCrosses()
        {
            var c = new BalanceConfig();
            Assert.IsTrue(TrafficLightRules.IsViolation(LightColor.Red));
            Assert.IsFalse(TrafficLightRules.IsViolation(LightColor.Yellow));
            Assert.IsTrue(TrafficLightRules.CrossedLine(c.playerHalfLength + 0.3f, c.playerHalfLength - 0.1f, c));
            Assert.IsFalse(TrafficLightRules.CrossedLine(c.playerHalfLength + 2f, c.playerHalfLength + 0.5f, c), "stopped just before the line");
        }

        [Test]
        public void StopNamesStayOnTheirRoute()
        {
            foreach (EnvironmentKind env in System.Enum.GetValues(typeof(EnvironmentKind)))
                foreach (int i in StopNames.ForEnvironment(env))
                    if (env != EnvironmentKind.Manila) Assert.AreNotEqual("QUIAPO", StopNames.All[i]);
            Assert.AreEqual(StopNames.AtlasColumns * StopNames.AtlasRows, StopNames.All.Length);
            var rng = new GameRandom(5);
            for (int k = 0; k < 300; k++)
            {
                StringAssert.DoesNotContain("QUIAPO", Lines.BoardFor(EnvironmentKind.Beach, rng));
                StringAssert.DoesNotContain("QUIAPO", Lines.BoardFor(EnvironmentKind.Provincial, rng));
            }
        }

        [Test]
        public void CrossTrafficOnlyMovesOnTheJeepneysRed()
        {
            var c = new BalanceConfig();
            var l = new TrafficLightCycle(c, 0);
            bool sawGo = false;
            for (float t = 0; t < l.Period * 2; t += 0.05f)
            {
                bool go = CrossTrafficRules.CrossGreen(l.Color, l.Remaining, c);
                if (go) Assert.AreEqual(LightColor.Red, l.Color, "cross cars move only on the jeepney's red");
                if (l.Color == LightColor.Red && c.lightRed - l.Remaining < CrossTrafficRules.Clearance) Assert.IsFalse(go, "all-red clearance");
                sawGo |= go;
                l.Tick(0.05f);
            }
            Assert.IsTrue(sawGo);
        }

        [Test]
        public void WaitingAtTheStopLineIsSafeFromCrossTraffic()
        {
            var c = new BalanceConfig();
            foreach (var k in new[] { HazardKind.Sedan, HazardKind.Suv, HazardKind.Tricycle, HazardKind.Motorcycle, HazardKind.ParkedJeepney })
                Assert.IsTrue(CrossTrafficRules.StopLineIsSafe(c, HazardCatalog.Get(k).HalfWidth), k.ToString());
            Assert.IsTrue(CrossTrafficRules.JeepneyInBox(0f, c));
            Assert.IsFalse(CrossTrafficRules.JeepneyInBox(-TrafficLightRules.StopLineOffset - c.playerHalfLength, c));
            Assert.IsFalse(CrossTrafficRules.JeepneyInBox(12f, c), "already through");
        }

        [Test]
        public void LitLampMatchesTheColour()
        {
            // regression: green used to light the top (red) lamp
            var lamps = JeepneyRush.Logic.Geometry.PropBuilder.SignalLamps();
            Assert.Greater(lamps[0].Y, lamps[1].Y);
            Assert.Greater(lamps[1].Y, lamps[2].Y);
            Assert.AreEqual(0, TrafficLightRules.LampIndex(LightColor.Red), "red is the top lamp");
            Assert.AreEqual(1, TrafficLightRules.LampIndex(LightColor.Yellow));
            Assert.AreEqual(2, TrafficLightRules.LampIndex(LightColor.Green), "green is the bottom lamp");
        }
    }
}
