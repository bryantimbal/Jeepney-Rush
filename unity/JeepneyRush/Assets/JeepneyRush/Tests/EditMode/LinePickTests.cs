using System.Collections.Generic;
using JeepneyRush.Logic;
using NUnit.Framework;

namespace JeepneyRush.Tests
{
    public class LinePickTests
    {
        [Test]
        public void NoRecentRepeats()
        {
            var rng = new GameRandom(9);
            foreach (var pool in new[] { Lines.Bump, Lines.Hump, Lines.Board, Lines.Alight, Lines.Traffic, Lines.BarkerFill, Lines.Smooth })
            {
                var last = new List<string>();
                for (int i = 0; i < 300; i++)
                {
                    string l = Lines.Pick(pool, rng);
                    int window = pool.Length >= 6 ? 3 : 1;
                    for (int k = 0; k < window && k < last.Count; k++)
                        Assert.AreNotEqual(last[last.Count - 1 - k], l, $"'{l}' repeated within {window} picks");
                    last.Add(l);
                }
            }
        }

        [Test]
        public void EveryLineStillGetsSaid()
        {
            var rng = new GameRandom(4);
            var seen = new HashSet<string>();
            for (int i = 0; i < 2000; i++) seen.Add(Lines.Pick(Lines.Traffic, rng));
            Assert.AreEqual(new HashSet<string>(Lines.Traffic).Count, seen.Count);
        }

        [Test]
        public void TinyPoolsWork()
        {
            var rng = new GameRandom(1);
            var two = new[] { "A", "B" };
            string prev = null;
            for (int i = 0; i < 20; i++) { string l = Lines.Pick(two, rng); Assert.AreNotEqual(prev, l); prev = l; }
            Assert.AreEqual("X", Lines.Pick(new[] { "X" }, rng));
            Assert.AreEqual("", Lines.Pick(new string[0], rng));
        }
    }
}
