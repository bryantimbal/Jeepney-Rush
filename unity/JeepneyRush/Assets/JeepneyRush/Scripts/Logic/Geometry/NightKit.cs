using System;

namespace JeepneyRush.Logic.Geometry
{
    /// <summary>
    /// Night-time dressing for scenery chunks. Everything here uses the "nightglow" / "lightpool"
    /// materials, which are invisible by day and fade in after sunset, so the same chunk works
    /// at noon and at midnight without real-time lights.
    /// </summary>
    public static class NightKit
    {
        private static readonly string[] Warm = { "#FFD98A", "#FFE4A3", "#FFC870", "#FFF0C8" };
        private static readonly string[] Cool = { "#CFE8FF", "#E8F4FF", "#BFE3FF" };

        /// <summary>
        /// Lit windows on the street-facing side of a facade box (lined up with the facade
        /// texture: 3 m window cells, 6 m tile). <paramref name="faceX"/> is the face's X,
        /// <paramref name="facingPlusX"/> its direction; the ground floor is skipped (shops).
        /// </summary>
        public static void Windows(MeshBuilder b, GameRandom rng, float faceX, bool facingPlusX, float zCenter, float length, float yBottom, float height, float litChance = 0.55f)
        {
            float x = faceX + (facingPlusX ? 0.03f : -0.03f);
            int cols = (int)Math.Floor(length / 3f), rows = (int)Math.Floor(height / 3f);
            for (int j = 0; j < cols; j++)
                for (int k = 1; k < rows; k++)
                {
                    if (j % 2 == 1 && k % 2 == 0) continue; // roll-up shutter cell in the facade texture
                    if (!rng.Chance(litChance)) continue;
                    float u0 = 3 * j + 0.5625f, u1 = 3 * j + 2.4375f;
                    float y0 = yBottom + 3 * k + 0.5625f, y1 = yBottom + 3 * k + 2.32f;
                    string col = rng.Chance(0.72) ? rng.Pick(Warm) : rng.Pick(Cool);
                    if (facingPlusX)
                    {
                        float z0 = zCenter - length / 2;
                        b.QuadColor("nightglow", col, new V3(x, y0, z0 + u0), new V3(x, y1, z0 + u0), new V3(x, y1, z0 + u1), new V3(x, y0, z0 + u1), new V3(1, 0, 0));
                    }
                    else
                    {
                        float z1 = zCenter + length / 2;
                        b.QuadColor("nightglow", col, new V3(x, y0, z1 - u0), new V3(x, y1, z1 - u0), new V3(x, y1, z1 - u1), new V3(x, y0, z1 - u1), new V3(-1, 0, 0));
                    }
                }
        }

        /// <summary>Simple glowing window panes on any wall (huts, houses, malls): a grid of small quads.</summary>
        public static void WindowGrid(MeshBuilder b, GameRandom rng, float faceX, bool facingPlusX, float zCenter, float length, float y0, float y1, float cell = 1.6f, float litChance = 0.6f)
        {
            float x = faceX + (facingPlusX ? 0.03f : -0.03f);
            for (float z = zCenter - length / 2 + cell * 0.3f; z + cell * 0.5f < zCenter + length / 2; z += cell)
                for (float y = y0; y + cell * 0.5f < y1; y += cell)
                {
                    if (!rng.Chance(litChance)) continue;
                    string col = rng.Chance(0.75) ? rng.Pick(Warm) : rng.Pick(Cool);
                    float za = z, zb = z + cell * 0.55f, ya = y, yb = y + cell * 0.5f;
                    if (facingPlusX) b.QuadColor("nightglow", col, new V3(x, ya, za), new V3(x, yb, za), new V3(x, yb, zb), new V3(x, ya, zb), new V3(1, 0, 0));
                    else b.QuadColor("nightglow", col, new V3(x, ya, zb), new V3(x, yb, zb), new V3(x, yb, za), new V3(x, ya, za), new V3(-1, 0, 0));
                }
        }

        /// <summary>Soft pool of street light on the ground (radius in metres).</summary>
        public static void Pool(MeshBuilder b, float x, float y, float z, float radius)
        {
            b.Quad("lightpool", new V3(x - radius, y, z - radius), new V3(x - radius, y, z + radius), new V3(x + radius, y, z + radius), new V3(x + radius, y, z - radius), V3.Up,
                new V2(0, 0), new V2(0, 1), new V2(1, 1), new V2(1, 0));
        }

        /// <summary>Street lamp on the curb leaning over the road, with its bulb and light pool.</summary>
        public static void StreetLamp(MeshBuilder b, float side, float roadHalf, float z, float height = 6f, string pole = "#495057")
        {
            float px = side * (roadHalf + 0.5f);
            b.Cylinder("paint", pole, 0.07f, 0.09f, height, 6, px, height / 2, z);
            b.Box("paint", pole, 1.6f, 0.08f, 0.1f, side * (roadHalf - 0.2f), height, z);
            b.Box("glow", "#FFF1C1", 0.5f, 0.1f, 0.3f, side * (roadHalf - 0.8f), height - 0.07f, z);
            Pool(b, side * (roadHalf - 1.2f), 0.035f, z, 3.6f);
            LampHaze(b, side * (roadHalf - 0.8f), height - 0.1f, z);
        }

        /// <summary>Cone of misty light under a street lamp – only visible on rainy nights (see MaterialLibrary.SetHaze).</summary>
        public static void LampHaze(MeshBuilder b, float x, float top, float z)
        {
            // pointing straight down: rotate the cone's +Y axis to −Y
            LifeBuilder.HazeCone(b, new V3(x, top, z), new Rot((float)Math.PI, 0, 0), top, 0.2f, 2.0f, 4);
        }
    }
}
