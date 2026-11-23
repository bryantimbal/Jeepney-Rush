using System;

namespace JeepneyRush.Logic.Geometry
{
    /// <summary>Provincial Road, Beach Highway and Mountain Province scenery slices (40 m, both sides of the road).</summary>
    public static class SceneryProvinces
    {
        private const float L = SceneryBuilder.ChunkLength;
        private const float R = SceneryBuilder.RoadHalfWidth;
        private const float SY = SceneryBuilder.SidewalkHeight;

        /// <param name="feature">the route's landmark slice: EDSA flyover, coastal bridge</param>
        public static MeshData Chunk(EnvironmentKind env, int seed, bool special, bool feature = false)
        {
            switch (env)
            {
                case EnvironmentKind.Provincial: return Provincial(seed, special);
                case EnvironmentKind.Beach: return feature ? CoastalBridge(seed) : Beach(seed, special);
                case EnvironmentKind.Mountain: return MountainProvince(seed, special);
                case EnvironmentKind.Edsa: return SceneryCities.Edsa(seed, special && !feature, feature);
                case EnvironmentKind.Cebu: return SceneryCities.Cebu(seed, special);
                case EnvironmentKind.Baguio: return SceneryCities.Baguio(seed, special);
                case EnvironmentKind.Fiesta: return SceneryCities.Fiesta(seed, special, feature);
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

        public static void Mountain(MeshBuilder b, float x, float z, float h, float r, string color)
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
                if (rng.Chance(0.35)) VendorBuilder.Random(b, rng, EnvironmentKind.Provincial, side, SY, rng.Range(-L / 2 + 4, L / 2 - 4));
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
            NightKit.StreetLamp(b, rng.Chance(0.5) ? -1f : 1f, R, rng.Range(-L / 2 + 6, L / 2 - 6), 5.5f, "#6D6875"); // one lamp per 40 m
            return b.Data;
        }

        // ------------------------------------------------------------------ beach
        public static void Bangka(MeshBuilder b, GameRandom rng, float x, float z, float yaw)
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
            // buko / sorbetes / fishball sellers on the beach-side walk
            if (rng.Chance(0.55)) VendorBuilder.Random(b, rng, EnvironmentKind.Beach, -1f, SY, rng.Range(-L / 2 + 4, L / 2 - 4));
            for (int i = 0; i < rng.Int(0, 3); i++)
            {
                b.Push(new V3(rng.Range(-SceneryBuilder.SidewalkOuter, SceneryBuilder.SidewalkOuter) > 0 ? rng.Range(R + 1f, 8.5f) : -rng.Range(R + 1f, 12f), SY, rng.Range(-L / 2, L / 2)), new Rot(0, rng.Range(0, 6.28f), 0));
                PropBuilder.AddPerson(b, rng.Int(0, 7), false);
                b.Pop();
            }
            NightKit.StreetLamp(b, rng.Chance(0.5) ? -1f : 1f, R, rng.Range(-L / 2 + 6, L / 2 - 6), 5.5f, "#6D6875"); // one lamp per 40 m
            return b.Data;
        }

        /// <summary>
        /// Coastal highway bridge over a river mouth: water on both sides (covering the
        /// shoulder strip), concrete parapets with railings on the road edge, steel arch
        /// trusses, lamp posts, a bangka or two and the sea opening up to the left.
        /// </summary>
        public const float ArchRise = 9.5f;
        public static MeshData CoastalBridge(int seed)
        {
            var rng = new GameRandom(seed);
            var b = new MeshBuilder();
            float edge = R + 0.25f;
            // the river: covers the shoulders/sand strip on both sides for the whole slice
            foreach (float s in new[] { -1f, 1f })
            {
                b.Box("paint", "#2E8FA8", 120f, 0.04f, L, s * (edge + 60f), SY + 0.04f, 0);
                b.Box("paint", "#5FC4D8", 6f, 0.05f, L, s * (edge + 8f), SY + 0.05f, 0);                 // shallow
                for (int i = 0; i < 4; i++)
                    b.Box("glow", "#E8FBFF", rng.Range(1.5f, 4f), 0.03f, 0.12f, s * rng.Range(edge + 4f, edge + 30f), SY + 0.08f, rng.Range(-L / 2, L / 2), new Rot(0, (float)(Math.PI / 2) + rng.Range(-0.3f, 0.3f), 0));
                // bridge deck edge, parapet, railing
                b.Box("paint", "#B9BCC0", 0.6f, 1.4f, L, s * (edge + 0.3f), SY - 0.4f, 0);
                b.Box("paint", "#D7D9DC", 0.35f, 0.75f, L, s * (edge + 0.15f), SY + 0.37f, 0);
                b.Box("chrome", "#A9AEB4", 0.08f, 0.08f, L, s * (edge + 0.15f), SY + 1.15f, 0);
                for (float z = -L / 2 + 1; z < L / 2; z += 2) b.Box("chrome", "#A9AEB4", 0.06f, 0.42f, 0.06f, s * (edge + 0.15f), SY + 0.95f, z);
                // steel arch truss (painted blue), hangers to the deck
                float ax = s * (edge + 0.9f);
                const int seg = 10;
                for (int i = 0; i < seg; i++)
                {
                    float t0 = i / (float)seg, t1 = (i + 1) / (float)seg;
                    float z0 = -L / 2 + 2 + t0 * (L - 4), z1 = -L / 2 + 2 + t1 * (L - 4);
                    float y0 = (float)Math.Sin(t0 * Math.PI) * ArchRise, y1 = (float)Math.Sin(t1 * Math.PI) * ArchRise;
                    float len = (float)Math.Sqrt((z1 - z0) * (z1 - z0) + (y1 - y0) * (y1 - y0));
                    float pitch = (float)Math.Atan2(y1 - y0, z1 - z0);
                    b.Box("paint", "#2F6FB0", 0.45f, 0.5f, len + 0.1f, ax, SY + 1f + (y0 + y1) / 2, (z0 + z1) / 2, new Rot(-pitch, 0, 0));
                    if (i > 0) b.Box("paint", "#6D8FB5", 0.08f, y0, 0.08f, ax, SY + 1f + y0 / 2, z0);
                }
                b.Box("paint", "#9EA1A6", 1.2f, 1.2f, 1.2f, ax, SY + 0.6f, -L / 2 + 2);
                b.Box("paint", "#9EA1A6", 1.2f, 1.2f, 1.2f, ax, SY + 0.6f, L / 2 - 2);
                // piers in the river
                foreach (float pz in new[] { -L / 4, L / 4 }) b.Box("paint", "#A3A6AA", 1.4f, 3f, 2.4f, s * (edge + 0.6f), SY - 1.6f, pz);
            }
            // top bracing across the road near the crown (above every vehicle and the chase camera)
            b.Box("paint", "#2F6FB0", (edge + 0.9f) * 2, 0.35f, 0.35f, 0, SY + 1f + ArchRise - 0.2f, 0);
            foreach (float bz in new[] { -7f, 7f }) b.Box("paint", "#2F6FB0", (edge + 0.9f) * 2, 0.3f, 0.3f, 0, SY + 1f + ArchRise * (float)Math.Sin((bz + L / 2 - 2) / (L - 4) * Math.PI) - 0.2f, bz);
            // lamps on the parapet, one each side
            foreach (float s in new[] { -1f, 1f })
            {
                float lz = s * 10f;
                b.Cylinder("paint", "#6D6875", 0.07f, 0.09f, 5f, 6, s * (edge + 0.15f), SY + 3.2f, lz);
                b.Box("paint", "#6D6875", 1.4f, 0.08f, 0.1f, s * (edge - 0.5f), SY + 5.7f, lz);
                b.Box("glow", "#FFF1C1", 0.5f, 0.1f, 0.3f, s * (edge - 1.0f), SY + 5.62f, lz);
                NightKit.Pool(b, s * (edge - 1.4f), 0.035f, lz, 3.4f);
            }
            for (int i = 0; i < rng.Int(1, 3); i++) Bangka(b, rng, (rng.Chance(0.5) ? -1 : 1) * rng.Range(edge + 8f, edge + 30f), rng.Range(-L / 2 + 4, L / 2 - 4), rng.Range(-0.6f, 0.6f));
            // the sea beyond the river mouth on the left, green hills upriver on the right
            b.Box("paint", "#1F7EA1", 200f, 0.04f, L, -(edge + 160f), SY + 0.03f, 0);
            for (int i = 0; i < 2; i++) b.Sphere("paint", rng.Pick(new[] { "#52B788", "#40916C", "#74C69D" }), rng.Range(10, 16), rng.Range(55, 80), 0, rng.Range(-L / 2, L / 2), 8, 1.4f, 0.7f, 1.2f);
            for (int i = 0; i < 2; i++) CoconutTree(b, rng, rng.Range(40f, 60f), SY, rng.Range(-L / 2, L / 2), rng.Range(7f, 9f));
            return b.Data;
        }

        // ------------------------------------------------------------------ Mountain Province
        /// <summary>Benguet pine: straight trunk and stacked dark cones.</summary>
        public static void PineTree(MeshBuilder b, GameRandom rng, float x, float y, float z, float h)
        {
            b.Cylinder("paint", "#5B4331", 0.14f, 0.22f, h * 0.45f, 6, x, y + h * 0.22f, z);
            string[] greens = { "#1F4D3A", "#24573F", "#2D6148", "#1B4433" };
            string g = rng.Pick(greens);
            int tiers = 4;
            for (int t = 0; t < tiers; t++)
            {
                float k = t / (float)tiers;
                float r = (1.5f - k * 1.0f) * (h / 9f);
                b.Cylinder("paint", t % 2 == 0 ? g : Palette.Shade(g, 1.12f), 0f, r, h * 0.32f, 7, x, y + h * (0.28f + k * 0.5f) + h * 0.16f, z);
            }
        }

        private static void Boulder(MeshBuilder b, GameRandom rng, float x, float y, float z, float s)
        {
            string c = rng.Pick(new[] { "#7D7A73", "#6B6860", "#8A857B" });
            b.Sphere("paint", c, s, x, y + s * 0.45f, z, 6, rng.Range(0.9f, 1.4f), rng.Range(0.6f, 0.9f), rng.Range(0.8f, 1.3f));
        }

        /// <summary>Cordillera house on posts with a tall pyramid roof (inspired by the traditional fale / binangiyan form).</summary>
        private static void HighlandHut(MeshBuilder b, float x, float z, float yaw)
        {
            b.Push(new V3(x, SY, z), new Rot(0, yaw, 0));
            foreach (float sx in new[] { -1f, 1f }) foreach (float sz in new[] { -1f, 1f }) b.Box("paint", "#4E3826", 0.22f, 1.6f, 0.22f, sx * 1.1f, 0.8f, sz * 1.1f);
            b.Cylinder("paint", "#6B4F35", 1.25f, 1.25f, 0.18f, 10, 0, 1.65f, 0);                // rat guard disc
            b.Box("paint", "#7A5A3E", 2.4f, 1.1f, 2.4f, 0, 2.25f, 0);
            b.Cylinder("paint", "#9C8455", 0f, 2.5f, 3.2f, 4, 0, 4.4f, 0, new Rot(0, (float)(Math.PI / 4), 0)); // thatch
            b.Box("paint", "#2F2218", 0.7f, 0.8f, 0.05f, 0, 2.2f, 1.21f);
            b.Pop();
        }

        /// <summary>Stone-walled rice terraces stepping up the hillside.</summary>
        private static void Terraces(MeshBuilder b, GameRandom rng, float side, float x0, float z, float length)
        {
            for (int step = 0; step < 7; step++)
            {
                float x = side * (x0 + step * 3.6f);
                float h = 0.6f + step * 1.15f;
                string paddy = rng.Pick(new[] { "#6A994E", "#7CB518", "#5E8C3A", "#8DB255", "#6FA8A0" });
                b.Box("paint", "#6E6558", 3.6f, h, length, x, h / 2f - 0.05f, z);                // stone wall
                b.Box("paint", paddy, 3.4f, 0.12f, length - 0.3f, x, h + 0.02f, z);              // paddy
                b.Box("paint", Palette.Shade(paddy, 0.85f), 0.25f, 0.18f, length - 0.3f, x - side * 1.6f, h + 0.08f, z); // dike lip
            }
        }

        /// <summary>
        /// Mountain Province: unpaved road through pine forest; a cut rock wall with
        /// boulders on one side, rice terraces on the other, misty peaks behind,
        /// wooden fence posts, highland huts and the odd vegetable stall.
        /// </summary>
        public static MeshData MountainProvince(int seed, bool waterfall)
        {
            var rng = new GameRandom(seed);
            var b = new MeshBuilder();
            float wallSide = rng.Chance(0.5) ? -1f : 1f;
            foreach (float side in new[] { -1f, 1f })
            {
                bool wall = side == wallSide;
                // fence posts / guard logs along the dirt shoulder
                for (float pz = -L / 2 + 2; pz < L / 2; pz += rng.Range(3.5f, 5.5f))
                    b.Box("paint", rng.Pick(new[] { "#6B4F35", "#5B4331" }), 0.16f, rng.Range(0.7f, 1.0f), 0.16f, side * (R + 1.6f), SY + 0.4f, pz);
                if (wall)
                {
                    // cut rock face rising from the shoulder, streaked with moss
                    for (float pz = -L / 2; pz < L / 2; pz += 5f)
                    {
                        float h = rng.Range(5f, 10f);
                        b.Box("paint", rng.Pick(new[] { "#7A7064", "#6E655A", "#857A6C" }), 4f, h, 5.2f, side * (R + 5f), h / 2f - 0.05f, pz + 2.5f, new Rot(0, 0, side * rng.Range(0.05f, 0.18f)));
                        if (rng.Chance(0.6)) b.Box("paint", "#4F6B3A", 0.3f, h * 0.6f, rng.Range(1f, 3f), side * (R + 3.05f), h * 0.4f, pz + rng.Range(0.5f, 4.5f));
                    }
                    for (int i = 0; i < rng.Int(2, 4); i++) Boulder(b, rng, side * rng.Range(R + 1.8f, R + 2.8f), SY, rng.Range(-L / 2, L / 2), rng.Range(0.4f, 0.9f));
                    for (int i = 0; i < rng.Int(5, 9); i++) PineTree(b, rng, side * rng.Range(R + 7.5f, 24f), rng.Range(6f, 10f), rng.Range(-L / 2, L / 2), rng.Range(9f, 15f));
                    if (waterfall)
                    {
                        b.Box("glow", "#CFE8F2", 1.4f, 9f, 0.3f, side * (R + 3.1f), 4.4f, 0, new Rot(0, (float)(Math.PI / 2), 0)); // falling water
                        b.Box("water", "#5F8FA0", 3f, 0.1f, 3.5f, side * (R + 2.4f), SY + 0.05f, 0);                              // pool
                        for (int i = 0; i < 4; i++) Boulder(b, rng, side * (R + 2.2f + rng.Range(-0.6f, 0.6f)), SY, rng.Range(-2f, 2f), rng.Range(0.3f, 0.5f));
                    }
                }
                else
                {
                    // terraces and pine forest on the open side
                    Terraces(b, rng, side, R + 6f, rng.Range(-6f, 6f), rng.Range(18f, 30f));
                    for (int i = 0; i < rng.Int(4, 7); i++) PineTree(b, rng, side * rng.Range(R + 2.2f, R + 5f), SY, rng.Range(-L / 2, L / 2), rng.Range(7f, 12f));
                    float roll = rng.Range(0, 1);
                    float hz = rng.Range(-L / 2 + 6, L / 2 - 6);
                    if (roll < 0.35f) HighlandHut(b, side * rng.Range(R + 4.5f, R + 6f), hz, rng.Range(0, 6.28f));
                    else if (roll < 0.55f)
                    {
                        // roadside vegetable stand (cabbage, carrots, sayote)
                        float vx = side * (R + 3.2f);
                        b.Box("paint", "#8D6E4F", 2.4f, 0.9f, 1.4f, vx, SY + 0.45f, hz);
                        for (int k = 0; k < 6; k++) b.Sphere("paint", k % 3 == 0 ? "#9BC53D" : k % 3 == 1 ? "#F18F01" : "#C5D86D", 0.22f, vx + (k % 3 - 1) * 0.6f, SY + 1.05f, hz + (k / 3 - 0.5f) * 0.6f, 6);
                        b.Box("paint", "#2E86AB", 2.8f, 0.08f, 1.9f, vx, SY + 2.2f, hz);
                        foreach (float sz in new[] { -0.8f, 0.8f }) b.Box("paint", "#5B4331", 0.08f, 2.2f, 0.08f, vx - side * 1.2f, SY + 1.1f, hz + sz);
                    }
                    for (int i = 0; i < rng.Int(0, 2); i++) Boulder(b, rng, side * rng.Range(R + 1.8f, R + 4f), SY, rng.Range(-L / 2, L / 2), rng.Range(0.3f, 0.7f));
                    if (rng.Chance(0.3)) VendorBuilder.Random(b, rng, EnvironmentKind.Mountain, side, SY, rng.Range(-L / 2 + 4, L / 2 - 4));
                    // villagers
                    for (int i = 0; i < rng.Int(0, 2); i++)
                    {
                        b.Push(new V3(side * rng.Range(R + 1.2f, R + 3f), SY, rng.Range(-L / 2, L / 2)), new Rot(0, rng.Range(0, 6.28f), 0));
                        PropBuilder.AddPerson(b, rng.Int(0, 7), false);
                        b.Pop();
                    }
                }
                // tall misty peaks behind
                for (int i = 0; i < 3; i++) Mountain(b, side * rng.Range(55, 100), rng.Range(-L / 2, L / 2), rng.Range(38, 70), rng.Range(20, 34), rng.Pick(new[] { "#4F6F63", "#5A7A6E", "#456558", "#6B8A80" }));
            }
            return b.Data;
        }
    }
}
