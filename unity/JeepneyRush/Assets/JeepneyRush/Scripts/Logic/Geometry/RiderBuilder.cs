using System;

namespace JeepneyRush.Logic.Geometry
{
    /// <summary>
    /// Passenger personalities you can tell apart at a glance (same proportions as
    /// <see cref="PropBuilder.AddPerson(MeshBuilder,int,bool)"/>: feet / seat at the origin, facing +Z):
    /// estudyante in a white polo with an ID lace and backpack, lola with a grey bun, duster and
    /// cane, chismosa with curlers and a pamaypay, the office worker in a hurry with a lanyard and
    /// laptop bag, the lasing in a sando with a towel and bottle, the fare dodger in a hoodie and
    /// cap, the tindera with an apron and a big striped bayong, the balikbayan with shades, an
    /// aloha shirt and a balikbayan box.
    /// </summary>
    public static class RiderBuilder
    {
        private const string Skin = "#A86B3C", SkinLight = "#C68642", SkinDark = "#8D5524";

        public static MeshData Rider(RiderKind kind, bool seated)
        {
            var b = new MeshBuilder();
            Add(b, kind, seated);
            return b.Data;
        }

        public static void Add(MeshBuilder b, RiderKind kind, bool seated)
        {
            float hip = seated ? 0f : 0.75f;
            float head = hip + 0.72f;
            switch (kind)
            {
                case RiderKind.Student:
                    Body(b, seated, "#F7F7F2", "#1D2B53", SkinLight, "#111111", shortSleeve: true);
                    b.Box("paint", "#C1121F", 0.03f, 0.3f, 0.02f, 0, hip + 0.4f, 0.135f);           // ID lace
                    b.Box("paint", "#F7F7F2", 0.08f, 0.1f, 0.015f, 0, hip + 0.22f, 0.14f);          // ID card
                    b.Box("paint", "#2B59C3", 0.36f, 0.4f, 0.16f, 0, hip + 0.33f, -0.21f);           // backpack
                    b.Box("paint", "#1D3C8F", 0.3f, 0.14f, 0.05f, 0, hip + 0.22f, -0.3f);
                    break;
                case RiderKind.Lola:
                    Body(b, seated, "#E8A0BF", "#E8A0BF", Skin, "#D9D9D9", dress: true);
                    b.Sphere("paint", "#D9D9D9", 0.09f, 0, head + 0.12f, -0.12f, 8);                // hair bun
                    b.Box("paint", "#F4E1C1", 0.5f, 0.2f, 0.3f, 0, hip + 0.5f, 0, new Rot(0.1f, 0, 0)); // shawl
                    foreach (float gx in new[] { -0.06f, 0.06f }) b.Box("chrome", "#B0B0B0", 0.09f, 0.06f, 0.015f, gx, head + 0.02f, 0.155f); // reading glasses
                    foreach (float k in new[] { -0.08f, 0.08f }) b.Box("paint", "#E8A0BF", 0.03f, 0.03f, 0.01f, k * 0.7f, hip + 0.18f, 0.14f); // flowers on the duster
                    if (seated) b.Cylinder("paint", "#6B4226", 0.02f, 0.02f, 0.75f, 6, 0.24f, 0.08f, 0.42f, new Rot(-0.2f, 0, 0)); // cane at her knee
                    else
                    {
                        b.Cylinder("paint", "#6B4226", 0.022f, 0.022f, 0.9f, 6, 0.36f, 0.45f, 0.08f);
                        b.Box("paint", "#6B4226", 0.14f, 0.03f, 0.03f, 0.31f, 0.9f, 0.08f);        // cane handle
                    }
                    break;
                case RiderKind.Chismosa:
                    Body(b, seated, "#D81B60", "#3D3D3D", SkinLight, "#2B1A10");
                    foreach (var (x, z) in new[] { (-0.1f, 0.02f), (0.0f, 0.06f), (0.1f, 0.02f), (-0.06f, -0.1f), (0.06f, -0.1f) })
                        b.Cylinder("paint", x < 0 ? "#4FC3F7" : "#FFEB3B", 0.045f, 0.045f, 0.12f, 6, x, head + 0.14f, z, new Rot(0, 0, (float)(Math.PI / 2))); // curlers
                    b.Box("paint", "#F4D35E", 0.02f, 0.24f, 0.2f, 0.36f, hip + 0.62f, 0.12f, new Rot(0, 0.4f, 0.3f)); // pamaypay (fan)
                    break;
                case RiderKind.Hurry:
                    Body(b, seated, "#A8DADC", "#14213D", Skin, "#111111");
                    b.Box("paint", "#1D3557", 0.03f, 0.3f, 0.02f, 0.05f, hip + 0.4f, 0.135f);       // lanyard
                    b.Box("paint", "#F1FAEE", 0.08f, 0.1f, 0.015f, 0.05f, hip + 0.22f, 0.14f);
                    b.Box("paint", "#1B1B1B", 0.42f, 0.3f, 0.08f, -0.36f, hip + (seated ? 0.12f : 0.2f), 0, new Rot(0, 0, 0.05f)); // laptop bag
                    b.Box("paint", "#C0C0C0", 0.05f, 0.05f, 0.05f, 0.31f, hip + 0.05f, 0.04f);     // wrist watch
                    break;
                case RiderKind.Drunk:
                    Body(b, seated, "#F4F4F0", "#6B4F3A", "#C8735A", "#111111", sando: true, shorts: true);
                    b.Box("paint", "#F4F1E8", 0.14f, 0.4f, 0.3f, -0.17f, hip + 0.56f, 0);          // towel on the shoulder
                    b.Cylinder("glass", "#2E7D32", 0.045f, 0.045f, 0.26f, 8, 0.32f, hip + (seated ? 0.12f : 0.05f), 0.08f); // bottle
                    b.Cylinder("glass", "#2E7D32", 0.02f, 0.03f, 0.08f, 6, 0.32f, hip + (seated ? 0.29f : 0.22f), 0.08f);
                    b.Sphere("paint", "#E05A4F", 0.05f, 0.08f, head - 0.02f, 0.13f, 6);              // red cheeks
                    b.Sphere("paint", "#E05A4F", 0.05f, -0.08f, head - 0.02f, 0.13f, 6);
                    break;
                case RiderKind.Dodger:
                    Body(b, seated, "#2B2D42", "#3D3D3D", SkinDark, "#111111");
                    b.Sphere("paint", "#2B2D42", 0.2f, 0, head + 0.02f, -0.04f, 8, 1f, 1.05f, 1.05f); // hood up
                    b.Sphere("paint", "#111111", 0.17f, 0, head + 0.1f, 0.0f, 8, 1f, 0.5f, 1f);      // cap
                    b.Box("paint", "#111111", 0.22f, 0.03f, 0.18f, 0, head + 0.09f, 0.18f);           // cap visor, pulled low
                    break;
                case RiderKind.Vendor:
                    Body(b, seated, "#F4A261", "#6B4F3A", Skin, "#2B1A10", dress: true);
                    b.Box("paint", "#FFFFFF", 0.36f, 0.42f, 0.02f, 0, hip + 0.16f, 0.14f);           // apron
                    b.Box("paint", "#E76F51", 0.38f, 0.05f, 0.02f, 0, hip + 0.36f, 0.15f);
                    b.Sphere("paint", "#F4F1E8", 0.17f, 0, head + 0.12f, 0, 8, 1.2f, 0.4f, 1.2f);   // towel turban
                    if (seated) Bayong(b, 0f, 0.1f, 0.42f, 0.8f); // on her lap (the bayong also takes the next seat)
                    else Bayong(b, 0.45f, 0f, 0.1f, 1f);
                    break;
                case RiderKind.Balikbayan:
                    Body(b, seated, "#FF6B6B", "#E9D8A6", SkinLight, "#111111", shortSleeve: true);
                    foreach (var (x, y) in new[] { (-0.12f, 0.42f), (0.1f, 0.3f), (-0.04f, 0.18f), (0.12f, 0.5f) })
                        b.Sphere("paint", "#FFD166", 0.04f, x, hip + y, 0.135f, 6, 1f, 1f, 0.3f);   // aloha flowers
                    b.Box("paint", "#111111", 0.26f, 0.06f, 0.02f, 0, head + 0.02f, 0.155f);         // shades
                    b.Box("paint", "#C9A227", 0.05f, 0.05f, 0.05f, -0.31f, hip + 0.05f, 0.04f);      // gold watch
                    // the balikbayan box, taped, with a handwritten name
                    // on the lap when seated, beside him when standing
                    float bx = seated ? 0f : 0.55f, by = seated ? 0.3f : 0.25f, bz = seated ? 0.38f : 0.05f, bs = seated ? 0.38f : 0.5f;
                    b.Box("paint", "#C69C6D", bs, bs, bs, bx, by, bz);
                    b.Box("paint", "#8D6E4F", bs + 0.02f, 0.06f, bs + 0.02f, bx, by + bs * 0.24f, bz);
                    b.Box("paint", "#8D6E4F", 0.06f, bs + 0.02f, bs + 0.02f, bx, by, bz);
                    b.Box("paint", "#1D3557", bs * 0.5f, 0.07f, 0.01f, bx + bs * 0.2f, by - bs * 0.16f, bz + bs / 2f + 0.01f);
                    break;
                default:
                    PropBuilder.AddPerson(b, 1, seated);
                    break;
            }
        }

        /// <summary>A big striped buri bag (bayong) with vegetables sticking out.</summary>
        private static void Bayong(MeshBuilder b, float x, float y, float z, float scale)
        {
            b.Push(new V3(x, y, z), Rot.None, scale);
            x = 0; y = 0; z = 0;
            b.Box("paint", "#D4A373", 0.42f, 0.44f, 0.3f, x, y + 0.22f, z);
            foreach (float k in new[] { 0.12f, 0.26f, 0.38f }) b.Box("paint", k > 0.2f && k < 0.3f ? "#2A9D8F" : "#C1121F", 0.43f, 0.04f, 0.31f, x, y + k, z);
            b.Box("paint", "#8D6E4F", 0.04f, 0.2f, 0.04f, x - 0.12f, y + 0.52f, z);              // handles
            b.Box("paint", "#8D6E4F", 0.04f, 0.2f, 0.04f, x + 0.12f, y + 0.52f, z);
            b.Sphere("paint", "#4C9A2A", 0.08f, x - 0.08f, y + 0.46f, z, 6);                       // pechay / kangkong
            b.Sphere("paint", "#6A994E", 0.07f, x + 0.06f, y + 0.47f, z + 0.04f, 6);
            b.Cylinder("paint", "#F77F00", 0.03f, 0.01f, 0.18f, 6, x + 0.1f, y + 0.5f, z - 0.06f); // carrot
            b.Pop();
        }

        /// <summary>Base body with outfit options (proportions of the generic commuter).</summary>
        private static void Body(MeshBuilder b, bool seated, string shirt, string pants, string skin, string hair,
            bool shortSleeve = false, bool dress = false, bool sando = false, bool shorts = false)
        {
            float hip = seated ? 0f : 0.75f;
            if (seated)
            {
                foreach (float s in new[] { -1f, 1f })
                {
                    b.Box("paint", shorts ? skin : pants, 0.15f, 0.15f, 0.42f, s * 0.11f, 0.02f, 0.2f);
                    if (shorts) b.Box("paint", pants, 0.16f, 0.16f, 0.22f, s * 0.11f, 0.02f, 0.1f);
                }
                if (dress) b.Box("paint", shirt, 0.42f, 0.12f, 0.4f, 0, 0.06f, 0.18f);              // skirt over the lap
            }
            else
            {
                foreach (float s in new[] { -1f, 1f })
                {
                    b.Box("paint", shorts || dress ? skin : pants, 0.15f, 0.75f, 0.16f, s * 0.11f, 0.38f, 0);
                    if (shorts) b.Box("paint", pants, 0.16f, 0.3f, 0.17f, s * 0.11f, 0.6f, 0);
                }
                if (dress) b.Cylinder("paint", shirt, 0.22f, 0.3f, 0.55f, 10, 0, 0.47f, 0);        // duster / skirt
            }
            b.Box("paint", shirt, 0.44f, 0.58f, 0.26f, 0, hip + 0.28f, 0);
            if (sando)
            {
                foreach (float s in new[] { -1f, 1f }) b.Box("paint", skin, 0.11f, 0.5f, 0.12f, s * 0.29f, hip + 0.27f, 0);
            }
            else if (shortSleeve)
            {
                foreach (float s in new[] { -1f, 1f })
                {
                    b.Box("paint", shirt, 0.12f, 0.2f, 0.13f, s * 0.29f, hip + 0.42f, 0);
                    b.Box("paint", skin, 0.1f, 0.32f, 0.11f, s * 0.29f, hip + 0.18f, 0);
                }
            }
            else foreach (float s in new[] { -1f, 1f }) b.Box("paint", shirt, 0.11f, 0.5f, 0.12f, s * 0.29f, hip + 0.27f, 0);
            b.Sphere("paint", skin, 0.16f, 0, hip + 0.72f, 0, 8);
            b.Sphere("paint", hair, 0.165f, 0, hip + 0.78f, -0.03f, 8, 1, 0.7f, 1);
        }
    }
}
