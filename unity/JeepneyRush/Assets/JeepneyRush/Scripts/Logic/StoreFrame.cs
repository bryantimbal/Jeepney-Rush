using System;

namespace JeepneyRush.Logic
{
    /// <summary>
    /// "Store frame" for screenshots: the largest 9:16 rectangle centred on the screen.
    /// Google Play rejects screenshots whose long side is more than twice the short side,
    /// and most phones today are 19.5:9 or taller – so the game can letterbox itself to
    /// 9:16 while taking screenshots. Pixel rectangles have their origin bottom-left.
    /// </summary>
    public static class StoreFrame
    {
        public const int AspectW = 9, AspectH = 16;

        public readonly struct Rect
        {
            public readonly int X, Y, W, H;
            public Rect(int x, int y, int w, int h) { X = x; Y = y; W = w; H = h; }
            public override string ToString() => $"{X},{Y} {W}x{H}";
        }

        /// <summary>The 9:16 frame for a portrait screen (taller screens get bars top and bottom, wider ones at the sides).</summary>
        public static Rect Compute(int screenW, int screenH)
        {
            if (screenW <= 0 || screenH <= 0) return new Rect(0, 0, Math.Max(0, screenW), Math.Max(0, screenH));
            // compare screenW/screenH with 9/16 using integers
            if ((long)screenH * AspectW > (long)screenW * AspectH)
            {
                int h = (int)((long)screenW * AspectH / AspectW);
                h -= h % 2;
                return new Rect(0, (screenH - h) / 2, screenW, h);
            }
            int w = (int)((long)screenH * AspectW / AspectH);
            w -= w % 2;
            return new Rect((screenW - w) / 2, 0, w, screenH);
        }

        /// <summary>Google Play's rule for phone/tablet screenshots: 320–3840 px per side and at most 2:1.</summary>
        public static bool PlayAccepts(int w, int h)
        {
            int lo = Math.Min(w, h), hi = Math.Max(w, h);
            return lo >= 320 && hi <= 3840 && hi <= lo * 2;
        }

        /// <summary>File name like JeepneyRush_2026-11-23_14-05-09.png (sortable, no spaces).</summary>
        public static string FileName(DateTime t, bool clean) => $"JeepneyRush_{t:yyyy-MM-dd_HH-mm-ss}{(clean ? "_clean" : "")}.png";
    }
}
