using System.Collections.Generic;
using JeepneyRush.Logic;
using NUnit.Framework;

namespace JeepneyRush.Tests
{
    public class ArcadeComboTests
    {
        private const float HalfW = 1.1f, HalfL = 3.0f;

        /// <summary>Drives past one car: z goes from far ahead to far behind while x follows <paramref name="xAt"/>.</summary>
        private static List<PassEvent> Pass(System.Func<float, float> xAt, bool moving = true, bool hit = false)
        {
            var tr = new NearMissTracker();
            var all = new List<PassEvent>();
            var buf = new List<PassEvent>();
            var items = new List<PassCandidate>();
            for (float z = 40f; z > -15f; z -= 0.5f)
            {
                items.Clear();
                items.Add(new PassCandidate { Id = 7, X = xAt(z), Z = z, HalfLength = 2.2f, HalfWidth = 0.9f, Moving = moving, Eligible = !(hit && z < 6f) });
                tr.Update(HalfW, HalfL, items, buf);
                all.AddRange(buf);
            }
            return all;
        }

        [Test]
        public void NormalAdjacentLanePassIsJustAnOvertake()
        {
            var e = Pass(_ => 3.2f);
            Assert.AreEqual(1, e.Count);
            Assert.AreEqual(PassKind.Overtake, e[0].Kind);
        }

        [Test]
        public void LastSecondSwerveIsANearMiss()
        {
            // in the car's lane until it is 7 m ahead of the bumper, then out of the way
            var e = Pass(z => z > 12f ? 0f : 3.2f);
            Assert.IsTrue(e.Exists(x => x.Kind == PassKind.NearMiss), "swerve near miss");
            Assert.IsTrue(e.Exists(x => x.Kind == PassKind.Overtake));
        }

        [Test]
        public void EarlyLaneChangeIsNotANearMiss()
        {
            var e = Pass(z => z > 25f ? 0f : 3.2f);
            Assert.IsFalse(e.Exists(x => x.Kind == PassKind.NearMiss));
        }

        [Test]
        public void BrushingPastIsANearMissAndCrashesNeverScore()
        {
            Assert.IsTrue(Pass(_ => 2.3f).Exists(x => x.Kind == PassKind.NearMiss), "0.3 m side gap");
            Assert.IsEmpty(Pass(_ => 0f, hit: true), "hit = nothing");
            var cone = Pass(_ => 2.3f, moving: false);
            Assert.AreEqual(1, cone.Count, "static obstacle: near miss only, no overtake");
        }

        [Test]
        public void ComboMultiplierStepsLapsesAndBreaks()
        {
            var c = new ComboMeter();
            Assert.AreEqual(40, c.Add(PassKind.Overtake, false));
            c.Add(PassKind.Overtake, false);
            Assert.AreEqual(1, c.Multiplier);
            Assert.AreEqual(150 * 2, c.Add(PassKind.NearMiss, false), "x2 from the 3rd link");
            for (int i = 0; i < 12; i++) c.Add(PassKind.Overtake, false);
            Assert.AreEqual(5, c.Multiplier);
            Assert.AreEqual(40 * 5 * 2, c.Add(PassKind.Overtake, true), "Turbo doubles");
            c.Tick(ComboMeter.Window + 0.1f);
            Assert.AreEqual(0, c.Chain, "lapses without links");
            c.Add(PassKind.Overtake, false); c.Break();
            Assert.AreEqual(1, c.Multiplier);
            Assert.AreEqual(16, c.BestChain);
        }

        [Test]
        public void ServerBoundAllowsTheComboPoints()
        {
            var r = new RunReport { run_id = "x", duration = 300, distance = 3000, score = 999999999, near_misses = 20, overtakes = 100 };
            RunBounds.Clamp(r);
            Assert.AreEqual(4500 + 1650 + 1000 + 20 * 1500 + 100 * 400, r.score, "same number as the SQL test");
            r = new RunReport { run_id = "x", duration = 300, distance = 3000, near_misses = 99999, overtakes = 99999 };
            RunBounds.Clamp(r);
            Assert.AreEqual(305, r.near_misses);
            Assert.AreEqual(505, r.overtakes);
        }
    }
}
