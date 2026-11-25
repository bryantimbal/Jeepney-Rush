using System;

namespace JeepneyRush.Logic.Geometry
{
    /// <summary>Models for the Filipino road events (all face +Z, origin on the road surface).</summary>
    public static class EventBuilder
    {
        private const float HalfPi = (float)(Math.PI / 2);
        private static void Person(MeshBuilder b, int i, float x, float y, float z, float yaw = 0)
        {
            b.Push(new V3(x, y, z), new Rot(0, yaw, 0));
            PropBuilder.AddPerson(b, i, false);
            b.Pop();
        }

        /// <summary>Magtataho walking along the curb lane with the bamboo yoke and two buckets.</summary>
        public static MeshData TahoVendor(int v)
        {
            var b = new MeshBuilder();
            Person(b, v + 2, 0, 0, 0);
            b.Box("paint", "#C8A14A", 0.07f, 0.07f, 2.0f, 0.2f, 1.36f, 0);       // yoke along the road
            foreach (float bz in new[] { -0.85f, 0.85f })
            {
                foreach (float sx in new[] { -0.15f, 0.15f }) b.Box("paint", "#2B2B2B", 0.015f, 0.62f, 0.015f, 0.2f + sx, 1.05f, bz);
                b.Cylinder("chrome", "#C9CED6", 0.24f, 0.2f, 0.5f, 10, 0.2f, 0.5f, bz);
                b.Cylinder("chrome", "#E1E5EA", 0.25f, 0.25f, 0.05f, 10, 0.2f, 0.77f, bz);
            }
            b.Cylinder("paint", "#E9D8A6", 0.16f, 0.16f, 0.05f, 10, 0, 1.69f, 0); // salakot-style hat brim
            b.Cylinder("paint", "#D4B483", 0f, 0.15f, 0.12f, 10, 0, 1.76f, 0);
            return b.Data;
        }

        /// <summary>Night balut vendor: a woven basket wrapped in cloth on the hip and a small lamp ("Baluuut!").</summary>
        public static MeshData BalutVendor(int v)
        {
            var b = new MeshBuilder();
            Person(b, v + 3, 0, 0, 0);
            b.Cylinder("paint", "#9C6B3C", 0.32f, 0.26f, 0.38f, 10, 0.38f, 0.95f, 0.05f);      // woven basket (bayong)
            b.Cylinder("paint", "#F1E3C6", 0.33f, 0.33f, 0.06f, 10, 0.38f, 1.16f, 0.05f);      // cloth keeping the eggs warm
            for (int i = 0; i < 3; i++) b.Sphere("paint", "#F2EBDD", 0.07f, 0.3f + i * 0.08f, 1.22f, 0.05f, 6);
            b.Box("paint", "#2B2B2B", 0.02f, 0.4f, 0.02f, 0.52f, 1.45f, 0.05f);                 // lamp handle
            b.Cylinder("glow", "#FFC24A", 0.07f, 0.07f, 0.14f, 8, 0.52f, 1.22f, 0.05f);          // little kerosene lamp
            b.Cylinder("paint", "#2D3A4A", 0.13f, 0.13f, 0.1f, 10, 0, 1.75f, 0);                 // cap
            return b.Data;
        }

        /// <summary>Pedicab: bicycle with a covered sidecar (rider pedalling, a passenger inside).</summary>
        public static MeshData Pedicab(int v)
        {
            var b = new MeshBuilder();
            string[] colors = { "#E63946", "#1D4ED8", "#2A9D8F", "#F4A261", "#7B2CBF" };
            string c = colors[v % colors.Length];
            // bicycle on the left
            foreach (float z in new[] { -0.55f, 0.6f }) b.CylinderX("paint", "#1A1A1A", 0.33f, 0.06f, 14, -0.45f, 0.33f, z, true);
            b.Box("paint", "#9AA0A6", 0.05f, 0.05f, 1.1f, -0.45f, 0.62f, 0.02f, new Rot(0.25f, 0, 0));
            b.Box("paint", "#9AA0A6", 0.05f, 0.55f, 0.05f, -0.45f, 0.62f, 0.55f);
            b.Box("paint", "#9AA0A6", 0.45f, 0.04f, 0.04f, -0.45f, 0.92f, 0.55f);   // handlebar
            b.Push(new V3(-0.45f, 0.25f, -0.25f), Rot.None);
            PropBuilder.AddPerson(b, v + 1, false);                                    // rider
            b.Pop();
            // sidecar
            b.Box("paint", c, 0.9f, 0.55f, 1.15f, 0.35f, 0.6f, 0.05f);
            b.Box("paint", "#F1FAEE", 0.92f, 0.08f, 1.17f, 0.35f, 0.84f, 0.05f);
            b.CylinderX("paint", "#1A1A1A", 0.24f, 0.08f, 12, 0.78f, 0.24f, -0.15f, true);
            b.Box("paint", c, 1.0f, 0.06f, 1.3f, 0.35f, 1.65f, 0.0f);                 // canopy
            foreach (float px in new[] { -0.08f, 0.78f }) foreach (float pz in new[] { -0.5f, 0.55f })
                b.Box("paint", "#333333", 0.04f, 0.8f, 0.04f, px, 1.25f, pz);
            b.Push(new V3(0.35f, 0.62f, 0.0f), Rot.None);
            PropBuilder.AddPerson(b, v + 4, true);                                     // passenger
            b.Pop();
            return b.Data;
        }

        /// <summary>Fiesta parade float: decorated flatbed with giant flowers, a sun and dancers.</summary>
        public static MeshData ParadeFloat(int v)
        {
            var b = new MeshBuilder();
            string[] skirts = { "#E63946", "#FFD166", "#06D6A0", "#118AB2", "#F78C6B", "#9B5DE5" };
            b.Box("paint", "#2B2B2B", 2.3f, 0.5f, 7.0f, 0, 0.55f, 0);                   // chassis
            foreach (float z in new[] { -2.6f, 2.6f }) foreach (float x in new[] { -1.0f, 1.0f })
                b.CylinderX("paint", "#1A1A1A", 0.42f, 0.3f, 12, x, 0.42f, z, true);
            for (int i = 0; i < 12; i++)                                                 // ruffled skirt
            {
                string col = skirts[(i + v) % skirts.Length];
                float z = -3.3f + i * 0.6f;
                foreach (float sx in new[] { -1f, 1f }) b.Box("paint", col, 0.06f, 0.7f, 0.6f, sx * 1.2f, 0.62f, z);
            }
            b.Box("paint", "#FFF3D6", 2.4f, 0.12f, 7.1f, 0, 1.0f, 0);                    // stage
            // giant sun with rays at the back
            b.Cylinder("glow", "#FFD60A", 0.9f, 0.9f, 0.2f, 16, 0, 2.6f, -2.6f, new Rot(HalfPi, 0, 0));
            for (int r = 0; r < 8; r++)
                b.Box("paint", "#FFB703", 0.18f, 0.7f, 0.12f, (float)Math.Sin(r * Math.PI / 4) * 1.25f, 2.6f + (float)Math.Cos(r * Math.PI / 4) * 1.25f, -2.6f, new Rot(0, 0, -(float)(r * Math.PI / 4)));
            b.Box("paint", "#6B4226", 0.2f, 1.6f, 0.2f, 0, 1.8f, -2.6f);
            // giant flowers along the sides
            for (int i = 0; i < 5; i++)
            {
                float z = -1.8f + i * 1.1f;
                foreach (float sx in new[] { -1f, 1f })
                {
                    string col = skirts[(i * 2 + (sx > 0 ? 1 : 0) + v) % skirts.Length];
                    b.Sphere("paint", col, 0.32f, sx * 1.05f, 1.35f, z, 7, 1f, 0.6f, 1f);
                    b.Sphere("paint", "#FFD60A", 0.12f, sx * 1.05f, 1.45f, z, 6);
                }
            }
            // banderitas strung from front to back
            for (int i = 0; i < 14; i++)
                b.Box("paint", skirts[i % skirts.Length], 0.02f, 0.22f, 0.22f, 0, 3.55f - (i % 2) * 0.05f, -3.2f + i * 0.48f, new Rot(0, 0, 0.785f));
            b.Box("paint", "#444444", 0.03f, 0.03f, 6.8f, 0, 3.6f, 0);
            foreach (float z in new[] { -3.4f, 3.4f }) b.Box("paint", "#6B4226", 0.1f, 2.6f, 0.1f, 0, 2.3f, z);
            // dancers in festival colours
            for (int i = 0; i < 3; i++) Person(b, i * 3 + v, (i - 1) * 0.65f, 1.06f, 1.3f + (i % 2) * 0.6f, (float)Math.PI);
            return b.Data;
        }

        /// <summary>Carabao crossing (standing across the lane) and the farmer with his rope and salakot.</summary>
        public static MeshData Carabao(int v)
        {
            var b = new MeshBuilder();
            string hide = v % 2 == 0 ? "#4A4A4F" : "#5A4E46";
            b.Push(new V3(0, 0, 0), new Rot(0, HalfPi * (v % 2 == 0 ? 1 : -1), 0)); // side-on to the jeepney
            b.Box("paint", hide, 0.95f, 0.85f, 1.9f, 0, 1.05f, 0);
            b.Box("paint", Palette.Shade(hide, 0.85f), 0.6f, 0.6f, 0.65f, 0, 1.15f, 1.2f);
            b.Box("paint", "#2E2E30", 0.5f, 0.3f, 0.25f, 0, 0.95f, 1.5f);                // muzzle
            foreach (float sx in new[] { -1f, 1f })
            {
                b.Box("paint", "#D9D2C0", 0.75f, 0.09f, 0.11f, sx * 0.55f, 1.5f, 1.1f, new Rot(0, -sx * 0.25f, sx * 0.55f)); // swept-back horns
                foreach (float sz in new[] { -0.65f, 0.65f }) b.Box("paint", Palette.Shade(hide, 0.9f), 0.2f, 0.7f, 0.2f, sx * 0.32f, 0.35f, sz);
            }
            b.Box("paint", hide, 0.06f, 0.06f, 0.5f, 0, 1.1f, -1.1f, new Rot(0.9f, 0, 0)); // tail
            b.Pop();
            // farmer leading it, rope to the nose
            b.Push(new V3(0.2f, 0, 1.3f), new Rot(0, (float)Math.PI, 0));
            PropBuilder.AddPerson(b, 5 + v, false);
            b.Pop();
            b.Cylinder("paint", "#D4B483", 0f, 0.33f, 0.18f, 12, 0.2f, 1.72f, 1.3f);       // salakot
            b.Box("paint", "#8B6B3D", 0.03f, 0.03f, 1.3f, -0.4f, 1.1f, 0.9f, new Rot(0, 0.9f, 0.2f));
            return b.Data;
        }

        /// <summary>Road-works flagman: orange vest, hard hat, waving a red flag.</summary>
        public static MeshData Flagman()
        {
            var b = new MeshBuilder();
            Person(b, 3, 0, 0, 0, (float)Math.PI);
            b.Box("paint", "#FF6D00", 0.48f, 0.4f, 0.3f, 0, 1.08f, 0);
            b.Box("glow", "#E8FF5A", 0.49f, 0.05f, 0.31f, 0, 1.0f, 0);                      // reflective stripe
            b.Sphere("paint", "#FFD60A", 0.19f, 0, 1.62f, 0, 8, 1f, 0.7f, 1f);
            b.Box("paint", "#6B4226", 0.035f, 1.4f, 0.035f, -0.45f, 1.75f, 0, new Rot(0, 0, 0.3f));
            b.Box("paint", "#D62828", 0.5f, 0.38f, 0.02f, -0.85f, 2.3f, 0, new Rot(0, 0, 0.3f));
            return b.Data;
        }

        /// <summary>Stalled jeepney by the curb: hood up, hazard lights, warning triangle, driver pushing.</summary>
        public static MeshData StalledJeepney(int v)
        {
            var b = new MeshBuilder();
            string[][] colors = { new[] { "#F4F1E8", "#C8102E", "#1F3FBF" }, new[] { "#FFD100", "#0B6E4F", "#C8102E" }, new[] { "#1F3FBF", "#FFD100", "#F4F1E8" } };
            var c = colors[v % colors.Length];
            MeshOps.Append(b.Data, VehicleBuilder.SimpleJeepney(c[0], c[1], c[2]), new V3(0, 0, 0));
            b.Box("paint", c[1], 1.4f, 0.06f, 0.9f, 0, 2.0f, 2.45f, new Rot(-1.0f, 0, 0));  // hood propped open
            b.Box("paint", "#444444", 0.05f, 0.7f, 0.05f, 0.4f, 1.65f, 2.2f);
            foreach (float sx in new[] { -1f, 1f }) b.Box("glow", "#FFB000", 0.16f, 0.16f, 0.06f, sx * 0.95f, 1.0f, 3.28f);
            // early-warning triangle behind the jeepney
            b.Cylinder("glow", "#FF2A2A", 0.32f, 0.32f, 0.04f, 3, 0, 0.32f, -4.2f, new Rot(HalfPi, 0, 0));
            // driver pushing from behind
            b.Push(new V3(0.5f, 0, -3.3f), new Rot(0.35f, 0, 0));
            PropBuilder.AddPerson(b, 6, false);
            b.Pop();
            return b.Data;
        }

        public static MeshData StrayDog(int v)
        {
            string[] colors = { "#C9A27E", "#8B5A2B", "#E8E1D3", "#3E3A36" };
            return LifeBuilder.Dog(colors[v % colors.Length]);
        }

        /// <summary>A basketball rolling out from a court by the road.</summary>
        public static MeshData Basketball()
        {
            var b = new MeshBuilder();
            b.Sphere("paint", "#E8742A", 0.14f, 0, 0.14f, 0, 10);
            b.Box("paint", "#1A1A1A", 0.29f, 0.012f, 0.012f, 0, 0.14f, 0);
            b.Box("paint", "#1A1A1A", 0.012f, 0.29f, 0.012f, 0, 0.14f, 0);
            b.Box("paint", "#1A1A1A", 0.012f, 0.012f, 0.29f, 0, 0.14f, 0);
            return b.Data;
        }
    }
}
