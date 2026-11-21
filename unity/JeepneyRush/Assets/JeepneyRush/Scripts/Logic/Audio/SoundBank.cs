using System;
using System.Collections.Generic;

namespace JeepneyRush.Logic.Audio
{
    public enum Sfx { Coin, Pickup, Full, Crash, Jump, Land, Lane, Brake, Horn, Click, GameOver, NewBest, Start, PowerUp, Deliver, Miss, Shield, Purchase, LevelUp, Turbo }

    /// <summary>Recipes for every sound effect plus the looping diesel engine.</summary>
    public static class SoundBank
    {
        public static IEnumerable<Sfx> AllSfx => (Sfx[])Enum.GetValues(typeof(Sfx));

        public static float[] Make(Sfx s)
        {
            float[] b;
            switch (s)
            {
                case Sfx.Coin:
                    b = Synth.Buffer(0.25f);
                    Synth.Tone(b, Synth.Wave.Square, 0, 0.07f, 988, 988, 0.4f, 0.002f, 6000);
                    Synth.Tone(b, Synth.Wave.Square, 0.06f, 0.18f, 1319, 1319, 0.4f, 0.002f, 6000);
                    return Synth.Normalize(b, 0.6f);
                case Sfx.Pickup:
                    b = Synth.Buffer(0.5f);
                    float[] notes = { 523, 659, 784, 1047 };
                    for (int i = 0; i < notes.Length; i++) Synth.Tone(b, Synth.Wave.Triangle, i * 0.06f, 0.22f, notes[i], notes[i], 0.5f);
                    return Synth.Normalize(b, 0.7f);
                case Sfx.Full:
                    b = Synth.Buffer(0.35f);
                    Synth.Tone(b, Synth.Wave.Square, 0, 0.1f, 440, 440, 0.3f, 0.003f, 3000);
                    Synth.Tone(b, Synth.Wave.Square, 0.12f, 0.16f, 349, 349, 0.3f, 0.003f, 3000);
                    return Synth.Normalize(b, 0.5f);
                case Sfx.Crash:
                    b = Synth.Buffer(0.8f);
                    Synth.Noise(b, 0, 0.6f, 1.0f, 3000, 200, 0.7f, lowpassOnly: true);
                    Synth.Tone(b, Synth.Wave.Sine, 0, 0.45f, 140, 35, 1.0f);
                    Synth.Noise(b, 0.05f, 0.35f, 0.5f, 5000, 2500, 3f); // glass
                    Synth.Drive(b, 1.8f);
                    return Synth.Normalize(b, 0.95f);
                case Sfx.Jump:
                    b = Synth.Buffer(0.35f);
                    Synth.Noise(b, 0, 0.3f, 0.5f, 400, 1800, 2f);
                    Synth.Tone(b, Synth.Wave.Sine, 0, 0.2f, 220, 520, 0.5f);
                    return Synth.Normalize(b, 0.6f);
                case Sfx.Land:
                    b = Synth.Buffer(0.25f);
                    Synth.Tone(b, Synth.Wave.Sine, 0, 0.18f, 110, 50, 1f);
                    Synth.Noise(b, 0, 0.15f, 0.4f, 800, 200, 0.7f, lowpassOnly: true);
                    return Synth.Normalize(b, 0.7f);
                case Sfx.Lane:
                    b = Synth.Buffer(0.22f);
                    Synth.Noise(b, 0, 0.2f, 1f, 900, 2200, 1.5f);
                    return Synth.Normalize(b, 0.35f);
                case Sfx.Brake:
                    b = Synth.Buffer(0.55f);
                    Synth.Tone(b, Synth.Wave.Saw, 0, 0.5f, 1150, 900, 0.3f, 0.01f, 3000);
                    Synth.Noise(b, 0, 0.5f, 0.6f, 2500, 1800, 6f);
                    return Synth.Normalize(b, 0.4f);
                case Sfx.PowerUp:
                    b = Synth.Buffer(0.6f);
                    for (int i = 0; i < 6; i++) Synth.Tone(b, Synth.Wave.Square, i * 0.05f, 0.18f, 523 * (float)Math.Pow(2, i / 4.0), 523 * (float)Math.Pow(2, i / 4.0), 0.25f, 0.003f, 5000);
                    Synth.Tone(b, Synth.Wave.Sine, 0, 0.5f, 300, 1200, 0.3f);
                    return Synth.Normalize(b, 0.65f);
                case Sfx.Deliver:
                    // "ka-ching": noise click then bright bells
                    b = Synth.Buffer(0.7f);
                    Synth.Noise(b, 0, 0.08f, 0.5f, 4000, 4000, 1f, highpass: true);
                    float[] bells = { 1568, 2093, 2637 };
                    for (int i = 0; i < bells.Length; i++) Synth.Tone(b, Synth.Wave.Sine, 0.05f + i * 0.07f, 0.55f, bells[i], bells[i], 0.4f);
                    Synth.Tone(b, Synth.Wave.Triangle, 0, 0.25f, 523, 523, 0.3f);
                    return Synth.Normalize(b, 0.7f);
                case Sfx.Miss:
                    b = Synth.Buffer(0.5f);
                    Synth.Tone(b, Synth.Wave.Triangle, 0, 0.2f, 392, 330, 0.5f);
                    Synth.Tone(b, Synth.Wave.Triangle, 0.17f, 0.28f, 330, 262, 0.5f);
                    return Synth.Normalize(b, 0.55f);
                case Sfx.Shield:
                    b = Synth.Buffer(0.5f);
                    Synth.Tone(b, Synth.Wave.Sine, 0, 0.45f, 1400, 500, 0.6f);
                    Synth.Noise(b, 0, 0.3f, 0.4f, 6000, 2000, 2f);
                    return Synth.Normalize(b, 0.7f);
                case Sfx.Purchase:
                    b = Synth.Buffer(0.6f);
                    Synth.Noise(b, 0, 0.06f, 0.6f, 5000, 5000, 1f, highpass: true);
                    Synth.Tone(b, Synth.Wave.Square, 0.04f, 0.12f, 1319, 1319, 0.3f, 0.002f, 6000);
                    Synth.Tone(b, Synth.Wave.Square, 0.14f, 0.3f, 1760, 1760, 0.3f, 0.002f, 6000);
                    return Synth.Normalize(b, 0.6f);
                case Sfx.LevelUp:
                    b = Synth.Buffer(1.2f);
                    float[] lv = { 523, 659, 784, 1047, 1319 };
                    for (int i = 0; i < lv.Length; i++) Synth.Tone(b, Synth.Wave.Square, i * 0.1f, 0.35f, lv[i], lv[i], 0.28f, 0.003f, 5000);
                    Synth.Tone(b, Synth.Wave.Triangle, 0.5f, 0.6f, 1047, 1047, 0.35f);
                    return Synth.Normalize(b, 0.7f);
                case Sfx.Turbo:
                    b = Synth.Buffer(0.9f);
                    Synth.Noise(b, 0, 0.85f, 1f, 300, 3500, 1.2f);
                    Synth.Tone(b, Synth.Wave.Saw, 0, 0.8f, 90, 400, 0.4f, 0.02f, 2000);
                    return Synth.Normalize(b, 0.7f);
                case Sfx.Horn:
                case Sfx.Start:
                    return Horn();
                case Sfx.Click:
                    b = Synth.Buffer(0.08f);
                    Synth.Tone(b, Synth.Wave.Square, 0, 0.06f, 880, 660, 0.4f, 0.001f, 4000);
                    return Synth.Normalize(b, 0.35f);
                case Sfx.GameOver:
                    b = Synth.Buffer(1.0f);
                    float[] go = { 523, 466, 392, 311 };
                    for (int i = 0; i < go.Length; i++) Synth.Tone(b, Synth.Wave.Triangle, i * 0.16f, 0.35f, go[i], go[i], 0.5f);
                    return Synth.Normalize(b, 0.7f);
                case Sfx.NewBest:
                    b = Synth.Buffer(0.9f);
                    float[] nb = { 523, 659, 784, 1047, 784, 1047, 1319 };
                    for (int i = 0; i < nb.Length; i++) Synth.Tone(b, Synth.Wave.Square, i * 0.09f, 0.22f, nb[i], nb[i], 0.3f, 0.003f, 5000);
                    return Synth.Normalize(b, 0.6f);
                default:
                    return Synth.Buffer(0.05f);
            }
        }

        /// <summary>The signature jeepney horn: brassy two-tone "beep–beeep".</summary>
        public static float[] Horn()
        {
            var b = Synth.Buffer(0.75f);
            void Blast(float st, float dur)
            {
                int s0 = (int)(st * Synth.Rate), n = (int)(dur * Synth.Rate);
                double p1 = 0, p2 = 0;
                for (int i = 0; i < n && s0 + i < b.Length; i++)
                {
                    float t = i / (float)Synth.Rate;
                    float bend = Math.Min(1f, t / 0.03f) * 0.03f + 0.97f;
                    p1 += 392 * bend / Synth.Rate;
                    p2 += 494 * bend / Synth.Rate;
                    float env = Math.Min(1f, t / 0.015f) * Math.Min(1f, (dur - t) / 0.04f);
                    b[s0 + i] += (Synth.Osc(Synth.Wave.Saw, p1) + Synth.Osc(Synth.Wave.Saw, p2)) * 0.5f * env;
                }
            }
            Blast(0, 0.14f);
            Blast(0.2f, 0.4f);
            Synth.Drive(b, 3f);
            // band-limit for that tinny trumpet-horn character
            float low = 0, band = 0;
            float fc = (float)(2 * Math.Sin(Math.PI * 900 / Synth.Rate));
            for (int i = 0; i < b.Length; i++)
            {
                low += fc * band;
                float high = b[i] - low - band / 0.7f;
                band += fc * high;
                b[i] = band;
            }
            return Synth.Normalize(b, 0.85f);
        }

        /// <summary>
        /// Seamlessly looping diesel idle. Every partial completes a whole number of
        /// cycles in the loop length; pitch is raised at runtime with AudioSource.pitch.
        /// </summary>
        public static float[] EngineLoop()
        {
            const float seconds = 1f;
            var b = Synth.Buffer(seconds);
            int n = b.Length;
            const int f = 48; // Hz – integer so the loop is seamless
            for (int i = 0; i < n; i++)
            {
                double t = i / (double)Synth.Rate;
                double chug = 0.65 + 0.35 * Math.Sin(2 * Math.PI * 12 * t); // 12 firings/s
                double s = Synth.Osc(Synth.Wave.Saw, f * t) * 0.6 + Synth.Osc(Synth.Wave.Square, f / 2.0 * t) * 0.35 + Math.Sin(2 * Math.PI * 96 * t) * 0.2;
                b[i] = (float)(s * chug);
            }
            // gentle low-pass (wraps around so the loop point stays continuous)
            float lp = 0, a = 1f - (float)Math.Exp(-2 * Math.PI * 450 / Synth.Rate);
            for (int pass = 0; pass < 2; pass++)
                for (int i = 0; i < n; i++)
                {
                    lp += a * (b[i] - lp);
                    if (pass == 1) b[i] = lp;
                }
            return Synth.Normalize(b, 0.5f);
        }
    }
}
