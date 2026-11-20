using System.Collections.Generic;
using JeepneyRush.Logic;
using JeepneyRush.Logic.Geometry;
using NUnit.Framework;

namespace JeepneyRush.Tests
{
    public class GeometryTests
    {
        private static IEnumerable<(string, MeshData)> All()
        {
            foreach (var l in JeepneyCatalog.All) yield return ("jeepney_" + l.Id, JeepneyBuilder.Build(l));
            yield return ("wheel", JeepneyBuilder.BuildWheel());
            foreach (var m in ModelCatalog.Preview()) yield return m;
            for (int i = 0; i < 6; i++) yield return ("chunk" + i, SceneryBuilder.ManilaChunk(1000 + i * 7919, i == 2));
        }

        [Test]
        public void MeshesAreWellFormedAndMobileFriendly()
        {
            foreach (var (name, m) in All())
            {
                Assert.AreEqual(m.Positions.Count, m.Normals.Count, name);
                Assert.AreEqual(m.Positions.Count, m.Uvs.Count, name);
                Assert.Less(m.Positions.Count, 65535, $"{name} exceeds 16-bit indices");
                Assert.Greater(m.TriangleCount, 0, name);
                foreach (var sub in m.SubmeshIndices)
                {
                    Assert.AreEqual(0, sub.Count % 3, name);
                    foreach (int i in sub) Assert.That(i, Is.InRange(0, m.Positions.Count - 1), name);
                }
                foreach (var n in m.Normals) Assert.That(n.Length, Is.InRange(0.98f, 1.02f), $"{name} has a bad normal");
            }
            Assert.LessOrEqual(Palette.Shared.Colors.Count, Palette.Size * Palette.Size);
        }

        [Test]
        public void TrianglesFaceTheirNormals()
        {
            // Unity convention: Cross(b-a, c-a) points out of the front face.
            foreach (var (name, m) in All())
            {
                int bad = 0, total = 0;
                foreach (var sub in m.SubmeshIndices)
                    for (int t = 0; t < sub.Count; t += 3)
                    {
                        var a = m.Positions[sub[t]]; var b = m.Positions[sub[t + 1]]; var c = m.Positions[sub[t + 2]];
                        var cr = V3.Cross(b - a, c - a);
                        if (cr.Length < 1e-7f) continue;
                        total++;
                        var n = m.Normals[sub[t]] + m.Normals[sub[t + 1]] + m.Normals[sub[t + 2]];
                        if (V3.Dot(cr, n) < 0) bad++;
                    }
                Assert.AreEqual(0, bad, $"{name}: {bad}/{total} triangles wound against their normals");
            }
        }

        [Test]
        public void HeroJeepneyMatchesGameplayFootprint()
        {
            var cfg = new BalanceConfig();
            var (min, max) = JeepneyBuilder.Build(JeepneyCatalog.All[0]).Bounds();
            Assert.That(max.Z - min.Z, Is.InRange(5.5f, 7.2f));
            // the collision box is intentionally a little smaller than the body (forgiving)
            Assert.Less(cfg.playerHalfLength * 2, max.Z - min.Z);
            Assert.Less(cfg.playerHalfWidth * 2, 2.1f);
        }

        [Test]
        public void TriangulateHandlesConcavePolygons()
        {
            var poly = new List<V2> { new V2(0, 0), new V2(4, 0), new V2(4, 3), new V2(2, 1), new V2(0, 3) };
            var tris = MeshBuilder.Triangulate(poly);
            Assert.AreEqual((poly.Count - 2) * 3, tris.Count);
        }
    }
}
