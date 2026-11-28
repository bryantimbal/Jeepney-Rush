using System;
using System.Collections.Generic;
using System.Linq;
using JeepneyRush.Logic;
using NUnit.Framework;

namespace JeepneyRush.Tests
{
    public class RoadEventTests
    {
        [Test]
        public void EveryRouteHasItsOwnEventsAndEdsaKeepsToTheHighway()
        {
            foreach (EnvironmentKind env in Enum.GetValues(typeof(EnvironmentKind)))
                Assert.IsNotEmpty(RoadEvents.For(env), env.ToString());
            var edsa = RoadEvents.For(EnvironmentKind.Edsa).Select(e => e.Item1).ToList();
            CollectionAssert.DoesNotContain(edsa, RoadEventKind.Carabao);
            CollectionAssert.DoesNotContain(edsa, RoadEventKind.Taho);
            CollectionAssert.Contains(RoadEvents.For(EnvironmentKind.Provincial).Select(e => e.Item1).ToList(), RoadEventKind.Carabao);
            CollectionAssert.Contains(RoadEvents.For(EnvironmentKind.Cebu).Select(e => e.Item1).ToList(), RoadEventKind.ParadeFloat);
        }

        [Test]
        public void EventsAreBuiltFromHazardsAndAnnounced()
        {
            var pieces = new List<RoadEvents.Piece>();
            foreach (RoadEventKind e in Enum.GetValues(typeof(RoadEventKind)))
            {
                RoadEvents.Build(e, 1, 3, pieces);
                Assert.IsNotEmpty(pieces, e.ToString());
                Assert.IsTrue(pieces.Exists(p => RoadEvents.EventOf(p.Kind) == e), $"{e}: one piece triggers its HUD warning");
                Assert.IsNotEmpty(RoadEvents.Warning(e));
                Assert.IsTrue(pieces.TrueForAll(p => p.Lane >= 0 && p.Lane < 3));
            }
            RoadEvents.Build(RoadEventKind.Taho, 0, 3, pieces);
            Assert.AreEqual(2, pieces[0].Lane, "taho vendors walk along the curb lane");
        }

        [Test]
        public void EventsAppearOnEveryRouteAndWeatherHasSuddenShowers()
        {
            int showers = 0;
            foreach (EnvironmentKind env in Enum.GetValues(typeof(EnvironmentKind)))
            {
                int events = 0;
                for (int seed = 1; seed <= 3; seed++)
                {
                    var w = new WeatherSchedule(seed * 31, env);
                    w.KindAt(6000);
                    showers += w.Showers;
                    var sink = new CountingSink();
                    var cfg = new BalanceConfig();
                    var rng = new GameRandom(seed * 31);
                    var d = new SpawnDirector(cfg, rng, sink, new PassengerRoute(cfg, rng, StopNames.ForEnvironment(env)), env, w.WetnessAt);
                    for (float dist = 0; dist < 3000; dist += 5f) d.Update(dist, cfg.SpeedAt(dist));
                    events += d.EventsPlaced;
                }
                Assert.Greater(events, 3, $"{env}: road events in 3 x 3 km");
            }
            Assert.Greater(showers, 3, "sudden showers happen");
        }

        private sealed class CountingSink : ISpawnSink
        {
            public IReadOnlyList<int> PlayerLanes { get; } = new[] { 1 };
            public void CollectBlockers(List<PredictBlocker> output) { }
            public void SpawnHazard(HazardKind kind, int lane, float z, float speed, bool solid) { }
            public void SpawnCoins(CoinFormation formation, int lane, int lane2, float z, int count, float spacing) { }
            public void SpawnStop(PassengerStop stop, float z) { }
            public void SpawnPowerUp(PowerUpKind kind, int lane, float z) { }
            public void SpawnGem(int lane, float z) { }
            public void SpawnParol(int lane, float z) { }
            public void SpawnStation(GasStation station, float z) { }
            public void SpawnIntersection(Intersection intersection, float z) { }
        }

        [Test]
        public void TahoAndCarabaoOnlyInDaylightBalutOnlyAtNight()
        {
            foreach (EnvironmentKind env in System.Enum.GetValues(typeof(EnvironmentKind)))
            {
                var night = new List<RoadEventKind>(); foreach (var (k, _) in RoadEvents.For(env, true)) night.Add(k);
                var day = new List<RoadEventKind>(); foreach (var (k, _) in RoadEvents.For(env, false)) day.Add(k);
                CollectionAssert.DoesNotContain(night, RoadEventKind.Taho, env.ToString());
                CollectionAssert.DoesNotContain(night, RoadEventKind.Carabao, env.ToString());
                CollectionAssert.DoesNotContain(day, RoadEventKind.Balut, env.ToString());
                Assert.IsNotEmpty(night, $"{env}: some events still happen at night");
                if (day.Contains(RoadEventKind.Taho)) CollectionAssert.Contains(night, RoadEventKind.Balut, $"{env}: the balut vendor replaces the taho at night");
            }
            // the generator respects it: a whole night-time run never places taho or carabao
            foreach (EnvironmentKind env in new[] { EnvironmentKind.Provincial, EnvironmentKind.Manila, EnvironmentKind.Fiesta })
            {
                var sink = new CountingSink();
                var cfg = new BalanceConfig();
                var rng = new GameRandom(5);
                var d = new SpawnDirector(cfg, rng, sink, new PassengerRoute(cfg, rng, StopNames.ForEnvironment(env)), env) { IsNight = () => true };
                for (float dist = 0; dist < 5000; dist += 5f) d.Update(dist, cfg.SpeedAt(dist));
                Assert.Greater(d.EventsPlaced, 3);
                CollectionAssert.DoesNotContain(d.EventLog, RoadEventKind.Taho);
                CollectionAssert.DoesNotContain(d.EventLog, RoadEventKind.Carabao);
                Assert.Contains(RoadEventKind.Balut, d.EventLog, $"{env}: balut vendors at night");
            }
            Assert.IsTrue(DayNight.IsNightFor(400f) || DayNight.IsNightFor(200f), "night comes during a run");
            Assert.IsFalse(DayNight.IsNightFor(0f), "runs start in daylight");
        }

        [Test]
        public void ManilaAndEdsaHaveDenserTraffic()
        {
            int Extra(EnvironmentKind env)
            {
                var sink = new CountingSink();
                var cfg = new BalanceConfig();
                var rng = new GameRandom(9);
                var d = new SpawnDirector(cfg, rng, sink, new PassengerRoute(cfg, rng, StopNames.ForEnvironment(env)), env);
                for (float dist = 0; dist < 3000; dist += 5f) d.Update(dist, cfg.SpeedAt(dist));
                return d.ExtraTrafficPlaced;
            }
            Assert.Greater(Extra(EnvironmentKind.Manila), 20, "extra city traffic on Manila streets");
            Assert.Greater(Extra(EnvironmentKind.Edsa), 20, "extra city traffic on EDSA");
            Assert.AreEqual(0, Extra(EnvironmentKind.Baguio));
            Assert.AreEqual(0, Extra(EnvironmentKind.Mountain));
            Assert.Less(Environments.RowGapScale(EnvironmentKind.Edsa), 1f);
            Assert.Less(Environments.RowGapScale(EnvironmentKind.Manila), 1f);
        }
    }
}
