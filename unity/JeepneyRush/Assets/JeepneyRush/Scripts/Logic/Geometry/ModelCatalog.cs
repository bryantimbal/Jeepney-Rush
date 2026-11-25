using System;
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
            yield return ("vendor_row", VendorRow());
            yield return ("gem", PropBuilder.Gem());
            yield return ("ev_taho", EventBuilder.TahoVendor(0));
            yield return ("ev_balut", EventBuilder.BalutVendor(0));
            yield return ("ev_pedicab", EventBuilder.Pedicab(0));
            yield return ("ev_float", EventBuilder.ParadeFloat(0));
            yield return ("ev_carabao", EventBuilder.Carabao(0));
            yield return ("ev_flagman", EventBuilder.Flagman());
            yield return ("ev_stalled", EventBuilder.StalledJeepney(1));
            yield return ("ev_dog", EventBuilder.StrayDog(0));
            yield return ("ev_ball", EventBuilder.Basketball());
            yield return ("barker", PropBuilder.Barker(1));
            yield return ("barker_arm", PropBuilder.BarkerArm(1));
            yield return ("stop_sign", PropBuilder.StopSign(0));
            yield return ("chunk", SceneryBuilder.ManilaChunk(1000, true));
            yield return ("enforcer", PropBuilder.Enforcer());
            yield return ("enforcer_police", PropBuilder.Enforcer(EnforcerKind.Police));
            yield return ("enforcer_city", PropBuilder.Enforcer(EnforcerKind.CityTraffic));
            yield return ("gas_marker", PropBuilder.GasMarker());
            yield return ("cockpit_pilipinas", CockpitBuilder.Extras(JeepneyCatalog.Get("pilipinas")));
            yield return ("cockpit_cogeo", CockpitBuilder.Extras(JeepneyCatalog.Get("cogeo")));
            yield return ("cockpit_modern", CockpitBuilder.Extras(JeepneyCatalog.Get("modern")));
            yield return ("cockpit_shell_pilipinas", CockpitBuilder.Shell(JeepneyCatalog.Get("pilipinas")));
            yield return ("cockpit_shell_cogeo", CockpitBuilder.Shell(JeepneyCatalog.Get("cogeo")));
            yield return ("cockpit_shell_modern", CockpitBuilder.Shell(JeepneyCatalog.Get("modern")));
            yield return ("steering_wheel", CockpitBuilder.SteeringWheel());
            yield return ("windshield_drops_classic", CockpitBuilder.WindshieldDrops(JeepneyCatalog.Get("pilipinas")));
            yield return ("windshield_drops_modern", CockpitBuilder.WindshieldDrops(JeepneyCatalog.Get("modern")));
            yield return ("wiper", CockpitBuilder.Wiper());
            yield return ("headlight_haze", LifeBuilder.HeadlightHaze());
            yield return ("bird_body", LifeBuilder.BirdBody("#5E636E", 1.4f));
            yield return ("bird_wing", LifeBuilder.BirdWing("#5E636E", 1.4f));
            yield return ("umbrella", LifeBuilder.Umbrella("#E63946"));
            yield return ("dog", LifeBuilder.Dog("#B08455"));
            yield return ("chicken", LifeBuilder.Chicken("#8B3A1E"));
            yield return ("dash_gauge", CockpitBuilder.GaugeFace());
            yield return ("dash_needle", CockpitBuilder.GaugeNeedle());
            yield return ("rosary", CockpitBuilder.Rosary());
            yield return ("siren_red", PropBuilder.Siren(true));
            yield return ("chunk_provincial", SceneryProvinces.Provincial(2000, true));
            yield return ("chunk_beach", SceneryProvinces.Beach(3000, true));
            yield return ("chunk_mountain", SceneryProvinces.MountainProvince(4000, false));
            yield return ("chunk_mountain_falls", SceneryProvinces.MountainProvince(4001, true));
            yield return ("chunk_edsa", SceneryCities.Edsa(5000, false));
            yield return ("chunk_edsa_station", SceneryCities.Edsa(5001, true));
            yield return ("chunk_edsa_flyover", SceneryCities.Edsa(5002, false, true));
            yield return ("chunk_beach_bridge", SceneryProvinces.CoastalBridge(3001));
            yield return ("chunk_cebu", SceneryCities.Cebu(6001, false));
            yield return ("chunk_cebu_sea", SceneryCities.Cebu(6000, true));
            yield return ("chunk_baguio", SceneryCities.Baguio(7000, false));
            yield return ("chunk_baguio_lake", SceneryCities.Baguio(7001, true));
            yield return ("chunk_fiesta", SceneryCities.Fiesta(8000, false, false));
            yield return ("garage", GarageBuilder.Build());
            yield return ("chunk_fiesta_church", SceneryCities.Fiesta(8001, true, false));
            yield return ("chunk_fiesta_perya", SceneryCities.Fiesta(8002, false, true));
            yield return ("mud_patch", VehicleBuilder.MudPatch(1));
            yield return ("rocks", VehicleBuilder.Rocks(2));
            foreach (PowerUpKind k in System.Enum.GetValues(typeof(PowerUpKind))) yield return ("pu_" + k, PropBuilder.PowerUp(k));
        }
    
        /// <summary>Preview only: every vendor kind in a row on the right sidewalk.</summary>
        private static MeshData VendorRow()
        {
            var b = new MeshBuilder();
            int n = 0;
            foreach (VendorKind k in Enum.GetValues(typeof(VendorKind)))
                VendorBuilder.Vendor(b, k, VendorBuilder.VendorX, SceneryBuilder.SidewalkHeight, -n++ * 3.2f, 1f, n);
            return b.Data;
        }
}
}
