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
            // menus: the portrait 1920-tall layout fits the screen height exactly, same scale on both axes
            var (rw, rh, m) = ScreenLayout.Panel(w, h, false);
            Assert.AreEqual(1f, m);
            Assert.AreEqual(1920, rh);
            float s = ScreenLayout.UiScale(w, h, false);
            Assert.AreEqual(h, 1920 * s, 0.5f, "the whole 1920-unit layout fits vertically");
            Assert.GreaterOrEqual(w / s, 1080f, "and its 1080-unit width fits too (extra width stays free)");
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
    }
}
