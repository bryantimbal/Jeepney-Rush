using System;
using System.Collections.Generic;

namespace JeepneyRush.Logic.Audio
{
    public enum Sfx { Coin, Pickup, Full, Crash, Jump, Land, Lane, Brake, Horn, Click, GameOver, NewBest, Start, PowerUp, Deliver, Miss, Shield, Purchase, LevelUp, Turbo, Whistle, CoinRush, Complain, Rev, Bump, Splash, Thunder, ReverseBeep, Blinker, Fare, Refuel, Sputter, Bell, BrakeSqueal, AirBrake, Siren, RoofBang, Wiper, Tapik, NearMiss, ComboUp,
        // talyer horns (customization)
        HornToot, HornMusical, HornAir, HornCucaracha }

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
                case Sfx.Whistle:
                    // traffic enforcer's whistle: two sharp trilled blasts
                    b = Synth.Buffer(0.75f);
                    foreach (var (st, dur) in new[] { (0f, 0.18f), (0.26f, 0.42f) })
                    {
                        int s0 = (int)(st * Synth.Rate), n = (int)(dur * Synth.Rate);
                        double ph = 0;
                        for (int i = 0; i < n && s0 + i < b.Length; i++)
                        {
                            double t = i / (double)Synth.Rate;
                            ph += (2900 + 140 * Math.Sin(2 * Math.PI * 38 * t)) / Synth.Rate; // pea trill
                            float env = (float)(Math.Min(1, t / 0.01) * Math.Min(1, (dur - t) / 0.03));
                            b[s0 + i] += (float)Math.Sin(ph * 2 * Math.PI) * env * 0.6f;
                        }
                    }
                    Synth.Noise(b, 0, 0.7f, 0.08f, 5000, 5000, 1f, highpass: true);
                    return Synth.Normalize(b, 0.6f);
                case Sfx.NearMiss:
                    // whoosh of air between two vehicles + a bright "ting"
                    b = Synth.Buffer(0.5f);
                    Synth.Noise(b, 0, 0.32f, 0.8f, 600, 3200, 1.4f);
                    Synth.Tone(b, Synth.Wave.Sine, 0.12f, 0.3f, 1760, 1760, 0.35f, 0.002f);
                    Synth.Tone(b, Synth.Wave.Sine, 0.12f, 0.3f, 2637, 2637, 0.18f, 0.002f);
                    return Synth.Normalize(b, 0.6f);
                case Sfx.ComboUp:
                    // rising three-note fanfare for a new multiplier step
                    b = Synth.Buffer(0.6f);
                    float[] up = { 784, 988, 1319 };
                    for (int i = 0; i < up.Length; i++) Synth.Tone(b, Synth.Wave.Square, i * 0.08f, 0.22f, up[i], up[i], 0.25f, 0.002f, 6000);
                    Synth.Tone(b, Synth.Wave.Triangle, 0.24f, 0.32f, 1568, 1568, 0.3f, 0.002f);
                    return Synth.Normalize(b, 0.6f);
                case Sfx.CoinRush:
                    b = Synth.Buffer(0.9f);
                    float[] rush = { 1047, 1319, 1568, 2093, 2637, 3136 };
                    for (int i = 0; i < rush.Length; i++) Synth.Tone(b, Synth.Wave.Square, i * 0.06f, 0.3f, rush[i], rush[i], 0.22f, 0.002f, 7000);
                    for (int i = 0; i < 4; i++) Synth.Tone(b, Synth.Wave.Sine, 0.38f + i * 0.08f, 0.4f, 2637 + i * 220, 2637 + i * 220, 0.15f);
                    return Synth.Normalize(b, 0.65f);
                case Sfx.Complain:
                    // grumbly "hmph!" murmur of the passengers
                    b = Synth.Buffer(0.5f);
                    Synth.Tone(b, Synth.Wave.Saw, 0, 0.22f, 220, 160, 0.3f, 0.01f, 900);
                    Synth.Tone(b, Synth.Wave.Saw, 0.2f, 0.25f, 260, 180, 0.3f, 0.01f, 900);
                    Synth.Noise(b, 0, 0.45f, 0.15f, 700, 400, 1.5f);
                    return Synth.Normalize(b, 0.5f);
                case Sfx.Rev:
                    return Rev();
                case Sfx.Blinker:
                    // relay "tik" of the turn signal
                    b = Synth.Buffer(0.05f);
                    Synth.Noise(b, 0, 0.012f, 0.9f, 3200, 1800, 2f);
                    Synth.Tone(b, Synth.Wave.Square, 0, 0.02f, 700, 500, 0.4f, 0.001f, 3000);
                    return Synth.Normalize(b, 0.4f);
                case Sfx.Fare:
                    // coins clinking into the driver's hand
                    b = Synth.Buffer(0.4f);
                    Synth.Tone(b, Synth.Wave.Sine, 0, 0.22f, 2637, 2637, 0.4f, 0.001f);
                    Synth.Tone(b, Synth.Wave.Sine, 0.07f, 0.25f, 3520, 3520, 0.35f, 0.001f);
                    Synth.Noise(b, 0, 0.03f, 0.4f, 6000, 6000, 1f, highpass: true);
                    Synth.Noise(b, 0.07f, 0.03f, 0.3f, 6000, 6000, 1f, highpass: true);
                    return Synth.Normalize(b, 0.55f);
                case Sfx.Refuel:
                    // pump motor whirr + fuel gurgling into the tank
                    b = Synth.Buffer(1.2f);
                    Synth.Tone(b, Synth.Wave.Saw, 0, 1.15f, 118, 124, 0.25f, 0.05f, 700);
                    Synth.Noise(b, 0, 1.15f, 0.35f, 900, 600, 2.5f);
                    for (int g = 0; g < 9; g++) Synth.Tone(b, Synth.Wave.Sine, 0.08f + g * 0.12f, 0.08f, 420 + (g % 3) * 90, 300, 0.25f, 0.005f);
                    return Synth.Normalize(b, 0.5f);
                case Sfx.Bell:
                    // pull-cord bell of a patok jeepney: two bright metallic "ding"s
                    b = Synth.Buffer(0.9f);
                    foreach (float t0 in new[] { 0f, 0.28f })
                    {
                        Synth.Tone(b, Synth.Wave.Sine, t0, 0.6f, 1760, 1755, 0.5f, 0.002f);
                        Synth.Tone(b, Synth.Wave.Sine, t0, 0.45f, 4400, 4390, 0.18f, 0.001f);
                        Synth.Tone(b, Synth.Wave.Sine, t0, 0.35f, 2637, 2630, 0.22f, 0.001f);
                    }
                    return Synth.Normalize(b, 0.7f);
                case Sfx.BrakeSqueal:
                    // brake shoes squealing as the jeepney slows to a stop
                    b = Synth.Buffer(1.0f);
                    {
                        double p = 0;
                        for (int i = 0; i < b.Length; i++)
                        {
                            float t = i / (float)Synth.Rate;
                            double f = 2350 + 140 * Math.Sin(t * 37) - 250 * t;
                            p += f / Synth.Rate;
                            float env = Math.Min(1f, t / 0.06f) * (float)Math.Exp(-1.6 * t) * (t > 0.85f ? (1f - t) / 0.15f : 1f);
                            b[i] += (float)Math.Sin(p * 2 * Math.PI) * env * 0.35f * (0.75f + 0.25f * (float)Math.Sin(t * 90));
                        }
                    }
                    Synth.Noise(b, 0, 0.9f, 0.25f, 3200, 2600, 5f);
                    return Synth.Normalize(b, 0.45f);
                case Sfx.AirBrake:
                    // "pssssh" once the jeepney has come to a complete stop
                    b = Synth.Buffer(0.8f);
                    Synth.Noise(b, 0.02f, 0.75f, 0.9f, 5200, 3000, 0.7f, highpass: true);
                    Synth.Noise(b, 0, 0.12f, 0.6f, 1500, 900, 1.5f);
                    return Synth.Normalize(b, 0.5f);
                case Sfx.Siren:
                    // police wail: rising and falling square/saw tone (one cycle; replayed while chasing)
                    b = Synth.Buffer(1.3f);
                    {
                        double p = 0;
                        for (int i = 0; i < b.Length; i++)
                        {
                            float t = i / (float)Synth.Rate;
                            double f = 720 + 520 * (0.5 - 0.5 * Math.Cos(t / 1.3 * 2 * Math.PI));
                            p += f / Synth.Rate;
                            float env = Math.Min(1f, t / 0.05f) * Math.Min(1f, (1.3f - t) / 0.05f);
                            b[i] = (Synth.Osc(Synth.Wave.Square, p) * 0.35f + Synth.Osc(Synth.Wave.Saw, p * 2) * 0.15f) * env;
                        }
                    }
                    return Synth.Normalize(b, 0.5f);
                case Sfx.RoofBang:
                    // panicking riders banging on the jeepney roof
                    b = Synth.Buffer(0.6f);
                    foreach (float t0 in new[] { 0f, 0.16f, 0.3f })
                    {
                        Synth.Noise(b, t0, 0.12f, 0.9f, 900, 300, 1.2f, lowpassOnly: true);
                        Synth.Tone(b, Synth.Wave.Sine, t0, 0.12f, 140, 70, 0.7f, 0.001f);
                    }
                    return Synth.Normalize(b, 0.7f);
                case Sfx.Tapik:
                    // the barker slaps the jeepney's side twice: "sige, larga!"
                    b = Synth.Buffer(0.45f);
                    foreach (float t0 in new[] { 0f, 0.17f })
                    {
                        Synth.Noise(b, t0, 0.07f, 1f, 2600, 700, 1.6f, lowpassOnly: true);
                        Synth.Tone(b, Synth.Wave.Sine, t0, 0.1f, 230, 120, 0.55f, 0.001f);
                    }
                    return Synth.Normalize(b, 0.6f);
                case Sfx.Wiper:
                    // rubber blade swishing across wet glass, with a soft thunk at the end of the stroke
                    b = Synth.Buffer(0.45f);
                    Synth.Noise(b, 0, 0.32f, 0.5f, 1800, 900, 1.4f);
                    Synth.Tone(b, Synth.Wave.Sine, 0.33f, 0.08f, 120, 80, 0.35f, 0.002f);
                    return Synth.Normalize(b, 0.35f);
                case Sfx.Sputter:
                    // engine coughing on the last drops of diesel
                    b = Synth.Buffer(0.9f);
                    foreach (float t0 in new[] { 0f, 0.22f, 0.5f })
                    {
                        Synth.Noise(b, t0, 0.12f, 0.9f, 500, 150, 1f, lowpassOnly: true);
                        Synth.Tone(b, Synth.Wave.Square, t0, 0.1f, 70, 40, 0.6f, 0.002f, 600);
                    }
                    Synth.Drive(b, 2f);
                    return Synth.Normalize(b, 0.7f);
                case Sfx.ReverseBeep:
                    // truck-style reversing alarm "beep"
                    b = Synth.Buffer(0.4f);
                    Synth.Tone(b, Synth.Wave.Square, 0, 0.32f, 1050, 1050, 0.5f, 0.004f, 3500);
                    return Synth.Normalize(b, 0.45f);
                case Sfx.Bump:
                    // tyres thumping over a speed hump + rattling body panels
                    b = Synth.Buffer(0.55f);
                    Synth.Tone(b, Synth.Wave.Sine, 0, 0.16f, 95, 45, 1f);
                    Synth.Tone(b, Synth.Wave.Sine, 0.2f, 0.16f, 90, 45, 0.85f);
                    Synth.Noise(b, 0, 0.12f, 0.5f, 900, 300, 0.8f, lowpassOnly: true);
                    Synth.Noise(b, 0.2f, 0.12f, 0.45f, 900, 300, 0.8f, lowpassOnly: true);
                    Synth.Noise(b, 0.02f, 0.45f, 0.18f, 3500, 2500, 4f); // tin rattle
                    return Synth.Normalize(b, 0.8f);
                case Sfx.Splash:
                    b = Synth.Buffer(0.9f);
                    Synth.Noise(b, 0, 0.8f, 1f, 2500, 700, 0.9f);
                    Synth.Noise(b, 0.05f, 0.6f, 0.5f, 6000, 3000, 1f, highpass: true);
                    Synth.Tone(b, Synth.Wave.Sine, 0, 0.2f, 180, 70, 0.4f);
                    return Synth.Normalize(b, 0.7f);
                case Sfx.Thunder:
                    b = Synth.Buffer(2.6f);
                    Synth.Noise(b, 0, 0.25f, 0.8f, 3000, 800, 0.7f, lowpassOnly: true); // crack
                    Synth.Noise(b, 0.08f, 2.4f, 1f, 260, 70, 0.7f, lowpassOnly: true);  // rumble
                    Synth.Noise(b, 0.6f, 1.6f, 0.6f, 160, 60, 0.7f, lowpassOnly: true);
                    Synth.Tone(b, Synth.Wave.Sine, 0.05f, 1.8f, 55, 32, 0.5f, 0.05f);
                    Synth.Drive(b, 1.5f);
                    return Synth.Normalize(b, 0.9f);
                case Sfx.Horn:
                case Sfx.Start:
                    return Horn();
                case Sfx.HornToot:   // quick "toot-toot-toot"
                    return TwinHorn(0.82f, 415.3f, 523.3f, new[] { (0f, 0.1f, 0.9f), (0.16f, 0.1f, 0.9f), (0.32f, 0.22f, 1f) });
                case Sfx.HornMusical: // an original little fiesta tune on a five-trumpet musical horn
                    {
                        float[] tune = { 523.3f, 659.3f, 784f, 659.3f, 880f, 784f };
                        var buf = Synth.Buffer(1.45f);
                        for (int i = 0; i < tune.Length; i++)
                        {
                            var one = TwinHorn(0.3f, tune[i], tune[i] * 1.5f, new[] { (0f, i == tune.Length - 1 ? 0.28f : 0.17f, 1f) }, raw: true);
                            int at = (int)(i * 0.2f * Synth.Rate);
                            for (int k = 0; k < one.Length && at + k < buf.Length; k++) buf[at + k] += one[k];
                        }
                        return Bell(buf);
                    }
                case Sfx.HornCucaracha:
                    {
                        // "La Cucaracha" (traditional Mexican folk song, public domain) on a musical
                        // trumpet horn, the way many jeepneys play it: la cu-ca-ra-CHA, la cu-ca-ra-CHA
                        var song = new (float f, float st, float dur)[]
                        {
                            (392f, 0f, 0.11f), (392f, 0.14f, 0.11f), (392f, 0.28f, 0.11f), (523.3f, 0.42f, 0.24f), (659.3f, 0.7f, 0.36f),
                            (392f, 1.16f, 0.11f), (392f, 1.3f, 0.11f), (392f, 1.44f, 0.11f), (523.3f, 1.58f, 0.24f), (659.3f, 1.86f, 0.5f),
                        };
                        var buf = Synth.Buffer(2.45f);
                        foreach (var (f, st, dur) in song)
                        {
                            var one = TwinHorn(dur + 0.05f, f, f * 2.003f, new[] { (0f, dur, 1f) }, raw: true, duty2: 0.18);
                            int at = (int)(st * Synth.Rate);
                            for (int k = 0; k < one.Length && at + k < buf.Length; k++) buf[at + k] += one[k];
                        }
                        return Bell(buf);
                    }
                case Sfx.HornAir:    // deep truck-style air horn (patok)
                    return TwinHorn(1.0f, 233.1f, 293.7f, new[] { (0f, 0.85f, 1f) }, duty1: 0.45, duty2: 0.5, drive: 3f);
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
        /// <summary>
        /// Jeepney horn: the twin electric "trumpet" horns most jeepneys carry – two buzzy
        /// diaphragm tones a major third apart (~415 / 523 Hz), each with a short pitch scoop as
        /// the diaphragm starts, a nasal bell resonance (~1.1 and ~2.6 kHz formants) and a little
        /// overdrive from the cheap horn relay. Played as the classic short-long "beep-beeep".
        /// </summary>
        public static float[] Horn() =>
            TwinHorn(0.78f, 415.3f, 523.3f, new[] { (0f, 0.13f, 0.9f), (0.19f, 0.52f, 1f) });

        /// <summary>
        /// Twin diaphragm horn (see <see cref="Horn"/>) with any two pitches and blast pattern.
        /// <paramref name="raw"/> skips the bell filter (used when notes are mixed first).
        /// </summary>
        private static float[] TwinHorn(float seconds, float f1, float f2, (float st, float dur, float level)[] blasts,
            bool raw = false, double duty1 = 0.28, double duty2 = 0.32, float drive = 2.2f)
        {
            var b = Synth.Buffer(seconds);
            void Blast(float st, float dur, float level)
            {
                int s0 = (int)(st * Synth.Rate), n = (int)(dur * Synth.Rate);
                double p1 = 0, p2 = 0, pv = 0;
                for (int i = 0; i < n && s0 + i < b.Length; i++)
                {
                    float t = i / (float)Synth.Rate;
                    float scoop = 0.94f + 0.06f * Math.Min(1f, t / 0.045f);
                    pv += 6.5 / Synth.Rate;
                    float wobble = 1f + 0.004f * (float)Math.Sin(2 * Math.PI * pv);
                    p1 += f1 * scoop * wobble / Synth.Rate;
                    p2 += f2 * scoop * wobble / Synth.Rate;
                    float a = Pulse(p1, duty1) + Pulse(p2, duty2) * 0.9f;
                    float env = Math.Min(1f, t / 0.008f) * Math.Min(1f, (dur - t) / 0.025f);
                    b[s0 + i] += a * env * level;
                }
            }
            float Pulse(double phase, double duty) => (phase - Math.Floor(phase)) < duty ? 1f : -0.38f;
            foreach (var (st, dur, level) in blasts) Blast(st, dur, level);
            Synth.Drive(b, drive);
            return raw ? b : Bell(b);
        }

        /// <summary>Trumpet bell resonances + low cut, normalised (shared by every horn).</summary>
        private static float[] Bell(float[] b)
        {
            // trumpet bell: two resonant band-passes summed, plus a gentle low cut
            var outp = new float[b.Length];
            foreach (var (fc, q, gain) in new[] { (1100f, 3.2f, 1f), (2600f, 4f, 0.55f), (520f, 1.6f, 0.45f) })
            {
                float low = 0, band = 0;
                float f = (float)(2 * Math.Sin(Math.PI * fc / Synth.Rate));
                for (int i = 0; i < b.Length; i++)
                {
                    low += f * band;
                    float high = b[i] - low - band / q;
                    band += f * high;
                    outp[i] += band * gain;
                }
            }
            float hp = 0, prev = 0;
            for (int i = 0; i < outp.Length; i++) { hp = 0.985f * (hp + outp[i] - prev); prev = outp[i]; outp[i] = hp; }
            return Synth.Normalize(outp, 0.85f);
        }

        /// <summary>
        /// Stepping on the accelerator: the old surplus diesel of a jeepney clatters up
        /// from idle to high revs ("brrRRRAAAM") with a throaty exhaust and loose-panel rattle.
        /// Built from individual cylinder firings so it sounds like an engine, not a tone.
        /// </summary>
        public static float[] Rev()
        {
            const float dur = 1.4f;
            var b = Synth.Buffer(dur);
            var rng = new Random(77);
            double t = 0, nextFire = 0;
            float lp = 0, lp2 = 0;
            for (int i = 0; i < b.Length; i++, t += 1.0 / Synth.Rate)
            {
                // rpm curve: quick throttle blip up, hold, then ease back toward cruise
                double rise = Math.Min(1, t / 0.55);
                double rpmShape = t < 0.55 ? rise * rise * (3 - 2 * rise) : 1 - 0.35 * Math.Min(1, (t - 0.55) / 0.85);
                double firingHz = 14 + 46 * rpmShape;
                float sample = 0;
                if (t >= nextFire)
                {
                    nextFire = t + 1.0 / firingHz * (0.94 + 0.12 * rng.NextDouble()); // uneven old engine
                }
                double since = t - (nextFire - 1.0 / firingHz);
                // each firing: a sharp knock (diesel clatter) plus a low thump
                double knock = Math.Exp(-since * 260) * (rng.NextDouble() * 2 - 1) * 0.9;
                double thump = Math.Exp(-since * 55) * Math.Sin(2 * Math.PI * 70 * since) * 1.2;
                sample = (float)(knock + thump);
                // exhaust roar: band of noise that opens up with throttle
                double roar = (rng.NextDouble() * 2 - 1) * (0.15 + 0.5 * rpmShape);
                lp += (float)(0.06 + 0.1 * rpmShape) * ((float)roar - lp);
                sample += lp * 2.2f;
                float env = (float)(Math.Min(1, t / 0.03) * Math.Min(1, (dur - t) / 0.35));
                lp2 += 0.35f * (sample - lp2); // tame the very top
                b[i] = lp2 * env;
            }
            Synth.Drive(b, 2.2f);
            return Synth.Normalize(b, 0.8f);
        }

        /// <summary>Seamless rain loop: hiss plus random droplet ticks (2 s).</summary>
        public static float[] RainLoop()
        {
            var b = Synth.Buffer(2f);
            int n = b.Length;
            var rng = new Random(5);
            float lp = 0, hp = 0;
            for (int pass = 0; pass < 2; pass++)
                for (int i = 0; i < n; i++)
                {
                    float x = (float)(rng.NextDouble() * 2 - 1);
                    lp += 0.25f * (x - lp);
                    hp = lp - hp * 0.2f;
                    if (pass == 1) b[i] = hp * 0.5f;
                }
            // droplets
            for (int d = 0; d < 140; d++)
            {
                int at = rng.Next(n);
                float f = 1800 + (float)rng.NextDouble() * 2600;
                for (int k = 0; k < 600; k++)
                    b[(at + k) % n] += (float)(Math.Sin(2 * Math.PI * f * k / Synth.Rate) * Math.Exp(-k / 90.0) * 0.35);
            }
            // cross-fade the ends so the loop is seamless
            int fade = Synth.Rate / 20;
            for (int i = 0; i < fade; i++)
            {
                float w = i / (float)fade;
                b[i] = b[i] * w + b[n - fade + i] * (1 - w);
            }
            Array.Resize(ref b, n - fade);
            return Synth.Normalize(b, 0.6f);
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
                double since = (t * 12) - Math.Floor(t * 12);  // time since last firing (in firing periods)
                double knock = Math.Exp(-since * 22) * Synth.Osc(Synth.Wave.Noise, 0) * 0.45;
                b[i] = (float)(s * chug + knock);
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
