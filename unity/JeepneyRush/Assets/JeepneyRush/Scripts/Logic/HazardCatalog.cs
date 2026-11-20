namespace JeepneyRush.Logic
{
    public enum HazardKind
    {
        // moving traffic
        Sedan, Suv, Motorcycle, Tricycle, Bus, Truck,
        // static obstacles
        Cone, Barrier, Pothole, ConstructionSign, Roadblock, ParkedSedan, ParkedJeepney
    }

    public readonly struct HazardDef
    {
        public readonly float HalfWidth;
        public readonly float HalfLength;
        public readonly float Height;
        public readonly bool Jumpable;
        public readonly float MinSpeed;
        public readonly float MaxSpeed;

        public HazardDef(float halfWidth, float halfLength, float height, bool jumpable, float minSpeed = 0, float maxSpeed = 0)
        {
            HalfWidth = halfWidth;
            HalfLength = halfLength;
            Height = height;
            Jumpable = jumpable;
            MinSpeed = minSpeed;
            MaxSpeed = maxSpeed;
        }

        public bool IsMoving => MaxSpeed > 0;
    }

    /// <summary>Physical dimensions of every hazard. Visual meshes and colliders are built from these.</summary>
    public static class HazardCatalog
    {
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
                case HazardKind.Cone: return new HazardDef(0.32f, 0.32f, 0.75f, true);
                case HazardKind.Barrier: return new HazardDef(1.2f, 0.3f, 0.9f, true);
                case HazardKind.Pothole: return new HazardDef(0.95f, 0.85f, 0.25f, true);
                case HazardKind.ConstructionSign: return new HazardDef(0.7f, 0.4f, 2.0f, false);
                case HazardKind.Roadblock: return new HazardDef(1.3f, 0.45f, 2.4f, false);
                case HazardKind.ParkedSedan: return new HazardDef(0.92f, 2.2f, 1.5f, false);
                case HazardKind.ParkedJeepney: return new HazardDef(1.0f, 2.9f, 2.5f, false);
                default: return new HazardDef(1, 1, 1, false);
            }
        }

        public static bool IsMoving(HazardKind k) => Get(k).IsMoving;
    }
}
