using JeepneyRush.Logic;
using UnityEngine;

namespace JeepneyRush
{
    /// <summary>Three fixed lanes, eased lane changes and one buffered input.</summary>
    public sealed class LaneController
    {
        private readonly BalanceConfig _cfg;
        private int _from;
        private float _t = 1f;
        private int _queued = -1;

        public int Lane { get; private set; }
        public float X { get; private set; }
        public float VelocityX { get; private set; }
        public bool Changing => _t < 1f;
        public int FromLane => _from;
        /// <summary>-1 turning left, +1 turning right, 0 straight (for the turn signals).</summary>
        public int TurnDirection => Changing ? (Lane > _from ? 1 : Lane < _from ? -1 : 0) : 0;
        /// <summary>Lane-change time multiplier (wet / flooded road = slippery = slower steering).</summary>
        public float DurationScale = 1f;

        public LaneController(BalanceConfig cfg) { _cfg = cfg; Reset(); }

        public void Reset()
        {
            Lane = _from = (_cfg.laneCount - 1) / 2;
            _t = 1f;
            _queued = -1;
            X = _cfg.LaneX(Lane);
            VelocityX = 0;
        }

        /// <summary>Returns false at the road edge.</summary>
        public bool Move(int dir)
        {
            int baseLane = _queued >= 0 ? _queued : Lane;
            int target = baseLane + dir;
            if (target < 0 || target >= _cfg.laneCount) return false;
            if (Changing) { _queued = target; return true; }
            Start(target);
            return true;
        }

        private void Start(int target)
        {
            _from = Lane;
            Lane = target;
            _t = 0f;
        }

        /// <summary>Side-swipe: steer back to the lane we came from.</summary>
        public void BounceBack()
        {
            if (!Changing) return;
            int back = _from;
            _from = Lane;
            Lane = back;
            _t = 1f - _t;
            _queued = -1;
        }

        public void Tick(float dt)
        {
            float prev = X;
            if (_t < 1f)
            {
                _t = Mathf.Min(1f, _t + dt / (_cfg.laneChangeDuration * Mathf.Max(0.5f, DurationScale)));
                if (_t >= 1f && _queued >= 0)
                {
                    int q = _queued;
                    _queued = -1;
                    if (q != Lane) Start(q);
                }
            }
            float e = _t * _t * (3f - 2f * _t); // smooth in and out, like a real driver easing the wheel
            X = Mathf.Lerp(_cfg.LaneX(_from), _cfg.LaneX(Lane), e);
            VelocityX = dt > 0 ? (X - prev) / dt : 0;
        }
    }
}
