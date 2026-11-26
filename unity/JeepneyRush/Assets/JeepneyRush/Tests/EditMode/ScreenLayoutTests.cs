using System;
using JeepneyRush.Logic;
using NUnit.Framework;

namespace JeepneyRush.Tests
{
    public class ScreenLayoutTests
    {
        [TestCase(2400, 1080)] [TestCase(1920, 1080)] [TestCase(2560, 1600)] [TestCase(2340, 1080)]
        public void LandscapeUiIsUniformAndFitsTheHeight(int w, int h)
        {
            // menus: uniform scale to the height, ~1.5x bigger than squeezing the 1920-unit portrait column in
            var (rw, rh, m) = ScreenLayout.Panel(w, h, false);
            Assert.AreEqual(1f, m);
            Assert.AreEqual(ScreenLayout.LandscapeMenuHeight, rh);
            float s = ScreenLayout.UiScale(w, h, false);
            Assert.GreaterOrEqual(s, h / 1920f * 1.4f, "much bigger than fitting the whole portrait column");
            Assert.GreaterOrEqual(w / s, 1080f * 1.6f, "room for side-by-side layouts");
            // a card taller than the screen shrinks to fit, never gets cut; short ones stay full size
            Assert.AreEqual(1f, ScreenLayout.FitScale(900, 1250));
            Assert.AreEqual(1250f / 1800f, ScreenLayout.FitScale(1800, 1250), 1e-4);
            // driving: bigger HUD, still fits (top HUD + bottom controls are well under 1400 units)
            float sd = ScreenLayout.UiScale(w, h, true);
            Assert.Greater(sd, s);
            Assert.AreEqual(h, ScreenLayout.LandscapeHudHeight * sd, 0.5f);
        }

        [Test]
        public void PortraitIsUnchanged()
        {
            Assert.AreEqual((1080, 1920, 0f), ScreenLayout.Panel(1080, 2400, false));
            Assert.AreEqual((1080, 1920, 0f), ScreenLayout.Panel(1080, 2400, true));
            Assert.AreEqual(1f, ScreenLayout.UiScale(1080, 2400, false), 1e-4);
        }

        [TestCase(2.22f)] [TestCase(1.78f)] [TestCase(1.6f)]
        public void LandscapeCameraIsNeverAFishEye(float aspect)
        {
            float across = ScreenLayout.HorizontalFov(ScreenLayout.ChaseFov(62f, aspect), aspect);
            Assert.LessOrEqual(across, 85f, "chase camera");
            Assert.GreaterOrEqual(across, 62f, "still at least as wide as portrait: all lanes visible");
            float driver = ScreenLayout.HorizontalFov(ScreenLayout.DriverFov(84f, aspect, 75f), aspect);
            Assert.LessOrEqual(driver, 105f);
            Assert.GreaterOrEqual(driver, 84f);
            // portrait as before
            Assert.AreEqual(62f, ScreenLayout.HorizontalFov(ScreenLayout.ChaseFov(62f, 0.75f), 0.75f), 0.5f);
        }

        [Test]
        public void TiltIsSidewaysOnTheScreenInEveryOrientation()
        {
            // the phone tilted to its right (screen right side down) – gravity along the screen's +x
            Assert.AreEqual(0.3f, ScreenLayout.ScreenTiltX(0.3f, 0f, ScreenLayout.Rotation.Portrait), 1e-6);
            Assert.AreEqual(-0.3f, ScreenLayout.ScreenTiltX(0.3f, 0f, ScreenLayout.Rotation.PortraitUpsideDown), 1e-6);
            // landscape left (top of the phone on the left): screen right = phone's -y
            Assert.AreEqual(0.3f, ScreenLayout.ScreenTiltX(0f, -0.3f, ScreenLayout.Rotation.LandscapeLeft), 1e-6);
            Assert.AreEqual(0.3f, ScreenLayout.ScreenTiltX(0f, 0.3f, ScreenLayout.Rotation.LandscapeRight), 1e-6);
        }

        [Test]
        public void RollIsTheSameWhetherOrNotTheSensorIsAlreadyRotated()
        {
            const double deg = Math.PI / 180;
            foreach (var r in new[] { ScreenLayout.Rotation.LandscapeLeft, ScreenLayout.Rotation.LandscapeRight, ScreenLayout.Rotation.Portrait })
                foreach (float roll in new[] { 0f, 15f, -20f })
                    foreach (float lean in new[] { 0.3f, 0.7f }) // phone leaned back: part of gravity goes into the screen
                    {
                        // gravity in SCREEN axes for this roll (down the screen, sideways by the roll), shortened by the lean
                        float sx = (float)(Math.Sin(roll * deg) * lean), sy = (float)(-Math.Cos(roll * deg) * lean);
                        // the same gravity as raw phone-axis readings
                        float ax, ay;
                        switch (r)
                        {
                            case ScreenLayout.Rotation.LandscapeLeft: ax = sy; ay = -sx; break;
                            case ScreenLayout.Rotation.LandscapeRight: ax = -sy; ay = sx; break;
                            default: ax = sx; ay = sy; break;
                        }
                        // detection: raw readings are recognised as raw, rotated ones as rotated (held at a small roll)
                        if (Math.Abs(roll) < 30f)
                        {
                            Assert.AreEqual(r == ScreenLayout.Rotation.Portrait, ScreenLayout.DetectCompensated(ax, ay, r), $"{r} raw");
                            Assert.IsTrue(ScreenLayout.DetectCompensated(sx, sy, r), $"{r} rotated");
                        }
                        Assert.AreEqual(roll, ScreenLayout.RollDegrees(ax, ay, r, false), 0.01f, $"{r} raw roll {roll} lean {lean}");
                        Assert.AreEqual(roll, ScreenLayout.RollDegrees(sx, sy, r, true), 0.01f, $"{r} rotated roll {roll}");
                    }
            Assert.IsNaN(ScreenLayout.RollDegrees(0.05f, -0.05f, ScreenLayout.Rotation.Portrait, true), "phone lying flat");
        }
    }
}
