using System;

namespace JeepneyRush.Logic.Geometry
{
    /// <summary>Kinds of Filipino street vendors ("tindero sa kalye").</summary>
    public enum VendorKind { Fishball, Taho, Sorbetes, Balut, BananaCue, Fruit }

    /// <summary>
    /// Street vendors and the corner tricycle terminal ("paradahan ng tricycle", run by the
    /// local TODA). Everything is baked into scenery / intersection meshes. Vendors stand at
    /// the building line facing the road, so the sidewalk in front of them stays walkable.
    /// </summary>
    public static class VendorBuilder
    {
        private const float HalfPi = (float)(Math.PI / 2);
        private static readonly string[] Umbrellas = { "#EF476F", "#FFD166", "#06D6A0", "#118AB2", "#F78C6B", "#E63946" };
        public static readonly string[] TrikeColors = { "#1D4ED8", "#D62828", "#2A9D8F", "#F4A261", "#7B2CBF", "#0B6E4F" };

        /// <summary>Distance from the road centre of a vendor's cart (just inside the building line).</summary>
        public static float VendorX => SceneryBuilder.SidewalkOuter - 0.7f;

        /// <summary>Vendors that fit the route (strawberry taho in Baguio, buko at the beach, ...).</summary>
        public static VendorKind[] KindsFor(EnvironmentKind env)
        {
            switch (env)
            {
                case EnvironmentKind.Beach: return new[] { VendorKind.Fruit, VendorKind.Fruit, VendorKind.Sorbetes, VendorKind.Fishball, VendorKind.BananaCue };
                case EnvironmentKind.Baguio: return new[] { VendorKind.Taho, VendorKind.Fruit, VendorKind.Balut, VendorKind.BananaCue };
                case EnvironmentKind.Mountain: return new[] { VendorKind.Fruit, VendorKind.Balut, VendorKind.BananaCue };
                case EnvironmentKind.Provincial: return new[] { VendorKind.BananaCue, VendorKind.Balut, VendorKind.Fruit, VendorKind.Taho, VendorKind.Sorbetes };
                default: return new[] { VendorKind.Fishball, VendorKind.Fishball, VendorKind.Taho, VendorKind.Sorbetes, VendorKind.Balut, VendorKind.BananaCue, VendorKind.Fruit };
            }
        }

        /// <summary>A random route-appropriate vendor on <paramref name="side"/> at <paramref name="z"/>.</summary>
        public static void Random(MeshBuilder b, GameRandom rng, EnvironmentKind env, float side, float y, float z)
        {
            var kinds = KindsFor(env);
            Vendor(b, kinds[rng.Int(0, kinds.Length - 1)], side * VendorX, y, z, side, rng.Int(0, 99), env);
        }

        /// <summary>
        /// One vendor at (x, y, z). Built in a local frame whose +Z (the customer side) is turned
        /// toward the road, so the vendor stands behind the cart with the shops at their back.
        /// </summary>
        public static void Vendor(MeshBuilder b, VendorKind kind, float x, float y, float z, float side, int variant, EnvironmentKind env = EnvironmentKind.Manila)
        {
            b.Push(new V3(x, y, z), new Rot(0, -side * HalfPi, 0));
            switch (kind)
            {
                case VendorKind.Fishball: Fishball(b, variant); break;
                case VendorKind.Taho: Taho(b, variant, env == EnvironmentKind.Baguio); break;
                case VendorKind.Sorbetes: Sorbetes(b, variant); break;
                case VendorKind.Balut: Balut(b, variant); break;
                case VendorKind.BananaCue: BananaCue(b, variant); break;
                default: Fruit(b, variant, env); break;
            }
            b.Pop();
        }

        private static void Person(MeshBuilder b, int i, float x, float y, float z, bool seated = false)
        {
            b.Push(new V3(x, y, z), Rot.None);
            PropBuilder.AddPerson(b, i, seated);
            b.Pop();
        }

        private static void Umbrella(MeshBuilder b, string color, float x, float z, float height = 2.4f, float radius = 1.25f)
        {
            b.Cylinder("paint", "#333333", 0.03f, 0.03f, height - 0.3f, 5, x, (height - 0.3f) / 2f, z);
            b.Cylinder("paint", color, 0.05f, radius, 0.45f, 8, x, height, z);
            b.Cylinder("paint", "#FFFFFF", radius * 0.98f, radius, 0.1f, 8, x, height - 0.25f, z); // white trim
            // a bare bulb under the umbrella, lit after dusk
            b.Sphere("nightglow", "#FFE3A0", 0.08f, x, height - 0.45f, z, 6);
            NightKit.Pool(b, x, 0.02f, z + 0.4f, 1.8f);
        }

        /// <summary>Fishball / kikiam / kwek-kwek cart with the frying pan and sauce jars.</summary>
        private static void Fishball(MeshBuilder b, int v)
        {
            string body = new[] { "#C1440E", "#2A9D8F", "#E9C46A", "#457B9D" }[v % 4];
            b.Box("paint", body, 1.45f, 0.75f, 0.72f, 0, 0.6f, 0);
            b.Box("paint", "#FFFFFF", 1.47f, 0.1f, 0.74f, 0, 0.45f, 0);
            b.Box("chrome", "#C0C0C0", 1.5f, 0.05f, 0.78f, 0, 1.0f, 0);
            foreach (float wx in new[] { -0.55f, 0.55f }) b.CylinderX("paint", "#222222", 0.22f, 0.08f, 8, wx, 0.22f, -0.38f);
            b.Box("paint", "#555555", 0.06f, 0.2f, 0.06f, 0.65f, 0.12f, 0.25f); // stand leg
            // wok of bubbling oil with fishballs (orange-brown) and kwek-kwek (bright orange)
            b.Cylinder("paint", "#1E1E1E", 0.34f, 0.24f, 0.14f, 12, -0.32f, 1.1f, 0);
            b.Cylinder("paint", "#C98B2B", 0.3f, 0.3f, 0.02f, 12, -0.32f, 1.16f, 0);
            for (int k = 0; k < 7; k++)
            {
                double a = k * 0.9 + v;
                float r = 0.08f + (k % 3) * 0.06f;
                b.Sphere("paint", k % 3 == 0 ? "#FF7A1A" : "#B5762A", 0.045f, -0.32f + (float)Math.Cos(a) * r, 1.18f, (float)Math.Sin(a) * r, 5);
            }
            // sauce jars (sweet, spicy, vinegar) and the cup of skewers
            string[] sauces = { "#5C3317", "#C1121F", "#E6E2C3" };
            for (int k = 0; k < 3; k++) b.Cylinder("glass", sauces[k], 0.055f, 0.055f, 0.2f, 8, 0.2f + k * 0.15f, 1.12f, 0.22f);
            b.Cylinder("paint", "#E5E5E5", 0.06f, 0.06f, 0.14f, 8, 0.55f, 1.1f, -0.15f);
            for (int k = 0; k < 4; k++) b.Box("paint", "#D9B26F", 0.012f, 0.3f, 0.012f, 0.53f + (k % 2) * 0.04f, 1.25f, -0.17f + (k / 2) * 0.04f);
            Umbrella(b, Umbrellas[v % Umbrellas.Length], 0, -0.1f);
            Person(b, v + 1, 0.1f, 0, -0.75f);
            if (v % 3 == 0) Person(b, v + 4, 0.95f, 0, 0.3f); // a customer with a stick
        }

        /// <summary>Taho vendor: two aluminium buckets hanging from a bamboo yoke on the shoulder.</summary>
        private static void Taho(MeshBuilder b, int v, bool strawberry)
        {
            // walking along the sidewalk (buckets front and back), calling "Tahoooo!"
            b.Push(new V3(0, 0, -0.2f), new Rot(0, HalfPi, 0));
            Person(b, v + 2, 0, 0, 0);
            b.Box("paint", "#B08D57", 0.06f, 0.06f, 1.9f, 0.18f, 1.36f, 0);
            foreach (float bz in new[] { -0.82f, 0.82f })
            {
                foreach (float sx in new[] { -0.15f, 0.15f }) b.Box("paint", "#2B2B2B", 0.012f, 0.62f, 0.012f, 0.18f + sx, 1.05f, bz);
                b.Cylinder("chrome", "#C9CED6", 0.21f, 0.18f, 0.48f, 10, 0.18f, 0.5f, bz);
                b.Cylinder("chrome", "#E1E5EA", 0.22f, 0.22f, 0.04f, 10, 0.18f, 0.76f, bz);
                b.Sphere("chrome", "#E1E5EA", 0.04f, 0.18f, 0.8f, bz, 5);
            }
            // a cup of taho (or Baguio's strawberry taho) in hand
            b.Cylinder("glass", strawberry ? "#E5383B" : "#F2E8CF", 0.05f, 0.04f, 0.12f, 8, -0.3f, 1.05f, 0.18f);
            b.Pop();
        }

        /// <summary>Sorbetes ("dirty ice cream") cart: bright painted box, three tubs and a bell.</summary>
        private static void Sorbetes(MeshBuilder b, int v)
        {
            b.Box("paint", "#E63946", 1.35f, 0.7f, 0.7f, 0, 0.62f, 0);
            string[] stripes = { "#FFD166", "#06D6A0", "#1D4ED8" };
            for (int k = 0; k < 3; k++) b.Box("paint", stripes[(k + v) % 3], 1.37f, 0.1f, 0.72f, 0, 0.42f + k * 0.2f, 0);
            string[] flavours = { "#5C3317", "#F7E7CE", "#7B2CBF" }; // chocolate, cheese, ube
            for (int k = 0; k < 3; k++)
            {
                float tx = -0.42f + k * 0.42f;
                b.Cylinder("chrome", "#D9DDE2", 0.17f, 0.17f, 0.12f, 10, tx, 1.03f, 0);
                b.Sphere("paint", flavours[k], 0.05f, tx, 1.12f, 0, 5);
            }
            // little boat-shaped roof on four posts
            foreach (float px in new[] { -0.62f, 0.62f }) foreach (float pz in new[] { -0.3f, 0.3f }) b.Box("paint", "#EEEEEE", 0.04f, 0.7f, 0.04f, px, 1.32f, pz);
            b.Box("paint", "#FFD166", 1.5f, 0.08f, 0.85f, 0, 1.7f, 0);
            b.Box("paint", "#06D6A0", 1.55f, 0.14f, 0.06f, 0, 1.62f, 0.42f);
            b.Box("paint", "#EF476F", 1.55f, 0.14f, 0.06f, 0, 1.62f, -0.42f);
            b.Sphere("nightglow", "#FFE3A0", 0.06f, 0, 1.6f, 0, 6);
            b.Sphere("gold", "#FFC928", 0.07f, 0.55f, 1.15f, 0.3f, 6); // hand bell
            foreach (float wx in new[] { -0.6f, 0.6f }) b.CylinderX("paint", "#222222", 0.26f, 0.08f, 10, wx, 0.26f, -0.2f);
            b.Box("chrome", "#C0C0C0", 0.05f, 0.05f, 0.5f, -0.75f, 0.95f, -0.5f); // push handle
            Person(b, v + 3, -0.2f, 0, -0.8f);
            NightKit.Pool(b, 0, 0.02f, 0.4f, 1.4f);
        }

        /// <summary>Balut / penoy vendor on a stool with the woven basket and a little lamp.</summary>
        private static void Balut(MeshBuilder b, int v)
        {
            b.Box("paint", "#6B4226", 0.4f, 0.45f, 0.4f, 0, 0.22f, -0.45f);
            Person(b, v + 5, 0, 0.45f, -0.45f, true);
            b.Cylinder("paint", "#A0703C", 0.34f, 0.27f, 0.42f, 10, 0, 0.21f, 0.25f);
            b.Cylinder("paint", "#8B5A2B", 0.35f, 0.35f, 0.05f, 10, 0, 0.42f, 0.25f);
            b.Box("paint", "#F1E3C8", 0.5f, 0.03f, 0.4f, 0, 0.45f, 0.25f); // cloth keeping the eggs warm
            for (int k = 0; k < 4; k++) b.Sphere("paint", "#F3E6CF", 0.05f, -0.12f + k * 0.08f, 0.5f, 0.3f + (k % 2) * 0.06f, 5, 1f, 1.25f, 1f);
            // salt / vinegar bottle and a kerosene lamp (lit at night)
            b.Cylinder("glass", "#E6E2C3", 0.04f, 0.04f, 0.16f, 6, 0.3f, 0.52f, 0.12f);
            b.Cylinder("paint", "#3A3A3A", 0.05f, 0.06f, 0.08f, 6, -0.28f, 0.49f, 0.12f);
            b.Sphere("nightglow", "#FFC870", 0.07f, -0.28f, 0.6f, 0.12f, 6);
            NightKit.Pool(b, 0, 0.02f, 0.3f, 1.2f);
        }

        /// <summary>Banana-cue / isaw grill: charcoal embers glowing under rows of skewers.</summary>
        private static void BananaCue(MeshBuilder b, int v)
        {
            // table with the cooked sticks, and the grill on its own stand in front
            b.Box("paint", "#6B4226", 1.3f, 0.06f, 0.6f, 0, 0.8f, -0.25f);
            foreach (float lx in new[] { -0.58f, 0.58f }) b.Box("paint", "#5A3820", 0.06f, 0.8f, 0.06f, lx, 0.4f, -0.25f);
            b.Box("paint", "#2A2A2A", 1.0f, 0.22f, 0.36f, 0, 0.85f, 0.25f);
            b.Box("paint", "#3A3A3A", 0.9f, 0.6f, 0.06f, 0, 0.45f, 0.25f);
            b.Box("glow", "#FF5A1F", 0.9f, 0.02f, 0.28f, 0, 0.97f, 0.25f);
            bool isaw = v % 2 == 1;
            for (int k = 0; k < 7; k++)
            {
                float sx = -0.38f + k * 0.125f;
                b.Box("paint", "#D9B26F", 0.012f, 0.012f, 0.5f, sx, 1.02f, 0.25f);
                if (isaw) for (int s = 0; s < 4; s++) b.Box("paint", "#A0522D", 0.04f, 0.04f, 0.07f, sx, 1.03f, 0.1f + s * 0.09f);
                else for (int s = 0; s < 2; s++) b.Box("paint", "#C46A1C", 0.06f, 0.05f, 0.13f, sx, 1.04f, 0.15f + s * 0.17f);
            }
            for (int k = 0; k < 5; k++) b.Box("paint", "#B5651D", 0.06f, 0.05f, 0.14f, -0.4f + k * 0.12f, 0.86f, -0.3f);
            b.Cylinder("paint", "#F7F3E3", 0.12f, 0.12f, 0.1f, 8, 0.45f, 0.88f, -0.3f); // dipping sauce
            Umbrella(b, Umbrellas[(v + 2) % Umbrellas.Length], -0.2f, -0.3f);
            Person(b, v + 6, 0.15f, 0, -0.85f);
        }

        /// <summary>Fruit stand: mangoes and bananas, buko at the beach, strawberries in Baguio.</summary>
        private static void Fruit(MeshBuilder b, int v, EnvironmentKind env)
        {
            b.Box("paint", "#8D6E4F", 1.6f, 0.75f, 0.8f, 0, 0.38f, 0);
            b.Box("paint", "#6B4F35", 1.62f, 0.06f, 0.82f, 0, 0.78f, 0);
            if (env == EnvironmentKind.Beach)
            {
                // young coconuts ("buko") with their tops cut, ready for a straw
                for (int k = 0; k < 6; k++) b.Sphere("paint", "#5E8C31", 0.16f, -0.55f + (k % 3) * 0.55f, 0.95f, (k / 3 - 0.5f) * 0.38f, 7);
                b.Cylinder("paint", "#F2E8CF", 0.07f, 0.07f, 0.05f, 8, 0, 1.12f, 0.19f);
            }
            else if (env == EnvironmentKind.Baguio)
            {
                // strawberry baskets
                for (int k = 0; k < 6; k++)
                {
                    float fx = -0.55f + (k % 3) * 0.55f, fz = (k / 3 - 0.5f) * 0.38f;
                    b.Box("paint", "#2E7D32", 0.36f, 0.12f, 0.28f, fx, 0.87f, fz);
                    for (int s = 0; s < 4; s++) b.Sphere("paint", "#E5383B", 0.05f, fx - 0.08f + (s % 2) * 0.16f, 0.96f, fz - 0.05f + (s / 2) * 0.1f, 5);
                }
            }
            else
            {
                for (int k = 0; k < 8; k++) b.Sphere("paint", k % 4 == 0 ? "#9BC53D" : "#FFC53D", 0.09f, -0.6f + (k % 4) * 0.18f, 0.88f, (k / 4 - 0.5f) * 0.3f, 6, 1.3f, 1f, 1f); // mangoes
                for (int k = 0; k < 5; k++) b.Box("paint", "#F6D743", 0.05f, 0.05f, 0.3f, 0.25f + k * 0.08f, 0.88f, 0, new Rot(0, 0, 0.3f)); // bananas
                b.Box("paint", "#6B4226", 0.04f, 0.3f, 0.04f, 0.55f, 0.95f, 0.15f);
            }
            b.Box("paint", Umbrellas[v % Umbrellas.Length], 2.0f, 0.06f, 1.4f, 0, 2.2f, -0.1f); // tarp roof
            foreach (float px in new[] { -0.9f, 0.9f }) b.Box("paint", "#5B4331", 0.06f, 2.2f, 0.06f, px, 1.1f, -0.65f);
            b.Sphere("nightglow", "#FFE3A0", 0.07f, 0, 2.08f, -0.1f, 6);
            NightKit.Pool(b, 0, 0.02f, 0.4f, 1.8f);
            Person(b, v + 7, 0.3f, 0, -0.85f);
        }

        // ------------------------------------------------------------------ tricycle terminal
        /// <summary>
        /// Corner tricycle terminal: a TODA sign facing the oncoming jeepney and a queue of
        /// tricycles parked on the sidewalk along the curb, drivers waiting on their bikes.
        /// Vehicles on the right side face +Z and on the left side −Z (traffic keeps right).
        /// </summary>
        /// <param name="z0">Start of the terminal (smaller z: where the sign stands).</param>
        public static void TodaTerminal(MeshBuilder b, float side, float z0, float length, int seed)
        {
            float rw = SceneryBuilder.RoadHalfWidth, sh = SceneryBuilder.SidewalkHeight;
            float cx = side * TrikeX;
            // painted bay on the sidewalk
            b.Box("paint", "#8A8F96", 2.2f, 0.012f, length, cx, sh + 0.006f, z0 + length / 2f);
            for (float z = z0 + 0.3f; z < z0 + length; z += 2.6f) b.Box("paint", "#FFD60A", 2.1f, 0.014f, 0.1f, cx, sh + 0.008f, z);
            // the sign on two posts, facing the jeepney (−Z) — u runs from −X to +X for that viewer
            const float sw = 2.4f, shh = 1.0f, sy = 2.5f;
            foreach (float px in new[] { cx - sw * 0.42f, cx + sw * 0.42f }) b.Box("paint", "#4A4E57", 0.1f, sy + shh, 0.1f, px, sh + (sy + shh) / 2f, z0);
            b.Box("paint", "#1B263B", sw + 0.14f, shh + 0.14f, 0.06f, cx, sh + sy + shh / 2f, z0 + 0.04f);
            b.Quad("utex:toda_sign", new V3(cx - sw / 2, sh + sy, z0 - 0.0f), new V3(cx + sw / 2, sh + sy, z0), new V3(cx + sw / 2, sh + sy + shh, z0), new V3(cx - sw / 2, sh + sy + shh, z0), new V3(0, 0, -1),
                new V2(0, 0), new V2(1, 0), new V2(1, 1), new V2(0, 1));
            b.Box("nightglow", "#FFF6D8", sw, 0.06f, 0.2f, cx, sh + sy + shh + 0.12f, z0 - 0.12f);
            // the queue
            var rng = new GameRandom(seed);
            int n = Math.Max(1, (int)((length - 1.2f) / 2.6f));
            for (int i = 0; i < n; i++)
            {
                float z = z0 + 1.6f + i * 2.6f;
                b.Push(new V3(cx, sh, z), new Rot(0, side > 0 ? 0f : (float)Math.PI, 0));
                VehicleBuilder.AddTricycle(b, rng.Pick(TrikeColors), VehicleBuilder.Shirts[rng.Int(0, VehicleBuilder.Shirts.Length - 1)], rng.Chance(0.5) ? "#FFD166" : "#F1FAEE");
                b.Pop();
            }
            // the dispatcher ("barker") by the sign, at the building line
            b.Push(new V3(side * (SceneryBuilder.SidewalkOuter - 0.35f), sh, z0 + 0.6f), new Rot(0, -side * HalfPi, 0));
            PropBuilder.AddPerson(b, seed % 7, false);
            b.Pop();
        }

        /// <summary>Distance from the road centre of the parked tricycles' centre line.</summary>
        public const float TrikeX = SceneryBuilder.RoadHalfWidth + 1.9f;
        /// <summary>Walkers pass the terminal along the curb at this distance from the road centre.</summary>
        public const float CurbWalkX = SceneryBuilder.RoadHalfWidth + 0.35f;
    }
}
