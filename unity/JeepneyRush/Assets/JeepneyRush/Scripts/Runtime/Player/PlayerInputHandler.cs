using System;
using UnityEngine;

namespace JeepneyRush
{
    /// <summary>
    /// Swipes (fire as soon as the finger travels far enough – no waiting for release,
    /// re-armed so one touch can chain swipes) plus mirrored keyboard controls.
    /// </summary>
    public sealed class PlayerInputHandler : MonoBehaviour
    {
        public enum Action { Left, Right, Jump, Down, Horn, Pause }
        public event Action<Action> OnAction;

        public bool InputEnabled { get; set; }
        /// <summary>Settings ▸ Swipe sensitivity (0.5 .. 1.5).</summary>
        public float Sensitivity = 1f;

        private bool _down;
        private Vector2 _start;
        private bool _blockedByUI;

        private float Threshold => Mathf.Max(18f, Mathf.Min(Screen.width, Screen.height) * 0.055f / Mathf.Clamp(Sensitivity, 0.5f, 1.5f));

        private void Update()
        {
            if (!InputEnabled) { _down = false; return; }
            if (InputCompat.KeyPressed(InputCompat.Key.Left)) Fire(Action.Left);
            if (InputCompat.KeyPressed(InputCompat.Key.Right)) Fire(Action.Right);
            if (InputCompat.KeyPressed(InputCompat.Key.Up) || InputCompat.KeyPressed(InputCompat.Key.Jump)) Fire(Action.Jump);
            if (InputCompat.KeyPressed(InputCompat.Key.Down)) Fire(Action.Down);
            if (InputCompat.KeyPressed(InputCompat.Key.Horn)) Fire(Action.Horn);
            if (InputCompat.KeyPressed(InputCompat.Key.Pause)) Fire(Action.Pause);

            if (InputCompat.PointerDown(out var pos))
            {
                if (!_down)
                {
                    _down = true;
                    _start = pos;
                    // touches that begin on HUD buttons (pause, horn) are not swipes
                    _blockedByUI = Services.UI != null && Services.UI.IsOverInteractive(pos);
                    return;
                }
                if (_blockedByUI) return;
                var d = pos - _start;
                if (Mathf.Max(Mathf.Abs(d.x), Mathf.Abs(d.y)) < Threshold) return;
                if (Mathf.Abs(d.x) > Mathf.Abs(d.y)) Fire(d.x < 0 ? Action.Left : Action.Right);
                else Fire(d.y > 0 ? Action.Jump : Action.Down); // screen Y grows upward
                _start = pos; // re-arm
            }
            else _down = false;
        }

        private void Fire(Action a) => OnAction?.Invoke(a);
    }
}
