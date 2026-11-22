using System;
using System.Collections.Generic;
using JeepneyRush.Logic;

namespace JeepneyRush.Logic.Geometry
{
    /// <summary>Commuters, coins, waiting sheds and road decals.</summary>
    public static class PropBuilder
    {
        private static readonly string[] Skins = { "#8D5524", "#A86B3C", "#C68642", "#7A4A26", "#B5835A" };
        private static readonly string[] Hair = { "#111111", "#2B1A10", "#1A1A1A", "#3B2416" };
        private static readonly string[] Pants = { "#283044", "#1D3557", "#3D3D3D", "#6B4F3A", "#14213D" };
        public const int PersonVariants = 8;

        /// <summary>Standing commuter, feet at the origin, facing +Z.</summary>
        public static MeshData Person(int i, bool seated = false)
        {
            var b = new MeshBuilder();
            AddPerson(b, i, seated);
            return b.Data;
        }

        public static void AddPerson(MeshBuilder b, int i, bool seated)
        {
            string shirt = VehicleBuilder.Shirts[i % VehicleBuilder.Shirts.Length];
            string skin = Skins[i % Skins.Length];
            string pants = Pants[(i * 3) % Pants.Length];
            float hip = seated ? 0f : 0.75f;
            if (seated)
            {
                foreach (float s in new[] { -1f, 1f }) b.Box("paint", pants, 0.15f, 0.15f, 0.42f, s * 0.11f, 0.02f, 0.2f);
            }
            else
            {
                foreach (float s in new[] { -1f, 1f }) b.Box("paint", pants, 0.15f, 0.75f, 0.16f, s * 0.11f, 0.38f, 0);
            }
            b.Box("paint", shirt, 0.44f, 0.58f, 0.26f, 0, hip + 0.28f, 0);
            b.Box("paint", shirt, 0.11f, 0.5f, 0.12f, -0.29f, hip + 0.27f, 0);
            b.Box("paint", skin, 0.11f, 0.5f, 0.12f, 0.29f, hip + 0.27f, 0, new Rot(0, 0, seated ? 0 : -2.6f)); // waving arm
            b.Sphere("paint", skin, 0.16f, 0, hip + 0.72f, 0, 8);
            b.Sphere("paint", Hair[i % Hair.Length], 0.165f, 0, hip + 0.78f, -0.03f, 8, 1, 0.7f, 1);
            if (i % 3 == 0) b.Box("paint", "#5C4033", 0.36f, 0.3f, 0.14f, 0, hip + 0.33f, -0.2f); // backpack
            if (i % 4 == 1) b.Cylinder("paint", "#F1C40F", 0.24f, 0.24f, 0.05f, 10, 0, hip + 0.93f, 0); // sun hat
        }

        public static MeshData Coin()
        {
            var b = new MeshBuilder();
            // coin faces +Z, spins around Y
            var rim = new[] { new V2(0.3f, -0.05f), new V2(0.42f, -0.05f), new V2(0.45f, 0f), new V2(0.42f, 0.05f), new V2(0.3f, 0.05f) };
            b.Lathe("gold", "#FFC928", rim, 20, 0, 0, 0, new Rot((float)(Math.PI / 2), 0, 0));
            b.Cylinder("gold", "#FFDF6E", 0.31f, 0.31f, 0.08f, 20, 0, 0, 0, new Rot((float)(Math.PI / 2), 0, 0));
            b.Box("gold", "#E0A800", 0.08f, 0.3f, 0.1f, 0, 0, 0); // embossed "1"
            return b.Data;
        }

/// <summary>
        /// Roadside gas station ("gasolinahan") on the right curb: canopy cantilevered over the
        /// curb lane, two pumps on an island, a price sign before it and the refuelling pad in
        /// the lane. Generic colours (no real brand). Local x is road space (lane centre = laneX).
        /// </summary>
        public static MeshData GasStation(float laneX, float laneWidth, float padLength)
        {
            var b = new MeshBuilder();
            float rw = SceneryBuilder.RoadHalfWidth, sh = SceneryBuilder.SidewalkHeight;
            const string teal = "#0E7C86", orange = "#FF8C1A", white = "#F4F4F0", grey = "#8E9499";
            // ---- pad in the lane: dashed white border + orange chevrons
            float hx = laneWidth / 2f, hz = padLength / 2f;
            for (float z = -hz; z < hz; z += 1.6f)
                foreach (float sx in new[] { -1f, 1f })
                    b.Box("paint", white, 0.14f, 0.012f, 0.9f, laneX + sx * (hx - 0.07f), 0.008f, z + 0.45f);
            foreach (float z in new[] { -hz, hz }) b.Box("paint", white, laneWidth, 0.012f, 0.16f, laneX, 0.008f, z);
            for (int i = 0; i < 4; i++)
            {
                float z = -hz + 2.2f + i * (padLength - 4.4f) / 3f;
                b.Box("paint", orange, 1.1f, 0.012f, 0.22f, laneX - 0.42f, 0.009f, z, new Rot(0, -0.6f, 0));
                b.Box("paint", orange, 1.1f, 0.012f, 0.22f, laneX + 0.42f, 0.009f, z, new Rot(0, 0.6f, 0));
            }
            // ---- canopy over the curb lane and the sidewalk
            float cx0 = laneX - 1.4f, cx1 = SceneryBuilder.SidewalkOuter - 0.2f, cy = 5.2f;
            float cw = cx1 - cx0, cmx = (cx0 + cx1) / 2f;
            b.Box("paint", white, cw, 0.18f, padLength + 2f, cmx, cy, 0);                 // roof slab
            b.Box("paint", teal, cw + 0.1f, 0.55f, 0.12f, cmx, cy + 0.1f, -(padLength + 2f) / 2f); // fascia (front)
            b.Box("paint", teal, cw + 0.1f, 0.55f, 0.12f, cmx, cy + 0.1f, (padLength + 2f) / 2f);
            b.Box("paint", teal, 0.12f, 0.55f, padLength + 2f, cx0, cy + 0.1f, 0);
            b.Box("glow", orange, cw + 0.12f, 0.12f, 0.13f, cmx, cy - 0.05f, -(padLength + 2f) / 2f); // lit stripe
            b.Box("glow", orange, 0.13f, 0.12f, padLength + 2f, cx0 - 0.01f, cy - 0.05f, 0);
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 2; j++)
                    b.Box("glow", "#FFFBEA", 1.0f, 0.04f, 1.4f, cx0 + 1.2f + j * (cw - 2.4f), cy - 0.11f, -hz + 2f + i * (padLength - 4f) / 2f); // light panels
            foreach (float z in new[] { -hz + 1.2f, hz - 1.2f })
            {
                b.Box("paint", white, 0.4f, cy, 0.4f, rw + 0.6f, cy / 2f, z);          // columns on the sidewalk
                b.Box("paint", teal, 0.44f, 0.6f, 0.44f, rw + 0.6f, sh + 0.3f, z);
                b.Box("paint", white, 0.4f, cy, 0.4f, cx1 - 0.4f, cy / 2f, z);
            }
            // ---- pump island with two pumps facing the road
            float px = rw + 1.0f;
            b.Box("paint", "#C9CCCF", 1.1f, 0.25f, padLength - 3f, px, sh + 0.12f, 0);
            b.Box("paint", "#FFD60A", 1.12f, 0.08f, padLength - 2.9f, px, sh + 0.04f, 0);
            foreach (float z in new[] { -2.4f, 2.4f })
            {
                b.Box("paint", white, 0.7f, 1.7f, 0.9f, px, sh + 0.25f + 0.85f, z);
                b.Box("paint", teal, 0.72f, 0.3f, 0.92f, px, sh + 0.25f + 1.55f, z);
                b.Box("glow", "#7CFFB2", 0.04f, 0.32f, 0.5f, px - 0.37f, sh + 0.25f + 1.15f, z); // display
                b.Box("glow", orange, 0.04f, 0.12f, 0.5f, px - 0.37f, sh + 0.25f + 0.8f, z);
                b.Box("paint", "#222222", 0.08f, 0.3f, 0.12f, px - 0.38f, sh + 0.25f + 0.5f, z + 0.25f); // nozzle
                b.CylinderZ("paint", "#222222", 0.025f, 0.6f, 6, px - 0.42f, sh + 0.25f + 0.35f, z + 0.25f);
            }
            // ---- tall pylon sign before the station, facing the oncoming jeepney (-Z): name + LED prices,
            //      self-lit so it reads from far away, day and night
            float sx2 = SceneryBuilder.SidewalkOuter - 1.2f, sz = -hz - 10f;
            const float pw = 3.4f, ph = 4.25f, py = 6.2f; // board size and bottom height
            foreach (float e in new[] { -1.1f, 1.1f }) b.Box("paint", grey, 0.3f, py + 0.2f, 0.3f, sx2 + e, (py + 0.2f) / 2f, sz + 0.1f);
            b.Box("paint", teal, pw + 0.4f, ph + 0.4f, 0.4f, sx2, py + ph / 2f, sz + 0.12f);
            b.Quad("utex:gas_sign", new V3(sx2 - pw / 2, py, sz - 0.1f), new V3(sx2 + pw / 2, py, sz - 0.1f), new V3(sx2 + pw / 2, py + ph, sz - 0.1f), new V3(sx2 - pw / 2, py + ph, sz - 0.1f), new V3(0, 0, -1),
                new V2(0, 0), new V2(1, 0), new V2(1, 1), new V2(0, 1));
            b.Box("glow", orange, pw + 0.45f, 0.18f, 0.45f, sx2, py + ph + 0.3f, sz + 0.12f);
            // ---- lettering on the canopy fascia: front (facing the jeepney) and the road side
            float fz = -(padLength + 2f) / 2f - 0.07f;
            b.Quad("utex:gas_fascia", new V3(cx0, cy - 0.17f, fz), new V3(cx1, cy - 0.17f, fz), new V3(cx1, cy + 0.37f, fz), new V3(cx0, cy + 0.37f, fz), new V3(0, 0, -1),
                new V2(0, 0), new V2(1, 0), new V2(1, 1), new V2(0, 1));
            float fx = cx0 - 0.07f, flen = Math.Min(padLength + 2f, 7f);
            // seen from the road (looking +X): +Z is on the viewer's left
            b.Quad("utex:gas_fascia", new V3(fx, cy - 0.17f, flen / 2), new V3(fx, cy + 0.37f, flen / 2), new V3(fx, cy + 0.37f, -flen / 2), new V3(fx, cy - 0.17f, -flen / 2), new V3(-1, 0, 0),
                new V2(0, 0), new V2(0, 1), new V2(1, 1), new V2(1, 0));
            // bright under the canopy at night
            NightKit.Pool(b, laneX, 0.04f, -hz / 2, 4.2f);
            NightKit.Pool(b, laneX, 0.04f, hz / 2, 4.2f);
            NightKit.Pool(b, rw + 2f, sh + 0.03f, 0, 4.0f);
            // drum of a gas can on the corner – unmistakably a fuel stop
            b.Cylinder("paint", "#D62828", 0.3f, 0.3f, 0.8f, 12, cx1 - 1.2f, sh + 0.4f, hz - 0.6f);
            return b.Data;
        }

/// <summary>Lamp centres (red, yellow, green) of the two signal heads, matching <see cref="Intersection"/>.</summary>
        public static V3[] SignalLamps()
        {
            var list = new List<V3>();
            foreach (var h in SignalHeads)
                for (int k = 0; k < 3; k++) list.Add(new V3(h.X, h.Y + 0.42f - k * 0.42f, h.Z - 0.2f));
            return list.ToArray();
        }

        /// <summary>Signal head centres: one hanging over the road on a mast arm, one on the left pole.</summary>
        public static readonly V3[] SignalHeads =
        {
            new V3(1.6f, 5.0f, -6.5f),
            new V3(-SceneryBuilder.RoadHalfWidth - 0.5f, 3.3f, -6.6f),
        };

        /// <summary>
        /// Signalised intersection centred at the origin (jeepney approaches from -Z): side
        /// streets, zebra crossings, the stop line and two traffic-light poles. The lit lamps
        /// are separate objects switched at runtime; this mesh has the dark lenses.
        /// </summary>
        public static MeshData Intersection()
        {
            var b = new MeshBuilder();
            float rw = SceneryBuilder.RoadHalfWidth, so = SceneryBuilder.SidewalkOuter, sh = SceneryBuilder.SidewalkHeight;
            const float half = 5f;
            const string asphalt = "#3C3C42", white = "#F2F2F2";
            // side streets crossing the sidewalks (left and right)
            foreach (float sx in new[] { -1f, 1f })
            {
                float cx = sx * (rw + so) / 2f;
                b.Box("paint", asphalt, so - rw, 0.04f, half * 2, cx, sh + 0.01f, 0);
                for (int i = 0; i < 2; i++) b.Box("paint", "#FFD60A", 0.9f, 0.012f, 0.14f, cx + (i - 0.5f) * 1.6f, sh + 0.035f, 0);
            }
            // zebra crossings before and after the box
            foreach (float z in new[] { -7.4f, 7.4f })
                for (float x = -rw + 0.5f; x <= rw - 0.4f; x += 1.0f)
                    b.Box("paint", white, 0.55f, 0.012f, 2.8f, x, 0.008f, z);
            // stop line across the jeepney's lanes
            b.Box("paint", white, rw * 2 - 0.4f, 0.014f, 0.45f, 0, 0.009f, -9.5f);
            // "yellow box" hatch inside the intersection (keep clear)
            b.Box("paint", "#FFD60A", rw * 2 - 0.6f, 0.01f, 0.18f, 0, 0.007f, -half + 0.2f);
            b.Box("paint", "#FFD60A", rw * 2 - 0.6f, 0.01f, 0.18f, 0, 0.007f, half - 0.2f);
            // ---- mast arm pole on the right, head hanging over the road
            var h0 = SignalHeads[0];
            float px = rw + 0.5f;
            b.Cylinder("paint", "#5A6068", 0.12f, 0.15f, 5.8f, 10, px, 2.9f, h0.Z);
            b.Box("paint", "#5A6068", px - h0.X + 0.4f, 0.14f, 0.14f, (px + h0.X) / 2f, h0.Y + 0.85f, h0.Z);
            Head(b, h0);
            // ---- left post-mounted signal
            var h1 = SignalHeads[1];
            b.Cylinder("paint", "#5A6068", 0.1f, 0.12f, h1.Y + 0.6f, 10, h1.X, (h1.Y + 0.6f) / 2f, h1.Z);
            Head(b, h1);
            // pedestrian signal boxes on the right pole
            b.Box("paint", "#1A1A1A", 0.32f, 0.32f, 0.2f, px, 2.6f, h0.Z - 0.18f);
            return b.Data;
        }

        private static void Head(MeshBuilder b, V3 h)
        {
            b.Box("paint", "#1E1E22", 0.5f, 1.4f, 0.36f, h.X, h.Y, h.Z);
            b.Box("paint", "#FFD60A", 0.66f, 1.56f, 0.04f, h.X, h.Y, h.Z + 0.2f); // reflective backplate edge
            for (int k = 0; k < 3; k++)
            {
                float y = h.Y + 0.42f - k * 0.42f;
                b.Cylinder("paint", "#26282C", 0.16f, 0.16f, 0.04f, 12, h.X, y, h.Z - 0.19f, new Rot((float)(Math.PI / 2), 0, 0));
                b.Box("paint", "#111111", 0.38f, 0.05f, 0.22f, h.X, y + 0.18f, h.Z - 0.3f); // visor
            }
        }

        public static MeshData SignalLamp(string color)
        {
            var b = new MeshBuilder();
            b.Cylinder("glow", color, 0.165f, 0.165f, 0.05f, 12, 0, 0, 0, new Rot((float)(Math.PI / 2), 0, 0));
            b.Sphere("bubble", color, 0.34f, 0, 0, -0.04f, 10); // glow halo
            return b.Data;
        }

        public static MeshData WaitingShed()
        {
            var b = new MeshBuilder();
            b.Box("paint", "#0B6E4F", 2.6f, 0.12f, 5.2f, 0, 2.6f, 0);
            b.Box("paint", "#FFD100", 2.7f, 0.06f, 5.3f, 0, 2.68f, 0);
            foreach (float z in new[] { -2.3f, 2.3f }) foreach (float x in new[] { -1.1f, 1.1f }) b.Box("paint", "#9AA0A6", 0.1f, 2.6f, 0.1f, x, 1.3f, z);
            b.Box("paint", "#8D6E63", 0.5f, 0.08f, 4.2f, 0.7f, 0.5f, 0);
            b.Box("paint", "#8D6E63", 0.1f, 0.5f, 4.2f, 0.95f, 0.75f, 0);
            b.Box("paint", "#9E9E9E", 2.6f, 0.1f, 5.2f, 0, 0.05f, 0);
            return b.Data;
        }

        /// <summary>Stop sign facing +Z (toward the oncoming jeepney) using a cell of the stop_signs atlas.</summary>
        public static MeshData StopSign(int nameIndex)
        {
            var b = new MeshBuilder();
            b.Cylinder("paint", "#888888", 0.06f, 0.06f, 3.3f, 6, 0, 1.65f, 0);
            b.Box("paint", "#222222", 2.1f, 1.08f, 0.06f, 0, 3.4f, -0.04f);
            // seen from the front (+Z looking toward -Z), -X is on the viewer's right
            b.Quad("tex:" + StopNames.SignTexture(nameIndex), new V3(1.0f, 2.9f, 0), new V3(-1.0f, 2.9f, 0), new V3(-1.0f, 3.9f, 0), new V3(1.0f, 3.9f, 0), new V3(0, 0, 1),
                new V2(0, 0), new V2(1, 0), new V2(1, 1), new V2(0, 1));
            return b.Data;
        }

        /// <summary>Yellow PARA zone decal lying on the road, centred at the origin.</summary>
        public static MeshData ParaZone(float width, float length)
        {
            var b = new MeshBuilder();
            float hw = width / 2, hl = length / 2;
            b.Quad("tex:para_zone", new V3(-hw, 0, -hl), new V3(hw, 0, -hl), new V3(hw, 0, hl), new V3(-hw, 0, hl), V3.Up,
                new V2(0, 0), new V2(1, 0), new V2(1, 1), new V2(0, 1));
            return b.Data;
        }

        /// <summary>Glowing arrow hovering over a stop (rotates in game).</summary>
        public static MeshData StopArrow(string color)
        {
            var b = new MeshBuilder();
            b.Cylinder("glow", color, 0f, 0.55f, 0.8f, 4, 0, 0, 0, new Rot((float)Math.PI, 0, 0));
            b.Box("glow", color, 0.36f, 0.7f, 0.36f, 0, 0.7f, 0);
            return b.Data;
        }

        /// <summary>Big orange marker that floats above the refuelling pad ("GAS" label + down arrow), seen from far away.</summary>
        public static MeshData GasMarker()
        {
            var b = new MeshBuilder();
            const string orange = "#FF8C1A";
            b.Cylinder("glow", orange, 0f, 0.7f, 1.0f, 4, 0, 0, 0, new Rot((float)Math.PI, 0, 0));
            b.Box("glow", orange, 0.45f, 0.8f, 0.45f, 0, 0.85f, 0);
            float w = 2.4f, h = 1.2f, y = 1.6f;
            // both sides of the label so it reads while it turns
            b.Quad("utexa:gas_label", new V3(-w / 2, y, -0.02f), new V3(w / 2, y, -0.02f), new V3(w / 2, y + h, -0.02f), new V3(-w / 2, y + h, -0.02f), new V3(0, 0, -1),
                new V2(0, 0), new V2(1, 0), new V2(1, 1), new V2(0, 1));
            b.Quad("utexa:gas_label", new V3(w / 2, y, 0.02f), new V3(-w / 2, y, 0.02f), new V3(-w / 2, y + h, 0.02f), new V3(w / 2, y + h, 0.02f), new V3(0, 0, 1),
                new V2(0, 0), new V2(1, 0), new V2(1, 1), new V2(0, 1));
            return b.Data;
        }

        /// <summary>Floating power-up pickup (rotates in game): glowing ring + an icon shape per kind.</summary>
        public static MeshData PowerUp(PowerUpKind k)
        {
            var b = new MeshBuilder();
            string ring = k == PowerUpKind.Magnet ? "#FF4D6D" : k == PowerUpKind.Shield ? "#4CC9F0" : k == PowerUpKind.Turbo ? "#FF9F1C"
                : k == PowerUpKind.DoubleCoins ? "#FFD60A" : k == PowerUpKind.PassengerBonus ? "#2BFF88" : "#FF3B30";
            // glowing halo ring standing upright, facing the player
            var halo = new[] { new V2(0.62f, -0.06f), new V2(0.72f, -0.06f), new V2(0.72f, 0.06f), new V2(0.62f, 0.06f), new V2(0.62f, -0.06f) };
            b.Lathe("glow", ring, halo, 24, 0, 0, 0, new Rot((float)(Math.PI / 2), 0, 0), false);
            switch (k)
            {
                case PowerUpKind.Magnet:
                    b.Box("paint", "#E63946", 0.16f, 0.5f, 0.16f, -0.22f, 0.05f, 0);
                    b.Box("paint", "#E63946", 0.16f, 0.5f, 0.16f, 0.22f, 0.05f, 0);
                    b.Box("paint", "#E63946", 0.6f, 0.16f, 0.16f, 0, -0.2f, 0);
                    b.Box("chrome", "#E6EBF2", 0.17f, 0.14f, 0.17f, -0.22f, 0.36f, 0);
                    b.Box("chrome", "#E6EBF2", 0.17f, 0.14f, 0.17f, 0.22f, 0.36f, 0);
                    break;
                case PowerUpKind.Shield:
                    b.Sphere("glass", "#4CC9F0", 0.42f, 0, 0, 0, 12, 1, 1.1f, 0.35f);
                    b.Box("paint", "#FFFFFF", 0.08f, 0.4f, 0.2f, 0, 0, 0.02f);
                    b.Box("paint", "#FFFFFF", 0.3f, 0.08f, 0.2f, 0, 0.06f, 0.02f);
                    break;
                case PowerUpKind.Turbo:
                    for (int i = 0; i < 2; i++)
                    {
                        b.Box("glow", "#FF9F1C", 0.42f, 0.12f, 0.12f, -0.14f, 0.12f - i * 0.0f + 0.05f, -0.1f + i * 0.2f, new Rot(0, 0, 0.8f));
                        b.Box("glow", "#FFD166", 0.42f, 0.12f, 0.12f, 0.14f, 0.17f, -0.1f + i * 0.2f, new Rot(0, 0, -0.8f));
                    }
                    b.Cylinder("paint", "#FF6B35", 0.0f, 0.22f, 0.45f, 8, 0, -0.18f, 0);
                    break;
                case PowerUpKind.DoubleCoins:
                    foreach (var (x, y) in new[] { (-0.14f, -0.1f), (0.14f, 0.1f) })
                    {
                        b.Cylinder("gold", "#FFC928", 0.3f, 0.3f, 0.08f, 18, x, y, 0, new Rot((float)(Math.PI / 2), 0, 0));
                        b.Cylinder("gold", "#E0A800", 0.2f, 0.2f, 0.1f, 18, x, y, 0, new Rot((float)(Math.PI / 2), 0, 0));
                    }
                    break;
                case PowerUpKind.PassengerBonus:
                    b.Push(new V3(0, -0.5f, 0), Rot.None, 0.6f);
                    AddPerson(b, 4, false);
                    b.Pop();
                    b.Box("glow", "#2BFF88", 0.12f, 0.34f, 0.12f, 0.36f, 0.2f, 0);
                    b.Box("glow", "#2BFF88", 0.34f, 0.12f, 0.12f, 0.36f, 0.2f, 0);
                    break;
                default: // repair kit
                    b.Box("paint", "#D62828", 0.6f, 0.42f, 0.32f, 0, 0, 0);
                    b.Box("paint", "#FFFFFF", 0.14f, 0.3f, 0.34f, 0, 0, 0);
                    b.Box("paint", "#FFFFFF", 0.3f, 0.14f, 0.34f, 0, 0, 0);
                    b.Box("paint", "#333333", 0.26f, 0.06f, 0.1f, 0, 0.25f, 0);
                    break;
            }
            return b.Data;
        }

        /// <summary>Translucent shield bubble around the jeepney (unit sphere scaled in game).</summary>
        public static MeshData Bubble()
        {
            var b = new MeshBuilder();
            b.Sphere("bubble", "#7FDBFF", 1f, 0, 0, 0, 16);
            return b.Data;
        }

        /// <summary>
        /// MMDA traffic enforcer at a checkpoint: blue uniform, white cap, reflective vest,
        /// raising a STOP paddle, next to a patrol motorcycle and a CHECKPOINT A-frame sign.
        /// Origin on the ground; the enforcer faces +Z (toward the oncoming jeepney).
        /// Siren lights are separate meshes (<see cref="Siren"/>) so they can flash.
        /// </summary>
        public static MeshData Enforcer() => Enforcer(EnforcerKind.Mmda);

        /// <summary>
        /// Checkpoint officer by route: MMDA (blue + lime vest, white cap) in Metro Manila, police
        /// (navy uniform, navy cap) on provincial roads, city traffic enforcer (orange) in Cebu/Baguio.
        /// </summary>
        public static MeshData Enforcer(EnforcerKind kind)
        {
            var b = new MeshBuilder();
            const string skin = "#A86B3C";
            string blue = kind == EnforcerKind.Police ? "#1A2340" : kind == EnforcerKind.CityTraffic ? "#F77F00" : "#1F3FBF";
            string pants = kind == EnforcerKind.CityTraffic ? "#2B2D42" : "#1B1F3B";
            string vest = kind == EnforcerKind.Police ? "#1A2340" : kind == EnforcerKind.CityTraffic ? "#FFD60A" : "#C6FF00";
            string cap = kind == EnforcerKind.Police ? "#1A2340" : "#FFFFFF";
            string bikeBody = kind == EnforcerKind.Police ? "#F8F9FA" : kind == EnforcerKind.CityTraffic ? "#F8F9FA" : "#F8F9FA";
            string signTex = Enforcers.SignTexture(kind);
            foreach (float s in new[] { -1f, 1f })
            {
                b.Box("paint", pants, 0.16f, 0.78f, 0.17f, s * 0.11f, 0.39f, 0);
                b.Box("paint", "#111111", 0.18f, 0.08f, 0.26f, s * 0.11f, 0.04f, 0.04f); // boots
            }
            b.Box("paint", blue, 0.46f, 0.6f, 0.28f, 0, 1.08f, 0);
            b.Box("paint", vest, 0.48f, 0.42f, 0.3f, 0, 1.12f, 0);
            foreach (float y in new[] { 1.02f, 1.2f }) b.Box("chrome", "#E6EBF2", 0.49f, 0.04f, 0.31f, 0, y, 0); // reflective stripes
            b.Box("paint", blue, 0.12f, 0.5f, 0.13f, -0.3f, 1.08f, 0);                                     // left arm down
            b.Box("paint", blue, 0.12f, 0.5f, 0.13f, 0.33f, 1.5f, 0.05f, new Rot(0, 0, 0.35f));           // right arm raised
            b.Box("paint", skin, 0.1f, 0.12f, 0.1f, 0.42f, 1.78f, 0.05f);
            b.Sphere("paint", skin, 0.16f, 0, 1.55f, 0, 8);
            b.Cylinder("paint", cap, 0.19f, 0.17f, 0.12f, 12, 0, 1.68f, 0);                               // cap
            if (kind == EnforcerKind.Police) b.Box("gold", "#FFC93C", 0.08f, 0.08f, 0.03f, 0, 1.7f, 0.18f);  // cap badge
            b.Cylinder("paint", blue, 0.195f, 0.195f, 0.04f, 12, 0, 1.64f, 0);
            b.Box("paint", "#111111", 0.3f, 0.03f, 0.18f, 0, 1.62f, 0.14f);                                // visor
            b.Cylinder("paint", "#FFFFFF", 0.015f, 0.015f, 0.35f, 5, 0.47f, 1.95f, 0.05f);                // paddle stick
            b.Cylinder("paint", "#D62828", 0.2f, 0.2f, 0.03f, 8, 0.47f, 2.22f, 0.06f, new Rot((float)(Math.PI / 2), 0, 0));
            b.Box("paint", "#FFFFFF", 0.26f, 0.06f, 0.035f, 0.47f, 2.22f, 0.08f);
            // patrol motorcycle (behind, angled)
            b.Push(new V3(-0.9f, 0, -1.4f), new Rot(0, 0.5f, 0));
            b.CylinderX("paint", "#16161A", 0.32f, 0.12f, 12, 0, 0.32f, 0.72f, true);
            b.CylinderX("paint", "#16161A", 0.32f, 0.14f, 12, 0, 0.32f, -0.72f, true);
            b.Box("paint", bikeBody, 0.4f, 0.4f, 1.2f, 0, 0.68f, 0);
            b.Box("paint", blue, 0.42f, 0.12f, 1.22f, 0, 0.62f, 0);
            b.Box("paint", "#111111", 0.32f, 0.12f, 0.6f, 0, 0.94f, -0.25f);
            b.Box("chrome", "#E6EBF2", 0.7f, 0.04f, 0.04f, 0, 1.12f, 0.55f);
            b.Box("glass", "#A0C4FF", 0.4f, 0.3f, 0.04f, 0, 1.25f, 0.62f, new Rot(-0.3f, 0, 0));
            b.Box("paint", "#333333", 0.06f, 0.5f, 0.06f, 0, 1.25f, -0.55f);                                // siren mast
            b.Pop();
            // A-frame checkpoint sign facing the oncoming traffic (+Z)
            b.Push(new V3(0.9f, 0, 0.6f), Rot.None);
            b.Box("paint", "#444444", 0.05f, 1.2f, 0.05f, -0.55f, 0.6f, -0.1f, new Rot(0.15f, 0, 0));
            b.Box("paint", "#444444", 0.05f, 1.2f, 0.05f, 0.55f, 0.6f, -0.1f, new Rot(0.15f, 0, 0));
            b.Box("paint", "#222222", 1.24f, 0.66f, 0.04f, 0, 0.95f, 0, new Rot(0.15f, 0, 0));
            b.Push(new V3(0, 0.95f, 0.025f), new Rot(0.15f, 0, 0));
            // seen from the front (+Z looking -Z): -X is on the viewer's right
            b.Quad("tex:" + signTex, new V3(0.6f, -0.3f, 0), new V3(-0.6f, -0.3f, 0), new V3(-0.6f, 0.3f, 0), new V3(0.6f, 0.3f, 0), new V3(0, 0, 1),
                new V2(0, 0), new V2(1, 0), new V2(1, 1), new V2(0, 1));
            b.Pop();
            b.Pop();
            // traffic cones in front of the checkpoint
            foreach (float z in new[] { 1.8f, 3.2f })
            {
                b.Cylinder("paint", "#FF6D00", 0.04f, 0.18f, 0.55f, 10, -0.2f, 0.3f, z, default, true);
                b.Cylinder("paint", "#FFFFFF", 0.1f, 0.13f, 0.1f, 10, -0.2f, 0.32f, z, default, true);
            }
            return b.Data;
        }

        /// <summary>Flashing siren light for the patrol bike (red or blue), matches the bike's mast.</summary>
        public static MeshData Siren(bool red)
        {
            var b = new MeshBuilder();
            b.Push(new V3(-0.9f, 0, -1.4f), new Rot(0, 0.5f, 0));
            b.Box("glow", red ? "#FF2A2A" : "#2A6BFF", 0.14f, 0.14f, 0.14f, red ? -0.1f : 0.1f, 1.55f, -0.55f);
            b.Pop();
            return b.Data;
        }

        /// <summary>UV rect of a cell in a cols x rows atlas (row 0 at the top of the image).</summary>
        public static (float u0, float v0, float u1, float v1) AtlasCell(int index, int cols, int rows)
        {
            int c = index % cols, r = index / cols;
            float u0 = c / (float)cols, u1 = (c + 1) / (float)cols;
            float v1 = 1 - r / (float)rows, v0 = 1 - (r + 1) / (float)rows;
            return (u0, v0, u1, v1);
        }
    }
}
