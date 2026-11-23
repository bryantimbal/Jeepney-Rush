using System.Collections.Generic;
using JeepneyRush.Logic;
using JeepneyRush.Logic.Geometry;
using UnityEngine;
using UnityEngine.Rendering;

namespace JeepneyRush
{
    /// <summary>
    /// Converts the engine-free procedural models into Unity meshes once and caches
    /// them for the whole session (meshes outlive scene changes).
    /// </summary>
    public static class ModelLibrary
    {
        public readonly struct Model
        {
            public readonly Mesh Mesh;
            public readonly Material[] Materials;
            public Model(Mesh mesh, Material[] mats) { Mesh = mesh; Materials = mats; }
        }

        private static readonly Dictionary<string, Model> Models = new Dictionary<string, Model>();

        public static Mesh ToMesh(MeshData d, string name)
        {
            var mesh = new Mesh { name = name };
            mesh.indexFormat = d.Positions.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            var v = new List<Vector3>(d.Positions.Count);
            var n = new List<Vector3>(d.Normals.Count);
            var uv = new List<Vector2>(d.Uvs.Count);
            foreach (var p in d.Positions) v.Add(new Vector3(p.X, p.Y, p.Z));
            foreach (var p in d.Normals) n.Add(new Vector3(p.X, p.Y, p.Z));
            foreach (var p in d.Uvs) uv.Add(new Vector2(p.X, p.Y));
            mesh.SetVertices(v);
            mesh.SetNormals(n);
            mesh.SetUVs(0, uv);
            mesh.subMeshCount = d.SubmeshKeys.Count;
            for (int s = 0; s < d.SubmeshKeys.Count; s++) mesh.SetTriangles(d.SubmeshIndices[s], s, false);
            mesh.RecalculateBounds();
            mesh.UploadMeshData(false);
            return mesh;
        }

        public static Model Get(string key, System.Func<MeshData> build)
        {
            if (Models.TryGetValue(key, out var m) && m.Mesh != null) return m;
            var data = build();
            var mats = new Material[data.SubmeshKeys.Count];
            for (int i = 0; i < mats.Length; i++) mats[i] = MaterialLibrary.ForKey(data.SubmeshKeys[i]);
            MaterialLibrary.RefreshPalette();
            m = new Model(ToMesh(data, key), mats);
            Models[key] = m;
            return m;
        }

        /// <summary>Creates a GameObject rendering the model.</summary>
        public static GameObject Instantiate(Model model, string name, Transform parent = null, bool castShadows = true)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = model.Mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = model.Materials;
            r.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            r.receiveShadows = true;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return go;
        }

        // ------------------------------------------------------------------ catalog
        public static Model Jeepney(string liveryId) => Get("jeepney_" + liveryId, () => JeepneyBuilder.Build(JeepneyCatalog.Get(liveryId)));
        public static Model Wheel() => Get("wheel", () => JeepneyBuilder.BuildWheel());
        public static Model Coin() => Get("coin", PropBuilder.Coin);
        public static Model Person(int i) => Get("person" + i, () => PropBuilder.Person(i));
        public static Model SeatedPerson(int i) => Get("seated" + i, () => PropBuilder.Person(i, true));
        public static Model Shed() => Get("shed", PropBuilder.WaitingShed);
        public static Model StopSign(int nameIndex) => Get("stopsign" + nameIndex, () => PropBuilder.StopSign(nameIndex));
        public static Model StopArrow(bool drop) => Get(drop ? "arrow_drop" : "arrow_pick", () => PropBuilder.StopArrow(drop ? "#2BFF88" : "#FFD100"));
        public static Model Zone(float w, float l) => Get($"zone{w}x{l}", () => PropBuilder.ParaZone(w, l));
        public static Model Chunk(int i) => Chunk(EnvironmentKind.Manila, i);
        public static Model Chunk(EnvironmentKind env, int i) =>
            Get($"chunk_{env}_{i}", () => SceneryProvinces.Chunk(env, (int)env * 100000 + 1000 + i * 7919, i == 2 || i == 6));
        public static Model PowerUp(PowerUpKind k) => Get("pu_" + k, () => PropBuilder.PowerUp(k));
        public static Model Bubble() => Get("bubble", PropBuilder.Bubble);
        public static Model WheelFor(string liveryId)
        {
            var chrome = JeepneyCatalog.Get(liveryId).Chrome;
            return Get("wheel_" + chrome, () => JeepneyBuilder.BuildWheel(null, chrome));
        }
        public const int ChunkVariants = 8;

        public static int VariantCount(HazardKind k)
        {
            switch (k)
            {
                case HazardKind.Sedan: return VehicleBuilder.CarColors.Length + 1; // + taxi
                case HazardKind.Suv: return 4;
                case HazardKind.Motorcycle: return 5;
                case HazardKind.Tricycle: return 5;
                case HazardKind.Bus: return 4;
                case HazardKind.Truck: return 4;
                case HazardKind.FloodPuddle: return 3;
                case HazardKind.MudPatch: return 3;
                case HazardKind.Rocks: return 3;
                case HazardKind.FloodWide: return 2;
                case HazardKind.ParkedSedan: return VehicleBuilder.CarColors.Length;
                case HazardKind.ParkedJeepney: return JeepColors.Length;
                case HazardKind.TahoVendor: return 3;
                case HazardKind.Pedicab: return 5;
                case HazardKind.ParadeFloat: return 3;
                case HazardKind.Carabao: return 2;
                case HazardKind.StalledJeepney: return 3;
                case HazardKind.StrayDog: return 4;
                default: return 1;
            }
        }

        private static readonly string[][] JeepColors =
        {
            new[] { "#E63946", "#FFD166", "#1D3557" }, new[] { "#06D6A0", "#EF476F", "#073B4C" },
            new[] { "#FFB703", "#219EBC", "#023047" }, new[] { "#9B5DE5", "#F15BB5", "#FEE440" },
        };
        private static readonly string[] SuvColors = { "#2A9D8F", "#212529", "#ADB5BD", "#9D0208" };
        private static readonly string[][] BusColors = { new[] { "#F8F9FA", "#E63946" }, new[] { "#1D3557", "#FFD166" }, new[] { "#2A9D8F", "#F8F9FA" }, new[] { "#FFB703", "#023047" } };
        private static readonly string[] TruckColors = { "#E63946", "#1D4ED8", "#2A9D8F", "#F4A261" };
        private static readonly string[] TrikeColors = { "#E63946", "#1D4ED8", "#06D6A0", "#FFB703", "#9B5DE5" };

        public static Model Hazard(HazardKind k, int variant)
        {
            int v = Mathf.Abs(variant) % VariantCount(k);
            var cars = VehicleBuilder.CarColors;
            var shirts = VehicleBuilder.Shirts;
            switch (k)
            {
                case HazardKind.Sedan:
                    return v == cars.Length ? Get("taxi", () => VehicleBuilder.Sedan("#F8F9FA", true)) : Get("sedan" + v, () => VehicleBuilder.Sedan(cars[v]));
                case HazardKind.ParkedSedan: return Get("sedan" + v, () => VehicleBuilder.Sedan(cars[v]));
                case HazardKind.Suv: return Get("suv" + v, () => VehicleBuilder.Suv(SuvColors[v]));
                case HazardKind.Motorcycle: return Get("moto" + v, () => VehicleBuilder.Motorcycle(cars[v], shirts[v]));
                case HazardKind.Tricycle: return Get("trike" + v, () => VehicleBuilder.Tricycle(TrikeColors[v], shirts[(v + 2) % shirts.Length], "#FFD166"));
                case HazardKind.Bus: return Get("bus" + v, () => VehicleBuilder.Bus(BusColors[v][0], BusColors[v][1]));
                case HazardKind.Truck: return Get("truck" + v, () => VehicleBuilder.Truck(TruckColors[v]));
                case HazardKind.ParkedJeepney: return Get("pjeep" + v, () => VehicleBuilder.SimpleJeepney(JeepColors[v][0], JeepColors[v][1], JeepColors[v][2]));
                case HazardKind.Cone: return Get("cone", VehicleBuilder.Cone);
                case HazardKind.Barrier: return Get("barrier", VehicleBuilder.Barrier);
                case HazardKind.Pothole: return Get("pothole", VehicleBuilder.Pothole);
                case HazardKind.ConstructionSign: return Get("csign", VehicleBuilder.ConstructionSign);
                case HazardKind.Roadblock: return Get("roadblock", VehicleBuilder.Roadblock);
                case HazardKind.SpeedHump: return Get("hump", VehicleBuilder.SpeedHump);
                case HazardKind.MudPatch: return Get("mud" + v, () => VehicleBuilder.MudPatch(30 + v));
                case HazardKind.Rocks: return Get("rocks" + v, () => VehicleBuilder.Rocks(40 + v));
                case HazardKind.FloodPuddle: return Get("flood" + v, () => VehicleBuilder.Flood(1.35f, 3.2f, 10 + v));
                case HazardKind.FloodWide: return Get("floodw" + v, () => VehicleBuilder.Flood(5.3f, 5.5f, 20 + v));
                case HazardKind.TahoVendor: return Get("ev_taho" + v, () => EventBuilder.TahoVendor(v));
                case HazardKind.Pedicab: return Get("ev_pedicab" + v, () => EventBuilder.Pedicab(v));
                case HazardKind.ParadeFloat: return Get("ev_float" + v, () => EventBuilder.ParadeFloat(v));
                case HazardKind.Carabao: return Get("ev_carabao" + v, () => EventBuilder.Carabao(v));
                case HazardKind.Flagman: return Get("ev_flagman", EventBuilder.Flagman);
                case HazardKind.StalledJeepney: return Get("ev_stalled" + v, () => EventBuilder.StalledJeepney(v));
                case HazardKind.StrayDog: return Get("ev_dog" + v, () => EventBuilder.StrayDog(v));
                case HazardKind.Basketball: return Get("ev_ball", EventBuilder.Basketball);
            }
            return Get("cone", VehicleBuilder.Cone);
        }

        public static void WarmUpTraffic()
        {
            foreach (HazardKind k in System.Enum.GetValues(typeof(HazardKind)))
                for (int v = 0; v < VariantCount(k); v++) Hazard(k, v);
            Coin();
            Wheel();
            for (int i = 0; i < PropBuilder.PersonVariants; i++) { Person(i); SeatedPerson(i); }
        }

        public static void WarmUpScenery(EnvironmentKind env = EnvironmentKind.Manila)
        {
            for (int i = 0; i < ChunkVariants; i++) Chunk(env, i);
            foreach (PowerUpKind k in System.Enum.GetValues(typeof(PowerUpKind))) PowerUp(k);
            Shed();
            StopArrow(true);
            StopArrow(false);
        }
    }
}
