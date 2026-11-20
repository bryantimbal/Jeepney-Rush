using JeepneyRush.Logic;

namespace JeepneyRush
{
    /// <summary>Fixed-duration parabolic jump (height & duration from GameBalance) with fast-fall.</summary>
    public sealed class JumpController
    {
        private readonly BalanceConfig _cfg;
        private float _t = -1f;
        private bool _fastFall;
        private float _fallY, _fallV;

        public float Y { get; private set; }
        public bool Grounded => _t < 0;
        /// <summary>-1..1 vertical phase for body pitch (up while rising, down while falling).</summary>
        public float Phase => Grounded ? 0 : 1f - 2f * (_t / _cfg.jumpDuration);

        public JumpController(BalanceConfig cfg) { _cfg = cfg; }

        public void Reset() { _t = -1f; Y = 0; _fastFall = false; }

        public bool Jump()
        {
            if (!Grounded) return false;
            _t = 0f;
            _fastFall = false;
            return true;
        }

        /// <summary>Swipe down mid-air slams the jeepney back onto the road.</summary>
        public bool FastFall()
        {
            if (Grounded || _fastFall) return false;
            _fastFall = true;
            _fallY = Y;
            _fallV = 0;
            return true;
        }

        /// <summary>Returns true on the frame the jeepney lands.</summary>
        public bool Tick(float dt)
        {
            if (Grounded) return false;
            _t += dt;
            if (_fastFall)
            {
                _fallV += 60f * dt;
                _fallY -= _fallV * dt;
                Y = _fallY;
            }
            else Y = _cfg.JumpHeightAt(_t);
            if (_t >= _cfg.jumpDuration || Y <= 0)
            {
                Reset();
                return true;
            }
            return false;
        }
    }
}
