using JeepneyRush.Logic.Perf;
using NUnit.Framework;

namespace JeepneyRush.Tests
{
    public class FrameGovernorTests
    {
        private static void Run(FrameGovernor g, float fps, float seconds)
        {
            float dt = 1f / fps;
            for (float t = 0; t < seconds; t += dt) g.Tick(dt);
        }

        [Test]
        public void SustainedSlowdownStepsDownToLow()
        {
            var g = new FrameGovernor(FrameGovernor.High, FrameGovernor.High);
            Run(g, 40f, 6f);
            Assert.AreEqual(FrameGovernor.Medium, g.Level);
            Run(g, 40f, 8f);
            Assert.AreEqual(FrameGovernor.Low, g.Level);
            Run(g, 30f, 20f);
            Assert.AreEqual(FrameGovernor.Low, g.Level, "never below Low");
        }

        [Test]
        public void ShortDipsAndLoadingHitchesAreIgnored()
        {
            var g = new FrameGovernor(FrameGovernor.High, FrameGovernor.High);
            for (int i = 0; i < 20; i++) { Run(g, 60f, 2f); Run(g, 40f, 1f); g.Tick(0.5f); }
            Assert.AreEqual(FrameGovernor.High, g.Level);
            Assert.AreEqual(0, g.Changes);
        }

        [Test]
        public void ClimbsBackOnceAndNeverAboveTheCeiling()
        {
            var g = new FrameGovernor(FrameGovernor.Low, FrameGovernor.Medium);
            Run(g, 60f, 30f);
            Assert.AreEqual(FrameGovernor.Medium, g.Level);
            Run(g, 60f, 120f);
            Assert.AreEqual(FrameGovernor.Medium, g.Level, "ceiling");
            Run(g, 40f, 6f);
            Assert.AreEqual(FrameGovernor.Low, g.Level);
            Run(g, 60f, 120f);
            Assert.AreEqual(FrameGovernor.Low, g.Level, "only one climb per session (no ping-pong)");
        }

        [Test]
        public void StartLevelFollowsTheDevice()
        {
            Assert.AreEqual(FrameGovernor.Low, FrameGovernor.StartLevel(2048, 8));
            Assert.AreEqual(FrameGovernor.Low, FrameGovernor.StartLevel(8192, 2));
            Assert.AreEqual(FrameGovernor.Medium, FrameGovernor.StartLevel(4096, 8));
            Assert.AreEqual(FrameGovernor.High, FrameGovernor.StartLevel(8192, 8));
        }
    }
}
