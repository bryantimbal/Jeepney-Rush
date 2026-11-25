using System;
using System.Collections.Generic;
using JeepneyRush.Logic;
using UnityEngine;

namespace JeepneyRush
{
    /// <summary>
    /// Analog steering plus the GAS / BRAKE hold buttons and keyboard controls.
    /// Steering (Settings ▸ Steering):
    ///  • BUTTONS – hold the LEFT / RIGHT buttons (left thumb); the jeepney turns smoothly for as
    ///              long as you hold, GAS / BRAKE are under the right thumb
    ///  • TILT    – tilt the phone like a steering wheel (calibrated to how you hold it at the start)
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
        /// <summary>Set by the HUD LEFT / RIGHT steering buttons (Buttons mode).</summary>
        public bool SteerLeftButton { get; set; }
        public bool SteerRightButton { get; set; }
        public bool BrakeButton { get; set; }
        public bool GasHeld => InputEnabled && (GasButton || InputCompat.KeyHeld(InputCompat.HoldKey.Gas));
        public bool BrakeHeld => InputEnabled && (BrakeButton || InputCompat.KeyHeld(InputCompat.HoldKey.Brake));

        /// <summary>Set every frame by the on-screen steering wheel (Wheel mode), -1 .. 1.</summary>
        public float WheelInput { get; set; }

        private readonly TiltSteer _tilt = new TiltSteer();

        /// <summary>Tilt settings from the save: calibrated neutral position, dead zone, smoothing.</summary>
        public void ApplyTiltSettings(SaveData d)
        {
            _tilt.DeadZone = d.tiltDeadZone;
            _tilt.Smoothing = d.tiltSmoothing;
            if (d.tiltCalibrated) _tilt.Calibrate(d.tiltCentre);
            else Recalibrate(); // never calibrated: the way the phone is held at the start is "straight"
        }

        /// <summary>The way the phone is held now counts as "straight ahead". Returns the neutral value (g).</summary>
        public float Recalibrate()
        {
            if (!InputCompat.HasTilt) return 0f;
            float g = InputCompat.TiltX();
            _tilt.Calibrate(g);
            return g;
        }

        private void Update()
        {
            if (!InputEnabled)
            {
                GasButton = BrakeButton = SteerLeftButton = SteerRightButton = false;
                WheelInput = 0;
                Steer = 0;
                return;
            }
            if (InputCompat.KeyPressed(InputCompat.Key.Down)) Fire(Action.Down);
            if (InputCompat.KeyPressed(InputCompat.Key.Horn)) Fire(Action.Horn);
            if (InputCompat.KeyPressed(InputCompat.Key.Pause)) Fire(Action.Pause);
            if (InputCompat.KeyPressed(InputCompat.Key.Gear)) Fire(Action.Gear);
            if (InputCompat.KeyPressed(InputCompat.Key.Boost)) Fire(Action.Boost);

            float dt = Time.unscaledDeltaTime;
            float steer = 0f;
            if (Mode == SteerMode.Tilt && InputCompat.HasTilt) steer = _tilt.Input(InputCompat.TiltX(), Sensitivity, dt);
            else if (Mode == SteerMode.Wheel) steer = WheelInput;
            else steer = TapSteer.Input(SteerLeftButton, SteerRightButton);
            // keyboard always works (PC, and phones with a keyboard)
            bool kl = InputCompat.KeyHeld(InputCompat.HoldKey.Left), kr = InputCompat.KeyHeld(InputCompat.HoldKey.Right);
            if (kl != kr) steer = kl ? -1f : 1f;
            Steer = steer;
        }

        private void Fire(Action a) => OnAction?.Invoke(a);
    }
}
