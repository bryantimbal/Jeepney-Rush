using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace JeepneyRush
{
    /// <summary>
    /// Thin wrapper so the game works with the Input System package (Unity 6 default)
    /// and with the legacy Input Manager. Exposes every active touch (multi-touch: hold
    /// GAS with one thumb and swipe with the other), the mouse, and the keys the game uses.
    /// </summary>
    public static class InputCompat
    {
        public enum Key { Left, Right, Down, Horn, Pause, Gear, Screenshot, ScreenshotClean, Boost }
        public enum HoldKey { Gas, Brake, Left, Right }

        public struct Pointer
        {
            public int Id;
            public Vector2 Position; // pixels, origin bottom-left
        }

        /// <summary>Fills <paramref name="output"/> with every finger/mouse button currently down.</summary>
        public static void GetPointers(List<Pointer> output)
        {
            output.Clear();
#if ENABLE_INPUT_SYSTEM
            var ts = Touchscreen.current;
            if (ts != null)
                foreach (var t in ts.touches)
                    if (t.press.isPressed) output.Add(new Pointer { Id = t.touchId.ReadValue(), Position = t.position.ReadValue() });
            var m = Mouse.current;
            if (output.Count == 0 && m != null && m.leftButton.isPressed) output.Add(new Pointer { Id = -1, Position = m.position.ReadValue() });
#else
            for (int i = 0; i < Input.touchCount; i++)
            {
                var t = Input.GetTouch(i);
                if (t.phase != TouchPhase.Ended && t.phase != TouchPhase.Canceled) output.Add(new Pointer { Id = t.fingerId, Position = t.position });
            }
            if (output.Count == 0 && Input.GetMouseButton(0)) output.Add(new Pointer { Id = -1, Position = Input.mousePosition });
#endif
        }

        public static bool KeyPressed(Key k)
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return false;
            switch (k)
            {
                case Key.Left: return kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame;
                case Key.Right: return kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame;
                case Key.Down: return kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame;
                case Key.Horn: return kb.leftCtrlKey.wasPressedThisFrame || kb.rightCtrlKey.wasPressedThisFrame || kb.hKey.wasPressedThisFrame;
                case Key.Gear: return kb.rKey.wasPressedThisFrame;
                case Key.Pause: return kb.pKey.wasPressedThisFrame;
                case Key.Screenshot: return kb.f12Key.wasPressedThisFrame;
                case Key.Boost: return kb.eKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame;
                case Key.ScreenshotClean: return kb.f11Key.wasPressedThisFrame;
            }
            return false;
#else
            switch (k)
            {
                case Key.Left: return Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A);
                case Key.Right: return Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D);
                case Key.Down: return Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S);
                case Key.Horn: return Input.GetKeyDown(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.RightControl) || Input.GetKeyDown(KeyCode.H);
                case Key.Gear: return Input.GetKeyDown(KeyCode.R);
                case Key.Pause: return Input.GetKeyDown(KeyCode.P);
                case Key.Screenshot: return Input.GetKeyDown(KeyCode.F12);
                case Key.Boost: return Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Return);
                case Key.ScreenshotClean: return Input.GetKeyDown(KeyCode.F11);
            }
            return false;
#endif
        }

        /// <summary>
        /// PC controls: hold ↑ (or W / Shift) to accelerate, hold Space (or B) to brake.
        /// Ctrl (or H) honks, E (or Enter) fires a TURBO booster, ←/→ (A/D) change lanes, R shifts gear,
        /// P pauses, F12 screenshot.
        /// </summary>
        public static bool KeyHeld(HoldKey k)
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return false;
            switch (k)
            {
                case HoldKey.Gas: return kb.upArrowKey.isPressed || kb.wKey.isPressed || kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
                case HoldKey.Brake: return kb.spaceKey.isPressed || kb.bKey.isPressed;
                case HoldKey.Left: return kb.leftArrowKey.isPressed || kb.aKey.isPressed;
                default: return kb.rightArrowKey.isPressed || kb.dKey.isPressed;
            }
#else
            switch (k)
            {
                case HoldKey.Gas: return Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                case HoldKey.Brake: return Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.B);
                case HoldKey.Left: return Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A);
                default: return Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D);
            }
#endif
        }

        /// <summary>True if the device has an accelerometer (phones; not PCs).</summary>
        public static bool HasTilt
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return Accelerometer.current != null;
#else
                return SystemInfo.supportsAccelerometer;
#endif
            }
        }

        /// <summary>Sideways gravity on the screen in g (+ = tilted to the right, in portrait or landscape). 0 without a sensor.</summary>
        public static float TiltX()
        {
#if ENABLE_INPUT_SYSTEM
            var acc = Accelerometer.current;
            if (acc == null) return 0f;
            if (!acc.enabled) InputSystem.EnableDevice(acc);
            var a = acc.acceleration.ReadValue();
#else
            if (!SystemInfo.supportsAccelerometer) return 0f;
            var a = Input.acceleration;
#endif
            // the sensor reports in the phone's own axes: turn it into "sideways on the screen"
            return Logic.ScreenLayout.ScreenTiltX(a.x, a.y, ScreenOrientationControl.CurrentRotation);
        }

        /// <summary>Android back maps to Escape in both input backends.</summary>
        public static bool BackPressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Escape);
#endif
        }
    }
}
