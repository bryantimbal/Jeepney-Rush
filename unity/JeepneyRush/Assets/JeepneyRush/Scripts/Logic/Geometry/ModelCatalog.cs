using System.Collections.Generic;

namespace JeepneyRush.Logic.Geometry
{
    /// <summary>Registry of every procedural model (used by the previewer and tests).</summary>
    public static class ModelCatalog
    {
        public static IEnumerable<(string name, MeshData mesh)> Preview()
        {
            yield return ("sedan", VehicleBuilder.Sedan("#1D4ED8"));
            yield return ("taxi", VehicleBuilder.Sedan("#F8F9FA", true));
            yield return ("suv", VehicleBuilder.Suv("#2A9D8F"));
            yield return ("bus", VehicleBuilder.Bus("#F8F9FA", "#E63946"));
            yield return ("truck", VehicleBuilder.Truck("#E63946"));
            yield return ("motorcycle", VehicleBuilder.Motorcycle("#C1121F", "#FFD166"));
            yield return ("tricycle", VehicleBuilder.Tricycle("#1D4ED8", "#E63946", "#FFD166"));
            yield return ("parked_jeepney", VehicleBuilder.SimpleJeepney("#06D6A0", "#EF476F", "#073B4C"));
            yield return ("cone", VehicleBuilder.Cone());
            yield return ("barrier", VehicleBuilder.Barrier());
            yield return ("pothole", VehicleBuilder.Pothole());
            yield return ("speed_hump", VehicleBuilder.SpeedHump());
            yield return ("flood_puddle", VehicleBuilder.Flood(1.35f, 3.2f, 1));
            yield return ("flood_wide", VehicleBuilder.Flood(5.3f, 5.5f, 2));
            yield return ("construction_sign", VehicleBuilder.ConstructionSign());
            yield return ("roadblock", VehicleBuilder.Roadblock());
            yield return ("person", PropBuilder.Person(1));
            yield return ("person_seated", PropBuilder.Person(2, true));
            yield return ("coin", PropBuilder.Coin());
            yield return ("shed", PropBuilder.WaitingShed());
            yield return ("intersection", PropBuilder.Intersection());
            yield return ("gas_station", PropBuilder.GasStation(3.2f, 2.7f, 14f));
            yield return ("stop_sign", PropBuilder.StopSign(0));
            yield return ("chunk", SceneryBuilder.ManilaChunk(1000, true));
            yield return ("enforcer", PropBuilder.Enforcer());
            yield return ("siren_red", PropBuilder.Siren(true));
            yield return ("chunk_provincial", SceneryProvinces.Provincial(2000, true));
            yield return ("chunk_beach", SceneryProvinces.Beach(3000, true));
            yield return ("chunk_mountain", SceneryProvinces.MountainProvince(4000, false));
            yield return ("chunk_mountain_falls", SceneryProvinces.MountainProvince(4001, true));
            yield return ("mud_patch", VehicleBuilder.MudPatch(1));
            yield return ("rocks", VehicleBuilder.Rocks(2));
            foreach (PowerUpKind k in System.Enum.GetValues(typeof(PowerUpKind))) yield return ("pu_" + k, PropBuilder.PowerUp(k));
        }
    }
}
