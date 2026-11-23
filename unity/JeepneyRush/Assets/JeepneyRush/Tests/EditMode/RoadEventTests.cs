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
            public void SpawnStation(GasStation station, float z) { }
            public void SpawnIntersection(Intersection intersection, float z) { }
        }
    }
}
