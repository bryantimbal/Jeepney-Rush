using System;

namespace JeepneyRush.Logic.Audio
{
    /// <summary>
    /// Tiny offline synthesiser: every sound and music loop in the game is generated
    /// from code at load time (original audio, no licensing concerns, tiny APK).
    /// Engine-free so it can be unit tested; AudioManager wraps the buffers in AudioClips.
    /// </summary>
    public static class Synth
    {
        public const int Rate = 44100;

        public enum Wave { Sine, Square, Saw, Triangle, Noise }

        private static readonly Random Rng = new Random(1234);

        public static float[] Buffer(float seconds) => new float[(int)(seconds * Rate)];

        public static float Osc(Wave w, double phase)
        {
            double p = phase - Math.Floor(phase);
            switch (w)
            {
                case Wave.Sine: return (float)Math.Sin(p * Math.PI * 2);
                case Wave.Square: return p < 0.5 ? 1f : -1f;
                case Wave.Saw: return (float)(2 * p - 1);
                case Wave.Triangle: return (float)(p < 0.5 ? 4 * p - 1 : 3 - 4 * p);
                default: return (float)(Rng.NextDouble() * 2 - 1);
            }
        }

        /// <summary>
        /// Adds a tone with an exponential pitch glide and an attack/exponential-decay envelope.
        /// Optional one-pole low-pass (cutoff Hz) softens bright waves.
        /// </summary>
        public static void Tone(float[] buf, Wave w, float startSec, float dur, float f0, float f1, float vol, float attack = 0.005f, float lowpass = 0)
        {
            int s0 = (int)(startSec * Rate);
            int n = (int)(dur * Rate);
            double phase = 0;
            float lp = 0;
            float a = lowpass > 0 ? 1f - (float)Math.Exp(-2 * Math.PI * lowpass / Rate) : 1f;
            for (int i = 0; i < n && s0 + i < buf.Length; i++)
            {
                if (s0 + i < 0) continue;
                float t = i / (float)Rate;
                double f = f0 * Math.Pow(f1 / f0, t / dur);
                phase += f / Rate;
                float env = t < attack ? t / attack : (float)Math.Exp(-5.0 * (t - attack) / Math.Max(1e-3, dur - attack));
                float s = Osc(w, phase);
                lp += a * (s - lp);
                buf[s0 + i] += lp * env * vol;
            }
        }

        /// <summary>Filtered noise burst. Band centre glides f0→f1 (state-variable filter).</summary>
        public static void Noise(float[] buf, float startSec, float dur, float vol, float f0, float f1, float q = 1f, bool highpass = false, bool lowpassOnly = false)
        {
            int s0 = (int)(startSec * Rate);
            int n = (int)(dur * Rate);
            float low = 0, band = 0;
            for (int i = 0; i < n && s0 + i < buf.Length; i++)
            {
                float t = i / (float)Rate;
                float fc = (float)(f0 * Math.Pow(f1 / f0, t / dur));
                float fcoef = (float)(2 * Math.Sin(Math.PI * Math.Min(fc, Rate / 6f) / Rate));
                float input = (float)(Rng.NextDouble() * 2 - 1);
                low += fcoef * band;
                float high = input - low - band / q;
                band += fcoef * high;
                float o = highpass ? high : lowpassOnly ? low : band;
                float env = (float)Math.Exp(-4.0 * t / dur) * Math.Min(1f, t / 0.003f);
                if (s0 + i >= 0) buf[s0 + i] += o * env * vol;
            }
        }

        public static float[] Normalize(float[] buf, float peak = 0.9f)
        {
            float m = 0;
            foreach (var v in buf) m = Math.Max(m, Math.Abs(v));
            if (m < 1e-6f) return buf;
            float k = peak / m;
            for (int i = 0; i < buf.Length; i++) buf[i] *= k;
            return buf;
        }

        /// <summary>Soft clip (tanh-ish) for brassy horn / punchy crash.</summary>
        public static void Drive(float[] buf, float amount)
        {
            for (int i = 0; i < buf.Length; i++) buf[i] = (float)Math.Tanh(buf[i] * amount);
        }
    }
}
