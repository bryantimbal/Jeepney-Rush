using System;

namespace JeepneyRush.Logic.Geometry
{
    /// <summary>
    /// Driver's-seat view extras, in jeepney-body coordinates (+Z forward, the driver sits on the left):
    /// dashboard sign, Santo Niño, rear-view mirror showing the riders, a lit Patok LED strip, plus the
    /// moving parts built separately (steering wheel, speedometer face + needle, swinging rosary).
    /// </summary>
    public static class CockpitBuilder
    {
        /// <summary>Driver's eye in body coordinates.</summary>
        public static readonly V3 Eye = new V3(-0.45f, 1.9f, 0.7f); // just in front of the cabin partition (z 0.645)
        public static readonly V3 WheelPos = new V3(-0.5f, 1.52f, 0.86f); // just in front of the dash (dash front face at z 0.9)
        public const float WheelTiltDeg = 30f;
        /// <summary>Camera pitch (degrees down) and the vertical field-of-view cap for tall phone screens.</summary>
        public const float PitchDeg = 10f, MaxVerticalFov = 100f;
        public static readonly V3 GaugePos = new V3(-0.34f, 1.75f, 0.98f);   // pod on the dash, just right of the wheel (in view on tall phones)
        public const float GaugeTiltDeg = 18f;
        public const float GaugeSize = 0.12f;
        public static readonly V3 MirrorPos = new V3(-0.36f, 2.0f, 1.12f);

        public static MeshData Extras(JeepneyLivery L)
        {
            var b = new MeshBuilder();
            // dashboard top trim (chrome) and a padded dash shelf
            b.Box("paint", "#2B2226", 1.84f, 0.06f, 0.24f, 0, 1.62f, 1.06f);
            b.Box("chrome", L.Chrome, 1.84f, 0.025f, 0.03f, 0, 1.65f, 0.95f);
            // "GOD BLESS OUR TRIP" sign standing on the dash, facing the driver (-Z)
            float sx = -0.56f, sy = 1.68f, sz = 1.12f, hw = 0.13f, hh = 0.024f;
            b.Quad("utex:dash_sign", new V3(sx - hw, sy - hh, sz), new V3(sx + hw, sy - hh, sz), new V3(sx + hw, sy + hh, sz), new V3(sx - hw, sy + hh, sz), new V3(0, 0, -1),
                new V2(0, 0), new V2(1, 0), new V2(1, 1), new V2(0, 1));
            // Santo Niño on the dash (red robe, gold crown)
            float nx = -0.12f, ny = 1.65f, nz = 1.12f;
            b.Cylinder("paint", "#C8102E", 0.025f, 0.05f, 0.13f, 10, nx, ny + 0.065f, nz);
            b.Sphere("paint", "#C68642", 0.028f, nx, ny + 0.16f, nz, 8);
            b.Cylinder("gold", "#D4AF37", 0.026f, 0.02f, 0.03f, 8, nx, ny + 0.195f, nz);
            b.Box("gold", "#D4AF37", 0.1f, 0.012f, 0.02f, nx, ny + 0.1f, nz - 0.03f);
            // rear-view mirror on a stalk from the roof, showing the riders behind
            var m = MirrorPos;
            b.Box("paint", "#222222", 0.02f, 2.15f - m.Y, 0.02f, m.X, (2.15f + m.Y) / 2f + 0.04f, m.Z + 0.02f);
            b.Box("paint", "#1A1A1A", 0.2f, 0.068f, 0.03f, m.X, m.Y, m.Z);
            b.Quad("utex:mirror_pax", new V3(m.X - 0.09f, m.Y - 0.028f, m.Z - 0.02f), new V3(m.X + 0.09f, m.Y - 0.028f, m.Z - 0.02f), new V3(m.X + 0.09f, m.Y + 0.028f, m.Z - 0.02f), new V3(m.X - 0.09f, m.Y + 0.028f, m.Z - 0.02f), new V3(0, 0, -1),
                new V2(0, 0), new V2(1, 0), new V2(1, 1), new V2(0, 1));
            // Patok: LED strip across the top of the windshield, inside
            if (L.SoundSystem)
                for (int i = 0; i < 12; i++)
                    b.Box("glow", i % 2 == 0 ? L.Accent1 : L.Accent2, 0.13f, 0.03f, 0.03f, -0.83f + i * 0.15f, 2.1f, 1.12f);
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

        /// <summary>Speedometer dial (chrome ring + face), facing the driver (−Z), centred at the origin.</summary>
        public static MeshData GaugeFace()
        {
            var b = new MeshBuilder();
            float h = GaugeSize / 2f;
            // the chrome bezel is painted into the dial texture; a thin dark disc sits behind it
            b.Push(new V3(0, 0, 0.012f), new Rot((float)(Math.PI / 2), 0, 0));
            b.Cylinder("paint", "#1A1A1A", h * 0.98f, h * 0.98f, 0.01f, 20, 0, 0, 0);
            b.Pop();
            b.Quad("utexa:gauge_face", new V3(-h, -h, 0), new V3(h, -h, 0), new V3(h, h, 0), new V3(-h, h, 0), new V3(0, 0, -1),
                new V2(0, 0), new V2(1, 0), new V2(1, 1), new V2(0, 1));
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
