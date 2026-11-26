using JeepneyRush.Logic;
using NUnit.Framework;

namespace JeepneyRush.Tests
{
    public class RiderTests
    {
        private static readonly BalanceConfig Cfg = new BalanceConfig();

        /// <summary>Route with stops 1 (at 100 m) and 2 (at 200 m); two riders of a kind board at stop 1 for stop 2.</summary>
        private static (PassengerRoute route, PassengerStop dest) Ride(RiderKind kind, int riders = 1)
        {
            var route = new PassengerRoute(Cfg, new GameRandom(1), null, new GameRandom(2)) { NextKind = kind };
            var a = route.Plan(100, 1, 2, riders);
            var b = route.Plan(200, 1, 2, 0);
            var r = route.Serve(a);
            Assert.AreEqual(riders, r.Boarded);
            route.NextKind = null;
            return (route, b);
        }

        [Test]
        public void RegularRidersPayAsBefore()
        {
            var (route, dest) = Ride(RiderKind.Regular, 2);
            var r = route.Serve(dest);
            Assert.AreEqual(2, r.Alighted);
            Assert.AreEqual(2 * Cfg.fareCoins + Cfg.groupBonusCoins, r.Coins);
            Assert.AreEqual(0, r.Tips);
        }

        [Test]
        public void StudentsGetADiscount()
        {
            var (route, dest) = Ride(RiderKind.Student, 2);
            Assert.AreEqual((int)System.Math.Round(2 * Cfg.fareCoins * Riders.StudentFare + Cfg.groupBonusCoins), route.Serve(dest).Coins);
        }

        [Test]
        public void LolaTipsOnlyAfterASmoothRide()
        {
            var (route, dest) = Ride(RiderKind.Lola);
            var r = route.Serve(dest);
            Assert.AreEqual(Riders.LolaTip, r.Tips);
            Assert.AreEqual(Cfg.fareCoins + Riders.LolaTip, r.Coins);
            (route, dest) = Ride(RiderKind.Lola);
            Assert.AreEqual(1, route.Jolt(false), "a fast hump upsets her");
            Assert.AreEqual(0, route.Jolt(true), "only once");
            Assert.AreEqual(0, route.Serve(dest).Tips);
        }

        [Test]
        public void BalikbayanMindsOnlyCrashes()
        {
            var (route, dest) = Ride(RiderKind.Balikbayan);
            Assert.AreEqual(0, route.Jolt(false));
            Assert.AreEqual(Riders.BalikbayanTip, route.Serve(dest).Tips);
            (route, dest) = Ride(RiderKind.Balikbayan);
            route.Jolt(true);
            Assert.AreEqual(0, route.Serve(dest).Tips);
        }

        [Test]
        public void HurriedRiderHasADeadline()
        {
            var (route, dest) = Ride(RiderKind.Hurry);
            float deadline = route.Aboard[0].Deadline;
            Assert.AreEqual(100f / Riders.HurrySpeed + Riders.HurrySlack, deadline, 0.01f);
            route.Clock = deadline - 1;
            var r = route.Serve(dest);
            Assert.AreEqual(1, r.OnTime);
            Assert.AreEqual(Riders.HurryTip, r.Tips);
            (route, dest) = Ride(RiderKind.Hurry);
            route.Clock = 999;
            r = route.Serve(dest);
            Assert.AreEqual(1, r.Late);
            Assert.AreEqual(0, r.Tips);
            Assert.AreEqual(Cfg.fareCoins, r.Coins, "late riders still pay the fare");
        }

        [Test]
        public void FareDodgerPaysNothingUnlessCaught()
        {
            var (route, dest) = Ride(RiderKind.Dodger);
            var r = route.Serve(dest);
            Assert.AreEqual(1, r.Dodgers);
            Assert.AreEqual(0, r.Coins);
            Assert.AreEqual(Cfg.fareCoins, r.DodgerFare);
            Assert.AreEqual(1, r.Alighted, "still counts as a delivery");
        }

        [Test]
        public void TinderaTakesTwoSeatsAndPaysForCargo()
        {
            var route = new PassengerRoute(Cfg, new GameRandom(1), null, new GameRandom(2)) { NextKind = RiderKind.Vendor };
            var a = route.Plan(100, 1, 2, 5);
            var b = route.Plan(200, 1, 2, 0);
            var r = route.Serve(a);
            // 5 seats: two tinderas (2 seats each), then the last seat goes to someone without a bayong
            Assert.AreEqual(3, r.Boarded);
            int vendors = 0;
            foreach (var p in route.Aboard) if (p.Kind == RiderKind.Vendor) vendors++;
            Assert.AreEqual(2, vendors, "two seats each");
            Assert.AreEqual(0, route.Room);
            Assert.AreEqual(Cfg.passengerCapacity, route.SeatsUsed);
            Assert.IsTrue(r.Full);
            r = route.Serve(b);
            Assert.GreaterOrEqual(r.Tips, 2 * Riders.VendorCargo);
        }

        [Test]
        public void KindsDependOnTimeAndRoute()
        {
            Assert.AreEqual(0f, Riders.Weight(RiderKind.Drunk, EnvironmentKind.Manila, false), "no drunks in the daytime");
            Assert.Greater(Riders.Weight(RiderKind.Drunk, EnvironmentKind.Manila, true), 0f);
            Assert.Greater(Riders.Weight(RiderKind.Hurry, EnvironmentKind.Edsa, false), Riders.Weight(RiderKind.Hurry, EnvironmentKind.Beach, false));
            var rng = new GameRandom(5);
            int balik = 0, drunk = 0, regular = 0;
            for (int i = 0; i < 5000; i++)
            {
                var k = Riders.Pick(rng, EnvironmentKind.Manila, false, 1, false);
                Assert.AreNotEqual(RiderKind.Vendor, k, "no room for a bayong");
                if (k == RiderKind.Balikbayan) balik++;
                if (k == RiderKind.Drunk) drunk++;
                if (k == RiderKind.Regular) regular++;
            }
            Assert.AreEqual(0, drunk);
            Assert.That(balik, Is.InRange(30, 200), "rare");
            Assert.Greater(regular, 2000, "most riders are ordinary");
            Assert.AreNotEqual(RiderKind.Balikbayan, Riders.Pick(new GameRandom(1), EnvironmentKind.Manila, false, 5, true));
        }

        [Test]
        public void EveryKindHasATagAndLines()
        {
            foreach (RiderKind k in System.Enum.GetValues(typeof(RiderKind)))
            {
                if (k == RiderKind.Regular) continue;
                Assert.IsNotEmpty(Riders.Tag(k), k.ToString());
                Assert.IsNotEmpty(Riders.Board[(int)k], k.ToString());
                Assert.IsNotNull(Riders.RideLine(k, new GameRandom(1)), k.ToString());
            }
        }

        [Test]
        public void TipsStayWithinTheServerCoinBound()
        {
            // worst case per delivery: base fare bound + the biggest tip at the best fare multiplier (★5 × passenger bonus)
            float bestMult = 1.2f * 2f;
            int perDelivery = RunBounds.MaxFarePerDelivery + (int)System.Math.Ceiling(Riders.BalikbayanTip * bestMult);
            // a busy kilometre: ~4.6 stops, full jeepney turnover each, plus every road coin
            float d = 1000f;
            int deliveries = (int)(4.6f * 15);
            int realistic = (int)(0.21f * d) + deliveries * (Cfg.fareCoins * 3 + 6);
            Assert.Less(realistic, RunBounds.MaxCoins(d, deliveries));
            // the average tip per delivery (every kind at its real chance, everyone happy, best multiplier)
            // fits in the distance headroom with room to spare (the server only clamps, it never rejects for coins)
            float total = 0, tips = 0;
            foreach (RiderKind k in System.Enum.GetValues(typeof(RiderKind)))
            {
                float w = Riders.Weight(k, EnvironmentKind.Edsa, true);
                total += w;
                int tip = k == RiderKind.Lola ? Riders.LolaTip : k == RiderKind.Hurry ? Riders.HurryTip : k == RiderKind.Vendor ? Riders.VendorCargo
                        : k == RiderKind.Balikbayan ? Riders.BalikbayanTip : k == RiderKind.Drunk ? Cfg.fareCoins : 0;
                tips += w * tip;
            }
            float avgTip = tips / total * bestMult;
            Assert.Less(deliveries * avgTip, 0.5f * d * (1f - 0.21f));
            Assert.Greater(perDelivery, 0);
        }

        [Test]
        public void PassengerMissionsStartInDecember()
        {
            bool IsNew(MissionType t) => t >= MissionType.TotalDodgersCaught;
            var day = new System.DateTime(2026, 11, 30);
            for (int k = 0; k < 200; k++)
                foreach (var m in Missions.Daily(Missions.DayKey(day.AddDays(-k)))) Assert.IsFalse(IsNew(m.Type), "missions of earlier days never change");
            int seen = 0;
            for (int k = 1; k <= 60; k++) foreach (var m in Missions.Daily(Missions.DayKey(day.AddDays(k)))) if (IsNew(m.Type)) seen++;
            Assert.Greater(seen, 10);
            for (int w = 100; w < Missions.PassengerWeeksFrom; w++) foreach (var m in Missions.Weekly(w)) Assert.IsFalse(IsNew(m.Type));
        }

        [Test]
        public void PassengerMissionProgressComesFromTheRun()
        {
            var r = new RunSummary { Tips = 12, DodgersCaught = 2, SmoothLolas = 1, OnTime = 3 };
            Assert.AreEqual(12, Missions.Value(MissionType.TotalTips, r));
            Assert.AreEqual(2, Missions.Value(MissionType.TotalDodgersCaught, r));
            Assert.AreEqual(1, Missions.Value(MissionType.TotalSmoothLolas, r));
            Assert.AreEqual(3, Missions.Value(MissionType.TotalOnTime, r));
            var rep = RunReport.From(r, 60, 1, false, 0, "pilipinas");
            rep.passengers = rep.deliveries = 1;
            rep.tips = 999;
            RunBounds.Clamp(rep);
            Assert.AreEqual(RunBounds.MaxTipPerDelivery, rep.tips);
            Assert.AreEqual(1, rep.dodgers_caught);
        }
    }
}
