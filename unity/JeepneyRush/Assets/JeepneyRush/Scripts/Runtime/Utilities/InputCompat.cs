using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace JeepneyRush
{
    /// <summary>
    /// Thin wrapper so the game works with the Input System package (Unity 6 default)
    /// and with the legacy Input Manager. Exposes the primary pointer (touch or mouse)
    /// and the keys the game uses.
    /// </summary>
    public static class InputCompat
    {
        public enum Key { Left, Right, Up, Down, Jump, Horn, Pause }

        public static bool PointerDown(out Vector2 pos)
        {
#if ENABLE_INPUT_SYSTEM
            var ts = Touchscreen.current;
            if (ts != null && ts.primaryTouch.press.isPressed) { pos = ts.primaryTouch.position.ReadValue(); return true; }
            var m = Mouse.current;
            if (m != null && m.leftButton.isPressed) { pos = m.position.ReadValue(); return true; }
            pos = default;
            return false;
#else
            if (Input.touchCount > 0) { pos = Input.GetTouch(0).position; return true; }
            if (Input.GetMouseButton(0)) { pos = Input.mousePosition; return true; }
            pos = default;
            return false;
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
                case Key.Up: return kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame;
                case Key.Jump: return kb.spaceKey.wasPressedThisFrame;
                case Key.Down: return kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame;
                case Key.Horn: return kb.hKey.wasPressedThisFrame;
                case Key.Pause: return kb.pKey.wasPressedThisFrame;
            }
            return false;
#else
            switch (k)
            {
                case Key.Left: return Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A);
                case Key.Right: return Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D);
                case Key.Up: return Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W);
                case Key.Jump: return Input.GetKeyDown(KeyCode.Space);
                case Key.Down: return Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S);
                case Key.Horn: return Input.GetKeyDown(KeyCode.H);
                case Key.Pause: return Input.GetKeyDown(KeyCode.P);
            }
            return false;
#endif
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
