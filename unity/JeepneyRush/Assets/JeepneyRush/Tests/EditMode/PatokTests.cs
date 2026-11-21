using System;
using System.Linq;
using JeepneyRush.Logic;
using JeepneyRush.Logic.Audio;
using NUnit.Framework;

namespace JeepneyRush.Tests
{
    public class PatokTests
    {
        [Test]
        public void PatokJeepneysHaveSoundSystemAndArt()
        {
            foreach (var id in new[] { "cogeo", "antipolo" })
            {
                Assert.IsTrue(JeepneyCatalog.Exists(id), id);
                var j = JeepneyCatalog.Get(id);
                Assert.IsTrue(j.SoundSystem, id);
                Assert.AreEqual(ArtMotif.Patok, j.Motif);
                Assert.Greater(j.Price, 0);
                Assert.Greater(j.GemPrice, 0);
                StringAssert.Contains("CUBAO", j.Route);
            }
            Assert.AreEqual(2, JeepneyCatalog.All.Count(j => j.SoundSystem));
        }

        [Test]
        public void PatokModelIncludesSpeakersAndIsBiggerThanPlainJeepney()
        {
            var patok = Logic.Geometry.JeepneyBuilder.Build(JeepneyCatalog.Get("cogeo"));
            var plain = Logic.Geometry.JeepneyBuilder.Build(JeepneyCatalog.Get("manila"));
            Assert.Greater(patok.TriangleCount, plain.TriangleCount);
        }

        [Test]
        public void SoundTripIsASeamlessLoudLoop()
        {
            var buf = MusicComposer.RenderSoundTrip();
            Assert.AreEqual(128 * 60.0 / MusicComposer.SoundTripBpm / 4, buf.Length / (double)Synth.Rate, 0.01);
            Assert.IsTrue(buf.All(v => !float.IsNaN(v) && Math.Abs(v) <= 1f));
            double rms = Math.Sqrt(buf.Average(v => (double)v * v));
            var run = MusicComposer.RenderRun();
            double runRms = Math.Sqrt(run.Average(v => (double)v * v));
            Assert.Greater(rms, runRms, "the sound trip should be louder than the normal run music");
            // strong low end: the kick lands on every beat
            int beat = (int)(60.0 / MusicComposer.SoundTripBpm * Synth.Rate);
            double onBeat = 0, offBeat = 0;
            for (int b = 0; b < 28; b++)
            {
                for (int i = 0; i < 2000; i++) onBeat += Math.Abs(buf[b * beat + 200 + i]);
                for (int i = 0; i < 2000; i++) offBeat += Math.Abs(buf[b * beat + beat * 3 / 4 + i]);
            }
            Assert.Greater(onBeat, offBeat);
#if !UNITY_5_3_OR_NEWER
            // JR_WAV_OUT=path writes the loop to a WAV file so it can be listened to
            var path = Environment.GetEnvironmentVariable("JR_WAV_OUT");
            if (!string.IsNullOrEmpty(path)) WriteWav(path, buf);
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
