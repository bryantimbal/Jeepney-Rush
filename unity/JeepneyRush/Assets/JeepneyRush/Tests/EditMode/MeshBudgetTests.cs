using System;
using System.Linq;
using JeepneyRush.Logic;
using JeepneyRush.Logic.Geometry;
using NUnit.Framework;

namespace JeepneyRush.Tests
{
    /// <summary>
    /// Performance guard rails: keeps every procedural mesh within budgets that run smoothly on
    /// mid-range Android phones (and within 16-bit index buffers), so new scenery can't quietly
    /// make the game slower. Raise a budget only after profiling on a real device.
    /// </summary>
    public class MeshBudgetTests
    {
        private const int VehicleVerts = 20000, ChunkVerts = 18000, PropVerts = 16000;
        private const int ChunkSubmeshes = 22; // ≈ draw calls per chunk (one per material)

        [Test]
        public void EveryCatalogModelFitsItsBudget()
        {
            foreach (var (name, mesh) in ModelCatalog.Preview())
            {
                int budget = name.StartsWith("jeepney") || name.StartsWith("cockpit") ? VehicleVerts : name.StartsWith("chunk") ? ChunkVerts : PropVerts;
                Assert.LessOrEqual(mesh.Positions.Count, budget, $"{name}: {mesh.Positions.Count} vertices");
                Assert.Less(mesh.Positions.Count, 65000, $"{name} needs 32-bit indices");
            }
        }

        [Test]
        public void SceneryChunksStayWithinBudgetOnEveryRoute()
        {
            foreach (EnvironmentKind env in Enum.GetValues(typeof(EnvironmentKind)))
                for (int seed = 0; seed < 40; seed++)
                    foreach (bool special in new[] { false, true })
                    {
                        var m = SceneryProvinces.Chunk(env, 1000 + seed * 7919, special);
                        Assert.LessOrEqual(m.Positions.Count, ChunkVerts, $"{env} seed {seed} special {special}: {m.Positions.Count} vertices");
                        Assert.LessOrEqual(m.SubmeshKeys.Count, ChunkSubmeshes, $"{env}: {m.SubmeshKeys.Count} materials");
                    }
        }

        [Test]
        public void IntersectionsStayWithinBudgetOnEveryRoute()
        {
            foreach (EnvironmentKind env in Enum.GetValues(typeof(EnvironmentKind)))
            {
                var m = PropBuilder.Intersection(env);
                Assert.LessOrEqual(m.Positions.Count, PropVerts, $"{env}: {m.Positions.Count} vertices");
            }
        }
    }
}
