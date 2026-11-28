using System.Collections.Generic;
using JeepneyRush.Logic;
using NUnit.Framework;

namespace JeepneyRush.Tests
{
    public class AlbumTests
    {
        [Test]
        public void EveryRouteHasItsCards()
        {
            Assert.AreEqual(System.Enum.GetValues(typeof(EnvironmentKind)).Length, Album.Routes, "a set per route");
            var names = new HashSet<string>();
            for (int c = 0; c < Album.Count; c++) Assert.IsTrue(names.Add(Album.Name(c)), Album.Name(c));
            Assert.AreEqual(EnvironmentKind.Cebu, (EnvironmentKind)Album.RouteOf(Album.Card(EnvironmentKind.Cebu, 2)));
        }

        [Test]
        public void PicksMissingCardsFirst()
        {
            var owned = new HashSet<int> { 0, 1, 2 };
            Assert.AreEqual(3, Album.Pick(EnvironmentKind.Manila, owned.Contains, new GameRandom(1)));
            owned.Add(3);
            Assert.IsTrue(Album.SetComplete(owned.Contains, 0));
            int dup = Album.Pick(EnvironmentKind.Manila, owned.Contains, new GameRandom(2));
            Assert.IsTrue(dup >= 0 && dup < 4, "a duplicate of the same route");
        }

        [Test]
        public void ServerRule()
        {
            Assert.IsTrue(Album.Valid(Album.Card(EnvironmentKind.Baguio, 0), (int)EnvironmentKind.Baguio, 900f));
            Assert.IsFalse(Album.Valid(Album.Card(EnvironmentKind.Baguio, 0), (int)EnvironmentKind.Manila, 900f), "wrong route");
            Assert.IsFalse(Album.Valid(0, 0, 500f), "too short");
        }
    }
}
