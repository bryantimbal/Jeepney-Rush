using JeepneyRush.Logic;
using JeepneyRush.Logic.Geometry;
using NUnit.Framework;

namespace JeepneyRush.Tests
{
    public class LifeTests
    {
        [Test]
        public void LivingThingsBuild()
        {
            Assert.Greater(LifeBuilder.BirdBody("#5E636E").TriangleCount, 10);
            Assert.Greater(LifeBuilder.BirdWing("#5E636E", 2f).TriangleCount, 4);
            Assert.Greater(LifeBuilder.Umbrella("#E63946").TriangleCount, 10);
            Assert.Greater(LifeBuilder.Dog("#B08455").TriangleCount, 20);
            Assert.Greater(LifeBuilder.Chicken("#8B3A1E").TriangleCount, 10);
        }

        [Test]
        public void RainyNightLightHazeOnHeadlightsAndStreetLamps()
        {
            Assert.IsTrue(LifeBuilder.HeadlightHaze().SubmeshKeys.Contains("haze"));
            var b = new MeshBuilder();
            NightKit.StreetLamp(b, 1f, SceneryBuilder.RoadHalfWidth, 0f);
            Assert.IsTrue(b.Data.SubmeshKeys.Contains("haze"), "street lamps get a haze cone");
            // the haze cones point down from the lamp to the road
            var (min, max) = b.Data.Bounds();
            Assert.Less(min.Y, 0.5f);
            foreach (EnvironmentKind env in System.Enum.GetValues(typeof(EnvironmentKind)))
                Assert.Less(SceneryProvinces.Chunk(env, 1234, false).Positions.Count, 65535, env.ToString());
        }
    }
}
