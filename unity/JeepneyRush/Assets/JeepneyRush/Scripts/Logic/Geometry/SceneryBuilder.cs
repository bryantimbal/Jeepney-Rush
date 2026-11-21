using System;
using System.Collections.Generic;

namespace JeepneyRush.Logic.Geometry
{
    /// <summary>
    /// One 40 m slice of Manila street scenery for both sides of the road.
    /// A few variants are pre-built at load and recycled by RoadManager.
    /// Facades use submesh keys "tex:facade|#RRGGBB" (texture tinted per building).
    /// </summary>
    public static class SceneryBuilder
    {
        public const float ChunkLength = 40f;
        public const float RoadHalfWidth = 5.6f;   // 3 lanes x 3.2 m + shoulders
        public const float SidewalkOuter = 9.0f;   // building line
        public const float SidewalkHeight = 0.2f;

        public static readonly string[] Walls = { "#F6BD60", "#F7EDE2", "#84A59D", "#F28482", "#BDE0FE", "#CDB4DB", "#FFC8DD", "#A3C4BC", "#E9EDC9", "#FFD6A5" };
        private static readonly string[] Awnings = { "#E63946", "#2A9D8F", "#F4A261", "#1D3557", "#FFB703", "#8338EC", "#06D6A0" };
        public static readonly string[] Towers = { "#D6E2E9", "#BCD4E6", "#E2ECE9", "#C9D6DF" };

        public static IEnumerable<string> FacadeKeys
        {
            get { foreach (var w in Walls) yield return "tex:facade|" + w; foreach (var t in Towers) yield return "tex:facade|" + t; }
        }

        /// <summary>Box whose faces tile the facade texture every <paramref name="tile"/> metres.</summary>
        public static void FacadeBox(MeshBuilder b, string key, float w, float h, float d, float x, float y, float z, float tile = 6f)
        {
            float hx = w / 2, hy = h / 2, hz = d / 2;
            V3 P(float sx, float sy, float sz) => new V3(x + sx * hx, y + sy * hy, z + sz * hz);
            V2 U(float u, float v) => new V2(u / tile, v / tile);
            b.Quad(key, P(1, -1, -1), P(1, 1, -1), P(1, 1, 1), P(1, -1, 1), new V3(1, 0, 0), U(0, 0), U(0, h), U(d, h), U(d, 0));
            b.Quad(key, P(-1, -1, 1), P(-1, 1, 1), P(-1, 1, -1), P(-1, -1, -1), new V3(-1, 0, 0), U(0, 0), U(0, h), U(d, h), U(d, 0));
            b.Quad(key, P(-1, -1, 1), P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1), new V3(0, 0, 1), U(0, 0), U(w, 0), U(w, h), U(0, h));
            b.Quad(key, P(1, -1, -1), P(-1, -1, -1), P(-1, 1, -1), P(1, 1, -1), new V3(0, 0, -1), U(0, 0), U(w, 0), U(w, h), U(0, h));
            b.QuadColor("paint", "#9A9A9A", P(-1, 1, -1), P(-1, 1, 1), P(1, 1, 1), P(1, 1, -1), V3.Up);
        }

        private static void SignQuad(MeshBuilder b, int index, float w, float h, V3 c, bool facingPlusZ)
        {
            var (u0, v0, u1, v1) = PropBuilder.AtlasCell(index, 4, 4);
            float hw = w / 2, hh = h / 2;
            if (facingPlusZ) // viewer looking toward -Z: -X is on the right
                b.Quad("tex:shop_signs", new V3(c.X + hw, c.Y - hh, c.Z), new V3(c.X - hw, c.Y - hh, c.Z), new V3(c.X - hw, c.Y + hh, c.Z), new V3(c.X + hw, c.Y + hh, c.Z), new V3(0, 0, 1),
                    new V2(u0, v0), new V2(u1, v0), new V2(u1, v1), new V2(u0, v1));
            else
                b.Quad("tex:shop_signs", new V3(c.X - hw, c.Y - hh, c.Z), new V3(c.X + hw, c.Y - hh, c.Z), new V3(c.X + hw, c.Y + hh, c.Z), new V3(c.X - hw, c.Y + hh, c.Z), new V3(0, 0, -1),
                    new V2(u0, v0), new V2(u1, v0), new V2(u1, v1), new V2(u0, v1));
        }

        public static MeshData ManilaChunk(int seed, bool crossing)
        {
            var rng = new GameRandom(seed);
            var b = new MeshBuilder();
            const float L = ChunkLength;
            float sy = SidewalkHeight;

            foreach (float side in new[] { -1f, 1f })
            {
                // ---- buildings packed along the street
                float z = -L / 2;
                while (z < L / 2 - 2)
                {
                    float w = Math.Min(rng.Range(6, 11), L / 2 - z);
                    float h = rng.Pick(new[] { 6f, 9f, 9f, 12f, 15f, 18f, 24f }) + rng.Range(-0.5f, 0.5f);
                    float d = rng.Range(7, 11);
                    float cz = z + w / 2;
                    float cx = side * (SidewalkOuter + d / 2 + rng.Range(0, 0.6f));
                    string wall = rng.Pick(Walls);
                    FacadeBox(b, "tex:facade|" + wall, d, h, w - 0.3f, cx, sy + h / 2, cz);
                    NightKit.Windows(b, rng, cx - side * d / 2, side < 0, cz, w - 0.3f, sy, h);
                    b.Box("paint", Palette.Shade(wall, 0.8f), d + 0.2f, 0.4f, w - 0.1f, cx, sy + h + 0.2f, cz);
                    if (rng.Chance(0.5)) b.Cylinder("paint", rng.Pick(new[] { "#1D4ED8", "#334155", "#E5E7EB" }), 0.7f, 0.7f, 1.4f, 8, cx + rng.Range(-1, 1), sy + h + 1.1f, cz + rng.Range(-1.5f, 1.5f));
                    // ground-floor storefront with goods and a striped awning
                    float front = side * SidewalkOuter;
                    b.Box("paint", "#2F2A2A", 0.2f, 2.9f, w - 0.5f, front + side * 0.05f, sy + 1.45f, cz);
                    b.Box("paint", rng.Pick(new[] { "#3A2F2A", "#4A3F35", "#26323D" }), 0.15f, 2.4f, w - 1.4f, front - side * 0.05f, sy + 1.3f, cz);
                    for (int s = 0; s < 3; s++) b.Box("paint", rng.Pick(new[] { "#FFD166", "#EF476F", "#06D6A0", "#118AB2", "#F78C6B" }), 0.12f, 0.25f, w - 1.8f, front - side * 0.12f, sy + 0.6f + s * 0.6f, cz);
                    string aw = rng.Pick(Awnings);
                    for (int s = 0; s < (int)Math.Floor(w - 0.6f); s++)
                        b.Box("paint", s % 2 == 1 ? aw : "#FFFFFF", 1.6f, 0.08f, 1f, front - side * 0.8f, sy + 3.15f, z + 0.8f + s, new Rot(0, 0, -side * 0.28f));
                    // blade sign perpendicular to the street – readable from the chase camera
                    if (rng.Chance(0.75))
                    {
                        int si = rng.Int(0, 15);
                        float sw = 2.3f, sh = 0.86f;
                        float sx = front - side * (sw / 2 + 0.2f);
                        float syy = sy + rng.Range(4.2f, Math.Min(h - 1, 7));
                        float bz = cz - w / 2 + 0.6f;
                        b.Box("paint", "#222222", sw + 0.12f, sh + 0.12f, 0.05f, sx, syy, bz);
                        SignQuad(b, si, sw, sh, new V3(sx, syy, bz + 0.035f), true);
                        SignQuad(b, si, sw, sh, new V3(sx, syy, bz - 0.035f), false);
                    }
                    if (h < 16 && rng.Chance(0.3))
                    {
                        float by = sy + h + 3.2f;
                        b.Box("paint", "#333333", 7.2f, 2.8f, 0.2f, cx, by, cz);
                        SignQuad(b, rng.Int(0, 15), 7f, 2.6f, new V3(cx, by, cz + 0.11f), true);
                        b.Box("paint", "#555555", 0.2f, 2.2f, 0.2f, cx - 2.5f, sy + h + 1.0f, cz);
                        b.Box("paint", "#555555", 0.2f, 2.2f, 0.2f, cx + 2.5f, sy + h + 1.0f, cz);
                    }
                    z += w;
                }
                // ---- skyline towers far behind
                for (int i = 0; i < 2; i++)
                {
                    if (!rng.Chance(0.55)) continue;
                    float th = rng.Range(28, 60), tw = rng.Range(10, 16);
                    FacadeBox(b, "tex:facade|" + rng.Pick(Towers), tw, th, tw, side * rng.Range(30, 46), th / 2, rng.Range(-L / 2, L / 2), 7.5f);
                }
                // ---- power pole with tangled wires (very Manila)
                float px = side * (RoadHalfWidth + 0.6f);
                b.Cylinder("paint", "#6D6875", 0.13f, 0.17f, 9, 6, px, sy + 4.5f, -L / 2 + 2);
                b.Box("paint", "#4A4E69", 2.2f, 0.14f, 0.14f, px - side * 0.3f, sy + 8.4f, -L / 2 + 2);
                if (rng.Chance(0.5)) b.Cylinder("paint", "#8D99AE", 0.35f, 0.35f, 0.9f, 8, px + side * 0.3f, sy + 7.4f, -L / 2 + 2);
                for (int wi = 0; wi < 5; wi++)
                {
                    float wx = px - side * (0.9f - wi * 0.35f);
                    float wy = sy + 8.2f - wi * 0.25f + rng.Range(-0.1f, 0.1f);
                    b.Box("paint", "#1B1B1B", 0.035f, 0.035f, L, wx, wy, 2, new Rot(rng.Range(-0.012f, 0.012f), 0, 0));
                }
                // ---- street lamp
                float lz = rng.Range(-L / 2 + 8, L / 2 - 6);
                b.Cylinder("paint", "#495057", 0.07f, 0.09f, 6, 6, side * (RoadHalfWidth + 0.5f), sy + 3, lz);
                b.Box("paint", "#495057", 1.6f, 0.08f, 0.1f, side * (RoadHalfWidth - 0.2f), sy + 6, lz);
                b.Box("glow", "#FFF1C1", 0.5f, 0.1f, 0.3f, side * (RoadHalfWidth - 0.8f), sy + 5.93f, lz);
                NightKit.Pool(b, side * (RoadHalfWidth - 1.2f), 0.035f, lz, 3.6f);
                // ---- tropical trees
                if (rng.Chance(0.6))
                {
                    float tz = rng.Range(-L / 2 + 4, L / 2 - 4), tx = side * (RoadHalfWidth + 1.6f);
                    b.Cylinder("paint", "#6B4226", 0.18f, 0.25f, 3, 6, tx, sy + 1.5f, tz);
                    string leaf = rng.Pick(new[] { "#2D6A4F", "#40916C", "#52B788", "#386641" });
                    b.Sphere("paint", leaf, 1.6f, tx, sy + 3.8f, tz, 7, 1.1f, 0.8f, 1.1f);
                    b.Sphere("paint", leaf, 1.1f, tx + side * 0.6f, sy + 4.6f, tz + 0.5f, 6);
                }
                // ---- street vendor cart with umbrella
                if (rng.Chance(0.45))
                {
                    float vz = rng.Range(-L / 2 + 5, L / 2 - 5), vx = side * (RoadHalfWidth + 1.4f);
                    b.Box("paint", rng.Pick(new[] { "#E9C46A", "#2A9D8F", "#E76F51" }), 0.9f, 0.8f, 1.5f, vx, sy + 0.75f, vz);
                    b.CylinderX("paint", "#222222", 0.25f, 0.1f, 8, vx + 0.5f, sy + 0.25f, vz);
                    b.Cylinder("paint", "#333333", 0.03f, 0.03f, 1.6f, 5, vx, sy + 1.9f, vz);
                    b.Cylinder("paint", rng.Pick(new[] { "#EF476F", "#FFD166", "#06D6A0", "#118AB2" }), 0.05f, 1.3f, 0.5f, 8, vx, sy + 2.75f, vz);
                    b.Cylinder("chrome", "#C0C0C0", 0.3f, 0.3f, 0.3f, 8, vx, sy + 1.3f, vz - 0.3f);
                }
                // ---- idle pedestrians
                int nPeds = rng.Int(0, 3);
                for (int i = 0; i < nPeds; i++)
                {
                    b.Push(new V3(side * rng.Range(RoadHalfWidth + 0.9f, SidewalkOuter - 0.6f), sy, rng.Range(-L / 2, L / 2)), new Rot(0, rng.Range(0, 6.28f), 0));
                    PropBuilder.AddPerson(b, rng.Int(0, 7), false);
                    b.Pop();
                }
            }

            if (crossing)
            {
                for (float x = -5.1f; x <= 5.1f; x += 0.95f) b.Box("paint", "#F1F1F1", 0.5f, 0.02f, 3.2f, x, 0.012f, 0);
                foreach (float side in new[] { -1f, 1f })
                {
                    b.Cylinder("paint", "#888888", 0.05f, 0.05f, 2.6f, 6, side * (RoadHalfWidth + 0.35f), sy + 1.3f, -2.4f);
                    b.Box("paint", "#1D4ED8", 0.7f, 0.7f, 0.05f, side * (RoadHalfWidth + 0.35f), sy + 2.7f, -2.4f, new Rot(0, 0, (float)(Math.PI / 4)));
                }
            }
            return b.Data;
        }
    }
}
