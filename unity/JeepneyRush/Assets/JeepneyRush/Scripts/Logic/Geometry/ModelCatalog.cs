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
            yield return ("construction_sign", VehicleBuilder.ConstructionSign());
            yield return ("roadblock", VehicleBuilder.Roadblock());
            yield return ("person", PropBuilder.Person(1));
            yield return ("person_seated", PropBuilder.Person(2, true));
            yield return ("coin", PropBuilder.Coin());
            yield return ("shed", PropBuilder.WaitingShed());
            yield return ("stop_sign", PropBuilder.StopSign(0));
            yield return ("chunk", SceneryBuilder.ManilaChunk(1000, true));
        }
    }
}
