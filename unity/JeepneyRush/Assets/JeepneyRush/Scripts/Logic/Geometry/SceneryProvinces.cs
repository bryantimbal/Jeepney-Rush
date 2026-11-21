using System;

namespace JeepneyRush.Logic.Geometry
{
    /// <summary>Provincial Road and Beach Highway scenery slices (40 m, both sides of the road).</summary>
    public static class SceneryProvinces
    {
        private const float L = SceneryBuilder.ChunkLength;
        private const float R = SceneryBuilder.RoadHalfWidth;
        private const float SY = SceneryBuilder.SidewalkHeight;

        public static MeshData Chunk(EnvironmentKind env, int seed, bool special)
        {
            switch (env)
            {
                case EnvironmentKind.Provincial: return Provincial(seed, special);
                case EnvironmentKind.Beach: return Beach(seed, special);
                default: return SceneryBuilder.ManilaChunk(seed, special);
            }
        }

        // ------------------------------------------------------------------ shared props
        public static void CoconutTree(MeshBuilder b, GameRandom rng, float x, float y, float z, float h)
        {
            float lean = rng.Range(-0.25f, 0.25f);
            int segs = 6;
            float px = x, py = y;
            for (int i = 0; i < segs; i++)
            {
                float sh = h / segs;
                float t = i / (float)segs;
                float dx = (float)Math.Sin(lean) * sh * (0.4f + t);
                b.Box("paint", i % 2 == 0 ? "#8D6E4F" : "#7A5C3E", 0.26f - t * 0.08f, sh + 0.05f, 0.26f - t * 0.08f, px + dx / 2, py + sh / 2, z, new Rot(0, 0, -lean * (0.4f + t)));
                px += dx;
                py += sh;
            }
            string[] greens = { "#2D6A4F", "#40916C", "#52B788" };
            for (int i = 0; i < 7; i++)
            {
                float a = i * (float)(Math.PI * 2 / 7) + rng.Range(0, 0.3f);
                b.Push(new V3(px, py, z), new Rot(0, a, 0));
                b.Box("paint", greens[i % 3], 0.5f, 0.06f, 2.2f, 0, -0.35f, 1.0f, new Rot(0.45f, 0, 0));
                b.Pop();
            }
            for (int i = 0; i < 3; i++) b.Sphere("paint", "#5C4033", 0.16f, px + (i - 1) * 0.2f, py - 0.25f, z + (i % 2) * 0.15f, 6);
        }

        private static void Mountain(MeshBuilder b, float x, float z, float h, float r, string color)
        {
            b.Cylinder("paint", color, 0f, r, h, 7, x, h / 2 - 1f, z);
            b.Cylinder("paint", Palette.Shade(color, 1.25f), 0f, r * 0.35f, h * 0.35f, 7, x, h - 1f - h * 0.17f + 0.02f, z);
        }

        private static void BahayKubo(MeshBuilder b, float x, float z, float yaw)
        {
            b.Push(new V3(x, SY, z), new Rot(0, yaw, 0));
            foreach (float sx in new[] { -1.2f, 1.2f }) foreach (float sz in new[] { -1.2f, 1.2f }) b.Box("paint", "#6B4F2C", 0.16f, 1.2f, 0.16f, sx, 0.6f, sz);
            b.Box("paint", "#A47148", 2.8f, 0.12f, 2.8f, 0, 1.2f, 0);
            b.Box("paint", "#D4A373", 2.6f, 1.6f, 2.6f, 0, 2.05f, 0);
            for (int i = 0; i < 6; i++) b.Box("paint", "#B98B57", 2.62f, 0.05f, 2.62f, 0, 1.4f + i * 0.28f, 0); // bamboo slats
            b.Box("glass", "#2B1D12", 0.7f, 0.6f, 0.05f, 0.5f, 2.1f, 1.31f);
            b.Box("glass", "#2B1D12", 0.6f, 1.1f, 0.05f, -0.6f, 1.8f, 1.31f);
            b.Cylinder("paint", "#9C7A3C", 0f, 2.6f, 1.8f, 4, 0, 3.75f, 0, new Rot(0, (float)(Math.PI / 4), 0));
            for (int i = 0; i < 4; i++) b.Box("paint", "#6B4F2C", 0.6f, 0.05f, 0.1f, -0.6f, 0.2f + i * 0.3f, 1.6f); // ladder rungs
            b.Pop();
        }

        private static void BasketballHoop(MeshBuilder b, float x, float z, float side)
        {
            b.Cylinder("paint", "#4A4E69", 0.07f, 0.09f, 3.2f, 6, x, SY + 1.6f, z);
            b.Box("paint", "#F8F9FA", 1.2f, 0.8f, 0.06f, x - side * 0.1f, SY + 3.1f, z - 0.3f);
            b.Box("paint", "#E63946", 0.5f, 0.35f, 0.07f, x - side * 0.1f, SY + 3.0f, z - 0.31f);
            var ring = new[] { new V2(0.22f, -0.02f), new V2(0.26f, -0.02f), new V2(0.26f, 0.02f), new V2(0.22f, 0.02f), new V2(0.22f, -0.02f) };
            b.Lathe("paint", "#FF7F11", ring, 12, x - side * 0.1f, SY + 2.85f, z - 0.6f, default, false);
        }

        private static void Carabao(MeshBuilder b, float x, float z, float yaw)
        {
            b.Push(new V3(x, SY, z), new Rot(0, yaw, 0));
            b.Box("paint", "#4A4A4F", 0.9f, 0.8f, 1.8f, 0, 1.0f, 0);
            b.Box("paint", "#3E3E42", 0.55f, 0.55f, 0.6f, 0, 1.1f, 1.1f);
            foreach (float sx in new[] { -1f, 1f })
            {
                b.Box("paint", "#C9C2B2", 0.7f, 0.08f, 0.1f, sx * 0.5f, 1.4f, 1.0f, new Rot(0, 0, sx * 0.5f));
                foreach (float sz in new[] { -0.6f, 0.6f }) b.Box("paint", "#3E3E42", 0.18f, 0.7f, 0.18f, sx * 0.3f, 0.35f, sz);
            }
            b.Pop();
        }

        private static void SariSari(MeshBuilder b, GameRandom rng, float x, float z, float side)
        {
            b.Box("paint", rng.Pick(new[] { "#F4A261", "#E9C46A", "#90BE6D" }), 2.4f, 2.4f, 3f, x, SY + 1.2f, z);
            b.Box("paint", "#2F2A2A", 0.1f, 1.2f, 2.2f, x - side * 1.21f, SY + 1.3f, z);
            for (int s = 0; s < 3; s++) b.Box("paint", rng.Pick(new[] { "#FFD166", "#EF476F", "#06D6A0" }), 0.1f, 0.2f, 2f, x - side * 1.25f, SY + 0.9f + s * 0.35f, z);
            b.Cylinder("paint", "#B5651D", 0f, 2.6f, 0.8f, 4, x, SY + 2.8f, z, new Rot(0, (float)(Math.PI / 4), 0));
            // shop sign facing the oncoming jeepney
            var (u0, v0, u1, v1) = PropBuilder.AtlasCell(0, 4, 4);
            float sx = x - side * 1.3f, syy = SY + 3.4f, sz = z - 1.6f;
            b.Box("paint", "#222222", 2.1f, 0.8f, 0.05f, sx, syy, sz);
            b.Quad("tex:shop_signs", new V3(sx + 1f, syy - 0.37f, sz - 0.035f), new V3(sx - 1f, syy - 0.37f, sz - 0.035f), new V3(sx - 1f, syy + 0.37f, sz - 0.035f), new V3(sx + 1f, syy + 0.37f, sz - 0.035f), new V3(0, 0, -1),
                new V2(u1, v0), new V2(u0, v0), new V2(u0, v1), new V2(u1, v1));
        }

        private static void PowerPole(MeshBuilder b, GameRandom rng, float side)
        {
            float px = side * (R + 0.8f);
            b.Cylinder("paint", "#8D7B68", 0.12f, 0.16f, 8, 6, px, SY + 4f, -L / 2 + 2);
            b.Box("paint", "#5C4B3B", 1.6f, 0.12f, 0.12f, px, SY + 7.6f, -L / 2 + 2);
            for (int w = 0; w < 2; w++) b.Box("paint", "#1B1B1B", 0.03f, 0.03f, L, px + (w - 0.5f) * 1.2f, SY + 7.5f + rng.Range(-0.05f, 0.05f), 2);
        }

        // ------------------------------------------------------------------ provincial
        public static MeshData Provincial(int seed, bool chapel)
        {
            var rng = new GameRandom(seed);
            var b = new MeshBuilder();
            foreach (float side in new[] { -1f, 1f })
            {
                // rice paddies with dikes
                for (float pz = -L / 2; pz < L / 2; pz += 10f)
                    for (float px = 10f; px < 46f; px += 9f)
                    {
                        string c = rng.Pick(new[] { "#7CB518", "#5C9E1F", "#9ACD32", "#8FBF5A", "#6FA8A0" });
                        b.Box("paint", c, 8.4f, 0.12f, 9.4f, side * (px + 4.5f), 0.08f, pz + 5f);
                        if (c != "#6FA8A0")
                            for (int row = 0; row < 6; row++) b.Box("paint", Palette.Shade(c, 0.8f), 8f, 0.2f, 0.12f, side * (px + 4.5f), 0.2f, pz + 1f + row * 1.5f);
                        b.Box("paint", "#8B6B3D", 0.35f, 0.25f, 9.6f, side * (px + 8.85f), 0.14f, pz + 5f);
                    }
                PowerPole(b, rng, side);
                // coconut trees along the road
                int trees = rng.Int(2, 4);
                for (int i = 0; i < trees; i++) CoconutTree(b, rng, side * rng.Range(R + 1.4f, 9f), SY, rng.Range(-L / 2, L / 2), rng.Range(6f, 9f));
                // a farmhouse, a sari-sari store or a basketball court
                float roll = rng.Range(0, 1);
                float hz = rng.Range(-L / 2 + 6, L / 2 - 6);
                if (roll < 0.45f) BahayKubo(b, side * rng.Range(12f, 16f), hz, rng.Range(0, 6.28f));
                else if (roll < 0.75f) SariSari(b, rng, side * (R + 3.2f), hz, side);
                else
                {
                    b.Box("paint", "#9E9E9E", 7f, 0.05f, 10f, side * 14f, 0.15f, hz);
                    BasketballHoop(b, side * 14f, hz + 4.5f, side);
                }
                if (rng.Chance(0.35)) Carabao(b, side * rng.Range(18f, 30f), rng.Range(-15, 15), rng.Range(0, 6.28f));
                // idle townsfolk
                for (int i = 0; i < rng.Int(0, 2); i++)
                {
                    b.Push(new V3(side * rng.Range(R + 1f, 8.5f), SY, rng.Range(-L / 2, L / 2)), new Rot(0, rng.Range(0, 6.28f), 0));
                    PropBuilder.AddPerson(b, rng.Int(0, 7), false);
                    b.Pop();
                }
                // distant mountains
                for (int i = 0; i < 2; i++) Mountain(b, side * rng.Range(60, 95), rng.Range(-L / 2, L / 2), rng.Range(22, 42), rng.Range(18, 28), rng.Pick(new[] { "#5F8F7A", "#6C9A8B", "#4F7F6A" }));
            }
            if (chapel)
            {
                float cx = 13f;
                b.Box("paint", "#F8F4E3", 6f, 5f, 8f, cx, SY + 2.5f, 0);
                b.Cylinder("paint", "#B23A48", 0f, 5.2f, 2.6f, 4, cx, SY + 6.3f, 0, new Rot(0, (float)(Math.PI / 4), 0));
                b.Box("paint", "#F8F4E3", 1.8f, 8f, 1.8f, cx, SY + 4f, -4.5f);
                b.Box("paint", "#FFD166", 0.15f, 1.2f, 0.15f, cx, SY + 8.6f, -4.5f);
                b.Box("paint", "#FFD166", 0.7f, 0.15f, 0.15f, cx, SY + 8.8f, -4.5f);
                b.Box("glass", "#5A3E2B", 1.6f, 2.4f, 0.05f, cx - 3.01f, SY + 1.2f, 0, new Rot(0, (float)(Math.PI / 2), 0));
            }
            return b.Data;
        }

        // ------------------------------------------------------------------ beach
        private static void Bangka(MeshBuilder b, GameRandom rng, float x, float z, float yaw)
        {
            b.Push(new V3(x, 0.15f, z), new Rot(0, yaw, 0));
            string hull = rng.Pick(new[] { "#F8F9FA", "#4CC9F0", "#FFD166", "#EF476F" });
            var prof = new System.Collections.Generic.List<V2> { new V2(-3.2f, 0.5f), new V2(-2.6f, 0f), new V2(2.6f, 0f), new V2(3.4f, 0.6f), new V2(3.0f, 0.7f), new V2(-3.0f, 0.65f) };
            b.ExtrudeX("paint", hull, prof, -0.4f, 0.4f);
            b.Box("paint", "#E63946", 0.82f, 0.08f, 5.6f, 0, 0.6f, 0);
            foreach (float sx in new[] { -1f, 1f })
            {
                foreach (float zz in new[] { -1.2f, 1.2f }) b.CylinderX("paint", "#C9A66B", 0.04f, 2.6f, 5, sx * 1.3f, 0.55f, zz);
                b.CylinderZ("paint", "#C9A66B", 0.08f, 5.2f, 6, sx * 2.5f, 0.15f, 0);
            }
            b.Cylinder("paint", "#8D6E4F", 0.04f, 0.04f, 2.2f, 5, 0, 1.6f, 0.5f);
            b.Pop();
        }

        private static void Umbrella(MeshBuilder b, GameRandom rng, float x, float z)
        {
            b.Cylinder("paint", "#EEEEEE", 0.03f, 0.03f, 2.2f, 5, x, SY + 1.1f, z);
            string c = rng.Pick(new[] { "#EF476F", "#FFD166", "#06D6A0", "#118AB2", "#F78C6B" });
            b.Cylinder("paint", c, 0.05f, 1.3f, 0.45f, 8, x, SY + 2.3f, z);
            b.Box("paint", Palette.Shade(c, 1.3f), 0.6f, 0.08f, 1.6f, x + 0.6f, SY + 0.25f, z); // beach towel / lounger
        }

        public static MeshData Beach(int seed, bool lighthouse)
        {
            var rng = new GameRandom(seed);
            var b = new MeshBuilder();
            // ---- sea side (left): sand beach, shallow turquoise, deep blue, waves and bangkas
            b.Box("paint", "#E9D8A6", 10f, 0.1f, L, -(SceneryBuilder.SidewalkOuter + 5f), 0.12f, 0);
            b.Box("paint", "#4CC9F0", 14f, 0.05f, L, -(SceneryBuilder.SidewalkOuter + 17f), 0.06f, 0);
            b.Box("paint", "#1F7EA1", 200f, 0.04f, L, -(SceneryBuilder.SidewalkOuter + 124f), 0.02f, 0);
            for (int i = 0; i < 6; i++)
                b.Box("glow", "#E8FBFF", rng.Range(2f, 6f), 0.03f, 0.15f, -(SceneryBuilder.SidewalkOuter + rng.Range(10.5f, 30f)), 0.1f, rng.Range(-L / 2, L / 2), new Rot(0, (float)(Math.PI / 2) + rng.Range(-0.2f, 0.2f), 0));
            for (int i = 0; i < rng.Int(1, 3); i++) Bangka(b, rng, -(SceneryBuilder.SidewalkOuter + rng.Range(16f, 40f)), rng.Range(-L / 2 + 4, L / 2 - 4), rng.Range(-0.6f, 0.6f));
            for (int i = 0; i < rng.Int(1, 3); i++) Umbrella(b, rng, -rng.Range(R + 2f, SceneryBuilder.SidewalkOuter + 6f), rng.Range(-L / 2 + 2, L / 2 - 2));
            for (int i = 0; i < rng.Int(2, 3); i++) CoconutTree(b, rng, -rng.Range(R + 1.2f, SceneryBuilder.SidewalkOuter + 4f), SY, rng.Range(-L / 2, L / 2), rng.Range(6f, 9.5f));
            if (lighthouse)
            {
                float lx = -(SceneryBuilder.SidewalkOuter + 45f);
                b.Cylinder("paint", "#9E9E9E", 4f, 4.5f, 2f, 10, lx, 1f, 0);
                for (int i = 0; i < 5; i++) b.Cylinder("paint", i % 2 == 0 ? "#FFFFFF" : "#D62828", 1.6f - i * 0.12f, 1.72f - i * 0.12f, 3f, 10, lx, 3.5f + i * 3f, 0);
                b.Cylinder("glow", "#FFF3B0", 1f, 1f, 1.2f, 10, lx, 18.6f, 0);
                b.Cylinder("paint", "#D62828", 0f, 1.3f, 1.2f, 10, lx, 19.8f, 0);
            }
            // ---- land side (right): resorts, palms, sari-sari, green hills
            float z = -L / 2;
            while (z < L / 2 - 4)
            {
                float w = Math.Min(rng.Range(8, 13), L / 2 - z);
                if (rng.Chance(0.6))
                {
                    float h = rng.Pick(new[] { 4f, 6f, 6f, 9f });
                    float cx = SceneryBuilder.SidewalkOuter + 4f + rng.Range(0, 3);
                    string wall = rng.Pick(new[] { "#FFD6A5", "#CAFFBF", "#9BF6FF", "#FDFFB6", "#FFADAD", "#F7EDE2" });
                    b.Box("paint", wall, 7f, h, w - 1f, cx, SY + h / 2, z + w / 2);
                    for (int f = 0; f < (int)(h / 3); f++)
                        for (float wz = z + 1.5f; wz < z + w - 1.5f; wz += 2.2f)
                            b.Box("glass", "#3A6EA5", 0.06f, 1.2f, 1.2f, cx - 3.52f, SY + 1.6f + f * 3f, wz);
                    b.Cylinder("paint", rng.Pick(new[] { "#B5651D", "#E76F51", "#2A9D8F" }), 0f, (w - 1f) * 0.75f, 2.2f, 4, cx, SY + h + 1.1f, z + w / 2, new Rot(0, (float)(Math.PI / 4), 0));
                }
                else SariSari(b, rng, R + 3.2f, z + w / 2, 1f);
                z += w;
            }
            for (int i = 0; i < rng.Int(1, 3); i++) CoconutTree(b, rng, rng.Range(R + 1.2f, SceneryBuilder.SidewalkOuter - 0.5f), SY, rng.Range(-L / 2, L / 2), rng.Range(6f, 9f));
            for (int i = 0; i < 2; i++) b.Sphere("paint", rng.Pick(new[] { "#52B788", "#40916C", "#74C69D" }), rng.Range(9, 14), rng.Range(48, 70), 0, rng.Range(-L / 2, L / 2), 7, 1.4f, 0.7f, 1.2f);
            PowerPole(b, rng, 1f);
            for (int i = 0; i < rng.Int(0, 3); i++)
            {
                b.Push(new V3(rng.Range(-SceneryBuilder.SidewalkOuter, SceneryBuilder.SidewalkOuter) > 0 ? rng.Range(R + 1f, 8.5f) : -rng.Range(R + 1f, 12f), SY, rng.Range(-L / 2, L / 2)), new Rot(0, rng.Range(0, 6.28f), 0));
                PropBuilder.AddPerson(b, rng.Int(0, 7), false);
                b.Pop();
            }
            return b.Data;
        }
    }
}
