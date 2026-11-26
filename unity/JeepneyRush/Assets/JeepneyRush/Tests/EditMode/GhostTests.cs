using JeepneyRush.Logic;
using NUnit.Framework;

namespace JeepneyRush.Tests
{
    public class GhostTests
    {
        private static GhostData Record(float seconds, float speed)
        {
            var rec = new GhostRecorder();
            float d = 0, x = 0;
            for (float t = 0; t < seconds; t += 1f / 30f)
            {
                rec.Tick(1f / 30f, d, x);
                d += speed / 30f;
                x = (float)System.Math.Sin(t) * 1.6f;
            }
            return rec.Finish(d, x);
        }

        [Test]
        public void RecordsAndReplaysThePath()
        {
            var g = Record(60, 12f);
            Assert.That(g.d.Length, Is.InRange(120, 123));
            Ghosts.Sample(g, 30f, out float d, out float x);
            Assert.AreEqual(360f, d, 3f, "12 m/s for 30 s");
            Assert.AreEqual(System.Math.Sin(30) * 1.6f, x, 0.15f);
            Ghosts.Sample(g, 9999f, out d, out _);
            Assert.AreEqual(g.distance, d, 0.2f, "stops at the end");
            Assert.IsNull(Ghosts.Violation(g, g.distance, 60f));
        }

        [Test]
        public void ReversingNeverMovesTheGhostBack()
        {
            var rec = new GhostRecorder();
            rec.Tick(0.5f, 100, 0);
            rec.Tick(0.5f, 80, 0); // backing up
            var g = rec.Finish(80, 0);
            for (int i = 1; i < g.d.Length; i++) Assert.GreaterOrEqual(g.d[i], g.d[i - 1]);
        }

        [Test]
        public void ImplausiblePathsAreRejected()
        {
            var g = Record(30, 12f);
            Assert.AreEqual("distance", Ghosts.Violation(g, g.distance * 2, 30f), "doesn't end where the run ended");
            Assert.AreEqual("samples", Ghosts.Violation(g, g.distance, 5f), "longer than the run");
            var fast = Record(30, 12f);
            fast.d[10] = fast.d[9] + 999;
            Assert.AreEqual("step", Ghosts.Violation(fast, fast.distance, 30f));
            var wide = Record(30, 12f);
            wide.x[5] = 9000;
            Assert.AreEqual("range", Ghosts.Violation(wide, wide.distance, 30f));
        }

        [Test]
        public void LeadAndWin()
        {
            var g = Record(60, 10f);
            g.score = 5000;
            Assert.AreEqual(50f, Ghosts.Lead(g, 10f, 150f), 2f);
            Assert.Less(Ghosts.Lead(g, 10f, 50f), 0);
            Assert.IsTrue(Ghosts.Beat(g, 5001));
            Assert.IsFalse(Ghosts.Beat(g, 5000));
        }
    }
}
