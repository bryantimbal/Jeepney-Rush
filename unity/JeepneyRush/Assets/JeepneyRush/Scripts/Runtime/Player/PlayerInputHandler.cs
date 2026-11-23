using System;
using System.Collections.Generic;
using JeepneyRush.Logic;
using UnityEngine;

namespace JeepneyRush
{
    /// <summary>
    /// Analog steering plus the GAS / BRAKE hold buttons and keyboard controls.
    /// Steering (Settings ▸ Steering):
    ///  • TAP  – hold a finger on the left or right half of the screen; the jeepney turns
    ///           smoothly for as long as you hold (touches that start on a HUD button never steer)
    ///  • TILT – tilt the phone like a steering wheel (calibrated to how you hold it at the start)
    /// On PC: hold ← / → (or A / D).
    /// </summary>
    public sealed class PlayerInputHandler : MonoBehaviour
    {
        public enum Action { Left, Right, Down, Horn, Pause, Gear, Boost }
        public event Action<Action> OnAction;

        public bool InputEnabled { get; set; }
        /// <summary>Settings ▸ Steering sensitivity (0.5 .. 1.5).</summary>
        public float Sensitivity = 1f;
        public SteerMode Mode = SteerMode.Tap;

        /// <summary>Steering input this frame: -1 full left .. +1 full right.</summary>
        public float Steer { get; private set; }

        /// <summary>Set by the HUD hold buttons.</summary>
        public bool GasButton { get; set; }
        public bool BrakeButton { get; set; }
        public bool GasHeld => InputEnabled && (GasButton || InputCompat.KeyHeld(InputCompat.HoldKey.Gas));
        public bool BrakeHeld => InputEnabled && (BrakeButton || InputCompat.KeyHeld(InputCompat.HoldKey.Brake));

        private struct Track { public bool Blocked; public bool Left; public bool Seen; }
        private readonly Dictionary<int, Track> _tracks = new Dictionary<int, Track>();
        private readonly List<InputCompat.Pointer> _pointers = new List<InputCompat.Pointer>();
        private readonly List<int> _stale = new List<int>();
        private readonly List<int> _keys = new List<int>();
        private readonly TiltSteer _tilt = new TiltSteer();

        /// <summary>Run start: the way the phone is held now counts as "straight ahead".</summary>
        public void Recalibrate() { if (InputCompat.HasTilt) _tilt.Calibrate(InputCompat.TiltX()); }

        private void Update()
        {
            if (!InputEnabled)
            {
                _tracks.Clear();
                GasButton = BrakeButton = false;
                Steer = 0;
                return;
            }
            if (InputCompat.KeyPressed(InputCompat.Key.Down)) Fire(Action.Down);
            if (InputCompat.KeyPressed(InputCompat.Key.Horn)) Fire(Action.Horn);
            if (InputCompat.KeyPressed(InputCompat.Key.Pause)) Fire(Action.Pause);
            if (InputCompat.KeyPressed(InputCompat.Key.Gear)) Fire(Action.Gear);
            if (InputCompat.KeyPressed(InputCompat.Key.Boost)) Fire(Action.Boost);

            // ---- touches: which side of the screen is held (only fingers that didn't start on a button)
            InputCompat.GetPointers(_pointers);
            _keys.Clear(); _keys.AddRange(_tracks.Keys);
            foreach (var k in _keys) { var t = _tracks[k]; t.Seen = false; _tracks[k] = t; }
            bool left = false, right = false;
            foreach (var p in _pointers)
            {
                if (!_tracks.TryGetValue(p.Id, out var t))
                    t = new Track { Blocked = Services.UI != null && Services.UI.IsOverInteractive(p.Position) };
                t.Seen = true;
                t.Left = p.Position.x < Screen.width * 0.5f; // the finger may slide across the middle: follow it
                _tracks[p.Id] = t;
                if (t.Blocked) continue;
                if (t.Left) left = true; else right = true;
            }
            _stale.Clear();
            foreach (var kv in _tracks) if (!kv.Value.Seen) _stale.Add(kv.Key);
            foreach (var k in _stale) _tracks.Remove(k);

            float dt = Time.unscaledDeltaTime;
            float steer = 0f;
            if (Mode == SteerMode.Tilt && InputCompat.HasTilt) steer = _tilt.Input(InputCompat.TiltX(), Sensitivity, dt);
            else steer = TapSteer.Input(left, right);
            // keyboard always works (PC, and phones with a keyboard)
            bool kl = InputCompat.KeyHeld(InputCompat.HoldKey.Left), kr = InputCompat.KeyHeld(InputCompat.HoldKey.Right);
            if (kl != kr) steer = kl ? -1f : 1f;
            Steer = steer;
        }

        private void Fire(Action a) => OnAction?.Invoke(a);
    }
}
