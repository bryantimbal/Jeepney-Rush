using System;

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
            var (u0, v0, u1, v1) = AtlasCell(nameIndex, 4, 8);
            // seen from the front (+Z looking toward -Z), -X is on the viewer's right
            b.Quad("tex:stop_signs", new V3(1.0f, 2.9f, 0), new V3(-1.0f, 2.9f, 0), new V3(-1.0f, 3.9f, 0), new V3(1.0f, 3.9f, 0), new V3(0, 0, 1),
                new V2(u0, v0), new V2(u1, v0), new V2(u1, v1), new V2(u0, v1));
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
