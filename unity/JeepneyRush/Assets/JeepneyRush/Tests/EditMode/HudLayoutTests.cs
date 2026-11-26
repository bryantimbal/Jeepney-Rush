using JeepneyRush.Logic;
using NUnit.Framework;

namespace JeepneyRush.Tests
{
    public class HudLayoutTests
    {
        [TestCase(1080f, 1920f, false)] // portrait (reference panel)
        [TestCase(2320f, 1150f, true)]  // landscape (wide phone)
        [TestCase(1600f, 1150f, true)]  // landscape (tablet-ish)
        public void BubblesNeverCoverTheRoadAhead(float pw, float ph, bool landscape)
        {
            float w = pw * HudLayout.BubbleMaxWidthFor(landscape), h = 130f;
            for (float x = 24f; x <= pw - w - 24f; x += pw / 20f) // (the game clamps bubbles inside the margin first)
                for (float y = 140f; y < ph - h; y += ph / 20f)
                {
                    float nx = HudLayout.ClearOfCorridor(x, y, w, h, pw, ph, landscape, 24f);
                    Assert.IsFalse(HudLayout.Overlaps(nx, y, w, h, pw, ph, landscape), $"bubble at {x},{y} moved to {nx} still covers the road");
                    Assert.GreaterOrEqual(nx, 24f - 0.01f);
                    Assert.LessOrEqual(nx + w, pw - 24f + 0.01f, "stays on screen");
                }
        }

        [Test]
        public void BubblesOutsideTheRoadStayPut()
        {
            Assert.AreEqual(30f, HudLayout.ClearOfCorridor(30f, 1500f, 250f, 120f, 1080f, 1920f, false, 24f), 0.01f);
        }
    }
}
