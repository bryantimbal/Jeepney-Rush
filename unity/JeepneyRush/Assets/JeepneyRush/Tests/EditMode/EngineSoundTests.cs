using System;
using JeepneyRush.Logic.Audio;
using NUnit.Framework;

namespace JeepneyRush.Tests
{
    public class EngineSoundTests
    {
        private static float[] Render(float rpm, float throttle, float seconds = 1.5f)
        {
            var s = new DieselEngineSynth(48000) { TargetRpm = rpm, TargetThrottle = throttle };
            var warm = new float[48000];
            s.Render(warm, 0, warm.Length); // settle the smoothing
            var b = new float[(int)(48000 * seconds)];
            s.Render(b, 0, b.Length);
            return b;
        }

        private static float Rms(float[] b) { double a = 0; foreach (var x in b) a += x * x; return (float)Math.Sqrt(a / b.Length); }

        /// <summary>Magnitude of one frequency (Goertzel).</summary>
        private static double Mag(float[] b, float f)
        {
            double co = 2 * Math.Cos(2 * Math.PI * f / 48000), s1 = 0, s2 = 0;
            foreach (var x in b) { double s0 = x + co * s1 - s2; s2 = s1; s1 = s0; }
            return Math.Sqrt(Math.Max(0, s1 * s1 + s2 * s2 - co * s1 * s2)) / b.Length;
        }

        [TestCase(750f)] [TestCase(1500f)]
        public void FiresFourTimesPerTwoRevolutions(float rpm)
        {
            float fire = rpm / 30f; // 4 cylinders, 4-stroke
            var b = Render(rpm, 0.3f);
            double at = Mag(b, fire);
            foreach (float off in new[] { fire * 0.5f, fire * 0.8f, fire * 1.25f })
                Assert.Greater(at, Mag(b, off) * 2.5, $"{rpm} rpm: firing frequency {fire:0} Hz should dominate {off:0} Hz");
        }

        [Test]
        public void LouderAndBusierWhenRevving()
        {
            Assert.Greater(Rms(Render(2600, 1f)), Rms(Render(750, 0.05f)) * 1.15f);
        }

        [Test]
        public void StaysInRangeAndHasNoDcOffset()
        {
            var b = Render(3000, 1f, 2f);
            double sum = 0;
            foreach (var x in b) { Assert.IsFalse(float.IsNaN(x)); Assert.LessOrEqual(Math.Abs(x), 1f); sum += x; }
            Assert.Less(Math.Abs(sum / b.Length), 0.02);
        }

        [Test]
        public void PullingAwayShiftsUpWithRevDrops()
        {
            var d = new JeepneyDrivetrain();
            float v = 0, prevRpm = 0;
            int drops = 0;
            for (float t = 0; t < 12f; t += 0.02f)
            {
                v += (22f - v) * (1f - (float)Math.Exp(-0.02f * 0.45f));
                d.Tick(0.02f, v, gas: true, brake: false, accelerating: true);
                if (d.Rpm < prevRpm - 150f) drops++;
                prevRpm = d.Rpm;
                Assert.That(d.Rpm, Is.InRange(JeepneyDrivetrain.IdleRpm - 1f, JeepneyDrivetrain.RedLine + 1f));
            }
            Assert.GreaterOrEqual(d.Gear, 4, "should be in a high gear at 22 m/s");
            Assert.GreaterOrEqual(d.ShiftCount, 3);
            Assert.Greater(drops, 0, "audible rev drop at gear changes");
        }

        [Test]
        public void IdlesWhenStoppedAndFreeRevsOnGas()
        {
            var d = new JeepneyDrivetrain();
            for (int i = 0; i < 100; i++) d.Tick(0.02f, 0, false, true, false);
            Assert.AreEqual(JeepneyDrivetrain.IdleRpm, d.Rpm, 30f);
            for (int i = 0; i < 60; i++) d.Tick(0.02f, 0, true, false, false);
            Assert.Greater(d.Rpm, 2000f, "blipping the throttle at a standstill");
            for (int i = 0; i < 200; i++) d.Tick(0.02f, 0, false, false, false);
            Assert.AreEqual(JeepneyDrivetrain.IdleRpm, d.Rpm, 40f);
        }
    }
}
