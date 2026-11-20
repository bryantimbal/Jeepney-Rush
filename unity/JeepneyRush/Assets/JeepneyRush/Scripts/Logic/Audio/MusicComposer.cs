using System;
using System.Collections.Generic;

namespace JeepneyRush.Logic.Audio
{
    /// <summary>
    /// Original Filipino-inspired instrumentals rendered to seamless loops:
    /// rondalla-style tremolo plucks (bandurria feel) over bass and light percussion.
    /// </summary>
    public static class MusicComposer
    {
        private struct Note { public int Step; public int Midi; public int Len; public Note(int s, int m, int l) { Step = s; Midi = m; Len = l; } }

        private sealed class Track
        {
            public float Bpm;
            public int Steps;
            public bool Drums;
            public readonly List<Note> Melody = new List<Note>();
            public readonly List<Note> Bass = new List<Note>();
            public readonly List<(int step, int[] notes)> Chords = new List<(int, int[])>();
        }

        private const int G4 = 67, A4 = 69, B4 = 71, C5 = 72, D5 = 74, E5 = 76, Fs5 = 78, G5 = 79, A5 = 81;
        private const int D4 = 62, E4 = 64, Fs4 = 66;
        private const int G2 = 43, A2 = 45, B2 = 47, C3 = 48, D3 = 50, E3 = 52;

        private static Track RunTrack()
        {
            var t = new Track { Bpm = 132, Steps = 128, Drums = true };
            int[,] mel =
            {
                {0,D5,2},{2,G5,2},{4,Fs5,2},{6,E5,2},{8,D5,4},{12,B4,2},{14,D5,2},
                {16,C5,2},{18,E5,2},{20,D5,2},{22,C5,2},{24,B4,6},{30,A4,2},
                {32,B4,2},{34,D5,2},{36,G5,3},{39,Fs5,1},{40,E5,2},{42,D5,2},{44,C5,2},{46,B4,2},
                {48,A4,4},{52,D5,2},{54,C5,2},{56,B4,2},{58,A4,2},{60,G4,4},
                {64,B4,2},{66,C5,2},{68,D5,4},{72,E5,2},{74,D5,2},{76,C5,2},{78,B4,2},
                {80,A4,2},{82,B4,2},{84,C5,4},{88,E5,2},{90,D5,2},{92,C5,2},{94,A4,2},
                {96,G5,2},{98,Fs5,2},{100,E5,2},{102,D5,2},{104,E5,2},{106,Fs5,2},{108,G5,4},
                {112,A5,2},{114,G5,2},{116,Fs5,2},{118,D5,2},{120,G5,6},
            };
            for (int i = 0; i < mel.GetLength(0); i++) t.Melody.Add(new Note(mel[i, 0], mel[i, 1], mel[i, 2]));
            var prog = new (int root, int[] notes)[]
            {
                (G2, new[]{G4,B4,D5}), (C3, new[]{G4,C5,E5}), (D3, new[]{Fs4,A4,D5}), (G2, new[]{G4,B4,D5}),
                (E3, new[]{E4,G4,B4}), (C3, new[]{E4,G4,C5}), (D3, new[]{Fs4,A4,D5}), (G2, new[]{D4,G4,B4}),
            };
            for (int bar = 0; bar < prog.Length; bar++)
            {
                int s = bar * 16, r = prog[bar].root;
                t.Bass.Add(new Note(s, r, 3)); t.Bass.Add(new Note(s + 4, r + 12, 2)); t.Bass.Add(new Note(s + 6, r + 7, 2));
                t.Bass.Add(new Note(s + 8, r, 3)); t.Bass.Add(new Note(s + 12, r + 7, 2)); t.Bass.Add(new Note(s + 14, r + 12, 2));
                foreach (int off in new[] { 2, 6, 10, 14 }) t.Chords.Add((s + off, prog[bar].notes));
            }
            return t;
        }

        private static Track MenuTrack()
        {
            var t = new Track { Bpm = 96, Steps = 64, Drums = false };
            int[,] mel =
            {
                {0,B4,4},{4,D5,2},{6,E5,2},{8,D5,6},{14,B4,2},
                {16,C5,4},{20,E5,2},{22,D5,2},{24,C5,6},{30,A4,2},
                {32,B4,4},{36,G4,2},{38,A4,2},{40,B4,4},{44,D5,4},
                {48,C5,3},{51,B4,1},{52,A4,4},{56,G4,8},
            };
            for (int i = 0; i < mel.GetLength(0); i++) t.Melody.Add(new Note(mel[i, 0], mel[i, 1], mel[i, 2]));
            var prog = new (int root, int[] notes)[] { (G2, new[]{G4,B4,D5}), (A2, new[]{E4,A4,C5}), (B2, new[]{D4,G4,B4}), (D3, new[]{Fs4,A4,D5}) };
            for (int bar = 0; bar < prog.Length; bar++)
            {
                int s = bar * 16, r = prog[bar].root;
                t.Bass.Add(new Note(s, r, 4)); t.Bass.Add(new Note(s + 8, r + 7, 4));
                t.Chords.Add((s + 4, prog[bar].notes)); t.Chords.Add((s + 12, prog[bar].notes));
            }
            return t;
        }

        private static double Hz(int midi) => 440.0 * Math.Pow(2, (midi - 69) / 12.0);

        public static float[] RenderRun() => Render(RunTrack());
        public static float[] RenderMenu() => Render(MenuTrack());

        private static float[] Render(Track tr)
        {
            double stepDur = 60.0 / tr.Bpm / 4.0;
            int len = (int)Math.Round(tr.Steps * stepDur * Synth.Rate);
            var buf = new float[len];
            var rng = new Random(7);
            foreach (var n in tr.Melody) Pluck(buf, n.Step * stepDur, n.Len * stepDur, Hz(n.Midi), 0.16f, true);
            foreach (var n in tr.Bass) Bass(buf, n.Step * stepDur, n.Len * stepDur, Hz(n.Midi));
            foreach (var (step, notes) in tr.Chords) foreach (int m in notes) Pluck(buf, step * stepDur, stepDur * 1.5, Hz(m), 0.045f, false);
            if (tr.Drums)
                for (int s = 0; s < tr.Steps; s++)
                {
                    double t = s * stepDur;
                    if (s % 8 == 0) Kick(buf, t);
                    if (s % 8 == 4) NoiseHit(buf, t, 0.12f, 0.12, rng, 0.35f);
                    if (s % 2 == 0) NoiseHit(buf, t, s % 4 == 2 ? 0.05f : 0.025f, 0.04, rng, 0.9f);
                }
            return Synth.Normalize(buf, 0.8f);
        }

        // Writes wrap around the end so the loop is seamless.
        private static void Add(float[] buf, int i, float v) => buf[i % buf.Length] += v;

        private static void Pluck(float[] buf, double start, double dur, double f, float vol, bool tremolo)
        {
            int repeats = tremolo && dur > 0.2 ? (int)(dur / 0.07) : 1;
            for (int r = 0; r < repeats; r++)
            {
                double st = start + r * 0.07;
                double d = repeats > 1 ? 0.09 : Math.Min(0.5, dur + 0.1);
                float v = vol * (r == 0 ? 1f : 0.6f);
                int s0 = (int)(st * Synth.Rate), n = (int)(d * Synth.Rate);
                double p1 = 0, p2 = 0;
                float lp = 0;
                for (int i = 0; i < n; i++)
                {
                    double t = i / (double)Synth.Rate;
                    p1 += f / Synth.Rate;
                    p2 += f * 2.002 / Synth.Rate;
                    double cutoff = f * (1.5 + 4.5 * Math.Exp(-t / 0.05));
                    float a = 1f - (float)Math.Exp(-2 * Math.PI * cutoff / Synth.Rate);
                    float s = Synth.Osc(Synth.Wave.Triangle, p1) + 0.25f * Synth.Osc(Synth.Wave.Square, p2);
                    lp += a * (s - lp);
                    float env = (float)(Math.Min(1.0, t / 0.004) * Math.Exp(-t / (d * 0.3)));
                    Add(buf, s0 + i, lp * env * v);
                }
            }
        }

        private static void Bass(float[] buf, double start, double dur, double f)
        {
            int s0 = (int)(start * Synth.Rate), n = (int)(dur * Synth.Rate);
            double p = 0;
            for (int i = 0; i < n; i++)
            {
                double t = i / (double)Synth.Rate;
                p += f / Synth.Rate;
                float env = (float)(Math.Min(1.0, t / 0.01) * Math.Exp(-3 * t / dur));
                Add(buf, s0 + i, Synth.Osc(Synth.Wave.Triangle, p) * env * 0.22f);
            }
        }

        private static void Kick(float[] buf, double start)
        {
            int s0 = (int)(start * Synth.Rate), n = (int)(0.18 * Synth.Rate);
            double p = 0;
            for (int i = 0; i < n; i++)
            {
                double t = i / (double)Synth.Rate;
                p += (45 + 95 * Math.Exp(-t / 0.04)) / Synth.Rate;
                Add(buf, s0 + i, (float)(Math.Sin(p * 2 * Math.PI) * Math.Exp(-t / 0.05) * 0.35));
            }
        }

        private static void NoiseHit(float[] buf, double start, float vol, double dur, Random rng, float brightness)
        {
            int s0 = (int)(start * Synth.Rate), n = (int)(dur * Synth.Rate);
            float prev = 0;
            for (int i = 0; i < n; i++)
            {
                double t = i / (double)Synth.Rate;
                float x = (float)(rng.NextDouble() * 2 - 1);
                float hp = x - prev * (1 - brightness); // crude high-pass for hats
                prev = x;
                Add(buf, s0 + i, (float)(hp * vol * Math.Exp(-4 * t / dur)));
            }
        }
    }
}
