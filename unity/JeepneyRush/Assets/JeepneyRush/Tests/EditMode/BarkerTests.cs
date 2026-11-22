using System;
using System.Linq;
using JeepneyRush.Logic;
using JeepneyRush.Logic.Audio;
using JeepneyRush.Logic.Geometry;
using NUnit.Framework;

namespace JeepneyRush.Tests
{
    public class BarkerTests
    {
        [Test]
        public void BarkerCallsOnlyNameStopsOnTheCurrentRoute()
        {
            var rng = new GameRandom(5);
            foreach (EnvironmentKind env in Enum.GetValues(typeof(EnvironmentKind)))
            {
                var mine = StopNames.ForEnvironment(env).Select(i => StopNames.All[i]).ToList();
                var others = StopNames.All.Where(n => !mine.Contains(n)).ToList();
                for (int k = 0; k < 300; k++)
                {
                    string line = Lines.BarkerCall(env, mine[0], 3, rng);
                    Assert.IsNotEmpty(line);
                    bool fill = Lines.BarkerFill.Contains(line) || line == "ISA NA LANG! ISA NA LANG!";
                    if (!fill) Assert.IsTrue(mine.Any(n => line.Contains(n)), $"{env}: '{line}' names no stop on this route");
                    foreach (var o in others) if (!mine.Any(m => m.Contains(o))) Assert.IsFalse(line.StartsWith(o + "!") || line.StartsWith(o + ",") || line.Contains(" " + o + "!"), $"{env}: '{line}' names another route's stop");
                    Assert.LessOrEqual(line.Length, 44, "bubble too long");
                }
            }
        }

        [Test]
        public void FullJeepneyGetsTheFullLine()
        {
            var rng = new GameRandom(1);
            for (int k = 0; k < 50; k++) CollectionAssert.Contains(Lines.BarkerFull, Lines.BarkerCall(EnvironmentKind.Manila, "QUIAPO", 0, rng));
        }

        [Test]
        public void CitiesHaveMoreBarkersThanTheMountains()
        {
            Assert.Greater(Barkers.Chance(EnvironmentKind.Edsa), Barkers.Chance(EnvironmentKind.Provincial));
            Assert.Greater(Barkers.Chance(EnvironmentKind.Provincial), Barkers.Chance(EnvironmentKind.Mountain));
            Assert.Greater(Barkers.Chance(EnvironmentKind.Mountain), 0f);
        }

        [Test]
        public void BarkerModelAndSlapSound()
        {
            var body = PropBuilder.Barker(2);
            var arm = PropBuilder.BarkerArm(2);
            Assert.Greater(body.Positions.Count, 50);
            // the arm hangs from its shoulder pivot and points up (+Y)
            Assert.GreaterOrEqual(arm.Positions.Min(p => p.Y), -0.05f);
            Assert.Greater(arm.Positions.Max(p => p.Y), 0.55f);
            var slap = SoundBank.Make(Sfx.Tapik);
            Assert.Greater(slap.Max(Math.Abs), 0.3f);
        }
    }
}
