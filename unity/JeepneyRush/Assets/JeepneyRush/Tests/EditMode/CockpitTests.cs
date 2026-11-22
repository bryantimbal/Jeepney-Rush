using JeepneyRush.Logic;
using JeepneyRush.Logic.Geometry;
using NUnit.Framework;

namespace JeepneyRush.Tests
{
    public class CockpitTests
    {
        [Test]
        public void SpeedometerSweepsFromBottomLeftToBottomRight()
        {
            Assert.AreEqual(-135f, Speedometer.NeedleAngle(0), 1e-3);
            Assert.AreEqual(0f, Speedometer.NeedleAngle(60), 1e-3);     // straight up at 60 km/h
            Assert.AreEqual(135f, Speedometer.NeedleAngle(120), 1e-3);
            Assert.AreEqual(135f, Speedometer.NeedleAngle(180), 1e-3);  // pinned in the red
            Assert.AreEqual(72f, Speedometer.Kmh(20f), 1e-3);
            Assert.AreEqual(72f, Speedometer.Kmh(-20f), 1e-3);
            Assert.AreEqual("000000", Speedometer.Odometer(0));
            Assert.AreEqual("012345", Speedometer.Odometer(1234.5 * 1000));   // 1,234.5 km
            Assert.AreEqual("000000", Speedometer.Odometer(100000.0 * 1000)); // rolls over
        }

        [Test]
        public void SabitOnlyOnProvincialRoads()
        {
            Assert.IsTrue(Sabit.Allowed(EnvironmentKind.Provincial));
            Assert.IsTrue(Sabit.Allowed(EnvironmentKind.Beach));
            Assert.IsTrue(Sabit.Allowed(EnvironmentKind.Mountain));
            foreach (var env in new[] { EnvironmentKind.Manila, EnvironmentKind.Edsa, EnvironmentKind.Cebu, EnvironmentKind.Baguio })
                Assert.AreEqual(0, Sabit.ExtraFor(env), env.ToString());
        }

        [Test]
        public void SabitRidersBoardOnceTheSeatsAreFull()
        {
            var cfg = new BalanceConfig { passengerCapacity = 5 };
            var route = new PassengerRoute(cfg, new GameRandom(3), StopNames.ForEnvironment(EnvironmentKind.Provincial));
            route.AllowSabit(Sabit.ExtraFor(EnvironmentKind.Provincial));
            Assert.AreEqual(8, route.Capacity);
            Assert.AreEqual(5, route.SeatCapacity);
            var a = route.Plan(100, 1, 2, 9);     // a crowd at the PARA
            route.Plan(300, 1, 2, 1);
            route.Plan(600, 1, 2, 1);
            var r = route.Serve(a);
            Assert.AreEqual(8, r.Boarded);
            Assert.AreEqual(8, route.Count);      // 5 seated + 3 hanging on the back
            Assert.AreEqual(3, route.Hanging);
            Assert.AreEqual(1, route.EjectAngry());
            Assert.AreEqual(2, route.Hanging);
            // city routes: no sabit
            var city = new PassengerRoute(cfg, new GameRandom(3), StopNames.ForEnvironment(EnvironmentKind.Manila));
            city.AllowSabit(Sabit.ExtraFor(EnvironmentKind.Manila));
            Assert.AreEqual(5, city.Capacity);
            Assert.AreEqual(0, city.Hanging);
        }

        [Test]
        public void CockpitPartsBuildForEveryJeepney()
        {
            foreach (var l in JeepneyCatalog.All)
            {
                var m = CockpitBuilder.Extras(l);
                Assert.IsTrue(m.SubmeshKeys.Contains("utex:dash_sign"), l.Id);
                Assert.IsTrue(m.SubmeshKeys.Contains("utex:mirror_pax"), l.Id);
                Assert.AreEqual(l.SoundSystem, m.SubmeshKeys.Contains("glow"), $"{l.Id}: Patok LED strip");
            }
            Assert.Greater(CockpitBuilder.SteeringWheel().TriangleCount, 50);
            Assert.IsTrue(CockpitBuilder.GaugeFace().SubmeshKeys.Contains("utexa:gauge_face"));
            Assert.IsTrue(CockpitBuilder.GaugeNeedle().SubmeshKeys.Contains("utexa:gauge_needle"));
            Assert.Greater(CockpitBuilder.Rosary().TriangleCount, 20);
            // the eye sits behind the steering wheel and under the roof
            Assert.Less(CockpitBuilder.Eye.Z, CockpitBuilder.WheelPos.Z);
            Assert.Less(CockpitBuilder.Eye.Y, JeepneyBuilder.RoofY);
        }
    }
}
