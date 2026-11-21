using System;

namespace JeepneyRush.Logic.Audio
{
    /// <summary>
    /// Drivetrain of a typical Manila jeepney: surplus 4-cylinder naturally aspirated diesel
    /// (Isuzu 4BC2 / 4BA1 class, ~750 rpm idle, ~3,000 rpm red line) and a 5-speed manual.
    /// Turns road speed + pedals into engine rpm, throttle and gear – including the rev drop
    /// at every upshift that gives a jeepney pulling away its "rrRRAA… rrRRAA" sound.
    /// </summary>
    public sealed class JeepneyDrivetrain
    {
        public const float IdleRpm = 750f, RedLine = 3100f, UpshiftRpm = 2700f, DownshiftRpm = 1150f;
        public const float WheelRadius = 0.44f;
        /// <summary>Overall ratios (gearbox × final drive) for gears 1..5.</summary>
        public static readonly float[] Ratios = { 24f, 14.5f, 9.2f, 6.1f, 4.4f };
        public const float ShiftTime = 0.38f;

        public float Rpm { get; private set; } = IdleRpm;
        public float Throttle { get; private set; }
        public int Gear { get; private set; } = 1;
        public bool Shifting => _shiftT > 0;
        /// <summary>Counts gear changes (the audio layer plays a clunk when it changes).</summary>
        public int ShiftCount { get; private set; }

        private float _shiftT;

        public static float RpmAt(float speed, int gear) => Math.Abs(speed) / (2f * (float)Math.PI * WheelRadius) * 60f * Ratios[Math.Max(1, Math.Min(5, gear)) - 1];

        public void Reset() { Rpm = IdleRpm; Throttle = 0; Gear = 1; _shiftT = 0; }

        /// <param name="speed">road speed (m/s, negative = reversing)</param>
        /// <param name="gas">GAS held</param>
        /// <param name="brake">BRAKE held</param>
        /// <param name="accelerating">the vehicle is gaining speed (engine under load)</param>
        public void Tick(float dt, float speed, bool gas, bool brake, bool accelerating)
        {
            if (dt <= 0) return;
            float v = Math.Abs(speed);
            // pedal → throttle (smoothed like a foot on a heavy pedal)
            float targetThrottle = brake ? 0f : gas ? 1f : accelerating ? 0.55f : v > 1f ? 0.28f : 0.05f;
            if (_shiftT > 0) targetThrottle = 0.05f; // foot off while the clutch is in
            Throttle += (targetThrottle - Throttle) * (1f - (float)Math.Exp(-dt * (targetThrottle > Throttle ? 9f : 6f)));

            if (_shiftT > 0)
            {
                // clutch in: revs fall toward what the next gear needs
                _shiftT -= dt;
                float next = Math.Max(IdleRpm, RpmAt(v, Gear));
                Rpm += (next - Rpm) * (1f - (float)Math.Exp(-dt * 16f));
                return;
            }

            if (v < 0.6f)
            {
                // standing (clutch in / neutral): free revving – a jeepney driver blipping the throttle
                Gear = 1;
                float target = IdleRpm + (gas ? (RedLine - IdleRpm) * 0.85f : 0f);
                Rpm += (target - Rpm) * (1f - (float)Math.Exp(-dt * (target > Rpm ? 6.5f : 2.6f))); // snappy blip, slower fall (flywheel)
                return;
            }

            // gear changes (upshift high, downshift when lugging)
            float rpmHere = RpmAt(v, Gear);
            if (rpmHere > (gas ? UpshiftRpm : UpshiftRpm - 500f) && Gear < 5) { Gear++; _shiftT = ShiftTime; ShiftCount++; return; }
            if (rpmHere < DownshiftRpm && Gear > 1) { Gear--; ShiftCount++; }

            // clutch slipping in first gear at walking pace keeps the engine above idle
            float wheelRpm = Math.Max(RpmAt(v, Gear), IdleRpm + (Gear == 1 ? Throttle * 900f : 0f));
            Rpm += (Math.Min(RedLine, wheelRpm) - Rpm) * (1f - (float)Math.Exp(-dt * 30f)); // clutch engaged: engine follows the wheels
        }
    }

    /// <summary>
    /// Real-time synthesis of an old 4-cylinder diesel with a straight exhaust pipe, built
    /// from physics-ish parts rather than a looped tone:
    ///  * one combustion event per firing (4 per 2 revolutions) with cylinder imbalance and
    ///    timing jitter – the lumpy, uneven idle of a tired surplus engine;
    ///  * diesel "clatter" (sharp injector knock) strongest at idle and light load;
    ///  * exhaust pulses through pipe resonances (~85 Hz / 240 Hz) and soft clipping – the
    ///    raspy, loud open "tambutso";
    ///  * valve-train ticking and loose body-panel rattle excited by the pulses.
    /// Engine-free and deterministic (seeded) so it can be unit tested; Unity feeds it from
    /// OnAudioFilterRead on the audio thread.
    /// </summary>
    public sealed class DieselEngineSynth
    {
        private readonly float _rate;
        private uint _rng;
        private double _sinceFire = 1e9, _nextFire;
        private int _cyl;
        private float _pulseAmp, _knockAmp;
        private float _rpm = JeepneyDrivetrain.IdleRpm, _thr;
        private static readonly float[] Imbalance = { 0.0f, 0.14f, -0.09f, 0.06f }; // one weak, one strong cylinder

        // resonators (2-pole) for the exhaust pipe and body panels
        private struct Reso { public float A1, A2, G, Y1, Y2; }
        private Reso _pipe1, _pipe2, _panel1, _panel2, _knockBand;
        private float _lp, _dcX, _dcY, _tick;

        public DieselEngineSynth(int sampleRate, uint seed = 1234567)
        {
            _rate = sampleRate;
            _rng = seed == 0 ? 1u : seed;
            _pipe1 = MakeReso(85f, 2.2f);
            _pipe2 = MakeReso(240f, 4f);
            _panel1 = MakeReso(430f, 28f);
            _panel2 = MakeReso(640f, 32f);
            _knockBand = MakeReso(3200f, 2.2f);
        }

        private Reso MakeReso(float f, float q)
        {
            double w = 2 * Math.PI * f / _rate, r = Math.Exp(-w / (2 * q));
            return new Reso { A1 = (float)(2 * r * Math.Cos(w)), A2 = (float)(-r * r), G = (float)(1 - r) };
        }

        private static float Run(ref Reso s, float x)
        {
            float y = s.G * x + s.A1 * s.Y1 + s.A2 * s.Y2;
            s.Y2 = s.Y1; s.Y1 = y;
            return y;
        }

        private float Noise()
        {
            _rng ^= _rng << 13; _rng ^= _rng >> 17; _rng ^= _rng << 5;
            return (_rng / 4294967295f) * 2f - 1f;
        }

        /// <summary>Set the engine state (call any time; smoothed per sample).</summary>
        public float TargetRpm = JeepneyDrivetrain.IdleRpm;
        public float TargetThrottle;

        /// <summary>Fills mono samples in [-1, 1].</summary>
        public void Render(float[] mono, int offset, int count)
        {
            float dt = 1f / _rate;
            for (int i = 0; i < count; i++)
            {
                _rpm += (TargetRpm - _rpm) * 0.0009f;
                _thr += (TargetThrottle - _thr) * 0.0012f;
                // ---- firing events: 4 cylinders, 4-stroke -> rpm/30 firings per second
                _sinceFire += dt;
                if (_sinceFire >= _nextFire)
                {
                    _sinceFire = 0;
                    double interval = 30.0 / Math.Max(300f, _rpm);
                    _nextFire = interval * (1.0 + Noise() * (0.045 - 0.03 * Math.Min(1f, _rpm / 2500f))); // rough idle, smoother at revs
                    _cyl = (_cyl + 1) & 3;
                    float strength = (1f + Imbalance[_cyl]) * (0.88f + 0.24f * Math.Abs(Noise()));
                    _pulseAmp = strength * (0.35f + 0.65f * _thr) * (0.75f + 0.25f * Math.Min(1f, _rpm / 2000f));
                    _knockAmp = strength * (0.55f + 0.35f * (1f - _thr)) * (_rpm < 1600 ? 1f : 0.7f);
                }
                float t = (float)_sinceFire;
                // exhaust pulse: fast attack, ~6 ms decay, pushed into the pipe resonances
                float pulse = _pulseAmp * (t < 0.0008f ? t / 0.0008f : (float)Math.Exp(-(t - 0.0008f) * 170f));
                float exhaust = (Run(ref _pipe1, pulse * 1.6f) + 0.55f * Run(ref _pipe2, pulse * 1f)) * 0.075f;
                // breathy flow noise that opens with throttle
                exhaust += Noise() * pulse * (0.08f + 0.22f * _thr);
                // diesel knock: very short broadband click, band-limited to the "clack" region
                float knock = Run(ref _knockBand, Noise() * _knockAmp * (float)Math.Exp(-t * 900f)) * 1.6f;
                // valve train: tiny ticks twice per firing
                _tick = (t < 0.0003f || Math.Abs(t - _nextFire * 0.5) < 0.00015) ? Noise() * 0.05f : _tick * 0.6f;
                // loose panels rattle when the pulses are strong (idle shake, revving)
                float shake = Math.Max(0f, Math.Abs(exhaust) - 0.45f);
                float rattle = (Run(ref _panel1, Noise() * shake * 1.5f) + Run(ref _panel2, Noise() * shake * 1.2f)) * 0.9f;
                // straight pipe rasp: drive harder with throttle
                float drive = 1.2f + 1.5f * _thr;
                float x = (float)Math.Tanh(exhaust * drive) * 0.62f + knock * 0.32f + rattle * 0.22f + _tick;
                // gentle top cut + DC block
                _lp += (x - _lp) * 0.55f;
                float y = _lp - _dcX + 0.995f * _dcY;
                _dcX = _lp; _dcY = y;
                mono[offset + i] = (float)Math.Tanh(y * 1.8f) * 0.95f; // make-up gain + soft limiter
            }
        }
    }
}
