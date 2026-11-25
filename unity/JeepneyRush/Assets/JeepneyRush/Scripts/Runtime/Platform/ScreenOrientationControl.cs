using JeepneyRush.Logic;
using UnityEngine;

namespace JeepneyRush
{
    /// <summary>Settings ▸ Screen: portrait (default), landscape (either way round) or auto-rotate.</summary>
    public static class ScreenOrientationControl
    {
        public static void Apply(OrientationMode mode)
        {
            switch (mode)
            {
                case OrientationMode.Landscape:
                    Screen.autorotateToPortrait = false;
                    Screen.autorotateToPortraitUpsideDown = false;
                    Screen.autorotateToLandscapeLeft = true;
                    Screen.autorotateToLandscapeRight = true;
                    // start in landscape, then follow the phone between its two landscape sides
                    if (Screen.orientation == ScreenOrientation.Portrait || Screen.orientation == ScreenOrientation.PortraitUpsideDown)
                        Screen.orientation = ScreenOrientation.LandscapeLeft;
                    Screen.orientation = ScreenOrientation.AutoRotation;
                    break;
                case OrientationMode.Auto:
                    Screen.autorotateToPortrait = true;
                    Screen.autorotateToPortraitUpsideDown = false;
                    Screen.autorotateToLandscapeLeft = true;
                    Screen.autorotateToLandscapeRight = true;
                    Screen.orientation = ScreenOrientation.AutoRotation;
                    break;
                default:
                    Screen.autorotateToPortrait = true;
                    Screen.autorotateToPortraitUpsideDown = false;
                    Screen.autorotateToLandscapeLeft = false;
                    Screen.autorotateToLandscapeRight = false;
                    Screen.orientation = ScreenOrientation.Portrait;
                    break;
            }
        }

        public static bool IsLandscape => Screen.width > Screen.height;

        /// <summary>How the screen is turned right now (for the tilt axes).</summary>
        public static ScreenLayout.Rotation CurrentRotation
        {
            get
            {
                switch (Screen.orientation)
                {
                    case ScreenOrientation.LandscapeLeft: return ScreenLayout.Rotation.LandscapeLeft;
                    case ScreenOrientation.LandscapeRight: return ScreenLayout.Rotation.LandscapeRight;
                    case ScreenOrientation.PortraitUpsideDown: return ScreenLayout.Rotation.PortraitUpsideDown;
                    case ScreenOrientation.Portrait: return ScreenLayout.Rotation.Portrait;
                    default:
                        // auto-rotation reported as such: use the physical device orientation
                        var d = Input.deviceOrientation;
                        if (d == DeviceOrientation.LandscapeLeft) return ScreenLayout.Rotation.LandscapeLeft;
                        if (d == DeviceOrientation.LandscapeRight) return ScreenLayout.Rotation.LandscapeRight;
                        return IsLandscape ? ScreenLayout.Rotation.LandscapeLeft : ScreenLayout.Rotation.Portrait;
                }
            }
        }
    }
}
