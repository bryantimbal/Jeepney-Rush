using System;
using System.Linq;
using JeepneyRush.Logic;
using NUnit.Framework;

namespace JeepneyRush.Tests
{
    public class CityRoutesTests
    {
        [Test]
        public void NewRoutesExistWithTheirOwnStops()
        {
            Assert.AreEqual(8, Environments.All.Length);
            foreach (var env in new[] { EnvironmentKind.Edsa, EnvironmentKind.Cebu, EnvironmentKind.Baguio, EnvironmentKind.Fiesta })
            {
                var stops = StopNames.ForEnvironment(env);
                Assert.AreEqual(12, stops.Count, env.ToString());
                Assert.Greater(Environments.Get(env).UnlockDistance, 0);
            }
            var edsa = StopNames.ForEnvironment(EnvironmentKind.Edsa).Select(i => StopNames.All[i]).ToList();
            CollectionAssert.Contains(edsa, "CUBAO");
            CollectionAssert.Contains(edsa, "AYALA");
            var cebu = StopNames.ForEnvironment(EnvironmentKind.Cebu).Select(i => StopNames.All[i]).ToList();
            CollectionAssert.Contains(cebu, "COLON");
            CollectionAssert.DoesNotContain(cebu, "QUIAPO");
            var baguio = StopNames.ForEnvironment(EnvironmentKind.Baguio).Select(i => StopNames.All[i]).ToList();
            CollectionAssert.Contains(baguio, "SESSION RD");
        }

        [Test]
        public void TricyclesBannedOnEdsaAndInBaguio()
        {
            Assert.AreEqual(0f, Environments.TrafficWeights(EnvironmentKind.Edsa)[2]);
            Assert.AreEqual(0f, Environments.TrafficWeights(EnvironmentKind.Baguio)[2]);
            Assert.Greater(Environments.TrafficDensity(EnvironmentKind.Edsa), Environments.TrafficDensity(EnvironmentKind.Manila));
        }

        [Test]
        public void EdsaHasRushHourJamsOtherRoutesMostlyDont()
        {
            var edsa = new JamSchedule(5, EnvironmentKind.Edsa);
            int jammed = 0;
            for (float d = 0; d < 4000; d += 10) if (edsa.At(d) > 0.5f) jammed++;
            Assert.Greater(jammed, 60, "EDSA should be jammed for a good part of 4 km");
            Assert.Less(jammed, 300, "…but not all the time");
            Assert.Less(edsa.NextJamFrom(0), 500f, "first EDSA jam comes early");
            var manila = new JamSchedule(5, EnvironmentKind.Manila);
            for (float d = 0; d < 4000; d += 50) Assert.AreEqual(0f, manila.At(d));
            var cebu = new JamSchedule(5, EnvironmentKind.Cebu);
            float cebuMax = 0, edsaMax = 0;
            for (float d = 0; d < 4000; d += 10) { cebuMax = Math.Max(cebuMax, cebu.At(d)); edsaMax = Math.Max(edsaMax, edsa.At(d)); }
            Assert.Greater(cebuMax, 0f);
            Assert.Less(cebuMax, edsaMax);
        }

        [Test]
        public void JamsRampInAndSlowTheJeepneyToACrawl()
        {
            var j = new JamSchedule(9, EnvironmentKind.Edsa);
            float from = j.NextJamFrom(0);
            Assert.Less(j.At(from + 5), j.At(from + JamSchedule.Ramp + 5));
            Assert.AreEqual(1f, JamRules.SpeedCap(0));
            Assert.Less(JamRules.SpeedCap(1), 0.5f);
            Assert.Greater(JamRules.SpeedCap(1), 0.3f);
            Assert.Less(JamRules.ComplaintInterval(30), JamRules.ComplaintInterval(0));
        }

        [Test]
        public void DaytimeLastsAtLeastAsLongAsTheNight()
        {
            // in real driving time, over the first 10 minutes and over whole days
            foreach (float span in new[] { 300f, 600f, (float)DayNight.CycleSeconds * 3 })
            {
                float day = 0, night = 0;
                for (float t = 0; t < span; t += 0.5f) { float n = DayNight.Night(DayNight.HourAt(t)); day += 1 - n; night += n; }
                Assert.GreaterOrEqual(day, night, $"first {span:0} s: day {day * 0.5f:0} s vs night {night * 0.5f:0} s");
            }
            float firstNight = 0;
            for (float t = 0; t < 600; t += 0.5f) if (DayNight.Night(DayNight.HourAt(t)) > 0.99f) { firstNight = t; break; }
            Assert.That(firstNight, Is.InRange(1f, 300f), "night still comes during a normal run");
        }

        [Test]
        public void ClockGoesThroughSunsetNightAndDawn()
        {
            Assert.AreEqual(DayNight.StartHour, DayNight.HourAt(0), 1e-4);
            Assert.AreEqual(0f, DayNight.Night(DayNight.HourAt(0)));
            float sunsetSecs = (DayNight.NightFrom - DayNight.StartHour) * DayNight.SecondsPerHour;
            Assert.AreEqual(1f, DayNight.Night(DayNight.HourAt(sunsetSecs + 1)));
            Assert.That(sunsetSecs, Is.InRange(90f, 200f), "a run starts in daylight; night comes after a couple of minutes");
            Assert.Greater(DayNight.Dusk(18.1f), 0.9f);
            Assert.AreEqual(0f, DayNight.Dusk(13f));
            // wraps past midnight (night hours pass faster), dawn, then day again
            float midnight = sunsetSecs + (24 - DayNight.NightFrom) * DayNight.NightSecondsPerHour;
            Assert.AreEqual(0f, DayNight.HourAt(midnight) % 24f, 0.01f);
            float dawn = midnight + DayNight.DawnFrom * DayNight.NightSecondsPerHour + (6.6f - DayNight.DawnFrom) * DayNight.SecondsPerHour;
            Assert.AreEqual(6.6f, DayNight.HourAt(dawn), 0.01f);
            Assert.AreEqual(0f, DayNight.Night(DayNight.HourAt(dawn)));
            Assert.AreEqual(DayNight.StartHour, DayNight.HourAt((float)DayNight.CycleSeconds), 0.01f, "a full day later it's the same time again");
            // the clock only moves forward, smoothly
            float prev = DayNight.HourAt(0);
            for (float t = 0.5f; t < 2000; t += 0.5f)
            {
                float hh = DayNight.HourAt(t), d = hh - prev;
                if (d < 0) d += 24;
                Assert.That(d, Is.InRange(0f, 0.051f), $"clock jump at {t} s");
                prev = hh;
            }
            Assert.AreEqual("6:45 PM", DayNight.Clock(18.76f));
            Assert.AreEqual("12:00 AM", DayNight.Clock(0.01f));
            for (float h = 0; h < 24; h += 0.25f)
            {
                float n = DayNight.Night(h);
                Assert.That(n, Is.InRange(0f, 1f));
            }
        }

        [Test]
        public void StuckCommutersComplainAboutWorkInTheMorningAndHomeAtNight()
        {
            var rng = new GameRandom(3);
            bool sawWork = false, sawHome = false;
            for (int i = 0; i < 200; i++)
            {
                string m = Lines.Stuck(7.5f, EnvironmentKind.Edsa, rng);
                if (Lines.LateForWork.Contains(m)) sawWork = true;
                Assert.IsFalse(Lines.GoingHome.Contains(m), m);
                string n = Lines.Stuck(20f, EnvironmentKind.Edsa, rng);
                if (Lines.GoingHome.Contains(n)) sawHome = true;
                Assert.IsFalse(Lines.LateForWork.Contains(n), n);
            }
            Assert.IsTrue(sawWork && sawHome);
        }
    }
}
