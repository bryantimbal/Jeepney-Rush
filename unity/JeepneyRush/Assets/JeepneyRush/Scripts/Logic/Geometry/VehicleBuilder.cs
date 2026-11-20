using System;

namespace JeepneyRush.Logic.Geometry
{
    /// <summary>Traffic vehicles and road obstacles (Unity coordinates, +Z forward, ground at y = 0).</summary>
    public static class VehicleBuilder
    {
        private const string Chrome = JeepneyBuilder.Chrome;
        private const string Tire = JeepneyBuilder.Tire;
        private const string Glass = JeepneyBuilder.Glass;
        private const float HalfPi = (float)(Math.PI / 2);

        public static readonly string[] CarColors = { "#C1121F", "#F8F9FA", "#ADB5BD", "#212529", "#1D4ED8", "#2A9D8F", "#E9C46A", "#6A4C93" };
        public static readonly string[] Shirts = { "#E63946", "#457B9D", "#2A9D8F", "#F4A261", "#FFD166", "#9B5DE5", "#F8F9FA", "#06D6A0" };

        private static void Wheels(MeshBuilder b, float x, float[] zs, float r, float w = 0.26f)
        {
            foreach (float z in zs)
                foreach (float s in new[] { -1f, 1f })
                {
                    b.CylinderX("paint", Tire, r, w, 12, s * x, r, z, true);
                    b.CylinderX("chrome", Chrome, r * 0.55f, w + 0.02f, 10, s * x, r, z);
                }
        }

        private static void TailLights(MeshBuilder b, float halfW, float y, float z, float h = 0.16f)
        {
            foreach (float s in new[] { -1f, 1f })
            {
                b.Box("glow", "#FF2121", 0.32f, h, 0.04f, s * (halfW - 0.2f), y, z);
                b.Box("glow", "#FF9F1C", 0.1f, h, 0.04f, s * (halfW - 0.42f), y, z);
            }
        }

        private static void HeadLights(MeshBuilder b, float halfW, float y, float z)
        {
            foreach (float s in new[] { -1f, 1f }) b.Box("glow", "#FFF8DC", 0.3f, 0.14f, 0.04f, s * (halfW - 0.25f), y, z);
        }

        public static MeshData Sedan(string color, bool taxi = false)
        {
            var b = new MeshBuilder();
            var body = new System.Collections.Generic.List<V2>
            {
                new V2(-2.2f, 0.3f), new V2(2.2f, 0.3f), new V2(2.25f, 0.62f), new V2(2.1f, 0.9f), new V2(0.95f, 0.98f),
                new V2(0.35f, 1.45f), new V2(-1.05f, 1.45f), new V2(-1.6f, 0.98f), new V2(-2.15f, 0.92f), new V2(-2.25f, 0.6f),
            };
            b.ExtrudeX("paint", color, body, -0.9f, 0.9f);
            // glass band
            var glass = new System.Collections.Generic.List<V2> { new V2(0.88f, 1.0f), new V2(0.32f, 1.4f), new V2(-1.0f, 1.4f), new V2(-1.5f, 1.0f) };
            b.ExtrudeX("glass", Glass, glass, -0.91f, 0.91f);
            b.Box("paint", "#2B2D33", 1.9f, 0.18f, 0.18f, 0, 0.38f, 2.22f);
            b.Box("paint", "#2B2D33", 1.9f, 0.18f, 0.18f, 0, 0.38f, -2.22f);
            TailLights(b, 0.92f, 0.78f, -2.26f);
            HeadLights(b, 0.92f, 0.72f, 2.26f);
            if (taxi)
            {
                b.Box("glow", "#FFD60A", 0.7f, 0.2f, 0.3f, 0, 1.56f, -0.3f);
                b.Box("paint", "#111111", 1.82f, 0.1f, 4.0f, 0, 0.75f, 0);
            }
            Wheels(b, 0.86f, new[] { -1.35f, 1.35f }, 0.34f);
            return b.Data;
        }

        public static MeshData Suv(string color)
        {
            var b = new MeshBuilder();
            b.Box("paint", color, 1.96f, 0.85f, 4.7f, 0, 0.8f, 0);
            b.Box("glass", Glass, 1.86f, 0.62f, 3.0f, 0, 1.52f, -0.4f);
            b.Box("paint", color, 1.88f, 0.1f, 3.1f, 0, 1.88f, -0.4f);
            for (int i = 0; i < 3; i++) b.Box("paint", color, 1.9f, 0.62f, 0.12f, 0, 1.52f, 1.05f - i * 1.2f);
            b.Box("chrome", Chrome, 1.7f, 0.05f, 0.05f, 0, 1.97f, -0.4f);
            b.Box("paint", "#2B2D33", 2.0f, 0.22f, 0.2f, 0, 0.45f, 2.38f);
            b.Box("paint", "#2B2D33", 2.0f, 0.22f, 0.2f, 0, 0.45f, -2.38f);
            b.CylinderZ("paint", Tire, 0.36f, 0.25f, 12, 0, 1.1f, -2.45f, true); // rear spare
            TailLights(b, 1.0f, 1.1f, -2.36f, 0.3f);
            HeadLights(b, 1.0f, 0.95f, 2.36f);
            Wheels(b, 0.92f, new[] { -1.5f, 1.5f }, 0.4f, 0.3f);
            return b.Data;
        }

        public static MeshData Bus(string body, string stripe)
        {
            var b = new MeshBuilder();
            b.Box("paint", body, 2.38f, 2.5f, 10.8f, 0, 1.75f, 0);
            b.Box("glass", Glass, 2.42f, 0.95f, 9.4f, 0, 2.15f, -0.3f);
            b.Box("paint", stripe, 2.42f, 0.3f, 10.82f, 0, 1.15f, 0);
            b.Box("paint", "#E9ECEF", 2.3f, 0.12f, 10.6f, 0, 3.06f, 0);
            b.Box("paint", "#CED4DA", 1.6f, 0.4f, 1.4f, 0, 3.25f, -1.5f);
            b.Box("glass", Glass, 2.2f, 1.3f, 0.05f, 0, 2.1f, 5.41f);
            b.Box("paint", "#111111", 1.7f, 0.36f, 0.05f, 0, 2.84f, -5.41f);
            b.Box("glow", "#FFA62B", 1.5f, 0.2f, 0.02f, 0, 2.84f, -5.44f);
            b.Box("glass", Glass, 1.9f, 0.8f, 0.05f, 0, 2.15f, -5.41f);
            TailLights(b, 1.19f, 1.0f, -5.42f, 0.4f);
            HeadLights(b, 1.19f, 0.9f, 5.42f);
            b.Box("paint", "#2B2D33", 2.42f, 0.28f, 0.22f, 0, 0.55f, -5.4f);
            Wheels(b, 1.06f, new[] { -3.3f, 3.6f }, 0.52f, 0.34f);
            return b.Data;
        }

        public static MeshData Truck(string color)
        {
            var b = new MeshBuilder();
            b.Box("paint", color, 2.24f, 1.9f, 1.9f, 0, 1.55f, 2.65f);
            b.Box("glass", Glass, 2.1f, 0.75f, 0.06f, 0, 1.95f, 3.62f);
            b.Box("paint", "#26262B", 2.0f, 0.4f, 7.2f, 0, 0.75f, 0);
            b.Box("paint", "#F1F1EE", 2.3f, 2.6f, 5.0f, 0, 2.05f, -1.0f);
            b.Box("paint", color, 2.32f, 0.4f, 5.02f, 0, 1.2f, -1.0f);
            b.Box("paint", "#D8D8D3", 2.2f, 2.4f, 0.05f, 0, 2.05f, -3.52f);
            b.Box("paint", "#8A8A85", 0.05f, 2.3f, 0.06f, 0, 2.05f, -3.55f);
            for (int i = 0; i < 6; i++) b.Box("glow", i % 2 == 1 ? "#FF2121" : "#FFD23F", 0.16f, 0.12f, 0.03f, -0.9f + i * 0.36f, 0.72f, -3.55f);
            TailLights(b, 1.12f, 0.75f, -3.56f);
            HeadLights(b, 1.12f, 1.0f, 3.62f);
            Wheels(b, 1.0f, new[] { 2.6f, -1.4f, -2.6f }, 0.5f, 0.34f);
            return b.Data;
        }

        private static void Rider(MeshBuilder b, float z, string shirt, string helmet)
        {
            b.Box("paint", shirt, 0.4f, 0.55f, 0.28f, 0, 1.25f, z);
            b.Sphere("paint", helmet, 0.17f, 0, 1.68f, z + 0.05f, 8);
            b.Box("glass", "#111111", 0.24f, 0.08f, 0.05f, 0, 1.7f, z + 0.2f);
            foreach (float s in new[] { -1f, 1f })
            {
                b.Box("paint", "#283044", 0.12f, 0.5f, 0.12f, s * 0.15f, 0.85f, z + 0.2f, new Rot(-1.2f, 0, 0));
                b.Box("paint", shirt, 0.1f, 0.42f, 0.1f, s * 0.22f, 1.25f, z + 0.3f, new Rot(-1.1f, 0, 0));
            }
        }

        public static MeshData Motorcycle(string color, string shirt)
        {
            var b = new MeshBuilder();
            AddMotorcycle(b, color, shirt);
            return b.Data;
        }

        private static void AddMotorcycle(MeshBuilder b, string color, string shirt)
        {
            b.CylinderX("paint", Tire, 0.32f, 0.12f, 12, 0, 0.32f, 0.7f, true);
            b.CylinderX("paint", Tire, 0.32f, 0.14f, 12, 0, 0.32f, -0.7f, true);
            b.Box("paint", color, 0.34f, 0.36f, 1.1f, 0, 0.66f, 0);
            b.Box("paint", "#111111", 0.3f, 0.12f, 0.6f, 0, 0.9f, -0.25f);
            b.Box("chrome", Chrome, 0.7f, 0.04f, 0.04f, 0, 1.12f, 0.55f);
            b.Box("glow", "#FF2121", 0.14f, 0.08f, 0.04f, 0, 0.8f, -0.98f);
            b.Box("glow", "#FFF8DC", 0.16f, 0.12f, 0.04f, 0, 0.95f, 0.85f);
            Rider(b, -0.15f, shirt, color);
        }

        /// <summary>Filipino tricycle: motorcycle + sidecar with the iconic canopy roof.</summary>
        public static MeshData Tricycle(string color, string shirt, string accent)
        {
            var b = new MeshBuilder();
            b.Push(new V3(-0.35f, 0, 0), Rot.None);
            AddMotorcycle(b, color, shirt);
            b.Pop();
            b.Box("paint", color, 0.95f, 0.85f, 1.7f, 0.5f, 0.85f, -0.05f);
            b.Box("glass", Glass, 0.9f, 0.42f, 1.2f, 0.5f, 1.48f, -0.15f);
            b.Box("paint", color, 1.95f, 0.08f, 2.1f, 0.1f, 1.9f, -0.05f);
            b.Box("paint", accent, 1.97f, 0.06f, 2.12f, 0.1f, 1.85f, -0.05f);
            b.Box("paint", "#222222", 0.06f, 0.95f, 0.06f, -0.8f, 1.45f, 0.9f);
            b.Box("paint", "#222222", 0.06f, 0.95f, 0.06f, -0.8f, 1.45f, -0.95f);
            b.Box("paint", accent, 0.97f, 0.12f, 1.72f, 0.5f, 1.25f, -0.05f);
            b.CylinderX("paint", Tire, 0.3f, 0.14f, 12, 0.85f, 0.3f, -0.2f, true);
            b.Box("glow", "#FF2121", 0.18f, 0.1f, 0.04f, 0.8f, 0.72f, -0.92f);
            return b.Data;
        }

        /// <summary>Simplified palette-only jeepney for parked/background use.</summary>
        public static MeshData SimpleJeepney(string body, string stripe, string roof)
        {
            var b = new MeshBuilder();
            b.Box("paint", "#1D1D22", 1.6f, 0.25f, 5.6f, 0, 0.55f, 0);
            b.Box("paint", body, 2.0f, 0.9f, 3.9f, 0, 1.0f, -0.9f);
            b.Box("paint", stripe, 2.02f, 0.12f, 3.92f, 0, 0.8f, -0.9f);
            b.Box("paint", body, 2.0f, 0.9f, 1.0f, 0, 1.0f, 1.55f);
            b.Box("glass", Glass, 1.92f, 0.6f, 4.9f, 0, 1.75f, -0.4f);
            for (int i = 0; i < 8; i++) b.Box("paint", stripe, 2.0f, 0.6f, 0.08f, 0, 1.75f, 1.9f - i * 0.68f);
            b.Box("paint", roof, 2.12f, 0.14f, 5.0f, 0, 2.12f, -0.4f);
            b.Box("paint", stripe, 1.5f, 0.52f, 1.1f, 0, 1.0f, 2.6f);
            b.Box("chrome", Chrome, 1.3f, 0.55f, 0.08f, 0, 1.0f, 3.18f);
            b.Box("chrome", Chrome, 2.1f, 0.18f, 0.2f, 0, 0.56f, 3.3f);
            b.Box("paint", "#120D18", 0.86f, 1.3f, 0.05f, 0, 1.35f, -2.88f);
            b.Box("chrome", Chrome, 2.04f, 0.14f, 0.12f, 0, 0.62f, -2.95f);
            foreach (float s in new[] { -1f, 1f })
            {
                b.Box("glow", "#FF1F1F", 0.09f, 0.4f, 0.04f, s * 0.94f, 1.2f, -2.88f);
                b.Box("glow", "#FFB000", 0.12f, 0.12f, 0.04f, s * 0.94f, 0.85f, -2.88f); // hazard blinkers
                b.Cylinder("glow", "#FFF7D6", 0.11f, 0.11f, 0.05f, 12, s * 0.6f, 1.1f, 3.24f, new Rot(HalfPi, 0, 0));
            }
            Wheels(b, 0.93f, new[] { 2.15f, -1.7f }, 0.42f, 0.3f);
            return b.Data;
        }

        // ---------------------------------------------------------------- obstacles
        public static MeshData Cone()
        {
            var b = new MeshBuilder();
            b.Box("paint", "#E85D04", 0.6f, 0.06f, 0.6f, 0, 0.03f, 0);
            b.Cylinder("paint", "#FF6D00", 0.05f, 0.26f, 0.72f, 12, 0, 0.42f, 0, default, true);
            b.Cylinder("paint", "#FFFFFF", 0.15f, 0.2f, 0.13f, 12, 0, 0.44f, 0, default, true);
            return b.Data;
        }

        public static MeshData Barrier()
        {
            var b = new MeshBuilder();
            foreach (float x in new[] { -1.05f, 1.05f })
            {
                b.Box("paint", "#666666", 0.08f, 0.85f, 0.08f, x, 0.42f, -0.16f, new Rot(-0.25f, 0, 0));
                b.Box("paint", "#666666", 0.08f, 0.85f, 0.08f, x, 0.42f, 0.16f, new Rot(0.25f, 0, 0));
                b.Sphere("glow", "#FFB703", 0.07f, x, 0.92f, 0, 6);
            }
            for (int i = 0; i < 6; i++) b.Box("paint", i % 2 == 1 ? "#FFFFFF" : "#FF6D00", 0.4f, 0.3f, 0.06f, -1.0f + i * 0.4f, 0.7f, 0);
            return b.Data;
        }

        public static MeshData Pothole()
        {
            var b = new MeshBuilder();
            b.Cylinder("paint", "#5B5A57", 0.95f, 1.0f, 0.04f, 12, 0, 0.025f, 0);
            b.Cylinder("paint", "#151417", 0.78f, 0.8f, 0.05f, 12, 0, 0.03f, 0);
            b.Cylinder("glass", "#2A6F97", 0.3f, 0.35f, 0.06f, 7, 0.35f, 0.035f, 0.2f);
            return b.Data;
        }

        public static MeshData ConstructionSign()
        {
            var b = new MeshBuilder();
            b.Box("paint", "#555555", 0.08f, 1.4f, 0.08f, -0.45f, 0.7f, 0.1f, new Rot(-0.15f, 0, 0));
            b.Box("paint", "#555555", 0.08f, 1.4f, 0.08f, 0.45f, 0.7f, 0.1f, new Rot(-0.15f, 0, 0));
            b.Box("paint", "#FF8C00", 1.1f, 1.1f, 0.06f, 0, 1.45f, 0, new Rot(0, 0, (float)(Math.PI / 4)));
            b.Box("paint", "#111111", 0.9f, 0.9f, 0.065f, 0, 1.45f, 0, new Rot(0, 0, (float)(Math.PI / 4)));
            b.Box("paint", "#FF8C00", 0.8f, 0.8f, 0.07f, 0, 1.45f, 0, new Rot(0, 0, (float)(Math.PI / 4)));
            // little worker pictogram
            b.Box("paint", "#111111", 0.08f, 0.3f, 0.08f, 0, 1.45f, 0.04f, new Rot(0, 0, 0.3f));
            b.Sphere("paint", "#111111", 0.07f, -0.05f, 1.66f, 0.04f, 6);
            b.Box("paint", "#111111", 0.3f, 0.05f, 0.08f, 0.05f, 1.34f, 0.04f);
            b.Sphere("glow", "#FFB703", 0.07f, 0, 2.05f, 0, 6);
            return b.Data;
        }

        public static MeshData Roadblock()
        {
            var b = new MeshBuilder();
            foreach (float x in new[] { -1.2f, 1.2f })
            {
                b.Box("paint", "#555555", 0.12f, 2.1f, 0.12f, x, 1.05f, 0);
                b.Box("paint", "#333333", 0.5f, 0.08f, 0.6f, x, 0.04f, 0);
                b.Sphere("glow", "#FF3B30", 0.11f, x, 2.2f, 0, 6);
            }
            for (int i = 0; i < 8; i++) b.Box("paint", i % 2 == 1 ? "#FFFFFF" : "#D00000", 0.3f, 0.42f, 0.08f, -1.05f + i * 0.3f, 1.5f, 0);
            b.Box("paint", "#FFD60A", 2.5f, 0.5f, 0.08f, 0, 0.75f, 0);
            b.Box("paint", "#111111", 1.7f, 0.18f, 0.09f, 0, 0.75f, 0.01f);
            return b.Data;
        }
    }
}
