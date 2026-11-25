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
        /// <summary>Reference height while driving in landscape (bigger HUD than the menus' 1920).</summary>
        public const int LandscapeHudHeight = 1400;

        public static bool IsLandscape(int w, int h) => w > h;

        /// <summary>UI reference resolution and match (0 = width, 1 = height) for the screen and state.</summary>
        public static (int refW, int refH, float match) Panel(int screenW, int screenH, bool driving) =>
            !IsLandscape(screenW, screenH) ? (RefShort, RefLong, 0f) : (RefShort, driving ? LandscapeHudHeight : RefLong, 1f);

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
        /// Sideways gravity on the SCREEN (g, + = tilted right) from the accelerometer, which reports
        /// in the phone's own axes (x across the portrait screen, y up it).
        /// </summary>
        public static float ScreenTiltX(float ax, float ay, Rotation r)
        {
            switch (r)
            {
                case Rotation.LandscapeLeft: return -ay;   // phone turned counter-clockwise (top to the left)
                case Rotation.LandscapeRight: return ay;   // turned clockwise (top to the right)
                case Rotation.PortraitUpsideDown: return -ax;
                default: return ax;
            }
        }

        private static float Clamp(float v, float lo, float hi) => Math.Max(lo, Math.Min(hi, v));
    }
}
