using System;
using System.Collections.Generic;

namespace JeepneyRush.Logic.Geometry
{
    /// <summary>
    /// Builds the hero jeepney (Unity coordinates: +Z forward, +Y up, origin on the
    /// ground at the vehicle centre). Modelled on classic Sarao/Francisco-style
    /// jeepneys: long hood with stacked chrome grille and horse mascots, swept chrome
    /// fenders, open-sided passenger body with painted art panels, roof route sign,
    /// side spare tyre, rear ladder and lots of lights.
    /// Wheels are built separately (<see cref="BuildWheel"/>) so they can spin.
    /// </summary>
    public static class JeepneyBuilder
    {
        public const string Chrome = "#E6EBF2";
        public const string ChromeDark = "#9AA3B2";
        public const string Tire = "#18181C";
        public const string Glass = "#1A2533";
        public const string Interior = "#2A1E2C";
        public const string Seat = "#8B1E2D";

        public const float WheelRadius = 0.44f;
        public const float FrontAxleZ = 1.95f;
        public const float RearAxleZ = -1.75f;
        public const float TrackHalf = 0.93f;

        public const float BodyRearZ = -3.0f;
        public const float BodyFrontZ = 0.65f;
        public const float BeltY = 1.45f;   // top of the painted lower body
        public const float SillY = 0.58f;   // bottom of the body
        public const float RoofY = 2.18f;

        /// <summary>Seat positions (local) for visible passengers, in boarding order.</summary>
        public static readonly V3[] Seats =
        {
            new V3(-0.62f, 1.05f, 0.1f), new V3(0.62f, 1.05f, -0.6f), new V3(-0.62f, 1.05f, -1.3f),
            new V3(0.62f, 1.05f, -2.0f), new V3(-0.62f, 1.05f, -2.4f),
            new V3(0.62f, 1.05f, 0.1f), new V3(-0.62f, 1.05f, -0.6f), new V3(0.62f, 1.05f, -1.3f),
            new V3(-0.62f, 1.05f, -1.85f), new V3(0.62f, 1.05f, -2.5f),
        };

        public static V3[] WheelPositions => new[]
        {
            new V3(-TrackHalf, WheelRadius, FrontAxleZ), new V3(TrackHalf, WheelRadius, FrontAxleZ),
            new V3(-TrackHalf, WheelRadius, RearAxleZ), new V3(TrackHalf, WheelRadius, RearAxleZ),
        };

        public static MeshData Build(JeepneyLivery L, Palette palette = null)
        {
            var b = new MeshBuilder(palette);
            string paint = L.Body, hood = L.Hood, trim = L.Trim, roof = L.Roof;
            string C = L.Chrome;
            bool modern = L.Style == JeepneyStyle.Modern;
            string lowerFender = L.ChromeLowerBody ? C : L.Body;
            string fenderKey = L.ChromeLowerBody ? "chrome" : "paint";

            // ---------------------------------------------------------------- chassis
            b.Box("paint", "#202126", 1.6f, 0.24f, 5.9f, 0, 0.5f, 0f);
            b.CylinderX("chrome", ChromeDark, 0.06f, 1.9f, 6, 0, WheelRadius, FrontAxleZ);
            b.CylinderX("chrome", ChromeDark, 0.06f, 1.9f, 6, 0, WheelRadius, RearAxleZ);
            // exhaust pipe
            b.CylinderZ("chrome", ChromeDark, 0.05f, 0.6f, 6, 0.55f, 0.42f, -3.05f);

            if (!modern)
            {
                // ---------------------------------------------------------------- hood
                var hoodProfile = new List<V2>
                {
                    new V2(1.2f, 0.62f), new V2(3.05f, 0.62f), new V2(3.05f, 1.12f), new V2(2.95f, 1.2f), new V2(1.2f, 1.34f),
                };
                b.ExtrudeX("paint", hood, hoodProfile, -0.74f, 0.74f);
                // hood chrome centre strip and side louvres
                b.Box("chrome", C, 0.08f, 0.04f, 1.8f, 0, 1.3f, 2.12f, new Rot(-0.075f, 0, 0));
                for (int i = 0; i < 4; i++)
                    foreach (float sx in new[] { -1f, 1f })
                        b.Box("chrome", C, 0.02f, 0.05f, 0.28f, sx * 0.75f, 0.98f, 1.6f + i * 0.3f);
                // ---------------------------------------------------------------- grille (stacked chrome)
                b.Box("chrome", C, 1.12f, 0.62f, 0.1f, 0, 0.9f, 3.06f);
                for (int i = 0; i < 9; i++) b.Box("paint", "#1D1E24", 0.05f, 0.5f, 0.04f, -0.48f + i * 0.12f, 0.9f, 3.12f);
                b.Box("chrome", C, 1.2f, 0.07f, 0.12f, 0, 1.24f, 3.07f);
                // ---------------------------------------------------------------- bumper & bull bar
                b.Box("chrome", C, 2.2f, 0.2f, 0.26f, 0, 0.52f, 3.28f);
                b.Box("chrome", C, 2.0f, 0.06f, 0.06f, 0, 0.7f, 3.38f);
                for (int i = 0; i < 4; i++) b.CylinderX("chrome", C, 0.035f, 1.7f - i * 0.15f, 8, 0, 0.75f + i * 0.12f, 3.4f);
                foreach (float sx in new[] { -1f, 1f })
                {
                    b.Cylinder("chrome", C, 0.035f, 0.035f, 0.6f, 8, sx * 0.85f, 0.85f, 3.4f);
                    b.Cylinder("chrome", C, 0.035f, 0.035f, 0.5f, 8, sx * 0.3f, 0.82f, 3.4f);
                }
                // ---------------------------------------------------------------- front fenders (swept)
                foreach (float sx in new[] { -1f, 1f })
                {
                    var outline = new List<V2>();
                    // arch over the wheel with a flange to the bumper and a sweep down to the running board
                    outline.Add(new V2(3.0f, 0.86f));
                    outline.AddRange(MeshBuilder.Arc(FrontAxleZ, WheelRadius, 0.62f, 18, 162, 12));
                    outline.Add(new V2(0.95f, 0.68f));
                    outline.Add(new V2(0.75f, 0.6f));
                    outline.Add(new V2(0.75f, 0.52f));
                    outline.Add(new V2(1.25f, 0.56f));
                    outline.AddRange(MeshBuilder.Arc(FrontAxleZ, WheelRadius, 0.52f, 160, 20, 12));
                    outline.Add(new V2(3.0f, 0.66f));
                    float x0 = sx < 0 ? -1.08f : 0.72f, x1 = sx < 0 ? -0.72f : 1.08f;
                    b.ExtrudeX(fenderKey, lowerFender, outline, x0, x1);
                    // ornamental headlights on the fender tops: chrome bucket + lens
                    b.Lathe("chrome", C, new[] { new V2(0.0f, -0.12f), new V2(0.14f, -0.1f), new V2(0.19f, 0f), new V2(0.2f, 0.06f) }, 14, sx * 0.86f, 1.22f, 2.75f, new Rot((float)(Math.PI / 2), 0, 0));
                    b.Cylinder("glow", "#FFF6D0", 0.17f, 0.17f, 0.04f, 14, sx * 0.86f, 1.22f, 2.82f, new Rot((float)(Math.PI / 2), 0, 0));
                    // small amber + white lamps along the fender
                    for (int i = 0; i < 2; i++)
                    {
                        b.Cylinder("chrome", C, 0.08f, 0.08f, 0.06f, 10, sx * 0.9f, 1.06f + i * 0.0f, 2.3f - i * 0.3f, new Rot((float)(Math.PI / 2), 0, 0));
                        b.Cylinder("glow", i == 0 ? "#FFB020" : "#FFFFFF", 0.06f, 0.06f, 0.05f, 10, sx * 0.9f, 1.06f, 2.34f - i * 0.3f, new Rot((float)(Math.PI / 2), 0, 0));
                    }
                    // fog lamps on the bumper
                    b.Cylinder("glow", "#FFE08A", 0.07f, 0.07f, 0.05f, 10, sx * 0.62f, 0.52f, 3.42f, new Rot((float)(Math.PI / 2), 0, 0));
                    // side mirror on a stalk
                    b.Cylinder("chrome", C, 0.015f, 0.015f, 0.5f, 6, sx * 1.12f, 1.72f, 1.18f, new Rot(0, 0, -sx * 0.9f));
                    b.Box("chrome", C, 0.06f, 0.26f, 0.18f, sx * 1.3f, 1.86f, 1.18f);
                    b.Box("glass", "#6E8FB5", 0.02f, 0.22f, 0.14f, sx * 1.3f, 1.86f, 1.12f);
                    // whip antenna with streamer / beaded antenna (San Juan style)
                    b.Cylinder("chrome", C, 0.012f, 0.015f, 1.5f, 5, sx * 1.02f, 1.2f, 3.3f);
                    if (L.BeadedAntennas)
                        for (int k = 0; k < 9; k++) b.Sphere("paint", "#7A0F1A", 0.045f, sx * 1.02f, 0.75f + k * 0.12f, 3.3f, 6);
                    else
                        for (int k = 0; k < 3; k++) b.Box("paint", k == 0 ? L.Accent1 : k == 1 ? L.Body : L.Hood, 0.03f, 0.22f, 0.04f, sx * 1.02f + (k - 1) * 0.03f, 1.95f, 3.3f, new Rot(0, 0, (k - 1) * 0.3f));
                }
                // signature horse mascots on the hood
                Horse(b, -0.4f, 1.2f, 2.86f, 1.2f, C);
                Horse(b, 0.4f, 1.2f, 2.86f, 1.2f, C);
                Horse(b, 0f, 1.28f, 2.25f, 0.9f, C);

                // ---------------------------------------------------------------- cab
                b.Box("paint", paint, 2.0f, BeltY - SillY, 0.55f, 0, (BeltY + SillY) / 2, (BodyFrontZ + 1.2f) / 2);
                // dashboard + steering wheel visible through the windshield
                b.Box("paint", "#2B2B30", 1.8f, 0.2f, 0.3f, 0, BeltY + 0.05f, 1.05f);
                b.Lathe("paint", "#111111", new[] { new V2(0.17f, -0.02f), new V2(0.2f, 0f), new V2(0.17f, 0.02f) }, 12, -0.45f, 1.62f, 0.85f, new Rot(1.1f, 0, 0), false);
                // windshield (split) with chrome frame
                float wsBottom = BeltY, wsTop = RoofY - 0.04f;
                b.Box("glass", Glass, 1.84f, wsTop - wsBottom, 0.04f, 0, (wsTop + wsBottom) / 2, 1.18f, new Rot(-0.08f, 0, 0));
                b.Box("chrome", C, 0.06f, wsTop - wsBottom, 0.08f, 0, (wsTop + wsBottom) / 2, 1.2f, new Rot(-0.08f, 0, 0));
                foreach (float sx in new[] { -1f, 1f })
                    b.Box("chrome", C, 0.08f, wsTop - wsBottom + 0.05f, 0.1f, sx * 0.96f, (wsTop + wsBottom) / 2, 1.2f, new Rot(-0.08f, 0, 0));

            }
            else ModernFront(b, L);

            // ---------------------------------------------------------------- passenger body
            float bodyLen = BodyFrontZ - BodyRearZ, bodyMidZ = (BodyFrontZ + BodyRearZ) / 2;
            // hollow cabin (no solid core) so seated passengers are visible through the
            // open windows and the rear doorway: floor, inner walls, ceiling, partition, lamps
            b.Box("paint", "#3A2E36", 1.9f, 0.06f, bodyLen - 0.05f, 0, SillY + 0.03f, bodyMidZ);
            foreach (float sx in new[] { -1f, 1f })
                b.Box("paint", Interior, 0.04f, BeltY - SillY, bodyLen - 0.05f, sx * 0.955f, (BeltY + SillY) / 2, bodyMidZ);
            b.Box("paint", "#E8E1D0", 1.9f, 0.04f, bodyLen - 0.05f, 0, RoofY - 0.02f, bodyMidZ);
            b.Box("paint", Interior, 1.9f, RoofY - SillY, 0.05f, 0, (RoofY + SillY) / 2, BodyFrontZ - 0.03f);
            for (int i = 0; i < 3; i++) b.Box("glow", "#FFF1C1", 0.5f, 0.03f, 0.12f, 0, RoofY - 0.05f, BodyFrontZ - 0.6f - i * 1.2f);
            // overhead grab rails over each bench
            foreach (float sx in new[] { -1f, 1f }) b.CylinderZ("chrome", C, 0.02f, bodyLen - 0.3f, 6, sx * 0.55f, RoofY - 0.18f, bodyMidZ);
            // painted art panels (textured) on both sides of the lower body
            float pz0 = BodyRearZ, pz1 = BodyFrontZ;
            var side = "tex:" + L.SideTexture;
            // left side, seen from -X: front is on the viewer's left
            b.Quad(side, new V3(-1.0f, SillY, pz1), new V3(-1.0f, SillY, pz0), new V3(-1.0f, BeltY, pz0), new V3(-1.0f, BeltY, pz1), new V3(-1, 0, 0),
                new V2(0, 0), new V2(1, 0), new V2(1, 1), new V2(0, 1));
            // right side, seen from +X: front is on the viewer's right
            b.Quad(side, new V3(1.0f, SillY, pz0), new V3(1.0f, SillY, pz1), new V3(1.0f, BeltY, pz1), new V3(1.0f, BeltY, pz0), new V3(1, 0, 0),
                new V2(0, 0), new V2(1, 0), new V2(1, 1), new V2(0, 1));
            // rear panel art (includes plate, slogan and the doorway look)
            // rear panel: left + right panels and a header, leaving the doorway OPEN so you can
            // see the passengers inside. Texture layout: doorway spans u 0.293-0.707, v 0-0.808.
            var rear = "tex:" + L.RearTexture;
            const float du0 = 0.293f, du1 = 0.707f, dv = 0.808f;
            float dx = 1.0f - du0 * 2f;                       // doorway half-width in metres (0.414)
            float dy = SillY + dv * (RoofY - SillY);          // top of the doorway
            void RearQuad(float x0, float x1, float y0, float y1)
            {
                // seen from behind, +X is on the viewer's right: u = (x + 1) / 2
                float u0 = (x0 + 1f) / 2f, u1 = (x1 + 1f) / 2f;
                float v0 = (y0 - SillY) / (RoofY - SillY), v1 = (y1 - SillY) / (RoofY - SillY);
                b.Quad(rear, new V3(x1, y0, BodyRearZ), new V3(x0, y0, BodyRearZ), new V3(x0, y1, BodyRearZ), new V3(x1, y1, BodyRearZ), new V3(0, 0, -1),
                    new V2(u1, v0), new V2(u0, v0), new V2(u0, v1), new V2(u1, v1));
            }
            RearQuad(-1.0f, -dx, SillY, RoofY);
            RearQuad(dx, 1.0f, SillY, RoofY);
            RearQuad(-dx, dx, dy, RoofY);
            // doorway frame
            foreach (float sx in new[] { -1f, 1f }) b.Box("paint", L.Trim, 0.05f, dy - SillY, 0.08f, sx * dx, (dy + SillY) / 2, BodyRearZ - 0.01f);
            b.Box("paint", L.Trim, dx * 2 + 0.05f, 0.05f, 0.08f, 0, dy, BodyRearZ - 0.01f);
            // belt-line chrome trims
            foreach (float sx in new[] { -1f, 1f })
            {
                b.Box("chrome", C, 0.05f, 0.06f, bodyLen + 0.55f, sx * 1.02f, BeltY + 0.02f, bodyMidZ + 0.27f);
                b.Box("chrome", C, 0.05f, 0.08f, bodyLen + 0.55f, sx * 1.02f, SillY + 0.03f, bodyMidZ + 0.27f);
                if (L.ChromeLowerBody) b.Box("chrome", C, 0.04f, 0.22f, bodyLen - 0.2f, sx * 1.015f, SillY + 0.16f, bodyMidZ);
                // window pillars and grab rail
                for (int i = 0; i <= 6; i++)
                    b.Box("paint", trim, 0.08f, RoofY - BeltY, 0.08f, sx * 0.98f, (RoofY + BeltY) / 2, BodyFrontZ - 0.05f - i * (bodyLen - 0.15f) / 6);
                b.CylinderZ("chrome", C, 0.022f, bodyLen - 0.2f, 6, sx * 1.0f, BeltY + 0.32f, bodyMidZ);
                // running board between the wheels
                b.Box("chrome", C, 0.32f, 0.05f, FrontAxleZ - RearAxleZ - 1.15f, sx * 1.02f, 0.5f, (FrontAxleZ + RearAxleZ) / 2 - 0.05f);
                // sweeping chrome rear fender skirt
                var rf = new List<V2>();
                rf.Add(new V2(RearAxleZ + 0.9f, 0.58f));
                rf.AddRange(MeshBuilder.Arc(RearAxleZ, WheelRadius, 0.64f, 25, 155, 12));
                rf.Add(new V2(BodyRearZ + 0.02f, 0.8f));
                rf.Add(new V2(BodyRearZ + 0.02f, 0.52f));
                rf.Add(new V2(RearAxleZ - 0.62f, 0.52f));
                rf.AddRange(MeshBuilder.Arc(RearAxleZ, WheelRadius, 0.52f, 160, 20, 12));
                float rx0 = sx < 0 ? -1.12f : 0.98f, rx1 = sx < 0 ? -0.98f : 1.12f;
                b.ExtrudeX(fenderKey, lowerFender, rf, rx0, rx1);
                // mud flaps
                b.Box("paint", L.Mudflap, 0.34f, 0.34f, 0.04f, sx * 0.93f, 0.36f, RearAxleZ - 0.62f);
                b.Box("paint", L.Mudflap, 0.34f, 0.3f, 0.04f, sx * 0.93f, 0.36f, FrontAxleZ - 0.66f);
                // tail lights
                b.Box("glow", "#FF2020", 0.1f, 0.45f, 0.05f, sx * 0.92f, 1.2f, BodyRearZ - 0.02f);
                b.Box("glow", "#FFB000", 0.1f, 0.14f, 0.05f, sx * 0.92f, 0.86f, BodyRearZ - 0.02f);
            }
            // benches inside
            foreach (float sx in new[] { -1f, 1f })
                b.Box("paint", Seat, 0.42f, 0.12f, bodyLen - 0.4f, sx * 0.7f, 0.98f, bodyMidZ);
            // rear step, bumper & grab bars
            b.Box("chrome", C, 1.0f, 0.06f, 0.4f, 0, 0.55f, BodyRearZ - 0.18f);
            b.Box("chrome", C, 2.05f, 0.14f, 0.12f, 0, 0.58f, BodyRearZ - 0.08f);
            foreach (float sx in new[] { -1f, 1f }) b.Cylinder("chrome", C, 0.025f, 0.025f, 1.2f, 6, sx * 0.48f, 1.3f, BodyRearZ - 0.05f);
            // chrome ladder on the rear right corner up to the roof rack
            for (int r = 0; r < 2; r++) b.Cylinder("chrome", C, 0.022f, 0.022f, RoofY - 0.6f, 6, 0.7f + r * 0.26f, (RoofY + 0.6f) / 2 + 0.05f, BodyRearZ - 0.08f);
            for (int k = 0; k < 6; k++) b.CylinderX("chrome", C, 0.018f, 0.28f, 6, 0.83f, 0.8f + k * 0.25f, BodyRearZ - 0.08f);

            // ---------------------------------------------------------------- roof
            b.Box("paint", roof, 2.16f, 0.12f, bodyLen + 0.75f, 0, RoofY + 0.06f, bodyMidZ + 0.3f);
            b.Box("paint", L.Accent2, 2.18f, 0.04f, bodyLen + 0.77f, 0, RoofY + 0.005f, bodyMidZ + 0.3f);
            if (!modern)
            {
                // front visor sweeping up over the windshield
                var visor = new List<V2> { new V2(1.0f, RoofY), new V2(1.62f, RoofY + 0.04f), new V2(1.7f, RoofY + 0.16f), new V2(1.0f, RoofY + 0.12f) };
                b.ExtrudeX("paint", trim, visor, -1.06f, 1.06f);
                // roof sign: angled board with the route / name, chrome frame + supports
                var sign = "tex:" + L.SignTexture;
                float sy0 = RoofY + 0.16f, sy1 = RoofY + 0.6f, sz0 = 1.44f, sz1 = 1.36f;
                b.Quad(sign, new V3(-0.95f, sy0, sz0 + 0.01f), new V3(0.95f, sy0, sz0 + 0.01f), new V3(0.95f, sy1, sz1 + 0.01f), new V3(-0.95f, sy1, sz1 + 0.01f), new V3(0, 0.2f, 1),
                    new V2(1, 0), new V2(0, 0), new V2(0, 1), new V2(1, 1)); // seen from the front, -X is on the viewer's right
                b.Box("chrome", C, 2.0f, 0.5f, 0.05f, 0, (sy0 + sy1) / 2, (sz0 + sz1) / 2 - 0.03f, new Rot(-(float)Math.Atan2(sz0 - sz1, sy1 - sy0), 0, 0));
                foreach (float sx in new[] { -0.8f, 0.8f }) b.Box("chrome", C, 0.05f, 0.2f, 0.05f, sx, RoofY + 0.12f, 1.38f);
            }
            // roof horns and a row of coloured marker lights
            foreach (float sx in new[] { -0.5f, 0.5f })
                b.Lathe("chrome", C, new[] { new V2(0.03f, 0), new V2(0.04f, 0.3f), new V2(0.11f, 0.5f) }, 10, sx, RoofY + 0.22f, 0.95f, new Rot((float)(Math.PI / 2), 0, 0));
            string[] markers = { "#FF3B3B", "#FFD23F", "#3BCEAC", "#4D9DE0", "#FF8C42", "#FFFFFF", "#E15FED" };
            for (int i = 0; i < markers.Length; i++) b.Sphere("glow", markers[i], 0.05f, -0.75f + i * 0.25f, RoofY + 0.15f, 1.66f, 6);
            // luggage rack
            string rack = L.ChromeLowerBody ? L.Accent1 : C;
            foreach (float sx in new[] { -0.95f, 0.95f }) b.Box(L.ChromeLowerBody ? "paint" : "chrome", rack, 0.06f, 0.06f, 1.8f, sx, RoofY + 0.32f, -1.9f);
            for (int i = 0; i < 4; i++) b.Box(L.ChromeLowerBody ? "paint" : "chrome", rack, 1.96f, 0.05f, 0.05f, 0, RoofY + 0.32f, -1.05f - i * 0.57f);
            foreach (float sx in new[] { -0.95f, 0.95f }) foreach (float z in new[] { -1.05f, -2.75f }) b.Box("chrome", C, 0.04f, 0.22f, 0.04f, sx, RoofY + 0.2f, z);

            // ---------------------------------------------------------------- spare tyre on the side (classic)
            if (L.SpareTire && !modern)
            {
                b.Push(new V3(-1.2f, 1.05f, 0.42f), new Rot(0, 0, (float)(Math.PI / 2)));
                TireShape(b, 0.4f, 0.26f);
                b.Pop();
            }
            if (L.Neon) NeonLights(b, L);
            if (L.Fiesta) Banderitas(b, L);
            if (L.SoundSystem) PatokKit(b, L);
            return b.Data;
        }

        internal static void Horse(MeshBuilder b, float x, float y, float z, float s, string chrome)
        {
            b.Push(new V3(x, y, z), Rot.None, s);
            b.Box("chrome", chrome, 0.16f, 0.03f, 0.3f, 0, 0.015f, 0);        // plinth
            b.Box("chrome", chrome, 0.09f, 0.11f, 0.24f, 0, 0.2f, 0);           // body
            b.Box("chrome", chrome, 0.07f, 0.2f, 0.08f, 0, 0.32f, 0.11f, new Rot(-0.5f, 0, 0)); // neck
            b.Box("chrome", chrome, 0.07f, 0.08f, 0.15f, 0, 0.42f, 0.2f, new Rot(-0.2f, 0, 0));  // head
            b.Box("chrome", chrome, 0.03f, 0.07f, 0.03f, 0, 0.48f, 0.15f);       // ear
            foreach (float lx in new[] { -0.03f, 0.03f })
            {
                b.Box("chrome", chrome, 0.03f, 0.15f, 0.03f, lx, 0.24f, 0.17f, new Rot(0.9f, 0, 0)); // rearing front legs
                b.Box("chrome", chrome, 0.03f, 0.16f, 0.03f, lx, 0.09f, -0.09f);                     // hind legs
            }
            b.Box("chrome", chrome, 0.03f, 0.14f, 0.03f, 0, 0.18f, -0.15f, new Rot(-0.6f, 0, 0)); // tail
            b.Pop();
        }

        /// <summary>Boxy modern PUV front: flat nose, big windshield, LED lights and route display.</summary>
        private static void ModernFront(MeshBuilder b, JeepneyLivery L)
        {
            float front = 2.95f;
            float cabLen = front - BodyFrontZ;
            float cabMid = (front + BodyFrontZ) / 2f;
            b.Box("paint", L.Body, 2.04f, BeltY - SillY, cabLen, 0, (BeltY + SillY) / 2, cabMid);
            b.Box("paint", L.Accent1, 2.06f, 0.14f, cabLen, 0, SillY + 0.25f, cabMid);
            b.Box("glass", Glass, 1.96f, RoofY - BeltY, cabLen - 0.25f, 0, (RoofY + BeltY) / 2, cabMid - 0.1f);
            foreach (float sx in new[] { -1f, 1f }) b.Box("paint", L.Body, 0.08f, RoofY - BeltY, 0.14f, sx * 0.99f, (RoofY + BeltY) / 2, front - 0.1f);
            b.Box("glass", "#22344A", 1.86f, RoofY - BeltY - 0.08f, 0.06f, 0, (RoofY + BeltY) / 2, front - 0.02f, new Rot(-0.12f, 0, 0));
            b.Box("paint", L.Body, 2.1f, 0.14f, cabLen + 0.1f, 0, RoofY + 0.06f, cabMid + 0.05f);
            // LED route display above the windshield
            var sign = "tex:" + L.SignTexture;
            b.Box("paint", "#111111", 1.7f, 0.3f, 0.06f, 0, RoofY - 0.12f, front + 0.02f);
            b.Quad(sign, new V3(0.8f, RoofY - 0.25f, front + 0.06f), new V3(-0.8f, RoofY - 0.25f, front + 0.06f), new V3(-0.8f, RoofY + 0.01f, front + 0.06f), new V3(0.8f, RoofY + 0.01f, front + 0.06f), new V3(0, 0, 1),
                new V2(0, 0), new V2(1, 0), new V2(1, 1), new V2(0, 1)); // seen from the front, -X is on the viewer's right
            // nose: LED headlight strips, black bumper, grille slot
            b.Box("paint", L.Accent1, 2.0f, 0.22f, 0.08f, 0, 1.15f, front + 0.02f);
            foreach (float sx in new[] { -1f, 1f })
            {
                b.Box("glow", "#F4FBFF", 0.5f, 0.1f, 0.04f, sx * 0.68f, 1.0f, front + 0.05f);
                b.Box("glow", "#FFB000", 0.14f, 0.1f, 0.04f, sx * 0.92f, 1.0f, front + 0.05f);
                b.Cylinder("chrome", L.Chrome, 0.015f, 0.015f, 0.45f, 6, sx * 1.12f, 1.78f, front - 0.25f, new Rot(0, 0, -sx * 0.9f));
                b.Box("paint", "#111111", 0.08f, 0.3f, 0.2f, sx * 1.3f, 1.92f, front - 0.25f);
                // wheel arches
                b.Box("paint", "#1A1A1A", 0.08f, 0.2f, 1.1f, sx * 1.03f, 0.95f, FrontAxleZ);
            }
            b.Box("paint", "#2A2A2E", 1.2f, 0.2f, 0.05f, 0, 0.78f, front + 0.04f);
            b.Box("paint", "#1A1A1A", 2.14f, 0.24f, 0.3f, 0, 0.5f, front + 0.12f);
        }

        /// <summary>Neon Street Racer: glowing underbody, belt-line and roof edge strips.</summary>
        private static void NeonLights(MeshBuilder b, JeepneyLivery L)
        {
            foreach (float sx in new[] { -1f, 1f })
            {
                b.Box("glow", L.Accent1, 0.04f, 0.05f, 5.6f, sx * 1.04f, 0.42f, 0f);
                b.Box("glow", L.Accent2, 0.03f, 0.04f, BodyFrontZ - BodyRearZ, sx * 1.05f, BeltY - 0.05f, (BodyFrontZ + BodyRearZ) / 2);
                b.Box("glow", L.Accent1, 0.03f, 0.04f, 4.6f, sx * 1.09f, RoofY + 0.12f, -0.4f);
            }
            b.Box("glow", L.Accent2, 1.9f, 0.04f, 0.03f, 0, 0.42f, BodyRearZ - 0.05f);
        }

        /// <summary>
        /// Patok kit (Cogeo–Antipolo style): two big speaker boxes on the roof rack, a fog-light
        /// bar over the windshield, LED strips along the body and roof, LED underglow, tinted
        /// rear window strip and extra chrome mirrors. Disco lights inside are added at runtime.
        /// </summary>
        private static void PatokKit(MeshBuilder b, JeepneyLivery L)
        {
            // roof speakers facing backwards and sideways (the "sound trip")
            foreach (float sx in new[] { -0.55f, 0.55f })
            {
                b.Box("paint", "#111114", 0.7f, 0.46f, 0.55f, sx, RoofY + 0.6f, -2.35f);
                b.Box("chrome", L.Chrome, 0.74f, 0.04f, 0.59f, sx, RoofY + 0.85f, -2.35f);
                // woofer cone on the back face + tweeter
                b.Push(new V3(sx, RoofY + 0.6f, -2.63f), new Rot((float)(Math.PI / 2), 0, 0));
                b.Cylinder("paint", "#2A2A30", 0.19f, 0.19f, 0.03f, 14, 0, 0, 0);
                b.Cylinder("chrome", L.Chrome, 0.07f, 0.07f, 0.05f, 10, 0, 0.01f, 0);
                b.Pop();
                b.Box("glow", L.Accent2, 0.6f, 0.03f, 0.03f, sx, RoofY + 0.36f, -2.63f);
            }
            // fog-light bar above the windshield
            b.Box("chrome", L.Chrome, 1.7f, 0.06f, 0.06f, 0, RoofY + 0.12f, 1.75f);
            for (int i = 0; i < 4; i++)
            {
                float x = -0.6f + i * 0.4f;
                b.Push(new V3(x, RoofY + 0.24f, 1.78f), new Rot((float)(Math.PI / 2), 0, 0));
                b.Cylinder("chrome", L.Chrome, 0.11f, 0.11f, 0.08f, 12, 0, 0, 0);
                b.Pop();
                b.Sphere("glow", "#FFF6D0", 0.085f, x, RoofY + 0.24f, 1.83f, 8);
            }
            // LED strips: belt line, roof edge, wheel arches, underglow (two colours)
            foreach (float sx in new[] { -1f, 1f })
            {
                b.Box("glow", L.Accent1, 0.03f, 0.04f, BodyFrontZ - BodyRearZ, sx * 1.05f, BeltY - 0.04f, (BodyFrontZ + BodyRearZ) / 2);
                b.Box("glow", L.Accent2, 0.03f, 0.035f, BodyFrontZ - BodyRearZ, sx * 1.05f, SillY + 0.1f, (BodyFrontZ + BodyRearZ) / 2);
                b.Box("glow", L.Accent1, 0.03f, 0.04f, 4.6f, sx * 1.09f, RoofY + 0.12f, -0.4f);
                b.Box("glow", L.Accent2, 0.04f, 0.05f, 5.6f, sx * 1.0f, 0.36f, 0f);
                // extra chrome mirrors on stalks
                b.Cylinder("chrome", L.Chrome, 0.014f, 0.014f, 0.5f, 6, sx * 1.18f, 1.7f, 0.95f, new Rot(0, 0, -sx * 0.8f));
                b.Box("chrome", L.Chrome, 0.06f, 0.24f, 0.16f, sx * 1.36f, 1.88f, 0.95f);
            }
            b.Box("glow", L.Accent1, 1.9f, 0.04f, 0.03f, 0, 0.4f, BodyRearZ - 0.05f);
            b.Box("glow", L.Accent2, 1.9f, 0.04f, 0.03f, 0, RoofY + 0.05f, BodyRearZ - 0.04f);
            // pull cord along the ceiling to the driver's bell: the music is too loud to hear "PARA!"
            foreach (float cx in new[] { -0.5f, 0.5f })
                b.Box("paint", "#E9E4D4", 0.025f, 0.025f, BodyFrontZ - BodyRearZ - 0.3f, cx, RoofY - 0.16f, (BodyFrontZ + BodyRearZ) / 2);
            b.Box("paint", "#E9E4D4", 1.0f, 0.025f, 0.025f, 0, RoofY - 0.16f, BodyFrontZ - 0.15f);
            b.Sphere("gold", "#FFC93C", 0.09f, -0.35f, RoofY - 0.24f, BodyFrontZ - 0.1f, 8);   // the bell by the driver
            b.Box("gold", "#FFC93C", 0.03f, 0.08f, 0.03f, -0.35f, RoofY - 0.12f, BodyFrontZ - 0.1f);
            // "HILA KUNG BABABA" sign hanging from the ceiling, facing the rear doorway (riders and the chase camera)
            float sz = -1.6f, sy = RoofY - 0.36f, hw = 0.7f, hh = 0.175f;
            b.Box("paint", "#111114", hw * 2 + 0.06f, hh * 2 + 0.06f, 0.03f, 0, sy, sz + 0.03f);
            b.Quad("utex:hila_sign", new V3(-hw, sy - hh, sz), new V3(hw, sy - hh, sz), new V3(hw, sy + hh, sz), new V3(-hw, sy + hh, sz), new V3(0, 0, -1),
                new V2(0, 0), new V2(1, 0), new V2(1, 1), new V2(0, 1));
            foreach (float hx in new[] { -0.6f, 0.6f }) b.Box("paint", "#333333", 0.015f, RoofY - (sy + hh), 0.015f, hx, (RoofY + sy + hh) / 2, sz + 0.03f);
            // and over the top of the rear doorway, under the rear slogan, where boarding riders see it
            float rz = BodyRearZ - 0.07f, ry = 1.7f;
            b.Quad("utex:hila_sign", new V3(-0.62f, ry - 0.155f, rz), new V3(0.62f, ry - 0.155f, rz), new V3(0.62f, ry + 0.155f, rz), new V3(-0.62f, ry + 0.155f, rz), new V3(0, 0, -1),
                new V2(0, 0), new V2(1, 0), new V2(1, 1), new V2(0, 1));
        }

        /// <summary>Fiesta Edition: strings of colourful banderitas over the roof.</summary>
        private static void Banderitas(MeshBuilder b, JeepneyLivery L)
        {
            string[] cols = { "#EF476F", "#FFD166", "#06D6A0", "#118AB2", "#F78C6B", "#FFFFFF" };
            foreach (float x in new[] { -0.9f, 0f, 0.9f })
            {
                float y = RoofY + (x == 0f ? 0.75f : 0.55f);
                b.Box("paint", "#333333", 0.015f, 0.015f, 4.8f, x, y, -0.6f);
                for (int i = 0; i < 16; i++)
                {
                    float z = 1.7f - i * 0.3f;
                    b.Box("paint", cols[i % cols.Length], 0.01f, 0.16f, 0.16f, x, y - 0.09f, z, new Rot((float)(Math.PI / 4), 0, 0));
                }
            }
            foreach (float z in new[] { 1.9f, -3.0f }) foreach (float x in new[] { -0.9f, 0f, 0.9f })
                b.Cylinder("chrome", L.Chrome, 0.015f, 0.015f, x == 0f ? 0.75f : 0.55f, 5, x, RoofY + (x == 0f ? 0.375f : 0.275f), z);
        }

        /// <summary>Tyre with rounded shoulders + chrome hubcap, axis along local Y.</summary>
        private static void TireShape(MeshBuilder b, float r, float w, string chrome = Chrome)
        {
            float h = w / 2;
            var tire = new[]
            {
                new V2(r * 0.62f, -h), new V2(r * 0.9f, -h), new V2(r * 0.98f, -h * 0.8f), new V2(r, -h * 0.4f),
                new V2(r, h * 0.4f), new V2(r * 0.98f, h * 0.8f), new V2(r * 0.9f, h), new V2(r * 0.62f, h),
            };
            b.Lathe("paint", Tire, tire, 16, 0, 0, 0);
            var hub = new[] { new V2(r * 0.64f, h - 0.005f), new V2(r * 0.55f, h + 0.02f), new V2(r * 0.3f, h + 0.05f), new V2(0.0f, h + 0.07f) };
            b.Lathe("chrome", chrome, hub, 16, 0, 0, 0);
            var hub2 = new[] { new V2(0.0f, -h - 0.07f), new V2(r * 0.3f, -h - 0.05f), new V2(r * 0.55f, -h - 0.02f), new V2(r * 0.64f, -h + 0.005f) };
            b.Lathe("chrome", chrome, hub2, 16, 0, 0, 0);
            b.Cylinder("paint", "#2A2A30", r * 0.63f, r * 0.63f, w * 0.96f, 14, 0, 0, 0);
        }

        /// <summary>One wheel centred at the origin, axis along X (rotate around X to spin).</summary>
        public static MeshData BuildWheel(Palette palette = null, string chrome = Chrome)
        {
            var b = new MeshBuilder(palette);
            b.Push(V3.Zero, new Rot(0, 0, (float)(Math.PI / 2)));
            TireShape(b, WheelRadius, 0.32f, chrome);
            b.Pop();
            // a couple of lug bars so the spin is visible
            b.Box("chrome", chrome, 0.36f, 0.05f, 0.05f, 0, 0, 0);
            return b.Data;
        }
    }
}
