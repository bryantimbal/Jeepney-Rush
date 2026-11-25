namespace JeepneyRush.Logic
{
    public enum HazardKind
    {
        // moving traffic
        Sedan, Suv, Motorcycle, Tricycle, Bus, Truck,
        // solid static obstacles (block their lane)
        Cone, Barrier, ConstructionSign, Roadblock, ParkedSedan, ParkedJeepney,
        // soft road features: drivable, but jolt / slow / soak the jeepney
        Pothole, SpeedHump, FloodPuddle, FloodWide,
        // Mountain Province: mud (soft, slows you down) and fallen rocks (solid)
        MudPatch, Rocks,
        // Filipino road events (appended: existing values never change)
        TahoVendor, Pedicab, ParadeFloat,         // slow movers (honk: taho / pedicab step aside)
        Carabao, Flagman, StalledJeepney,         // solid, stationary
        StrayDog, Basketball,                     // soft: drive over = a scare, not a crash
        BalutVendor,                              // night vendor (honk: steps aside)
    }

    public readonly struct HazardDef
    {
        public readonly float HalfWidth;
        public readonly float HalfLength;
        public readonly float Height;
        /// <summary>
        /// Soft hazards never block a lane: the jeepney drives over them (no health lost)
        /// but gets jolted (potholes, humps taken too fast) or splashed and slowed (floods).
        /// Jeepneys don't jump – there is no jumping in the game.
        /// </summary>
        public readonly bool Soft;
        public readonly float MinSpeed;
        public readonly float MaxSpeed;

        public HazardDef(float halfWidth, float halfLength, float height, bool soft, float minSpeed = 0, float maxSpeed = 0)
        {
            HalfWidth = halfWidth;
            HalfLength = halfLength;
            Height = height;
            Soft = soft;
            MinSpeed = minSpeed;
            MaxSpeed = maxSpeed;
        }

        public bool IsMoving => MaxSpeed > 0;
    }

    /// <summary>Physical dimensions of every hazard. Visual meshes and colliders are built from these.</summary>
    public static class HazardCatalog
    {
        /// <summary>Width that spans all three lanes (humps, deep floods).</summary>
        public const float FullRoadHalfWidth = 5.3f;

        public static HazardDef Get(HazardKind k)
        {
            switch (k)
            {
                case HazardKind.Sedan: return new HazardDef(0.92f, 2.2f, 1.5f, false, 5f, 8f);
                case HazardKind.Suv: return new HazardDef(1.0f, 2.4f, 1.9f, false, 5f, 8f);
                case HazardKind.Motorcycle: return new HazardDef(0.42f, 1.0f, 1.5f, false, 6f, 9f);
                case HazardKind.Tricycle: return new HazardDef(0.85f, 1.25f, 1.8f, false, 3f, 5f);
                case HazardKind.Bus: return new HazardDef(1.2f, 5.4f, 3.3f, false, 4f, 7f);
                case HazardKind.Truck: return new HazardDef(1.15f, 3.6f, 3.1f, false, 4f, 6f);
                case HazardKind.Cone: return new HazardDef(0.32f, 0.32f, 0.75f, false);
                case HazardKind.Barrier: return new HazardDef(1.2f, 0.3f, 0.9f, false);
                case HazardKind.ConstructionSign: return new HazardDef(0.7f, 0.4f, 2.0f, false);
                case HazardKind.Roadblock: return new HazardDef(1.3f, 0.45f, 2.4f, false);
                case HazardKind.ParkedSedan: return new HazardDef(0.92f, 2.2f, 1.5f, false);
                case HazardKind.ParkedJeepney: return new HazardDef(1.0f, 2.9f, 2.5f, false);
                case HazardKind.Pothole: return new HazardDef(0.95f, 0.85f, 0.3f, true);
                case HazardKind.SpeedHump: return new HazardDef(FullRoadHalfWidth, 0.45f, 0.3f, true);
                case HazardKind.FloodPuddle: return new HazardDef(1.35f, 3.2f, 0.3f, true);
                case HazardKind.FloodWide: return new HazardDef(FullRoadHalfWidth, 5.5f, 0.3f, true);
                case HazardKind.MudPatch: return new HazardDef(1.35f, 3.0f, 0.3f, true);
                case HazardKind.Rocks: return new HazardDef(1.05f, 0.95f, 1.1f, false);
                case HazardKind.TahoVendor: return new HazardDef(0.55f, 0.9f, 1.8f, false, 0.8f, 1.2f);
                case HazardKind.BalutVendor: return new HazardDef(0.55f, 0.7f, 1.8f, false, 0.8f, 1.2f);
                case HazardKind.Pedicab: return new HazardDef(0.75f, 1.2f, 1.9f, false, 2.5f, 3.5f);
                case HazardKind.ParadeFloat: return new HazardDef(1.25f, 3.6f, 3.8f, false, 1.6f, 2.4f);
                case HazardKind.Carabao: return new HazardDef(0.85f, 1.5f, 1.7f, false);
                case HazardKind.Flagman: return new HazardDef(0.4f, 0.4f, 1.9f, false);
                case HazardKind.StalledJeepney: return new HazardDef(1.0f, 2.9f, 2.5f, false);
                case HazardKind.StrayDog: return new HazardDef(0.45f, 0.6f, 0.6f, true);
                case HazardKind.Basketball: return new HazardDef(0.25f, 0.25f, 0.5f, true);
                default: return new HazardDef(1, 1, 1, false);
            }
        }

        public static bool IsMoving(HazardKind k) => Get(k).IsMoving;
        public static bool IsSoft(HazardKind k) => Get(k).Soft;
        public static bool IsFlood(HazardKind k) => k == HazardKind.FloodPuddle || k == HazardKind.FloodWide;
        public static bool IsMud(HazardKind k) => k == HazardKind.MudPatch;
        /// <summary>Slow movers that a honk sends off the road (onto the sidewalk).</summary>
        public static bool StepsAsideOnHorn(HazardKind k) => k == HazardKind.TahoVendor || k == HazardKind.Pedicab || k == HazardKind.BalutVendor;
        public static bool IsRoadEvent(HazardKind k) => k >= HazardKind.TahoVendor;
    }
}
