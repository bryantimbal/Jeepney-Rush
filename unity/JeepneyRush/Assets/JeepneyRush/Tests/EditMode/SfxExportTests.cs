using System;
using System.Linq;
using JeepneyRush.Logic.Audio;
using NUnit.Framework;

namespace JeepneyRush.Tests
{
    public class SfxExportTests
    {
        [Test]
        public void HornIsLoudTwoBlastsAndBright()
        {
            var h = SoundBank.Make(Sfx.Horn);
            Assert.Greater(h.Max(Math.Abs), 0.5f);
            // silence between the short and the long blast ("beep-beeep")
            int gapStart = (int)(0.15f * Synth.Rate), gapEnd = (int)(0.18f * Synth.Rate);
            float gap = 0; for (int i = gapStart; i < gapEnd; i++) gap = Math.Max(gap, Math.Abs(h[i]));
            float blast = 0; for (int i = (int)(0.3f * Synth.Rate); i < (int)(0.5f * Synth.Rate); i++) blast = Math.Max(blast, Math.Abs(h[i]));
            Assert.Less(gap, blast * 0.35f);
#if !UNITY_5_3_OR_NEWER
            // JR_SFX_OUT=folder writes every sound effect as WAV for listening
            var dir = Environment.GetEnvironmentVariable("JR_SFX_OUT");
            if (!string.IsNullOrEmpty(dir))
                foreach (Sfx s in Enum.GetValues(typeof(Sfx))) WriteWav(System.IO.Path.Combine(dir, s + ".wav"), SoundBank.Make(s));
#endif
        }

#if !UNITY_5_3_OR_NEWER
        private static void WriteWav(string path, float[] data)
        {
            using var w = new System.IO.BinaryWriter(System.IO.File.Create(path));
            int bytes = data.Length * 2;
            w.Write("RIFF".ToCharArray()); w.Write(36 + bytes); w.Write("WAVEfmt ".ToCharArray());
            w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(Synth.Rate); w.Write(Synth.Rate * 2); w.Write((short)2); w.Write((short)16);
            w.Write("data".ToCharArray()); w.Write(bytes);
            foreach (var v in data) w.Write((short)(Math.Max(-1f, Math.Min(1f, v)) * 32767));
        }
#endif
    }
}
