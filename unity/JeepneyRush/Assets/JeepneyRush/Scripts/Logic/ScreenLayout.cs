using System;

namespace JeepneyRush.Logic
{
    /// <summary>Settings ▸ Screen. Stored in the save – append only.</summary>
    public enum OrientationMode { Portrait = 0, Landscape = 1, Auto = 2 }

    /// <summary>
    /// Portrait / landscape rules (engine-free, tested):
    /// • UI: the layouts are designed for 1080×1920 portrait. In landscape the whole UI is scaled
    ///   uniformly to fit the screen HEIGHT (nothing stretches; the extra width stays free), with a
    ///   larger scale while driving so the pedals stay thumb-sized.
    /// • Camera: a wide screen would turn the "keep 3 lanes in view" rule into a fish-eye; the
    ///   field of view is capped in landscape.
    /// • Tilt: the accelerometer reports in the phone's own axes, so "sideways" depends on how the
    ///   phone is turned.
    /// </summary>
    public static class ScreenLayout
    {
        public const int RefShort = 1080, RefLong = 1920;
        /// <summary>
        /// Landscape reference heights: the menus are drawn as if the screen were 1300 units tall
        /// (≈1.5× bigger than fitting the whole 1920-unit portrait column), the driving HUD as if
        /// 1150 tall (big thumb controls). Cards taller than the screen shrink to fit (never cut).
        /// </summary>
        public const int LandscapeMenuHeight = 1300, LandscapeHudHeight = 1150;

        public static bool IsLandscape(int w, int h) => w > h;

        /// <summary>UI reference resolution and match (0 = width, 1 = height) for the screen and state.</summary>
        public static (int refW, int refH, float match) Panel(int screenW, int screenH, bool driving) =>
            !IsLandscape(screenW, screenH) ? (RefShort, RefLong, 0f) : (RefShort, driving ? LandscapeHudHeight : LandscapeMenuHeight, 1f);

        /// <summary>Scale for a card of <paramref name="cardHeight"/> units so it fits <paramref name="available"/> (never enlarges).</summary>
        public static float FitScale(float cardHeight, float available) =>
            cardHeight <= 0 || available <= 0 ? 1f : Math.Min(1f, available / cardHeight);

        /// <summary>UI scale (screen pixels per reference unit).</summary>
        public static float UiScale(int screenW, int screenH, bool driving)
        {
            var (rw, rh, m) = Panel(screenW, screenH, driving);
            return m < 0.5f ? screenW / (float)rw : screenH / (float)rh;
        }

        /// <summary>Vertical FOV (degrees) that shows <paramref name="horizontalFov"/> across at this aspect.</summary>
        public static float VerticalFov(float horizontalFov, float aspect) =>
            (float)(2 * Math.Atan(Math.Tan(horizontalFov * 0.5 * Math.PI / 180) / Math.Max(0.3f, aspect)) * 180 / Math.PI);

        public static float HorizontalFov(float verticalFov, float aspect) =>
            (float)(2 * Math.Atan(Math.Tan(verticalFov * 0.5 * Math.PI / 180) * aspect) * 180 / Math.PI);

        /// <summary>
        /// Chase-camera vertical FOV: portrait keeps <paramref name="horizontalFov"/> across (3 lanes
        /// visible on any phone); landscape shows ~80° across at most, never a fish-eye.
        /// </summary>
        public static float ChaseFov(float horizontalFov, float aspect) =>
            aspect <= 1f ? Clamp(VerticalFov(horizontalFov, aspect), 50f, 78f)
                         : Clamp(VerticalFov(80f, aspect), 34f, 50f);

        /// <summary>Driver's-seat vertical FOV (landscape: ~100° across at most).</summary>
        public static float DriverFov(float horizontalFov, float aspect, float maxVertical) =>
            aspect <= 1f ? Clamp(VerticalFov(horizontalFov, aspect), 60f, maxVertical)
                         : Clamp(VerticalFov(100f, aspect), 42f, maxVertical);

        public enum Rotation { Portrait, PortraitUpsideDown, LandscapeLeft, LandscapeRight }

        /// <summary>
        /// Sideways gravity on the SCREEN (g, + = tilted right) from accelerometer values in the
        /// phone's own axes (x across the portrait screen, y up it).
        /// </summary>
        public static float ScreenTiltX(float ax, float ay, Rotation r) => ToScreen(ax, ay, r, false).sx;

        /// <summary>
        /// Gravity in SCREEN axes. <paramref name="compensated"/>: the readings are already rotated to
        /// the screen (the Input System does this by default) – otherwise they are in the phone's own
        /// portrait axes and get rotated here.
        /// </summary>
        public static (float sx, float sy) ToScreen(float ax, float ay, Rotation r, bool compensated)
        {
            if (compensated) return (ax, ay);
            switch (r)
            {
                case Rotation.LandscapeLeft: return (-ay, ax);   // top of the phone to the left
                case Rotation.LandscapeRight: return (ay, -ax);  // top of the phone to the right
                case Rotation.PortraitUpsideDown: return (-ax, -ay);
                default: return (ax, ay);
            }
        }

        /// <summary>
        /// Are these readings already in screen axes? Held normally (screen facing you, upright),
        /// gravity points DOWN the screen: in landscape that is along the phone's x axis if the
        /// readings are raw, along y if they were rotated to the screen.
        /// </summary>
        public static bool DetectCompensated(float ax, float ay, Rotation r)
        {
            if (r == Rotation.LandscapeLeft || r == Rotation.LandscapeRight) return Math.Abs(ay) >= Math.Abs(ax);
            if (r == Rotation.PortraitUpsideDown) return ay < 0f;
            return true; // portrait: both are the same
        }

        /// <summary>
        /// Steering-wheel roll of the phone (degrees, + = turned clockwise / right side down),
        /// independent of how far the phone is leaned back. NaN when the phone lies too flat to tell.
        /// </summary>
        public static float RollDegrees(float ax, float ay, Rotation r, bool compensated)
        {
            var (sx, sy) = ToScreen(ax, ay, r, compensated);
            if (sx * sx + sy * sy < 0.09f) return float.NaN;
            return (float)(Math.Atan2(sx, -sy) * 180.0 / Math.PI);
        }

        private static float Clamp(float v, float lo, float hi) => Math.Max(lo, Math.Min(hi, v));
    }
}
