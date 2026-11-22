using System;
using System.Collections.Generic;
using System.Linq;
using JeepneyRush.Logic;
using JeepneyRush.Logic.Geometry;
using NUnit.Framework;

namespace JeepneyRush.Tests
{
    public class VendorTests
    {
        private const float Rw = SceneryBuilder.RoadHalfWidth, So = SceneryBuilder.SidewalkOuter;

        /// <summary>Vertices of every submesh except ground decals (night light pools).</summary>
        private static IEnumerable<V3> Solid(MeshData m)
        {
            for (int s = 0; s < m.SubmeshKeys.Count; s++)
            {
                if (m.SubmeshKeys[s] == "lightpool") continue;
                foreach (int i in m.SubmeshIndices[s]) yield return m.Positions[i];
            }
        }

        [Test]
        public void EveryVendorStaysAtTheBuildingLineOffTheRoadAndTheWalkway()
        {
            foreach (EnvironmentKind env in Enum.GetValues(typeof(EnvironmentKind)))
                foreach (VendorKind kind in Enum.GetValues(typeof(VendorKind)))
                    foreach (float side in new[] { -1f, 1f })
                        for (int v = 0; v < 6; v++)
                        {
                            var b = new MeshBuilder();
                            VendorBuilder.Vendor(b, kind, side * VendorBuilder.VendorX, SceneryBuilder.SidewalkHeight, 0, side, v, env);
                            Assert.Greater(b.Data.Positions.Count, 20, $"{kind} is empty");
                            foreach (var p in Solid(b.Data))
                            {
                                Assert.Greater(side * p.X, Rw, $"{kind} {env} reaches the road");
                                // below head height nothing sticks into the walkers' lane (umbrellas may overhang)
                                if (p.Y < 1.8f) Assert.Greater(side * p.X, So - 1.4f, $"{kind} {env} blocks the walkway at y {p.Y:F2}");
                            }
                        }
        }

        [Test]
        public void TricycleTerminalsOnlyWhereTricyclesAreAllowed()
        {
            foreach (EnvironmentKind env in Enum.GetValues(typeof(EnvironmentKind)))
            {
                var keys = PropBuilder.Intersection(env).SubmeshKeys;
                Assert.AreEqual(Environments.Tricycles(env), keys.Contains("utex:toda_sign"), env.ToString());
            }
            Assert.IsFalse(Environments.Tricycles(EnvironmentKind.Edsa));
            Assert.IsFalse(Environments.Tricycles(EnvironmentKind.Baguio));
        }

        [Test]
        public void ParkedTricyclesStayOnTheSidewalkAndLeaveACurbPath()
        {
            foreach (var t in PropBuilder.TodaZones)
            {
                var b = new MeshBuilder();
                VendorBuilder.TodaTerminal(b, t.Side, t.Z0, t.Length, 3);
                foreach (var p in Solid(b.Data))
                {
                    Assert.Greater(t.Side * p.X, Rw + 0.6f, "terminal reaches the curb path / road");
                    Assert.LessOrEqual(t.Side * p.X, So + 0.05f, "terminal pokes into the buildings");
                    Assert.That(p.Z, Is.InRange(t.Z0 - 0.3f, t.Z0 + t.Length + 0.3f), "terminal spills out of its zone");
                }
                Assert.Less(VendorBuilder.CurbWalkX, VendorBuilder.TrikeX - 1.2f);
            }
            // terminals clear the zebra crossings (|z| 5.9..8.9) and the side street (|z| < 5)
            foreach (var t in PropBuilder.TodaZones)
                Assert.IsTrue(t.Z0 > 9f || t.Z0 + t.Length < -9f);
        }

        [Test]
        public void RouteVendorsFitTheRoute()
        {
            CollectionAssert.Contains(VendorBuilder.KindsFor(EnvironmentKind.Baguio), VendorKind.Taho);
            CollectionAssert.Contains(VendorBuilder.KindsFor(EnvironmentKind.Beach), VendorKind.Fruit);
            CollectionAssert.Contains(VendorBuilder.KindsFor(EnvironmentKind.Manila), VendorKind.Fishball);
            Assert.IsTrue(VendorBuilder.KindsFor(EnvironmentKind.Mountain).All(k => k != VendorKind.Sorbetes));
        }
    }
}
