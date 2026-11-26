using System;

namespace JeepneyRush.Logic
{
    /// <summary>
    /// Keeps HUD pop-ups off the road ahead. The "driving corridor" is the part of the screen
    /// where the lane ahead is (a share of the screen; y grows downward).
    /// </summary>
    public static class HudLayout
    {
        public static readonly (float x, float y, float w, float h) CorridorPortrait = (0.27f, 0.22f, 0.46f, 0.58f);
        public static readonly (float x, float y, float w, float h) CorridorLandscape = (0.30f, 0.24f, 0.40f, 0.62f);
        /// <summary>A bubble is at most this share of the screen width, so it always fits beside the road
        /// (portrait has narrower sides than landscape).</summary>
        public const float BubbleMaxWidth = 0.26f, BubbleMaxWidthPortrait = 0.215f;
        public static float BubbleMaxWidthFor(bool landscape) => landscape ? BubbleMaxWidth : BubbleMaxWidthPortrait;
        public const int MaxFloats = 2, MaxBubbles = 2;

        public static bool Overlaps(float x, float y, float w, float h, float pw, float ph, bool landscape)
        {
            var c = landscape ? CorridorLandscape : CorridorPortrait;
            float cx0 = c.x * pw, cx1 = (c.x + c.w) * pw, cy0 = c.y * ph, cy1 = (c.y + c.h) * ph;
            return x < cx1 && x + w > cx0 && y < cy1 && y + h > cy0;
        }

        /// <summary>The left edge for a bubble (panel px) so it doesn't cover the corridor: slid to the nearer side of the road.</summary>
        public static float ClearOfCorridor(float x, float y, float w, float h, float pw, float ph, bool landscape, float margin)
        {
            if (!Overlaps(x, y, w, h, pw, ph, landscape)) return x;
            var c = landscape ? CorridorLandscape : CorridorPortrait;
            float cx0 = c.x * pw, cx1 = (c.x + c.w) * pw;
            bool left = x + w * 0.5f < pw * 0.5f;
            float nx = left ? cx0 - w - 8f : cx1 + 8f;
            return Math.Max(margin, Math.Min(Math.Max(margin, pw - w - margin), nx));
        }
    }
}
