using System;
using System.Collections.Generic;
using UnityEngine;

namespace JeepneyRush
{
    /// <summary>
    /// Multi-touch swipes (fire as soon as the finger travels far enough; exactly one action
    /// per touch – lift and swipe again for a second lane) plus the GAS / BRAKE hold buttons and mirrored keyboard
    /// controls. Touches that start on a HUD button never count as swipes.
    /// </summary>
    public sealed class PlayerInputHandler : MonoBehaviour
    {
        public enum Action { Left, Right, Down, Horn, Pause, Gear }
        public event Action<Action> OnAction;

        public bool InputEnabled { get; set; }
        /// <summary>Settings ▸ Swipe sensitivity (0.5 .. 1.5).</summary>
        public float Sensitivity = 1f;

        /// <summary>Set by the HUD hold buttons.</summary>
        public bool GasButton { get; set; }
        public bool BrakeButton { get; set; }
        public bool GasHeld => InputEnabled && (GasButton || InputCompat.KeyHeld(InputCompat.HoldKey.Gas));
        public bool BrakeHeld => InputEnabled && (BrakeButton || InputCompat.KeyHeld(InputCompat.HoldKey.Brake));

        private struct Track { public Vector2 Start; public bool Blocked; public bool Seen; public bool Fired; }
        private readonly Dictionary<int, Track> _tracks = new Dictionary<int, Track>();
        private readonly List<InputCompat.Pointer> _pointers = new List<InputCompat.Pointer>();
        private readonly List<int> _stale = new List<int>();

        private float Threshold => Mathf.Max(18f, Mathf.Min(Screen.width, Screen.height) * 0.055f / Mathf.Clamp(Sensitivity, 0.5f, 1.5f));

        private void Update()
        {
            if (!InputEnabled)
            {
                _tracks.Clear();
                GasButton = BrakeButton = false;
                return;
            }
            if (InputCompat.KeyPressed(InputCompat.Key.Left)) Fire(Action.Left);
            if (InputCompat.KeyPressed(InputCompat.Key.Right)) Fire(Action.Right);
            if (InputCompat.KeyPressed(InputCompat.Key.Up) || InputCompat.KeyPressed(InputCompat.Key.Jump)) Fire(Action.Horn); // jeepneys don't jump: honk instead
            if (InputCompat.KeyPressed(InputCompat.Key.Down)) Fire(Action.Down);
            if (InputCompat.KeyPressed(InputCompat.Key.Horn)) Fire(Action.Horn);
            if (InputCompat.KeyPressed(InputCompat.Key.Pause)) Fire(Action.Pause);
            if (InputCompat.KeyPressed(InputCompat.Key.Gear)) Fire(Action.Gear);

            InputCompat.GetPointers(_pointers);
            var keys = new List<int>(_tracks.Keys);
            foreach (var k in keys) { var t = _tracks[k]; t.Seen = false; _tracks[k] = t; }
            foreach (var p in _pointers)
            {
                if (!_tracks.TryGetValue(p.Id, out var t))
                {
                    // touches that begin on HUD buttons (gas, brake, pause, horn) are not swipes
                    _tracks[p.Id] = new Track { Start = p.Position, Blocked = Services.UI != null && Services.UI.IsOverInteractive(p.Position), Seen = true };
                    continue;
                }
                t.Seen = true;
                // exactly ONE action per finger: a long or fast swipe must not count twice
                // (that used to queue a second lane change straight into the next car)
                if (!t.Blocked && !t.Fired)
                {
                    var d = p.Position - t.Start;
                    if (Mathf.Max(Mathf.Abs(d.x), Mathf.Abs(d.y)) >= Threshold)
                    {
                        if (Mathf.Abs(d.x) > Mathf.Abs(d.y)) Fire(d.x < 0 ? Action.Left : Action.Right);
                        else Fire(d.y > 0 ? Action.Horn : Action.Down); // screen Y grows upward; swipe up = honk
                        t.Fired = true; // lift the finger to swipe again
                    }
                }
                _tracks[p.Id] = t;
            }
            _stale.Clear();
            foreach (var kv in _tracks) if (!kv.Value.Seen) _stale.Add(kv.Key);
            foreach (var k in _stale) _tracks.Remove(k);
        }

        private void Fire(Action a) => OnAction?.Invoke(a);
    }
}
