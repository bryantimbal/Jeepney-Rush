using JeepneyRush.Logic;

namespace JeepneyRush
{
    /// <summary>
    /// The jeepney's sideways movement: analog steering (<see cref="SteeringModel"/>) driven by
    /// <see cref="Steer"/> (-1 .. 1) from the tap zones, phone tilt or keyboard. Lane, FromLane and
    /// Changing describe which lanes the jeepney occupies (for the spawner, stops and turn signals).
    /// </summary>
    public sealed class LaneController
    {
        private readonly SteeringModel _model;

        public LaneController(BalanceConfig cfg) { _model = new SteeringModel(cfg); }

        /// <summary>Steering input this frame (-1 full left .. +1 full right).</summary>
        public float Steer;
        public bool Assist { get => _model.Assist; set => _model.Assist = value; }
        /// <summary>Lane-change time multiplier (wet / flooded road = slippery = slower steering).</summary>
        public float DurationScale { get => _model.DurationScale; set => _model.DurationScale = value; }

        public int Lane => _model.Lane;
        public float X => _model.X;
        public float VelocityX => _model.VelocityX;
        public bool Changing => _model.Changing;
        public int FromLane => _model.FromLane;
        /// <summary>-1 turning left, +1 turning right, 0 straight (for the turn signals).</summary>
        public int TurnDirection => _model.TurnDirection;

        public void Reset() { _model.Reset(); Steer = 0; }

        /// <summary>Side-swipe: shoved back the way we came.</summary>
        public void BounceBack() => _model.BounceBack();

        public void Tick(float dt) => _model.Tick(Steer, dt);
    }
}
