using System.Collections.Generic;
using JeepneyRush.Logic;
using UnityEngine;

namespace JeepneyRush
{
    /// <summary>
    /// Bridges the engine-free <see cref="SpawnDirector"/> to the Unity managers.
    /// Converts logic Z (negative = ahead) to Unity Z (positive = ahead).
    /// </summary>
    public sealed class LevelSpawner : ISpawnSink
    {
        private readonly TrafficManager _traffic;
        private readonly ObstacleManager _obstacles;
        private readonly CoinManager _coins;
        private readonly PassengerManager _passengers;
        private readonly PowerUpManager _powerUps;
        public StationManager Stations;
        public IntersectionManager Intersections;
        private readonly int[] _one = new int[1];
        private readonly int[] _two = new int[2];
        private IReadOnlyList<int> _lanes;

        public LevelSpawner(TrafficManager t, ObstacleManager o, CoinManager c, PassengerManager p, PowerUpManager pu)
        {
            _traffic = t; _obstacles = o; _coins = c; _passengers = p; _powerUps = pu;
            _lanes = _one;
        }

        public void SetPlayerLanes(int a, int b)
        {
            if (a == b || b < 0) { _one[0] = a; _lanes = _one; }
            else { _two[0] = a; _two[1] = b; _lanes = _two; }
        }

        public IReadOnlyList<int> PlayerLanes => _lanes;

        public void CollectBlockers(List<PredictBlocker> output)
        {
            _traffic.CollectBlockers(output);
            _obstacles.CollectBlockers(output);
        }

        public void SpawnHazard(HazardKind kind, int lane, float z, float speed, bool solid)
        {
            if (HazardCatalog.IsMoving(kind))
            {
                var h = _traffic.Spawn(kind, lane, -z, speed, solid);
                // some curb-lane tricycles pull out from a side street instead of already driving
                if (kind == HazardKind.Tricycle && lane == _traffic.Config.laneCount - 1 && Random.value < 0.45f) h.PullOut = 2.8f;
            }
            else _obstacles.Spawn(kind, lane, -z, speed, solid);
        }

        public void SpawnCoins(CoinFormation formation, int lane, int lane2, float z, int count, float spacing) =>
            _coins.SpawnFormation(formation, lane, lane2, -z, count, spacing);

        public void SpawnStop(PassengerStop stop, float z) => _passengers.SpawnStop(stop, -z);

        public void SpawnPowerUp(PowerUpKind kind, int lane, float z) => _powerUps.Spawn(kind, lane, -z);
        public void SpawnGem(int lane, float z) => _coins.SpawnGem(lane, -z);
        public void SpawnParol(int lane, float z) => _coins.SpawnParol(lane, -z);

        public void SpawnStation(GasStation station, float z) { if (Stations != null) Stations.Spawn(station, -z); }

        public void SpawnIntersection(Intersection x, float z) { if (Intersections != null) Intersections.Spawn(x, -z); }
    }
}
