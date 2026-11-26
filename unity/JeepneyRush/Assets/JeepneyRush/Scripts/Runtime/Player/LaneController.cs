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
        /// <summary>Forward speed 0 .. 1 of top speed (lower speeds steer a little tighter).</summary>
        public float Speed01 { get => _model.Speed01; set => _model.Speed01 = value; }
        /// <summary>Forward speed (m/s): stopped = steering only turns the wheels.</summary>
        public float ForwardSpeed { get => _model.ForwardSpeed; set => _model.ForwardSpeed = value; }
        public SteeringPrefs Prefs { get => _model.Prefs; set => _model.Prefs = value; }
        /// <summary>Steering wheel position -1 .. 1 (cockpit wheel, HUD wheel).</summary>
        public float Wheel => _model.Wheel;
        /// <summary>Heading relative to the road (radians) at this forward speed.</summary>
        public float Heading(float forwardSpeed) => _model.Heading(forwardSpeed);
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
