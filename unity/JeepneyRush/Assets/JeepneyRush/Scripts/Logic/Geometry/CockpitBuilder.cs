using System;

namespace JeepneyRush.Logic.Geometry
{
    /// <summary>Where things sit in the driver's-seat view for one jeepney style (body coordinates, +Z forward).</summary>
    public struct CockpitLayout
    {
        public V3 Eye, WheelPos, GaugePos, MirrorPos;
        /// <summary>Z of the windshield.</summary>
        public float Windshield;
        public bool Modern;
    }

    /// <summary>
    /// The driver's-seat view. Instead of looking out from inside the full jeepney model (walls, signs and
    /// roof boxes everywhere), the view uses its own clean, open cab shell: the hood with its chrome horses
    /// (classic) or the short nose (modern), thin windshield pillars, a slim roof edge and a low dashboard,
    /// so the road reads clearly and wide. Moving parts are built separately (wheel, gauge needle, rosary).
    /// </summary>
    public static class CockpitBuilder
    {
        /// <summary>Camera pitch (degrees down) and the vertical field-of-view cap for tall phone screens.</summary>
        public const float PitchDeg = 11f, MaxVerticalFov = 108f;
        public const float WheelTiltDeg = 30f;
        public const float GaugeSize = 0.12f;
        public const float GaugeTiltDeg = 18f;
        /// <summary>The modern PUV's windshield is this much further forward than the classic one.</summary>
        public const float ModernShift = 1.73f;

        public static CockpitLayout Layout(JeepneyLivery L)
        {
            bool modern = L != null && L.Style == JeepneyStyle.Modern;
            float dz = modern ? ModernShift : 0f;
            return new CockpitLayout
            {
                Modern = modern,
                Windshield = 1.2f + dz,
                Eye = new V3(-0.45f, 1.93f, 0.72f + dz),
                WheelPos = new V3(-0.5f, 1.5f, 0.9f + dz),
                GaugePos = new V3(-0.34f, 1.69f, 1.0f + dz),
                MirrorPos = new V3(-0.36f, 2.02f, 1.14f + dz),
            };
        }

        // kept for older callers/tests: the classic layout
        public static V3 Eye => Layout(null).Eye;
        public static V3 WheelPos => Layout(null).WheelPos;
        public static V3 GaugePos => Layout(null).GaugePos;
        public static V3 MirrorPos => Layout(null).MirrorPos;

        /// <summary>The open cab shell seen from the driver's seat, in the jeepney's own colours.</summary>
        public static MeshData Shell(JeepneyLivery L)
        {
            var b = new MeshBuilder();
            var lay = Layout(L);
            string C = L.Chrome;
            float ws = lay.Windshield, belt = JeepneyBuilder.BeltY, roof = JeepneyBuilder.RoofY;
            if (!lay.Modern)
            {
                // long hood with chrome centre strip, fender tops, ornamental headlights and the horses
                var hoodProfile = new System.Collections.Generic.List<V2> { new V2(1.2f, 0.62f), new V2(3.05f, 0.62f), new V2(3.05f, 1.12f), new V2(2.95f, 1.2f), new V2(1.2f, 1.34f) };
                b.ExtrudeX("paint", L.Hood, hoodProfile, -0.74f, 0.74f);
                b.Box("chrome", C, 0.08f, 0.04f, 1.8f, 0, 1.3f, 2.12f, new Rot(-0.075f, 0, 0));
                foreach (float sx in new[] { -1f, 1f })
                {
                    b.Box(L.ChromeLowerBody ? "chrome" : "paint", L.ChromeLowerBody ? C : L.Body, 0.36f, 0.08f, 1.8f, sx * 0.9f, 1.0f, 2.1f);
                    b.Lathe("chrome", C, new[] { new V2(0.0f, -0.12f), new V2(0.14f, -0.1f), new V2(0.19f, 0f), new V2(0.2f, 0.06f) }, 14, sx * 0.86f, 1.22f, 2.75f, new Rot((float)(Math.PI / 2), 0, 0));
                    b.Cylinder("glow", "#FFF6D0", 0.17f, 0.17f, 0.04f, 14, sx * 0.86f, 1.22f, 2.82f, new Rot((float)(Math.PI / 2), 0, 0));
                }
                JeepneyBuilder.Horse(b, -0.4f, 1.2f, 2.86f, 1.2f, C);
                JeepneyBuilder.Horse(b, 0.4f, 1.2f, 2.86f, 1.2f, C);
                JeepneyBuilder.Horse(b, 0f, 1.28f, 2.25f, 0.9f, C);
                // cowl between the hood and the windshield, in the body colour
                b.Box("paint", L.Body, 2.0f, 0.12f, 0.2f, 0, belt - 0.02f, ws + 0.02f);
            }
            else
            {
                // modern PUV: short flat nose just below the big windshield
                b.Box("paint", L.Body, 2.04f, 0.16f, 0.35f, 0, belt - 0.05f, ws + 0.12f);
                b.Box("paint", L.Accent1, 2.06f, 0.06f, 0.36f, 0, belt - 0.13f, ws + 0.12f);
            }
            // thin windshield frame: two corner pillars, a centre post (classic split screen) and a slim header
            foreach (float sx in new[] { -1f, 1f }) b.Box(lay.Modern ? "paint" : "chrome", lay.Modern ? L.Body : C, 0.05f, roof - belt, 0.05f, sx * 0.97f, (roof + belt) / 2, ws);
            if (!lay.Modern) b.Box("chrome", C, 0.025f, roof - belt, 0.03f, 0, (roof + belt) / 2, ws);
            b.Box("paint", L.Roof, 2.04f, 0.05f, 0.5f, 0, roof + 0.05f, ws - 0.2f);          // roof (outside)
            b.Box("paint", "#E8E1D0", 1.98f, 0.02f, 0.48f, 0, roof + 0.015f, ws - 0.2f);     // light interior ceiling
            b.Box("paint", L.Trim, 2.04f, 0.035f, 0.04f, 0, roof - 0.02f, ws + 0.04f);
            // low dashboard with a chrome edge (keeps the road visible)
            b.Box("paint", "#2B2226", 1.84f, 0.12f, 0.24f, 0, 1.52f, ws - 0.14f);
            b.Box("chrome", C, 1.84f, 0.02f, 0.025f, 0, 1.585f, ws - 0.26f);
            return b.Data;
        }

        public static MeshData Extras(JeepneyLivery L)
        {
            var b = new MeshBuilder();
            var lay = Layout(L);
            float ws = lay.Windshield;
            // "GOD BLESS OUR TRIP" sign standing on the dash, facing the driver (-Z)
            float sx = -0.58f, sy = 1.62f, sz = ws - 0.06f, hw = 0.13f, hh = 0.024f;
            b.Quad("utex:dash_sign", new V3(sx - hw, sy - hh, sz), new V3(sx + hw, sy - hh, sz), new V3(sx + hw, sy + hh, sz), new V3(sx - hw, sy + hh, sz), new V3(0, 0, -1),
                new V2(0, 0), new V2(1, 0), new V2(1, 1), new V2(0, 1));
            // Santo Niño on the dash (red robe, gold crown)
            float nx = -0.12f, ny = 1.59f, nz = ws - 0.08f;
            b.Cylinder("paint", "#C8102E", 0.02f, 0.04f, 0.1f, 10, nx, ny + 0.05f, nz);
            b.Sphere("paint", "#C68642", 0.022f, nx, ny + 0.125f, nz, 8);
            b.Cylinder("gold", "#D4AF37", 0.02f, 0.016f, 0.024f, 8, nx, ny + 0.152f, nz);
            b.Box("gold", "#D4AF37", 0.08f, 0.01f, 0.016f, nx, ny + 0.08f, nz - 0.025f);
            // rear-view mirror: chrome stalk, dark housing with a chrome rim, slightly turned toward the driver
            var m = lay.MirrorPos;
            b.Box("chrome", L.Chrome, 0.016f, JeepneyBuilder.RoofY - m.Y + 0.02f, 0.016f, m.X, (JeepneyBuilder.RoofY + m.Y) / 2f + 0.03f, m.Z + 0.02f);
            b.Push(m, new Rot(0, -0.14f, 0));
            b.Box("chrome", "#C9CED6", 0.214f, 0.078f, 0.026f, 0, 0, 0.004f);
            b.Box("paint", "#151515", 0.204f, 0.068f, 0.03f, 0, 0, 0.012f);
            b.Quad("utex:mirror_view_0", new V3(-0.096f, -0.03f, -0.012f), new V3(0.096f, -0.03f, -0.012f), new V3(0.096f, 0.03f, -0.012f), new V3(-0.096f, 0.03f, -0.012f), new V3(0, 0, -1),
                new V2(0, 0), new V2(1, 0), new V2(1, 1), new V2(0, 1));
            b.Pop();
            // Patok: LED strip across the top of the windshield, inside
            if (L.SoundSystem)
                for (int i = 0; i < 12; i++)
                    b.Box("glow", i % 2 == 0 ? L.Accent1 : L.Accent2, 0.13f, 0.025f, 0.025f, -0.83f + i * 0.15f, JeepneyBuilder.RoofY - 0.06f, ws - 0.06f);
            return b.Data;
        }

        /// <summary>Big thin steering wheel, axis along +Z, centred at the origin (rotate around Z to steer).</summary>
        public static MeshData SteeringWheel()
        {
            var b = new MeshBuilder();
            b.Push(V3.Zero, new Rot((float)(Math.PI / 2), 0, 0)); // lathe axis Y -> Z
            var rim = new[] { new V2(0.205f, -0.018f), new V2(0.225f, -0.02f), new V2(0.24f, 0f), new V2(0.225f, 0.02f), new V2(0.205f, 0.018f), new V2(0.2f, 0f), new V2(0.205f, -0.018f) };
            b.Lathe("paint", "#151515", rim, 24, 0, 0, 0, default, true);
            b.Pop();
            foreach (float a in new[] { -0.5f, (float)Math.PI + 0.5f, (float)(Math.PI * 1.5) })
                b.Box("chrome", "#D9DDE3", 0.2f, 0.018f, 0.012f, (float)Math.Cos(a) * 0.1f, (float)Math.Sin(a) * 0.1f, 0, new Rot(0, 0, a));
            b.Cylinder("chrome", "#C9CED6", 0.05f, 0.05f, 0.04f, 14, 0, 0, -0.01f, new Rot((float)(Math.PI / 2), 0, 0));
            b.Cylinder("paint", "#C8102E", 0.022f, 0.022f, 0.01f, 10, 0, 0, -0.035f, new Rot((float)(Math.PI / 2), 0, 0)); // horn button
            return b.Data;
        }

        /// <summary>Speedometer dial facing the driver (−Z), centred at the origin.</summary>
        public static MeshData GaugeFace()
        {
            var b = new MeshBuilder();
            float h = GaugeSize / 2f;
            b.Quad("utexa:gauge_face", new V3(-h, -h, 0), new V3(h, -h, 0), new V3(h, h, 0), new V3(-h, h, 0), new V3(0, 0, -1),
                new V2(0, 0), new V2(1, 0), new V2(1, 1), new V2(0, 1));
            b.Push(new V3(0, 0, 0.012f), new Rot((float)(Math.PI / 2), 0, 0));
            b.Cylinder("paint", "#1A1A1A", h * 0.98f, h * 0.98f, 0.01f, 20, 0, 0, 0);
            b.Pop();
            return b.Data;
        }

        /// <summary>The needle, pointing up; rotate around Z (positive = counter-clockwise as the driver sees it).</summary>
        public static MeshData GaugeNeedle()
        {
            var b = new MeshBuilder();
            float h = GaugeSize / 2f;
            b.Quad("utexa:gauge_needle", new V3(-h, -h, -0.004f), new V3(h, -h, -0.004f), new V3(h, h, -0.004f), new V3(-h, h, -0.004f), new V3(0, 0, -1),
                new V2(0, 0), new V2(1, 0), new V2(1, 1), new V2(0, 1));
            return b.Data;
        }

        /// <summary>Rosary hanging from the mirror (pivot at the top): a loop of beads and a small cross.</summary>
        public static MeshData Rosary()
        {
            var b = new MeshBuilder();
            const int n = 18;
            for (int i = 0; i <= n; i++)
            {
                float t = i / (float)n;
                float x = (t < 0.5f ? -1f : 1f) * 0.025f * (float)Math.Sin(t * Math.PI) + (t - 0.5f) * 0.02f;
                float y = -0.16f * (float)Math.Sin(t * Math.PI);
                b.Sphere("paint", "#6B4226", 0.0042f, x * 0.7f, y * 0.7f, 0, 6);
            }
            b.Box("gold", "#D4AF37", 0.006f, 0.035f, 0.005f, 0, -0.135f, 0);
            b.Box("gold", "#D4AF37", 0.02f, 0.006f, 0.005f, 0, -0.128f, 0);
            return b.Data;
        }
    }
}
