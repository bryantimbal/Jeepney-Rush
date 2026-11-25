using System;

namespace JeepneyRush.Logic.Geometry
{
    /// <summary>
    /// The garage where you choose your jeepney: a typical Filipino talyer (jeepney workshop) –
    /// hollow-block walls in two-tone paint, corrugated GI sheet sides and roof on steel trusses,
    /// a tarpaulin sign, fluorescent tubes, stacked tyres, oil drums, a workbench and a red tool
    /// cabinet, monobloc chairs with a stand fan, a Santo Niño altar with its red light, and an
    /// oil-stained concrete floor. The jeepney is parked at the origin (facing +Z), the open front
    /// (garage door) is toward -Z where the camera stands.
    /// </summary>
    public static class GarageBuilder
    {
        public const float HalfWidth = 7.2f, Back = 8.5f, Front = -6.5f, Height = 5.6f;

        public static MeshData Build()
        {
            var b = new MeshBuilder();
            var rng = new GameRandom(77);
            float depth = Back - Front, cz = (Back + Front) / 2;

            // ---- floor: concrete slab, oil stains, painted bay lines, a drain grate
            b.Box("paint", "#8C8A84", HalfWidth * 2 + 8, 0.1f, depth + 10, 0, -0.05f, cz - 3);
            for (int i = 0; i < 7; i++)
                b.Cylinder("paint", i % 2 == 0 ? "#4A4844" : "#5C5A55", rng.Range(0.4f, 1.1f), rng.Range(0.4f, 1.1f), 0.012f, 12, rng.Range(-2.5f, 2.5f), 0.006f, rng.Range(-3f, 3f));
            foreach (float x in new[] { -2.4f, 2.4f }) b.Box("paint", "#F2C200", 0.14f, 0.012f, 9f, x, 0.008f, 0.3f);
            b.Box("paint", "#3A3A3A", 0.6f, 0.014f, 3.2f, 3.6f, 0.008f, -1.5f);
            for (float z = -2.9f; z < 0f; z += 0.25f) b.Box("paint", "#222222", 0.62f, 0.016f, 0.05f, 3.6f, 0.01f, z);

            // ---- back wall: hollow blocks, teal below / cream above, block lines
            b.Box("paint", "#3E8E89", HalfWidth * 2, 1.4f, 0.3f, 0, 0.7f, Back);
            b.Box("paint", "#EFE6CF", HalfWidth * 2, Height - 1.4f, 0.3f, 0, 1.4f + (Height - 1.4f) / 2, Back);
            for (float y = 0.4f; y < Height; y += 0.4f) b.Box("paint", "#C9C0A8", HalfWidth * 2, 0.02f, 0.02f, 0, y, Back - 0.16f);
            b.Box("paint", "#2E6E6A", HalfWidth * 2, 0.1f, 0.05f, 0, 1.4f, Back - 0.17f);
            // tarpaulin sign across the back wall (faces the camera, -Z)
            Quad(b, "tex:garage_tarp", 0, 3.45f, Back - 0.17f, 8.8f, 2.2f);
            // "BAWAL MANIGARILYO" sign and a calendar
            Quad(b, "tex:garage_nosmoking", -5.7f, 3.75f, Back - 0.17f, 1.5f, 0.75f);
            b.Box("paint", "#FFFFFF", 0.9f, 1.25f, 0.03f, 5.4f, 2.6f, Back - 0.17f);
            b.Box("paint", "#D62828", 0.9f, 0.35f, 0.035f, 5.4f, 3.05f, Back - 0.18f);
            for (int r = 0; r < 4; r++) for (int c = 0; c < 5; c++) b.Box("paint", "#9A9A9A", 0.12f, 0.12f, 0.036f, 5.08f + c * 0.16f, 2.75f - r * 0.18f, Back - 0.18f);
            // Santo Niño altar shelf with a red votive light
            b.Box("paint", "#6B4F35", 1.2f, 0.08f, 0.45f, -5.7f, 2.55f, Back - 0.35f);
            b.Box("paint", "#8B1E2D", 0.28f, 0.55f, 0.2f, -5.7f, 2.33f, Back - 0.3f);   // robe
            b.Sphere("paint", "#C68B59", 0.11f, -5.7f, 2.7f, Back - 0.3f, 8);
            b.Cylinder("gold", "#D4AF37", 0.13f, 0.13f, 0.05f, 10, -5.7f, 2.87f, Back - 0.3f); // crown
            b.Cylinder("glow", "#FF2A2A", 0.06f, 0.06f, 0.12f, 8, -5.35f, 2.11f, Back - 0.3f);
            b.Cylinder("glow", "#FF2A2A", 0.06f, 0.06f, 0.12f, 8, -6.05f, 2.11f, Back - 0.3f);
            // wall clock
            b.Cylinder("paint", "#FFFFFF", 0.35f, 0.35f, 0.05f, 16, 2.4f, 4.9f, Back - 0.17f, new Rot((float)(Math.PI / 2), 0, 0));
            b.Box("paint", "#111111", 0.03f, 0.24f, 0.02f, 2.4f, 4.98f, Back - 0.2f);
            b.Box("paint", "#111111", 0.18f, 0.03f, 0.02f, 2.47f, 4.9f, Back - 0.2f);

            // ---- side walls: corrugated GI sheets (ridges) on a hollow-block base
            foreach (float s in new[] { -1f, 1f })
            {
                float x = s * HalfWidth;
                b.Box("paint", "#B9B3A3", 0.3f, 1.2f, depth, x, 0.6f, cz);
                b.Box("paint", "#9FA4A8", 0.06f, Height - 1.2f, depth, x, 1.2f + (Height - 1.2f) / 2, cz);
                for (float z = Front + 0.2f; z < Back; z += 0.35f) b.Box("chrome", "#B7BCC0", 0.08f, Height - 1.2f, 0.06f, x - s * 0.05f, 1.2f + (Height - 1.2f) / 2, z);
                // rust streaks
                for (int i = 0; i < 4; i++) b.Box("paint", "#8D5A3B", 0.07f, rng.Range(0.6f, 1.6f), 0.12f, x - s * 0.08f, rng.Range(2.4f, 4.6f), rng.Range(Front + 1, Back - 1));
                // front posts and the rolled-up door drum
                b.Box("paint", "#5C5F66", 0.25f, Height, 0.25f, x - s * 0.1f, Height / 2, Front);
            }
            b.CylinderX("paint", "#7C8187", 0.35f, HalfWidth * 2, 14, 0, Height - 0.35f, Front + 0.1f, true);
            b.Box("paint", "#5C5F66", HalfWidth * 2, 0.18f, 0.25f, 0, Height - 0.82f, Front);

            // ---- roof: steel trusses + corrugated sheets (seen from below)
            for (float z = Front + 1f; z < Back; z += 3f)
            {
                b.Box("paint", "#4B4F56", HalfWidth * 2, 0.12f, 0.12f, 0, Height - 0.1f, z);
                b.Box("paint", "#4B4F56", HalfWidth * 2, 0.08f, 0.08f, 0, Height + 0.55f, z);
                for (float x = -HalfWidth + 1; x < HalfWidth; x += 2f) b.Box("paint", "#4B4F56", 0.06f, 0.72f, 0.06f, x, Height + 0.24f, z, new Rot(0, 0, x < 0 ? 0.6f : -0.6f));
            }
            foreach (float s in new[] { -1f, 1f })
                b.Box("paint", "#8E949A", HalfWidth + 0.4f, 0.05f, depth + 0.4f, s * HalfWidth / 2, Height + 0.45f, cz, new Rot(0, 0, s * -0.12f));
            // fluorescent tubes on chains + one caged bulb
            foreach (float z in new[] { -1.5f, 3.5f })
                foreach (float x in new[] { -2.6f, 2.6f })
                {
                    b.Box("paint", "#2F2F2F", 0.02f, 0.9f, 0.02f, x, Height - 0.55f, z);
                    b.Box("paint", "#D9D9D9", 1.4f, 0.08f, 0.18f, x, Height - 1.05f, z);
                    b.Box("glow", "#F4FBFF", 1.3f, 0.06f, 0.08f, x, Height - 1.11f, z);
                }
            b.Sphere("glow", "#FFE2A8", 0.13f, 0, Height - 0.9f, 6.5f, 8);

            // ---- left: tyre stacks, oil drums, batteries, a spare jeepney hood being painted
            Tires(b, -5.9f, 6.6f, 5);
            Tires(b, -6.0f, 5.2f, 3);
            Tires(b, -5.2f, 6.9f, 2);
            Drum(b, -6.1f, 3.4f, "#1D4ED8");
            Drum(b, -6.1f, 2.5f, "#D62828");
            b.Cylinder("paint", "#2E7D32", 0.24f, 0.24f, 0.45f, 10, -5.4f, 0.22f, 3.0f); // jerry can (rounded)
            for (int i = 0; i < 3; i++) b.Box("paint", "#222222", 0.32f, 0.22f, 0.2f, -5.9f, 0.11f + i * 0.23f, 0.9f);
            b.Box("paint", "#F2F2F2", 1.7f, 0.08f, 1.2f, -5.6f, 0.95f, -1.4f, new Rot(0.15f, 0.4f, 1.2f)); // spare hood leaning on the wall
            b.Box("paint", "#C62828", 1.0f, 0.085f, 0.5f, -5.58f, 0.96f, -1.4f, new Rot(0.15f, 0.4f, 1.2f));

            // ---- right: workbench with vise and tools, red tool cabinet, welding tank, jack
            b.Box("paint", "#7A5634", 2.6f, 0.1f, 0.9f, 5.9f, 1.0f, 5.6f);
            foreach (float dx in new[] { -1.15f, 1.15f }) foreach (float dz in new[] { -0.35f, 0.35f }) b.Box("paint", "#5C4027", 0.08f, 1.0f, 0.08f, 5.9f + dx, 0.5f, 5.6f + dz);
            b.Box("paint", "#37474F", 0.25f, 0.2f, 0.25f, 5.1f, 1.15f, 5.6f);            // vise
            for (int i = 0; i < 5; i++) b.Box("chrome", "#B0B6BC", 0.04f, 0.35f, 0.03f, 5.3f + i * 0.28f, 1.9f, Back - 0.2f); // tools on pegboard
            b.Box("paint", "#6B5B45", 2.2f, 1.0f, 0.04f, 5.9f, 1.95f, Back - 0.17f);   // pegboard
            b.Box("paint", "#C62828", 1.0f, 1.5f, 0.6f, 6.4f, 0.75f, 3.6f);           // tool cabinet
            for (int i = 0; i < 4; i++) b.Box("chrome", "#E0E0E0", 0.7f, 0.03f, 0.02f, 6.1f, 0.4f + i * 0.32f, 3.29f);
            b.Cylinder("paint", "#2E7D32", 0.18f, 0.18f, 1.3f, 10, 6.6f, 0.65f, 2.2f); // oxygen
            b.Cylinder("paint", "#B71C1C", 0.18f, 0.18f, 1.2f, 10, 6.3f, 0.6f, 1.9f);  // acetylene
            b.Box("paint", "#D62828", 0.3f, 0.18f, 1.1f, 4.8f, 0.09f, -0.6f);          // floor jack
            b.Box("chrome", "#9E9E9E", 0.05f, 0.05f, 1.0f, 4.8f, 0.35f, -1.3f, new Rot(0.5f, 0, 0));

            // ---- front left: monobloc chairs, a small table with softdrinks, the stand fan
            Monobloc(b, -4.6f, -3.6f, 0.5f);
            Monobloc(b, -3.4f, -4.5f, -0.3f);
            b.Cylinder("paint", "#F2F2F2", 0.45f, 0.45f, 0.05f, 12, -4.0f, 0.72f, -4.2f);
            b.Cylinder("paint", "#F2F2F2", 0.05f, 0.25f, 0.7f, 8, -4.0f, 0.35f, -4.2f);
            for (int i = 0; i < 3; i++) b.Cylinder("glass", "#3B1F14", 0.04f, 0.04f, 0.22f, 8, -4.1f + i * 0.1f, 0.86f, -4.2f + (i - 1) * 0.08f);
            b.Box("paint", "#C62828", 0.45f, 0.28f, 0.32f, -3.4f, 0.14f, -3.4f);       // softdrinks crate
            b.Cylinder("paint", "#9E9E9E", 0.04f, 0.2f, 1.2f, 8, -6.2f, 0.6f, -4.6f);  // stand fan
            b.Cylinder("paint", "#1565C0", 0.3f, 0.3f, 0.12f, 14, -6.2f, 1.3f, -4.5f, new Rot((float)(Math.PI / 2), 0.6f, 0));

            // ---- outside the open front: a bit of street so the doorway isn't empty
            b.Box("paint", "#45474E", 30f, 0.08f, 6f, 0, -0.02f, Front - 4f);
            return b.Data;
        }

        private static void Quad(MeshBuilder b, string key, float cx, float cy, float z, float w, float h)
        {
            float x0 = cx - w / 2, x1 = cx + w / 2, y0 = cy - h / 2, y1 = cy + h / 2;
            b.Quad(key, new V3(x0, y0, z), new V3(x1, y0, z), new V3(x1, y1, z), new V3(x0, y1, z), new V3(0, 0, -1), new V2(0, 0), new V2(1, 0), new V2(1, 1), new V2(0, 1));
        }

        private static void Tires(MeshBuilder b, float x, float z, int n)
        {
            for (int i = 0; i < n; i++)
            {
                b.Cylinder("paint", "#1E1E1E", 0.48f, 0.48f, 0.26f, 14, x, 0.13f + i * 0.27f, z);
                b.Cylinder("paint", "#3A3A3A", 0.24f, 0.24f, 0.27f, 10, x, 0.13f + i * 0.27f, z);
            }
        }

        private static void Drum(MeshBuilder b, float x, float z, string color)
        {
            b.Cylinder("paint", color, 0.32f, 0.32f, 0.95f, 14, x, 0.475f, z);
            foreach (float y in new[] { 0.3f, 0.65f }) b.Cylinder("paint", "#2A2A2A", 0.33f, 0.33f, 0.04f, 14, x, y, z);
        }

        private static void Monobloc(MeshBuilder b, float x, float z, float yaw)
        {
            b.Push(new V3(x, 0, z), new Rot(0, yaw, 0));
            b.Box("paint", "#F5F5F5", 0.48f, 0.05f, 0.46f, 0, 0.44f, 0);
            b.Box("paint", "#F5F5F5", 0.48f, 0.5f, 0.05f, 0, 0.72f, 0.22f, new Rot(-0.12f, 0, 0));
            foreach (float dx in new[] { -0.2f, 0.2f }) foreach (float dz in new[] { -0.2f, 0.2f }) b.Box("paint", "#ECECEC", 0.05f, 0.44f, 0.05f, dx, 0.22f, dz);
            b.Pop();
        }
    }
}
