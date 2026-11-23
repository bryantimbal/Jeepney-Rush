using System;
using JeepneyRush.Logic;
using NUnit.Framework;

namespace JeepneyRush.Tests
{
    public class StoreFrameTests
    {
        [TestCase(1080, 2400)] [TestCase(1080, 2340)] [TestCase(1440, 3200)] [TestCase(720, 1600)]
        [TestCase(1080, 1920)] [TestCase(1200, 1920)] [TestCase(1536, 2048)] [TestCase(1600, 2560)]
        public void FrameIsNineBySixteenCentredAndPlayAccepts(int w, int h)
        {
            var r = StoreFrame.Compute(w, h);
            Assert.That(Math.Abs(r.W * 16.0 / r.H - 9.0), Is.LessThan(0.02), r.ToString());
            Assert.LessOrEqual(r.W, w); Assert.LessOrEqual(r.H, h);
            Assert.IsTrue(r.W == w || r.H == h, "fills one side");
            Assert.LessOrEqual(Math.Abs((w - r.W) - 2 * r.X), 1, "centred horizontally");
            Assert.LessOrEqual(Math.Abs((h - r.H) - 2 * r.Y), 1, "centred vertically");
            Assert.IsTrue(StoreFrame.PlayAccepts(r.W, r.H));
        }

        [Test]
        public void TallPhonesNeedTheFrame()
        {
            Assert.IsFalse(StoreFrame.PlayAccepts(1080, 2400), "19.5:9 and taller is rejected by Play");
            Assert.IsTrue(StoreFrame.PlayAccepts(1080, 1920));
            Assert.AreEqual("JeepneyRush_2026-11-23_14-05-09_clean.png", StoreFrame.FileName(new DateTime(2026, 11, 23, 14, 5, 9), true));
        }
    }
}
