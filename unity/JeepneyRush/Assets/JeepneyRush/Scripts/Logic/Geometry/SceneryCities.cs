using System;

namespace JeepneyRush.Logic.Geometry
{
    /// <summary>
    /// EDSA, Cebu City and Baguio scenery slices (40 m, both sides of the road). Original,
    /// stylised low-poly art: no real logos or brands (billboards use the game's made-up shop signs).
    /// Every slice carries night dressing (lit windows, lamp pools) that fades in after sunset.
    /// </summary>
    public static class SceneryCities
    {
        private const float L = SceneryBuilder.ChunkLength;
        private const float R = SceneryBuilder.RoadHalfWidth;
        private const float SY = SceneryBuilder.SidewalkHeight;
        private const float BL = SceneryBuilder.SidewalkOuter; // building line

        // ------------------------------------------------------------------ shared
        /// <summary>Self-lit billboard (shop-sign atlas cell) on two poles, facing the driver.</summary>
        private static void Billboard(MeshBuilder b, GameRandom rng, float x, float z, float w, float h, float y)
        {
            int cell = rng.Int(0, 15);
            var (u0, v0, u1, v1) = PropBuilder.AtlasCell(cell, 4, 4);
            foreach (float px in new[] { x - w * 0.3f, x + w * 0.3f }) b.Box("paint", "#5C5F66", 0.25f, y, 0.25f, px, y / 2, z + 0.2f);
            b.Box("paint", "#2B2D31", w + 0.3f, h + 0.3f, 0.25f, x, y + h / 2, z + 0.1f);
            float hw = w / 2;
            // seen from behind the jeepney (looking +Z): -X is on the viewer's left
            b.Quad("utex:shop_signs", new V3(x - hw, y, z - 0.03f), new V3(x + hw, y, z - 0.03f), new V3(x + hw, y + h, z - 0.03f), new V3(x - hw, y + h, z - 0.03f), new V3(0, 0, -1),
                new V2(u0, v0), new V2(u1, v0), new V2(u1, v1), new V2(u0, v1));
            b.Box("nightglow", "#FFF6D8", w, 0.08f, 0.3f, x, y + h + 0.25f, z - 0.25f); // lamp bar above the board
        }

        /// <summary>City bus parked in the busway / at a stop (scenery only).</summary>
        private static void Bus(MeshBuilder b, GameRandom rng, float x, float z)
        {
            string body = rng.Pick(new[] { "#E63946", "#1D4ED8", "#F4A261", "#2A9D8F", "#FFFFFF" });
            b.Box("paint", body, 2.5f, 2.6f, 11f, x, SY + 1.75f, z);
            b.Box("glass", "#1E2B3A", 2.54f, 0.9f, 9.6f, x, SY + 2.35f, z - 0.2f);
            b.Box("nightglow", "#FFF0C8", 2.56f, 0.7f, 9.2f, x, SY + 2.35f, z - 0.2f);
            b.Box("paint", Palette.Shade(body, 0.75f), 2.52f, 0.25f, 11f, x, SY + 1.0f, z);
            b.Box("glow", "#FF2121", 2.0f, 0.15f, 0.05f, x, SY + 1.1f, z - 5.52f);
            foreach (float wz in new[] { -3.6f, 3.6f }) foreach (float wx in new[] { -1.15f, 1.15f }) b.CylinderX("paint", "#18181C", 0.5f, 0.35f, 10, x + wx, SY + 0.5f, z + wz);
        }

        private static void Pedestrians(MeshBuilder b, GameRandom rng, float side, float x0, float x1, int max)
        {
            int n = rng.Int(0, max);
            for (int i = 0; i < n; i++)
            {
                b.Push(new V3(side * rng.Range(x0, x1), SY, rng.Range(-L / 2, L / 2)), new Rot(0, rng.Range(0, 6.28f), 0));
                PropBuilder.AddPerson(b, rng.Int(0, 7), false);
                b.Pop();
            }
        }

        /// <summary>Tall office/condo tower with lit windows on the road-facing side.</summary>
        private static void Tower(MeshBuilder b, GameRandom rng, float side, float xNear, float z, float w, float d, float h)
        {
            string wall = rng.Pick(SceneryBuilder.Towers);
            float cx = side * (xNear + d / 2);
            SceneryBuilder.FacadeBox(b, "tex:facade|" + wall, d, h, w, cx, SY + h / 2, z, 7.5f);
            NightKit.WindowGrid(b, rng, cx - side * d / 2, side < 0, z, w, SY + 3f, SY + h - 1f, 2.5f, 0.5f);
            b.Box("glow", "#FF3B3B", 0.3f, 0.3f, 0.3f, cx, SY + h + 0.4f, z); // aircraft warning light
        }

        // ------------------------------------------------------------------ EDSA
        /// <summary>
        /// EDSA: concrete median and busway with the elevated MRT on the left, malls, towers,
        /// giant billboards and footbridges on the right, tall lamps all along the road.
        /// </summary>
        /// <summary>
        /// EDSA flyover: an elevated road crossing high over EDSA and the MRT line,
        /// on round piers outside the lanes, with parapets, lamps, a green direction sign and
        /// cars on top. Purely scenery: nothing stands on the jeepney's lanes.
        /// </summary>
        public static void Flyover(MeshBuilder b, GameRandom rng, float fz)
        {
            float x0 = -(R + 14f), x1 = BL - 0.4f, cx = (x0 + x1) / 2, w = x1 - x0;
            // deck high enough to clear the MRT trains and catenary (≈13.3 m) and the chase camera (6.8 m)
            const float y = FlyoverDeckY, deckW = 9f;
            b.Box("paint", "#BFC1C4", w, 1.1f, deckW, cx, SY + y, fz);                                  // deck slab
            b.Box("paint", "#A3A6AA", w, 0.5f, deckW - 2.4f, cx, SY + y - 0.75f, fz);                    // box girder underside
            b.Box("paint", "#3E4045", w, 0.06f, deckW - 0.6f, cx, SY + y + 0.58f, fz);                   // asphalt
            for (float lx = x0 + 3; lx < x1 - 2; lx += 6) b.Box("paint", "#F2F2F2", 2.6f, 0.02f, 0.15f, lx, SY + y + 0.62f, fz); // lane dashes
            foreach (float e in new[] { -deckW / 2 + 0.2f, deckW / 2 - 0.2f })
            {
                b.Box("paint", "#D0D2D5", w, 1.0f, 0.4f, cx, SY + y + 1.05f, fz + e);                     // parapets
                b.Box("glow", "#FFC300", w, 0.06f, 0.05f, cx, SY + y + 0.9f, fz + e + (e < 0 ? -0.21f : 0.21f)); // reflector strip
            }
            // piers beyond the MRT line and on the sidewalk – never on the lanes or through the viaduct
            foreach (float px in new[] { -(R + 12.6f), R + 2.0f })
            {
                b.Cylinder("paint", "#B5B7BA", 0.65f, 0.7f, y - 0.9f, 10, px, SY + (y - 0.9f) / 2, fz);
                b.Box("paint", "#A7A9AD", 1.8f, 0.6f, deckW - 1.6f, px, SY + y - 1.0f, fz);
            }
            // lamps on the deck
            foreach (float lx in new[] { x0 + 5f, cx, x1 - 4f })
            {
                b.Cylinder("paint", "#5C5F66", 0.07f, 0.09f, 4f, 6, lx, SY + y + 2.5f, fz + deckW / 2 - 0.2f);
                b.Box("glow", "#FFF1C1", 0.5f, 0.1f, 0.3f, lx, SY + y + 4.45f, fz + deckW / 2 - 0.9f);
            }
            // green direction sign hung on the side facing the traffic (towards -z)
            b.Box("paint", "#1F7A43", 7.5f, 1.6f, 0.12f, -1.6f, SY + y - 0.2f, fz - deckW / 2 - 0.1f);
            b.Box("paint", "#F2F2F2", 7.1f, 0.08f, 0.13f, -1.6f, SY + y + 0.4f, fz - deckW / 2 - 0.11f);
            b.Box("paint", "#F2F2F2", 7.1f, 0.08f, 0.13f, -1.6f, SY + y - 0.8f, fz - deckW / 2 - 0.11f);
            b.Box("paint", "#F2F2F2", 0.9f, 0.5f, 0.13f, -4.4f, SY + y - 0.2f, fz - deckW / 2 - 0.11f);   // arrow block
            b.Box("nightglow", "#E8FFF0", 6.6f, 0.9f, 0.05f, -1.6f, SY + y - 0.2f, fz - deckW / 2 - 0.2f);
            // cars crossing on top
            for (int i = 0; i < rng.Int(1, 3); i++)
            {
                float carX = rng.Range(x0 + 3, x1 - 3), lane = rng.Chance(0.5) ? -1.8f : 1.8f;
                string col = rng.Pick(new[] { "#D62828", "#F2F2F2", "#1D3557", "#8D99AE", "#2A9D8F" });
                b.Box("paint", col, 4.2f, 0.9f, 1.8f, carX, SY + y + 1.1f, fz + lane);
                b.Box("glass", "#22344A", 2.2f, 0.6f, 1.7f, carX, SY + y + 1.8f, fz + lane);
            }
        }

        public const float FlyoverDeckY = 14.6f;

        public static MeshData Edsa(int seed, bool station, bool flyover = false)
        {
            var rng = new GameRandom(seed);
            var b = new MeshBuilder();
            float s = -1f;
            // ---- median barrier with reflectors
            b.Box("paint", "#C9C9C4", 0.5f, 0.9f, L, s * (R + 0.35f), SY + 0.45f, 0);
            b.Box("paint", "#B5B5AF", 0.85f, 0.25f, L, s * (R + 0.35f), SY + 0.12f, 0);
            for (float z = -L / 2 + 2; z < L / 2; z += 4) b.Box("glow", "#FFC300", 0.05f, 0.12f, 0.25f, s * (R + 0.08f), SY + 0.6f, z);
            // ---- busway (bus-only lane between concrete barriers)
            b.Box("paint", "#3A3B40", 3.9f, 0.04f, L, s * (R + 2.65f), SY + 0.02f, 0);
            for (float z = -L / 2 + 1; z < L / 2; z += 6) b.Box("paint", "#F2C200", 0.15f, 0.02f, 3f, s * (R + 1.0f), SY + 0.05f, z);
            b.Box("paint", "#C9C9C4", 0.4f, 0.9f, L, s * (R + 4.8f), SY + 0.45f, 0);
            if (rng.Chance(0.45)) Bus(b, rng, s * (R + 2.65f), rng.Range(-L / 2 + 7, L / 2 - 7));
            // ---- MRT viaduct: pillars, pier caps, deck, rails
            float px = s * (R + 7.2f);
            foreach (float pz in new[] { -L / 4, L / 4 })
            {
                b.Box("paint", "#B5B7BA", 1.4f, 8.6f, 1.4f, px, SY + 4.3f, pz);
                b.Box("paint", "#A7A9AD", 3.4f, 0.9f, 1.8f, px, SY + 8.8f, pz);
            }
            b.Box("paint", "#C4C6C9", 7.6f, 1.0f, L, px, SY + 9.7f, 0);
            foreach (float e in new[] { -3.7f, 3.7f }) b.Box("paint", "#9EA1A6", 0.25f, 1.1f, L, px + e, SY + 10.7f, 0);
            foreach (float r in new[] { -2.4f, -1.2f, 1.2f, 2.4f }) b.Box("chrome", "#8C9096", 0.1f, 0.1f, L, px + r, SY + 10.3f, 0);
            for (float z = -L / 2 + 4; z < L / 2; z += 8) b.Box("paint", "#6D6F73", 0.12f, 2.6f, 0.12f, px + 3.5f, SY + 11.5f, z); // catenary posts
            b.Box("paint", "#33353A", 0.05f, 0.05f, L, px + 1.8f, SY + 12.7f, 0);
            if (station)
            {
                // MRT station: platforms, canopy, glass walls, stairs down to the sidewalk side
                b.Box("paint", "#D8DADD", 11f, 0.6f, 30f, px, SY + 10.4f, 0);
                b.Box("glass", "#5D7A90", 0.2f, 3.2f, 28f, px + 5.4f, SY + 12.3f, 0);
                b.Box("nightglow", "#E8F4FF", 0.22f, 2.6f, 26f, px + 5.45f, SY + 12.3f, 0);
                b.Box("paint", "#2F6FB0", 12f, 0.5f, 31f, px, SY + 14.2f, 0);
                for (float z = -12; z <= 12; z += 6) foreach (float e in new[] { -5.2f, 5.2f }) b.Box("paint", "#9EA1A6", 0.3f, 3.6f, 0.3f, px + e, SY + 12.3f, z);
            }
            else if (rng.Chance(0.45))
            {
                // a train on the line: three cars, lit windows at night
                float tz = rng.Range(-8f, 8f);
                for (int c = -1; c <= 1; c++)
                {
                    float cz = tz + c * 12.8f;
                    b.Box("paint", "#E9EDF2", 2.9f, 3.0f, 12.4f, px + 1.2f, SY + 11.8f, cz);
                    b.Box("paint", "#1F4FA8", 2.94f, 0.35f, 12.4f, px + 1.2f, SY + 10.9f, cz);
                    b.Box("glass", "#22344A", 2.96f, 0.9f, 10.8f, px + 1.2f, SY + 12.2f, cz);
                    b.Box("nightglow", "#F2F8FF", 2.98f, 0.8f, 10.6f, px + 1.2f, SY + 12.2f, cz);
                }
            }
            // far-left skyline over the MRT
            for (int i = 0; i < 2; i++) if (rng.Chance(0.7)) Tower(b, rng, s, R + 14 + rng.Range(0, 14), rng.Range(-L / 2 + 6, L / 2 - 6), rng.Range(10, 16), rng.Range(10, 14), rng.Range(26, 58));
            // tall median lamps with pools on the road
            foreach (float lz in new[] { -L / 4 - 2, L / 4 - 2 })
            {
                b.Cylinder("paint", "#5C5F66", 0.1f, 0.13f, 9f, 6, s * (R + 0.35f), SY + 4.5f, lz);
                b.Box("paint", "#5C5F66", 2.6f, 0.1f, 0.12f, s * (R - 0.9f), SY + 9f, lz);
                b.Box("glow", "#FFF1C1", 0.6f, 0.12f, 0.35f, s * (R - 2.0f), SY + 8.92f, lz);
                NightKit.Pool(b, s * (R - 2.2f), 0.035f, lz, 4.2f);
                NightKit.LampHaze(b, s * (R - 2.0f), SY + 8.8f, lz);
            }

            // ---- right side: malls, towers, billboards, footbridge, bus stop
            s = 1f;
            float zc = -L / 2;
            while (zc < L / 2 - 4)
            {
                float roll = rng.Range(0, 1);
                if (roll < 0.45f)
                {
                    // mall: wide and low with a glass front, a big lit sign and a car-park ramp
                    float w = Math.Min(rng.Range(18, 26), L / 2 - zc), d = rng.Range(18, 24), h = rng.Range(13, 18);
                    float cx = s * (BL + d / 2), cz = zc + w / 2;
                    string wall = rng.Pick(new[] { "#E9ECEF", "#F1E3D3", "#DDE5ED", "#EFE7DA" });
                    b.Box("paint", wall, d, h, w - 0.4f, cx, SY + h / 2, cz);
                    b.Box("glass", "#35526B", 0.15f, h * 0.55f, w - 3f, s * BL - 0.05f, SY + 2.2f + h * 0.27f, cz);
                    NightKit.WindowGrid(b, rng, s * BL - 0.1f, false, cz, w - 3f, SY + 2.4f, SY + 2.2f + h * 0.55f, 1.8f, 0.8f);
                    b.Box("paint", Palette.Shade(wall, 0.8f), d + 0.4f, 0.6f, w, cx, SY + h + 0.3f, cz);
                    // big lit sign standing on the roof at the front edge, facing the traffic (fully in the open)
                    Billboard(b, rng, cx, cz - (w - 0.4f) / 2 + 1.2f, Math.Min(10f, d - 2f), 3.2f, SY + h + 0.7f);
                    zc += w;
                }
                else
                {
                    float w = Math.Min(rng.Range(10, 15), L / 2 - zc), d = rng.Range(10, 14);
                    Tower(b, rng, s, BL, zc + w / 2, w - 0.4f, d, rng.Range(24, 50));
                    zc += w;
                }
            }
            // roadside billboard: must fit on the sidewalk between the curb and the building line (3.4 m)
            if (rng.Chance(0.7)) Billboard(b, rng, s * ((R + BL) / 2f), rng.Range(-L / 2 + 4, L / 2 - 4), BL - R - 0.5f, 1.25f, SY + 5.2f);
            if (flyover) Flyover(b, rng, rng.Range(-4f, 4f));
            else if (station || rng.Chance(0.18))
            {
                // pedestrian footbridge across EDSA
                float fz = station ? 14f : rng.Range(-L / 2 + 6, L / 2 - 8);
                float x0 = -(R + 1.2f), x1 = R + 3.6f;
                b.Box("paint", "#B9BCC0", x1 - x0, 0.5f, 2.8f, (x0 + x1) / 2, SY + 6.4f, fz);
                foreach (float e in new[] { -1.3f, 1.3f }) b.Box("paint", "#8D9096", x1 - x0, 1.0f, 0.1f, (x0 + x1) / 2, SY + 7.1f, fz + e);
                b.Box("paint", "#2F6FB0", x1 - x0, 0.15f, 3.2f, (x0 + x1) / 2, SY + 9.0f, fz);
                foreach (float bx in new[] { x0 + 0.3f, x1 - 0.3f }) b.Box("paint", "#9EA1A6", 0.5f, 6.4f, 0.5f, bx, SY + 3.2f, fz);
                b.Box("paint", "#B9BCC0", 2.2f, 0.4f, 9f, R + 4.8f, SY + 3.4f, fz - 5f, new Rot(-0.6f, 0, 0)); // stairs down
                b.Box("nightglow", "#FFF0C8", x1 - x0, 0.08f, 0.2f, (x0 + x1) / 2, SY + 8.8f, fz);
            }
            else if (rng.Chance(0.45))
            {
                // bus stop shelter
                float bz = rng.Range(-L / 2 + 6, L / 2 - 6), bx = s * (R + 2.2f);
                b.Box("paint", "#2F6FB0", 2.2f, 0.12f, 5f, bx, SY + 2.7f, bz);
                foreach (float e in new[] { -2.3f, 2.3f }) b.Box("paint", "#5C5F66", 0.1f, 2.6f, 0.1f, bx + s * 0.9f, SY + 1.3f, bz + e);
                b.Box("glass", "#8FB3C9", 0.05f, 2f, 4.6f, bx + s * 1.05f, SY + 1.4f, bz);
                b.Box("paint", "#6B4F35", 0.5f, 0.45f, 3.6f, bx + s * 0.6f, SY + 0.45f, bz);
                b.Box("nightglow", "#E8F4FF", 1.6f, 0.06f, 4f, bx, SY + 2.6f, bz);
                Pedestrians(b, rng, s, R + 1.2f, R + 2.8f, 3);
            }
            NightKit.StreetLamp(b, s, R, rng.Range(-L / 2 + 6, L / 2 - 6), 7f, "#5C5F66");
            if (rng.Chance(0.4)) VendorBuilder.Random(b, rng, EnvironmentKind.Edsa, s, SY, rng.Range(-L / 2 + 4, L / 2 - 4));
            Pedestrians(b, rng, s, R + 1f, BL - 0.6f, 3);
            return b.Data;
        }

        // ------------------------------------------------------------------ Cebu City
        /// <summary>
        /// Cebu City: heritage-style downtown blocks (Colon), glass towers of IT Park in the
        /// distance, and seaside stretches with boats and green mountains across the water.
        /// Sinulog banners hang across the road in special slices.
        /// </summary>
        public static MeshData Cebu(int seed, bool sinulog)
        {
            var rng = new GameRandom(seed);
            var b = new MeshBuilder();
            bool seaside = seed % 3 == 0;
            string[] walls = { "#F4D35E", "#EE964B", "#FAF0CA", "#E9C46A", "#F7EDE2", "#CDE7B0", "#A8DADC", "#F28482" };
            foreach (float side in new[] { -1f, 1f })
            {
                if (seaside && side > 0)
                {
                    // seawall, railing, palms, water, boats and mountains across the strait
                    b.Box("paint", "#BDB6A8", 1.2f, 0.9f, L, R + 2.2f, SY + 0.45f, 0);
                    for (float rz = -L / 2; rz < L / 2; rz += 2.5f) b.Box("paint", "#E9ECEF", 0.08f, 0.9f, 0.08f, R + 2.7f, SY + 1.35f, rz);
                    b.Box("paint", "#E9ECEF", 0.08f, 0.08f, L, R + 2.7f, SY + 1.8f, 0);
                    b.Box("water", "#2A86B8", 120f, 0.1f, L, R + 63f, -0.02f, 0); // just above the ground plane, under the sidewalk
                    for (int i = 0; i < rng.Int(1, 3); i++) SceneryProvinces.Bangka(b, rng, R + rng.Range(12, 40), rng.Range(-L / 2 + 4, L / 2 - 4), rng.Range(-0.5f, 0.5f));
                    for (int i = 0; i < 2; i++) SceneryProvinces.Mountain(b, R + rng.Range(90, 130), rng.Range(-L / 2, L / 2), rng.Range(26, 42), rng.Range(26, 38), rng.Pick(new[] { "#5F8F6A", "#4F7F5C", "#6A9A74" }));
                    for (int i = 0; i < rng.Int(1, 3); i++) SceneryProvinces.CoconutTree(b, rng, R + 1.4f, SY, rng.Range(-L / 2 + 3, L / 2 - 3), rng.Range(6f, 8f));
                    NightKit.StreetLamp(b, side, R, rng.Range(-L / 2 + 6, L / 2 - 6), 6f, "#3D5A80");
                    continue;
                }
                // downtown: 2–5 storey blocks with arcades, awnings and signs
                float z = -L / 2;
                while (z < L / 2 - 2)
                {
                    float w = Math.Min(rng.Range(7, 12), L / 2 - z);
                    float h = rng.Pick(new[] { 6f, 9f, 9f, 12f, 15f });
                    float d = rng.Range(8, 11);
                    float cx = side * (BL + d / 2), cz = z + w / 2;
                    string wall = rng.Pick(walls);
                    SceneryBuilder.FacadeBox(b, "tex:facade|" + wall, d, h, w - 0.3f, cx, SY + h / 2, cz);
                    NightKit.Windows(b, rng, cx - side * d / 2, side < 0, cz, w - 0.3f, SY, h);
                    b.Box("paint", Palette.Shade(wall, 0.78f), d + 0.3f, 0.5f, w - 0.1f, cx, SY + h + 0.25f, cz);
                    // arcade (covered walkway) along the street
                    float front = side * BL;
                    b.Box("paint", Palette.Shade(wall, 0.9f), 1.8f, 0.35f, w - 0.3f, front - side * 0.9f, SY + 3.4f, cz);
                    for (float az = z + 1f; az < z + w - 0.5f; az += 2.6f) b.Box("paint", Palette.Shade(wall, 0.85f), 0.35f, 3.2f, 0.35f, front - side * 1.7f, SY + 1.6f, az);
                    b.Box("paint", "#2F2A2A", 0.2f, 2.8f, w - 0.8f, front + side * 0.05f, SY + 1.4f, cz);
                    b.Box("nightglow", "#FFE4A3", 0.22f, 2.2f, w - 1.6f, front - side * 0.02f, SY + 1.3f, cz); // lit shop fronts
                    if (rng.Chance(0.6)) Billboard(b, rng, front - side * 1.5f, cz - w / 2 + 0.8f, 2.6f, 0.9f, SY + rng.Range(4.2f, Math.Max(4.4f, h - 1.5f)));
                    z += w;
                }
                // IT Park glass towers behind
                if (rng.Chance(0.6)) Tower(b, rng, side, BL + 16 + rng.Range(0, 12), rng.Range(-L / 2 + 6, L / 2 - 6), rng.Range(12, 16), rng.Range(12, 16), rng.Range(34, 62));
                // parked motorbikes on the curb
                for (int i = 0; i < rng.Int(0, 3); i++)
                {
                    float mz = rng.Range(-L / 2 + 2, L / 2 - 2), mx = side * (R + 1.0f);
                    b.Box("paint", rng.Pick(new[] { "#1D3557", "#E63946", "#111111", "#F1FAEE" }), 0.35f, 0.5f, 1.6f, mx, SY + 0.55f, mz);
                    b.CylinderX("paint", "#18181C", 0.28f, 0.12f, 8, mx, SY + 0.28f, mz - 0.6f);
                    b.CylinderX("paint", "#18181C", 0.28f, 0.12f, 8, mx, SY + 0.28f, mz + 0.6f);
                }
                if (rng.Chance(0.5)) VendorBuilder.Random(b, rng, EnvironmentKind.Cebu, side, SY, rng.Range(-L / 2 + 4, L / 2 - 4));
                Pedestrians(b, rng, side, R + 0.9f, BL - 0.5f, 3);
                if (side < 0 || !seaside) NightKit.StreetLamp(b, side, R, rng.Range(-L / 2 + 6, L / 2 - 6), 6f, "#3D5A80");
            }
            if (sinulog)
            {
                // Sinulog banners: strings of red/yellow flags across the street
                string[] cols = { "#E63946", "#FFD166", "#06D6A0", "#118AB2", "#F78C6B" };
                foreach (float fz in new[] { -8f, 8f })
                {
                    b.Box("paint", "#333333", 2 * BL, 0.03f, 0.03f, 0, SY + 7.2f, fz);
                    for (float fx = -BL + 0.6f; fx < BL; fx += 0.9f)
                        b.Box("paint", cols[(int)Math.Abs(fx * 3) % cols.Length], 0.5f, 0.6f, 0.02f, fx, SY + 6.85f, fz);
                }
            }
            return b.Data;
        }

        // ------------------------------------------------------------------ Baguio
        /// <summary>A colourful hillside house with a pitched roof and lit windows facing the road.</summary>
        private static void HillHouse(MeshBuilder b, GameRandom rng, float side, float x, float y, float z)
        {
            string wall = rng.Pick(new[] { "#E76F51", "#F4A261", "#E9C46A", "#2A9D8F", "#FFFFFF", "#D62828", "#8ECAE6", "#B5838D", "#FFB4A2" });
            float w = rng.Range(3.5f, 5f), d = rng.Range(3.5f, 4.5f), h = rng.Range(2.8f, 5.5f);
            b.Box("paint", wall, d, h, w, x, y + h / 2, z);
            string roof = rng.Pick(new[] { "#5A3E36", "#7F5539", "#3D405B", "#9C6644", "#2B2D42" });
            b.Push(new V3(x, y + h, z), new Rot(0, (float)(Math.PI / 4), 0));
            b.Cylinder("paint", roof, 0f, Math.Max(w, d) * 0.78f, 1.8f, 4, 0, 0.9f, 0);
            b.Pop();
            NightKit.WindowGrid(b, rng, x - side * d / 2, side < 0, z, w, y + 0.8f, y + h - 0.3f, 1.4f, 0.6f);
        }

        /// <summary>
        /// Baguio: pine-covered hills stepping up from the road, colourful houses stacked on the
        /// slopes, Session Road shops in some slices, and Burnham Park's lake in special slices.
        /// Thick highland fog comes from the route's lighting.
        /// </summary>
        public static MeshData Baguio(int seed, bool lake)
        {
            var rng = new GameRandom(seed);
            var b = new MeshBuilder();
            float shopsSide = rng.Chance(0.4) ? (rng.Chance(0.5) ? -1f : 1f) : 0f;
            float lakeSide = lake ? (shopsSide < 0 ? 1f : -1f) : 0f;
            foreach (float side in new[] { -1f, 1f })
            {
                // guard posts along the curb
                for (float pz = -L / 2 + 1; pz < L / 2; pz += 4f) b.Box("paint", "#F1F1F1", 0.15f, 0.7f, 0.15f, side * (R + 0.9f), SY + 0.35f, pz);
                if (side == lakeSide)
                {
                    // Burnham Park lake with swan boats and pines around it
                    b.Box("water", "#4F8FA8", 22f, 0.1f, 30f, side * (R + 14f), SY - 0.1f, 0);
                    b.Box("paint", "#6A994E", 24f, 0.12f, 34f, side * (R + 14f), SY - 0.15f, 0);
                    for (int i = 0; i < rng.Int(2, 4); i++)
                    {
                        float bx = side * (R + rng.Range(7, 21)), bz = rng.Range(-12, 12);
                        b.Box("paint", "#FFFFFF", 1.2f, 0.5f, 2f, bx, SY + 0.2f, bz);
                        b.Sphere("paint", "#FFFFFF", 0.35f, bx, SY + 0.9f, bz + 0.9f, 6);
                        b.Box("paint", "#F4A261", 0.12f, 0.12f, 0.3f, bx, SY + 0.95f, bz + 1.25f);
                    }
                    for (int i = 0; i < rng.Int(4, 7); i++) SceneryProvinces.PineTree(b, rng, side * rng.Range(R + 2.5f, R + 28f), SY, rng.Range(-L / 2, L / 2), rng.Range(8f, 13f));
                    NightKit.StreetLamp(b, side, R, rng.Range(-L / 2 + 6, L / 2 - 6), 5.5f, "#2B2D42");
                    continue;
                }
                if (side == shopsSide)
                {
                    // Session Road: narrow 2–3 storey shops with awnings close to the road
                    float z = -L / 2;
                    while (z < L / 2 - 2)
                    {
                        float w = Math.Min(rng.Range(5, 8), L / 2 - z), h = rng.Pick(new[] { 6f, 9f }), d = 7f;
                        float cx = side * (R + 3.4f + d / 2), cz = z + w / 2;
                        string wall = rng.Pick(SceneryBuilder.Walls);
                        SceneryBuilder.FacadeBox(b, "tex:facade|" + wall, d, h, w - 0.2f, cx, SY + h / 2, cz);
                        NightKit.Windows(b, rng, cx - side * d / 2, side < 0, cz, w - 0.2f, SY, h);
                        b.Box("paint", rng.Pick(new[] { "#E63946", "#2A9D8F", "#1D3557", "#F4A261" }), 1.4f, 0.08f, w - 0.4f, side * (R + 2.7f), SY + 2.9f, cz, new Rot(0, 0, -side * 0.25f));
                        b.Box("nightglow", "#FFE4A3", 0.22f, 2.0f, w - 1.2f, side * (R + 3.38f), SY + 1.2f, cz);
                        z += w;
                    }
                    // Session Road vendors sit in front of the shops (R + 3.4 is the shop line)
                    if (rng.Chance(0.55))
                        VendorBuilder.Vendor(b, rng.Pick(VendorBuilder.KindsFor(EnvironmentKind.Baguio)), side * (R + 2.7f), SY, rng.Range(-L / 2 + 4, L / 2 - 4), side, rng.Int(0, 99), EnvironmentKind.Baguio);
                    Pedestrians(b, rng, side, R + 1.2f, R + 3f, 4);
                }
                // hillside terraces rising away from the road, with houses and pines
                float baseX = side == shopsSide ? R + 12f : R + 3f;
                for (int step = 0; step < 4; step++)
                {
                    float x0 = baseX + step * 6f, hgt = 1.2f + step * 3.2f;
                    b.Box("paint", step % 2 == 0 ? "#557A4C" : "#4D7046", 6f, hgt, L, side * (x0 + 3f), hgt / 2 - 0.05f, 0);
                    int houses = rng.Int(1, 3);
                    for (int i = 0; i < houses; i++) HillHouse(b, rng, side, side * (x0 + rng.Range(1.8f, 3.6f)), hgt, rng.Range(-L / 2 + 4, L / 2 - 4));
                    for (int i = 0; i < rng.Int(1, 3); i++) SceneryProvinces.PineTree(b, rng, side * (x0 + rng.Range(0.6f, 5.4f)), hgt, rng.Range(-L / 2, L / 2), rng.Range(8f, 14f));
                }
                // tall pines right by the road and misty ridges behind
                for (int i = 0; i < rng.Int(1, 3); i++) SceneryProvinces.PineTree(b, rng, side * (R + rng.Range(1.6f, 2.4f)), SY, rng.Range(-L / 2, L / 2), rng.Range(9f, 15f));
                for (int i = 0; i < 2; i++) SceneryProvinces.Mountain(b, side * rng.Range(60, 95), rng.Range(-L / 2, L / 2), rng.Range(36, 60), rng.Range(22, 32), rng.Pick(new[] { "#4F6F63", "#5A7A6E", "#456558" }));
                if (rng.Chance(0.6)) NightKit.StreetLamp(b, side, R, rng.Range(-L / 2 + 6, L / 2 - 6), 5.5f, "#2B2D42");
            }
            return b.Data;
        }

        // ------------------------------------------------------------------ Fiesta Town
        private static readonly string[] Bunting = { "#D62828", "#FCD116", "#0038A8", "#2A9D8F", "#FFFFFF", "#F77F00", "#FF5D8F" };
        /// <summary>Lowest point of the banderitas strung across the street (above every vehicle and the chase camera).</summary>
        public const float BanderitasY = 8.6f;

        /// <summary>
        /// Banderitas: a sagging string of small triangular flags across the street at
        /// <paramref name="z"/>, from one balcony line to the other.
        /// </summary>
        public static void Banderitas(MeshBuilder b, GameRandom rng, float z, float top)
        {
            float x0 = -BL + 0.3f, x1 = BL - 0.3f;
            const int segs = 6;
            for (int i = 0; i < segs; i++)
            {
                float t0 = i / (float)segs, t1 = (i + 1) / (float)segs;
                float y0 = top - (float)Math.Sin(t0 * Math.PI) * 0.9f, y1 = top - (float)Math.Sin(t1 * Math.PI) * 0.9f;
                float xa = x0 + (x1 - x0) * t0, xb = x0 + (x1 - x0) * t1;
                float len = (float)Math.Sqrt((xb - xa) * (xb - xa) + (y1 - y0) * (y1 - y0));
                b.Box("paint", "#444444", len, 0.02f, 0.02f, (xa + xb) / 2, SY + (y0 + y1) / 2, z, new Rot(0, 0, (float)Math.Atan2(y1 - y0, xb - xa)));
            }
            int start = rng.Int(0, Bunting.Length - 1);
            for (int k = 0; k < 22; k++)
            {
                float t = (k + 0.5f) / 22f, x = x0 + (x1 - x0) * t, y = top - (float)Math.Sin(t * Math.PI) * 0.9f;
                // a little diamond turned 45°, its top half hidden in the string reads as a pennant
                b.Box("paint", Bunting[(start + k) % Bunting.Length], 0.34f, 0.34f, 0.015f, x, SY + y - 0.2f, z, new Rot(0, 0, (float)(Math.PI / 4)));
            }
        }

        /// <summary>Bahay na bato: stone ground floor, overhanging painted wooden upper floor with capiz windows and a tiled roof.</summary>
        private static void BahayNaBato(MeshBuilder b, GameRandom rng, float side, float z, float w)
        {
            float d = rng.Range(8f, 10f), cx = side * (BL + d / 2);
            string stone = rng.Pick(new[] { "#B89B7A", "#A88B6C", "#C7AE8E" });
            string wood = rng.Pick(new[] { "#E9C46A", "#F4A261", "#A8DADC", "#F1FAEE", "#E76F51", "#CDB4DB", "#90BE6D" });
            b.Box("paint", stone, d, 3.2f, w - 0.3f, cx, SY + 1.6f, z);
            b.Box("paint", "#5A3A22", 0.12f, 2.4f, 1.6f, side * BL - side * 0.02f, SY + 1.2f, z);                 // big wooden door
            b.Box("paint", wood, d + 0.8f, 3.0f, w - 0.1f, cx - side * 0.4f, SY + 4.7f, z);                   // overhanging upper floor
            for (float wz = z - w / 2 + 1.2f; wz < z + w / 2 - 0.8f; wz += 1.7f)
            {
                b.Box("glass", "#FFF6E0", 0.08f, 1.5f, 1.2f, side * (BL - 0.42f), SY + 4.8f, wz);                 // capiz sliding windows
                b.Box("nightglow", "#FFE2A8", 0.09f, 1.3f, 1.0f, side * (BL - 0.43f), SY + 4.8f, wz);
                b.Box("paint", "#6B4F35", 0.1f, 1.52f, 0.06f, side * (BL - 0.44f), SY + 4.8f, wz);                     // sliding-panel mullion
                b.Box("paint", "#6B4F35", 0.1f, 0.4f, 1.3f, side * (BL - 0.42f), SY + 3.65f, wz);                  // ventanillas
            }
            b.Box("paint", "#A4442B", d + 1.6f, 0.4f, w + 0.4f, cx - side * 0.4f, SY + 6.4f, z);                // tiled roof eave
            b.Cylinder("paint", "#8E3B24", 0f, (w + 0.4f) * 0.72f, 2.2f, 4, cx, SY + 7.7f, z, new Rot(0, (float)(Math.PI / 4), 0));
            // fiesta bunting draped along the balcony
            for (float bz = z - w / 2 + 0.6f; bz < z + w / 2 - 0.4f; bz += 1.2f)
                b.Box("paint", Bunting[rng.Int(0, Bunting.Length - 1)], 0.04f, 0.3f, 0.3f, side * (BL - 0.86f), SY + 3.3f, bz, new Rot((float)(Math.PI / 4), 0, 0));
        }

        /// <summary>Plaza stall with a striped awning: lechon, kakanin, toys and balloons.</summary>
        private static void FiestaStall(MeshBuilder b, GameRandom rng, float side, float z)
        {
            float x = side * (R + 2.4f);
            string a = rng.Pick(new[] { "#D62828", "#0038A8", "#2A9D8F", "#F77F00" });
            b.Box("paint", "#8D6E4F", 1.4f, 0.9f, 2.4f, x, SY + 0.45f, z);
            foreach (float e in new[] { -1.1f, 1.1f }) foreach (float f in new[] { -0.6f, 0.6f }) b.Box("paint", "#6B4F35", 0.06f, 2.2f, 0.06f, x + f, SY + 1.1f, z + e);
            for (int i = 0; i < 4; i++) b.Box("paint", i % 2 == 0 ? a : "#FFFFFF", 1.7f, 0.06f, 0.65f, x, SY + 2.25f, z - 0.98f + i * 0.65f, new Rot(0, 0, side * 0.2f));
            if (rng.Chance(0.5))
            {
                // lechon on the counter
                b.Sphere("paint", "#B5651D", 0.3f, x, SY + 1.15f, z, 8, 1f, 0.8f, 2.4f);
                b.Box("paint", "#E63946", 0.12f, 0.12f, 0.12f, x, SY + 1.2f, z + 0.78f);
            }
            else for (int i = 0; i < 5; i++) b.Sphere("paint", Bunting[rng.Int(0, Bunting.Length - 1)], 0.22f, x + rng.Range(-0.4f, 0.4f), SY + 2.9f + rng.Range(0, 0.6f), z + rng.Range(-0.8f, 0.8f), 6); // balloons
            b.Box("nightglow", "#FFE8B0", 1.2f, 0.05f, 2.0f, x, SY + 2.1f, z);
        }

        /// <summary>Baroque town church set back behind its patio, bell tower, belfry lights.</summary>
        private static void Church(MeshBuilder b, float side, float z)
        {
            float x = side * (BL + 9f);
            b.Box("paint", "#EFE3C8", 10f, 13f, 16f, x, SY + 6.5f, z);                                      // nave
            b.Box("paint", "#E6D7B5", 1.2f, 16f, 14f, side * (BL + 3.6f), SY + 8f, z);                       // facade
            b.Cylinder("paint", "#E6D7B5", 0f, 7.6f, 3.6f, 3, side * (BL + 3.6f), SY + 17.6f, z, new Rot((float)(Math.PI / 2), 0, (float)(Math.PI / 2))); // pediment
            b.Box("paint", "#5A3A22", 0.2f, 4.6f, 3.2f, side * (BL + 2.95f), SY + 2.3f, z);                  // main door
            b.Cylinder("paint", "#5A3A22", 1.6f, 1.6f, 0.2f, 12, side * (BL + 2.95f), SY + 4.6f, z, new Rot(0, 0, (float)(Math.PI / 2)));
            b.Cylinder("glass", "#3A6EA5", 1.1f, 1.1f, 0.15f, 12, side * (BL + 2.95f), SY + 10f, z, new Rot(0, 0, (float)(Math.PI / 2))); // rose window
            b.Cylinder("nightglow", "#FFD58A", 1.0f, 1.0f, 0.16f, 12, side * (BL + 2.9f), SY + 10f, z, new Rot(0, 0, (float)(Math.PI / 2)));
            foreach (float e in new[] { -5.5f, -2.6f, 2.6f, 5.5f }) b.Box("paint", "#D8C69D", 0.5f, 14f, 0.6f, side * (BL + 2.95f), SY + 7f, z + e); // pilasters
            // bell tower
            float tz = z - 10.5f;
            b.Box("paint", "#E6D7B5", 5f, 18f, 5f, side * (BL + 5f), SY + 9f, tz);
            b.Box("paint", "#2B2D42", 0.1f, 2.2f, 1.6f, side * (BL + 2.45f), SY + 14.5f, tz);                  // belfry opening
            b.Cylinder("gold", "#C9A227", 0.5f, 0.5f, 0.6f, 10, side * (BL + 2.6f), SY + 14.3f, tz);           // bell
            b.Cylinder("paint", "#A4442B", 0f, 3.4f, 4f, 4, side * (BL + 5f), SY + 20f, tz, new Rot(0, (float)(Math.PI / 4), 0));
            b.Box("gold", "#C9A227", 0.15f, 1.4f, 0.15f, side * (BL + 5f), SY + 22.6f, tz);
            b.Box("gold", "#C9A227", 0.15f, 0.15f, 0.8f, side * (BL + 5f), SY + 22.8f, tz);
            // patio with fiesta lights along the fence
            b.Box("paint", "#CFC6B4", 6f, 0.06f, 22f, side * (BL + 0.5f + 3f) - side * 3f, SY + 0.03f, z);
            for (float lz = z - 9; lz <= z + 9; lz += 1.5f) b.Box("glow", Bunting[(int)(lz * 3 + 100) % Bunting.Length], 0.15f, 0.15f, 0.15f, side * (BL + 0.1f), SY + 2.6f, lz);
        }

        /// <summary>"MALIGAYANG PIYESTA!" arch across the street on two bamboo-wrapped posts.</summary>
        private static void FiestaArch(MeshBuilder b, float z)
        {
            float top = BanderitasY + 1.4f, hw = BL - 1.2f;
            foreach (float s in new[] { -1f, 1f })
            {
                b.Cylinder("paint", "#C9A227", 0.25f, 0.28f, top + 1.2f, 8, s * (R + 1.4f), SY + (top + 1.2f) / 2, z);
                for (float y = 0.5f; y < top; y += 1.3f) b.Cylinder("paint", Bunting[(int)(y * 5) % Bunting.Length], 0.3f, 0.3f, 0.18f, 6, s * (R + 1.4f), SY + y, z);
            }
            b.Box("paint", "#5A1238", hw * 2 + 0.4f, 2.4f, 0.3f, 0, SY + top, z);
            // the painted board faces the oncoming jeepney (towards -Z)
            float y0 = SY + top - 1.05f, y1 = SY + top + 1.05f, x0 = -hw, x1 = hw, zf = z - 0.17f;
            b.Quad("tex:fiesta_arch", new V3(x0, y0, zf), new V3(x1, y0, zf), new V3(x1, y1, zf), new V3(x0, y1, zf), new V3(0, 0, -1), new V2(0, 0), new V2(1, 0), new V2(1, 1), new V2(0, 1));
            b.Box("glow", "#FFE8B0", hw * 2 + 0.4f, 0.08f, 0.08f, 0, SY + top - 1.25f, z - 0.2f);
        }

        /// <summary>Perya (travelling fair): a Ferris wheel and a carousel behind the stalls.</summary>
        private static void Perya(MeshBuilder b, GameRandom rng, float side, float z)
        {
            float x = side * (BL + 14f);
            const float r = 9f, hub = 11f;
            for (int i = 0; i < 12; i++)
            {
                double a = i * Math.PI / 6;
                b.Box("paint", "#E6E8EB", 0.15f, r, 0.15f, x, SY + hub + (float)Math.Cos(a) * r / 2, z + (float)Math.Sin(a) * r / 2, new Rot((float)a, 0, 0));
                float gy = SY + hub + (float)Math.Cos(a) * r, gz = z + (float)Math.Sin(a) * r;
                b.Box("paint", Bunting[i % Bunting.Length], 1.2f, 1.0f, 1.0f, x, gy - 0.7f, gz);                 // gondola
                b.Box("glow", Bunting[(i + 3) % Bunting.Length], 0.25f, 0.25f, 0.25f, x - side * 0.2f, gy, gz);    // rim lights
            }
            for (int i = 0; i < 24; i++)
            {
                double a0 = i * Math.PI / 12, a1 = (i + 1) * Math.PI / 12;
                float y0 = (float)Math.Cos(a0) * r, z0 = (float)Math.Sin(a0) * r, y1 = (float)Math.Cos(a1) * r, z1 = (float)Math.Sin(a1) * r;
                float len = (float)Math.Sqrt((y1 - y0) * (y1 - y0) + (z1 - z0) * (z1 - z0));
                b.Box("paint", "#F2F2F2", 0.18f, 0.18f, len + 0.05f, x, SY + hub + (y0 + y1) / 2, z + (z0 + z1) / 2, new Rot((float)-Math.Atan2(y1 - y0, z1 - z0), 0, 0));
            }
            foreach (float e in new[] { -3.5f, 3.5f }) b.Box("paint", "#9EA1A6", 0.3f, hub + 0.5f, 0.3f, x + side * 0.6f, SY + hub / 2, z + e, new Rot(-e / 18f, 0, 0));
            // carousel
            float cz = z + rng.Range(-14f, -11f), cx2 = side * (BL + 5.5f);
            b.Cylinder("paint", "#F1FAEE", 3.2f, 3.2f, 0.4f, 16, cx2, SY + 0.2f, cz);
            b.Cylinder("paint", "#FF5D8F", 0f, 3.6f, 1.6f, 16, cx2, SY + 3.9f, cz);
            b.Cylinder("gold", "#C9A227", 0.15f, 0.15f, 3f, 8, cx2, SY + 1.9f, cz);
            for (int i = 0; i < 8; i++)
            {
                double a = i * Math.PI / 4;
                float hx = cx2 + (float)Math.Cos(a) * 2.4f, hz = cz + (float)Math.Sin(a) * 2.4f;
                b.Cylinder("gold", "#C9A227", 0.04f, 0.04f, 2.8f, 6, hx, SY + 1.8f, hz);
                b.Box("paint", "#FFFFFF", 0.3f, 0.5f, 0.8f, hx, SY + 1.2f, hz, new Rot(0, (float)a, 0));
                b.Box("glow", Bunting[i % Bunting.Length], 0.2f, 0.2f, 0.2f, cx2 + (float)Math.Cos(a) * 3.5f, SY + 3.2f, cz + (float)Math.Sin(a) * 3.5f);
            }
        }

        /// <summary>
        /// Fiesta Town: a crowded old town centre during its fiesta – bahay na bato with capiz
        /// windows, banderitas strung across the street, food stalls and crowds; the special slice
        /// has the church with the "MALIGAYANG PIYESTA" arch, the feature slice the perya.
        /// </summary>
        public static MeshData Fiesta(int seed, bool church, bool perya)
        {
            var rng = new GameRandom(seed);
            var b = new MeshBuilder();
            foreach (float s in new[] { -1f, 1f })
            {
                bool open = (church && s > 0) || (perya && s < 0);
                if (!open)
                {
                    float z = -L / 2;
                    while (z < L / 2 - 3)
                    {
                        float w = Math.Min(rng.Range(8f, 12f), L / 2 - z);
                        BahayNaBato(b, rng, s, z + w / 2, w);
                        z += w;
                    }
                }
                for (int i = 0; i < rng.Int(1, 2); i++) FiestaStall(b, rng, s, rng.Range(-L / 2 + 3, L / 2 - 3));
                Pedestrians(b, rng, s, R + 0.8f, BL - 0.6f, 3);
                if (rng.Chance(0.5)) VendorBuilder.Random(b, rng, EnvironmentKind.Fiesta, s, SY, rng.Range(-L / 2 + 4, L / 2 - 4));
            }
            if (church)
            {
                Church(b, 1f, 2f);
                FiestaArch(b, -L / 2 + 6f);
                Pedestrians(b, rng, 1f, BL + 0.5f, BL + 5f, 3);
            }
            if (perya)
            {
                Perya(b, rng, -1f, 4f);
                Pedestrians(b, rng, -1f, BL + 0.5f, BL + 8f, 4);
            }
            // banderitas across the street, two or three strings per slice
            int strings = rng.Int(2, 3);
            for (int i = 0; i < strings; i++) Banderitas(b, rng, -L / 2 + (i + 0.5f) * L / strings + rng.Range(-2f, 2f), BanderitasY + 0.9f + rng.Range(0f, 0.8f));
            NightKit.StreetLamp(b, rng.Chance(0.5) ? -1f : 1f, R, rng.Range(-L / 2 + 6, L / 2 - 6), 6f, "#3D3D3D");
            return b.Data;
        }
    }
}
