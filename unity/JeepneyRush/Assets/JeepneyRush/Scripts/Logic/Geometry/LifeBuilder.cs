using System;

namespace JeepneyRush.Logic.Geometry
{
    /// <summary>
    /// Small living things that make the streets feel alive: birds (body + wings built separately so the
    /// wings can flap), umbrellas for walkers in the rain, stray dogs ("askal") and chickens, and the
    /// soft light haze cones seen around headlights and street lamps on rainy nights.
    /// </summary>
    public static class LifeBuilder
    {
        /// <summary>Bird body, beak forward (+Z). <paramref name="size"/> 1 = pigeon, ~1.8 = hawk.</summary>
        public static MeshData BirdBody(string color, float size = 1f)
        {
            var b = new MeshBuilder();
            b.Push(V3.Zero, Rot.None, size);
            b.Sphere("paint", color, 0.09f, 0, 0, 0, 8, 0.9f, 0.8f, 1.6f);
            b.Sphere("paint", color, 0.055f, 0, 0.04f, 0.15f, 6);
            b.Box("paint", "#E0A100", 0.02f, 0.02f, 0.05f, 0, 0.035f, 0.21f);
            b.Box("paint", color, 0.1f, 0.012f, 0.1f, 0, 0, -0.17f, new Rot(0.1f, 0, 0)); // tail
            b.Pop();
            return b.Data;
        }

        /// <summary>One wing, hinged at the origin and reaching out along +X (mirror with a −1 X scale for the other side).</summary>
        public static MeshData BirdWing(string color, float size = 1f)
        {
            var b = new MeshBuilder();
            b.Push(V3.Zero, Rot.None, size);
            b.Box("paint", color, 0.26f, 0.012f, 0.13f, 0.13f, 0, 0);
            b.Box("paint", color, 0.14f, 0.01f, 0.08f, 0.31f, 0, -0.02f, new Rot(0, 0.2f, 0));
            b.Pop();
            return b.Data;
        }

        /// <summary>Open umbrella held overhead (handle at the origin).</summary>
        public static MeshData Umbrella(string color)
        {
            var b = new MeshBuilder();
            b.Cylinder("paint", "#333333", 0.012f, 0.012f, 0.9f, 5, 0, 0.45f, 0);
            b.Cylinder("paint", color, 0.03f, 0.55f, 0.28f, 10, 0, 0.98f, 0);
            b.Cylinder("paint", Palette.Shade(color, 0.8f), 0.56f, 0.56f, 0.02f, 10, 0, 0.84f, 0);
            return b.Data;
        }

        /// <summary>Stray dog ("askal"), facing +Z.</summary>
        public static MeshData Dog(string color)
        {
            var b = new MeshBuilder();
            b.Box("paint", color, 0.22f, 0.2f, 0.55f, 0, 0.36f, 0);
            b.Box("paint", color, 0.16f, 0.16f, 0.18f, 0, 0.5f, 0.32f);
            b.Box("paint", Palette.Shade(color, 0.7f), 0.08f, 0.07f, 0.1f, 0, 0.47f, 0.44f);          // snout
            foreach (float sx in new[] { -0.06f, 0.06f }) b.Box("paint", Palette.Shade(color, 0.8f), 0.04f, 0.08f, 0.04f, sx, 0.61f, 0.3f); // ears
            foreach (float sx in new[] { -0.08f, 0.08f }) foreach (float sz in new[] { -0.2f, 0.2f }) b.Box("paint", color, 0.05f, 0.26f, 0.05f, sx, 0.13f, sz);
            b.Box("paint", color, 0.04f, 0.04f, 0.2f, 0, 0.48f, -0.34f, new Rot(-0.6f, 0, 0));     // tail up
            return b.Data;
        }

        /// <summary>Chicken (manok), facing +Z.</summary>
        public static MeshData Chicken(string color)
        {
            var b = new MeshBuilder();
            b.Sphere("paint", color, 0.13f, 0, 0.22f, 0, 8, 0.9f, 0.9f, 1.2f);
            b.Sphere("paint", color, 0.07f, 0, 0.34f, 0.12f, 6);
            b.Box("paint", "#D62828", 0.025f, 0.05f, 0.06f, 0, 0.41f, 0.12f);   // comb
            b.Box("paint", "#E0A100", 0.03f, 0.02f, 0.05f, 0, 0.33f, 0.2f);    // beak
            b.Box("paint", "#2D6A4F", 0.03f, 0.12f, 0.08f, 0, 0.3f, -0.15f, new Rot(-0.5f, 0, 0)); // tail feathers
            foreach (float sx in new[] { -0.04f, 0.04f }) b.Box("paint", "#E0A100", 0.015f, 0.12f, 0.015f, sx, 0.06f, 0);
            return b.Data;
        }

        /// <summary>
        /// Soft cone of light hanging in the rainy air (additive "haze" material): <paramref name="segments"/>
        /// stacked frustums fading from bright at the lamp to nothing at the far end.
        /// </summary>
        public static void HazeCone(MeshBuilder b, V3 origin, Rot dir, float length, float r0, float r1, int segments = 4, int dimmer = 0)
        {
            string[] shade = { "#7A7362", "#57524A", "#38342D", "#1E1C18", "#0E0D0B" }; // dim: additive, a soft glow
            b.Push(origin, dir);
            for (int i = 0; i < segments; i++)
            {
                float t0 = i / (float)segments, t1 = (i + 1) / (float)segments;
                float ra = r0 + (r1 - r0) * t0, rb = r0 + (r1 - r0) * t1, h = length / segments;
                // Cylinder: rTop is at +Y (far end after the push rotation)
                b.Cylinder("haze", shade[Math.Min(i + dimmer, shade.Length - 1)], rb, ra, h, 12, 0, (t0 + t1) / 2f * length, 0);
            }
            b.Pop();
        }

        /// <summary>Both headlight haze cones of a jeepney, reaching ~18 m ahead and dipping to the road.</summary>
        public static MeshData HeadlightHaze()
        {
            var b = new MeshBuilder();
            foreach (float sx in new[] { -0.8f, 0.8f })
                HazeCone(b, new V3(sx, 1.15f, 3.0f), new Rot((float)(Math.PI / 2) + 0.07f, sx * 0.04f, 0), 20f, 0.18f, 2.6f, 5, 1); // one step dimmer: right in front of the camera
            return b.Data;
        }
    }
}
