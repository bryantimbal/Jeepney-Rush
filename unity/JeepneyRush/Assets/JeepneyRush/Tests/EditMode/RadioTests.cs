using JeepneyRush.Logic;
using NUnit.Framework;

namespace JeepneyRush.Tests
{
    public class RadioTests
    {
        [Test]
        public void StationsAndLines()
        {
            Assert.AreEqual(0, Radio.Next(Radio.Stations - 1), "cycles back to the route's music");
            var rng = new GameRandom(4);
            Assert.IsNull(Radio.Line(0, EnvironmentKind.Manila, 12, false, 0, rng), "the route's music has no DJ");
            for (int st = 1; st < Radio.Stations; st++) Assert.IsNotEmpty(Radio.Line(st, EnvironmentKind.Edsa, 8, true, 0.7f, rng));
            string t = Radio.Traffic(EnvironmentKind.Edsa, 8, true, 0.8f, rng);
            StringAssert.Contains("EDSA", t);
            StringAssert.Contains("mabigat", t);
            StringAssert.Contains("ulan", t);
        }

        [Test]
        public void TracksRender()
        {
            Assert.Greater(Logic.Audio.MusicComposer.RenderOpm().Length, 1000);
            Assert.Greater(Logic.Audio.MusicComposer.RenderJams().Length, 1000);
            Assert.Greater(Logic.Audio.MusicComposer.RenderNewsBed().Length, 1000);
        }
    }
}
