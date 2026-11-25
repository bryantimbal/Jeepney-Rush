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

        /// <summary>
        /// The store frame for the screen: 9:16 in portrait, 16:9 in landscape (bars on the long
        /// sides of the screen, centred).
        /// </summary>
        public static Rect Compute(int screenW, int screenH)
        {
            if (screenW <= 0 || screenH <= 0) return new Rect(0, 0, Math.Max(0, screenW), Math.Max(0, screenH));
            bool landscape = screenW > screenH;
            int aw = landscape ? AspectH : AspectW, ah = landscape ? AspectW : AspectH;
            // compare screenW/screenH with aw/ah using integers
            if ((long)screenH * aw > (long)screenW * ah)
            {
                int h = (int)((long)screenW * ah / aw);
                h -= h % 2;
                return new Rect(0, (screenH - h) / 2, screenW, h);
            }
            int w = (int)((long)screenH * aw / ah);
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
