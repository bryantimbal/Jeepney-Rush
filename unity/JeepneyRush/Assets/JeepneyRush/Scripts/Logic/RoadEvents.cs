using System;
using System.Collections.Generic;

namespace JeepneyRush.Logic
{
    public enum RoadEventKind { Taho, Pedicab, Carabao, Construction, StalledJeepney, StrayDog, Basketball, ParadeFloat }

    /// <summary>
    /// Filipino road events: occasional extra rows between the normal traffic rows. Each event is
    /// built from hazards and validated exactly like a normal row (a lane is always open, stop and
    /// station zones stay clear), drawn from its own random stream, and announced on the HUD before
    /// the player reaches it.
    /// </summary>
    public static class RoadEvents
    {
        public const float FirstAt = 260f;
        public const float SpacingMin = 170f, SpacingMax = 340f;
        /// <summary>Distance ahead at which the HUD announces an event.</summary>
        public const float WarnAt = 95f;

        /// <summary>Which events happen on a route, and how often (weights).</summary>
        public static (RoadEventKind, float)[] For(EnvironmentKind env)
        {
            switch (env)
            {
                case EnvironmentKind.Edsa:
                    return new[] { (RoadEventKind.Construction, 2f), (RoadEventKind.StalledJeepney, 1.6f) };
                case EnvironmentKind.Cebu:
                    return new[] { (RoadEventKind.ParadeFloat, 1.4f), (RoadEventKind.Taho, 1f), (RoadEventKind.Pedicab, 1.2f), (RoadEventKind.StrayDog, 0.8f),
                                   (RoadEventKind.Construction, 0.9f), (RoadEventKind.StalledJeepney, 0.7f) };
                case EnvironmentKind.Baguio:
                    return new[] { (RoadEventKind.Taho, 1.4f), (RoadEventKind.Construction, 1f), (RoadEventKind.StalledJeepney, 0.8f), (RoadEventKind.StrayDog, 0.8f) };
                case EnvironmentKind.Provincial:
                    return new[] { (RoadEventKind.Carabao, 1.8f), (RoadEventKind.Pedicab, 1.1f), (RoadEventKind.Taho, 0.8f), (RoadEventKind.StrayDog, 1f),
                                   (RoadEventKind.Basketball, 0.7f), (RoadEventKind.Construction, 0.6f), (RoadEventKind.ParadeFloat, 0.5f) };
                case EnvironmentKind.Beach:
                    return new[] { (RoadEventKind.Pedicab, 1.4f), (RoadEventKind.StrayDog, 1.2f), (RoadEventKind.Basketball, 1f), (RoadEventKind.Carabao, 0.4f) };
                case EnvironmentKind.Mountain:
                    return new[] { (RoadEventKind.Carabao, 1.6f), (RoadEventKind.Construction, 1.2f), (RoadEventKind.StrayDog, 0.7f) };
                default: // Manila streets
                    return new[] { (RoadEventKind.Taho, 1.3f), (RoadEventKind.Pedicab, 1.2f), (RoadEventKind.StalledJeepney, 1f), (RoadEventKind.StrayDog, 0.9f),
                                   (RoadEventKind.Basketball, 0.8f), (RoadEventKind.Construction, 1f) };
            }
        }

        /// <summary>One hazard of an event: lane offset from the event lane (-1 = curb lane) and distance offset.</summary>
        public struct Piece { public HazardKind Kind; public int Lane; public float Dz; }

        /// <summary>The hazards making up an event in <paramref name="lane"/> (curb events ignore it).</summary>
        public static void Build(RoadEventKind e, int lane, int laneCount, List<Piece> output)
        {
            output.Clear();
            int curb = laneCount - 1;
            switch (e)
            {
                case RoadEventKind.Taho: output.Add(new Piece { Kind = HazardKind.TahoVendor, Lane = curb }); break;
                case RoadEventKind.Pedicab: output.Add(new Piece { Kind = HazardKind.Pedicab, Lane = curb }); break;
                case RoadEventKind.Carabao: output.Add(new Piece { Kind = HazardKind.Carabao, Lane = lane }); break;
                case RoadEventKind.StalledJeepney: output.Add(new Piece { Kind = HazardKind.StalledJeepney, Lane = curb }); break;
                case RoadEventKind.StrayDog: output.Add(new Piece { Kind = HazardKind.StrayDog, Lane = lane }); break;
                case RoadEventKind.Basketball: output.Add(new Piece { Kind = HazardKind.Basketball, Lane = lane }); break;
                case RoadEventKind.ParadeFloat: output.Add(new Piece { Kind = HazardKind.ParadeFloat, Lane = lane }); break;
                case RoadEventKind.Construction:
                    // lane closure: roadblock, a flagman just behind it and cones tapering toward the driver
                    output.Add(new Piece { Kind = HazardKind.Roadblock, Lane = lane });
                    output.Add(new Piece { Kind = HazardKind.Flagman, Lane = lane, Dz = -2.5f });
                    output.Add(new Piece { Kind = HazardKind.Cone, Lane = lane, Dz = 4.5f });
                    output.Add(new Piece { Kind = HazardKind.Cone, Lane = lane, Dz = 9f });
                    break;
            }
        }

        public static string Warning(RoadEventKind e)
        {
            switch (e)
            {
                case RoadEventKind.Taho: return "TAHOOO! MAY MAGTATAHO SA GILID";
                case RoadEventKind.Pedicab: return "MAY PEDICAB SA UNAHAN";
                case RoadEventKind.Carabao: return "KALABAW SA DAAN!";
                case RoadEventKind.Construction: return "MAY GINAGAWA SA DAAN · ISANG LANE SARADO";
                case RoadEventKind.StalledJeepney: return "TIRIK NA JEEP SA GILID!";
                case RoadEventKind.StrayDog: return "ASO SA KALSADA!";
                case RoadEventKind.Basketball: return "BOLA! MAY NAGLALARO NG BASKETBALL";
                default: return "FIESTA! MAY PARADA SA UNAHAN";
            }
        }

        /// <summary>The event a hazard belongs to (for the HUD warning), or null for ordinary hazards.</summary>
        public static RoadEventKind? EventOf(HazardKind k)
        {
            switch (k)
            {
                case HazardKind.TahoVendor: return RoadEventKind.Taho;
                case HazardKind.Pedicab: return RoadEventKind.Pedicab;
                case HazardKind.Carabao: return RoadEventKind.Carabao;
                case HazardKind.Flagman: return RoadEventKind.Construction;
                case HazardKind.StalledJeepney: return RoadEventKind.StalledJeepney;
                case HazardKind.StrayDog: return RoadEventKind.StrayDog;
                case HazardKind.Basketball: return RoadEventKind.Basketball;
                case HazardKind.ParadeFloat: return RoadEventKind.ParadeFloat;
                default: return null;
            }
        }
    }
}
