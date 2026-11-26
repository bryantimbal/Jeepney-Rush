using System.Collections.Generic;
using JeepneyRush.Logic;
using UnityEngine;
using UnityEngine.UIElements;

namespace JeepneyRush
{
    /// <summary>
    /// Driving controls: the three steering layouts (buttons, tilt, on-screen wheel), the virtual
    /// steering wheel itself, the contextual gear selector, and Settings ▸ Controls.
    /// Every hold control keeps its own pointer, so any combination works at once (LEFT + GAS,
    /// wheel + GAS + TURBO, ...), and all of them are released when the game pauses.
    /// </summary>
    public partial class UIManager
    {
        private VisualElement _ctlBottom, _ctlLeft, _ctlRight, _ctlTopRow, _hornCentre, _speedBox;
        private VisualElement _brakeBtn, _gasBtn, _hornBtn, _steerLeftBtn, _steerRightBtn, _steerRow;
        private VisualElement _wheelEl, _wheelImg, _controls;
        private readonly VirtualWheel _vwheel = new VirtualWheel();
        private readonly List<System.Action> _holdReleases = new List<System.Action>();
        private float _ctlOpacity = 1f;
        private bool _driverView;
        public SteerMode ControlLayout { get; private set; } = (SteerMode)(-1);

        /// <summary>Lets go of every held control (pause, game over): nothing can stay stuck down.</summary>
        public void ReleaseAllHolds()
        {
            foreach (var r in _holdReleases) r();
            _vwheel.Release();
        }

        private static void Shape(VisualElement b, float w, float h, float radius, float lip)
        {
            b.style.width = w;
            b.style.height = h;
            // icon and label scale with the button so short buttons (TURBO, BUSINA) never overflow
            float s = Mathf.Min(w, h);
            var icon = b.Q<Image>();
            if (icon != null) icon.style.width = icon.style.height = Mathf.Clamp(s * 0.42f, 44f, 96f);
            var label = b.Q<Label>();
            if (label != null && label.name != "count") label.style.fontSize = Mathf.Clamp(s * 0.19f, 22f, 40f);
            UiKit.Border(b.style, 7, UiKit.Ink, radius);
            b.style.borderBottomWidth = lip;
            b.style.marginTop = b.style.marginBottom = b.style.marginLeft = b.style.marginRight = 0;
        }

        // ------------------------------------------------------------------ virtual steering wheel
        private VisualElement BuildWheel()
        {
            var w = new VisualElement { name = "steeringWheel" };
            w.pickingMode = PickingMode.Position;
            w.AddToClassList(InteractiveClass);
            w.style.alignItems = Align.Center;
            w.style.justifyContent = Justify.Center;
            _wheelImg = UiKit.Icon("ui_wheel", 360);
            _wheelImg.pickingMode = PickingMode.Ignore;
            w.Add(_wheelImg);
            int pointer = -1;
            Vector2 Rel(Vector3 local) => new Vector2(local.x - w.resolvedStyle.width * 0.5f, local.y - w.resolvedStyle.height * 0.5f);
            float Radius() => Mathf.Max(40f, w.resolvedStyle.width * 0.5f);
            w.RegisterCallback<PointerDownEvent>(e =>
            {
                if (pointer >= 0) return;
                pointer = e.pointerId;
                w.CapturePointer(pointer);
                var p = Rel(e.localPosition);
                _vwheel.Press(p.x, p.y, Radius());
                e.StopPropagation();
            });
            w.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (e.pointerId != pointer) return;
                var p = Rel(e.localPosition);
                _vwheel.Drag(p.x, p.y, Radius());
            });
            void Up() { pointer = -1; _vwheel.Release(); }
            w.RegisterCallback<PointerUpEvent>(e => { if (e.pointerId == pointer) { w.ReleasePointer(pointer); Up(); } });
            w.RegisterCallback<PointerCancelEvent>(e => { if (e.pointerId == pointer) Up(); });
            w.RegisterCallback<PointerCaptureOutEvent>(_ => { if (pointer >= 0) Up(); });
            w.RegisterCallback<DetachFromPanelEvent>(_ => { if (pointer >= 0) Up(); });
            _holdReleases.Add(() => { if (pointer >= 0) { if (w.HasPointerCapture(pointer)) w.ReleasePointer(pointer); Up(); } });
            // spring back + feed the steering input every frame; the drawn wheel shows the real input
            w.schedule.Execute(() =>
            {
                _vwheel.Tick(Mathf.Min(0.05f, Time.unscaledDeltaTime));
                _wheelImg.style.rotate = new Rotate(_vwheel.Angle);
                var s = Services.Game?.Session;
                if (s != null && s.Input != null) s.Input.WheelInput = ControlLayout == SteerMode.Wheel ? _vwheel.Input : 0f;
            }).Every(16);
            return w;
        }

        private void ApplyWheelSettings(SaveData d)
        {
            if (_wheelEl == null) return;
            float size = d.wheelSize == 0 ? 300f : d.wheelSize == 2 ? 420f : 360f;
            _wheelEl.style.width = _wheelEl.style.height = size;
            _wheelImg.style.width = _wheelImg.style.height = size;
            _wheelEl.style.opacity = d.wheelOpacity;
            _vwheel.MaxAngle = d.wheelMaxAngle;
            _vwheel.Sensitivity = d.swipeSensitivity;
            _vwheel.ReturnSpeed = 420f * d.steerReturn;
        }

        // ------------------------------------------------------------------ layouts
        /// <summary>
        /// Thumb controls for the steering mode (mirrored for left-handed players):
        /// BUTTONS: TURBO above ◀ LEFT and ▶ RIGHT side by side (left thumb) · BUSINA above GAS above BRAKE (right thumb).
        /// WHEEL: TURBO above the steering wheel · BUSINA, GAS, BRAKE on the right.
        /// TILT: TURBO above BRAKE (left) · BUSINA above GAS (right).
        /// The same layout in portrait and landscape; LEFT is always on the left (also for left-handed players).
        /// GEAR D/R only shows while stopped or reversing.
        /// </summary>
        public void ApplyControlLayout(SteerMode mode)
        {
            if (_ctlLeft == null) return;
            var d = Services.Save != null ? Services.Save.Data : new SaveData();
            ControlLayout = mode;
            float k = d.buttonScale;
            bool lefty = d.leftHanded;
            _ctlOpacity = d.buttonOpacity;
            _ctlLeft.Clear(); _ctlRight.Clear(); _ctlTopRow.Clear(); _hornCentre.Clear();
            _ctlBottom.style.flexDirection = lefty ? FlexDirection.RowReverse : FlexDirection.Row;
            _ctlLeft.style.alignItems = lefty ? Align.FlexEnd : Align.FlexStart;
            _ctlRight.style.alignItems = lefty ? Align.FlexStart : Align.FlexEnd;
            _ctlTopRow.style.flexDirection = lefty ? FlexDirection.RowReverse : FlexDirection.Row;
            float w = 300 * k, h = 180 * k, small = 140 * k, gap = 16 * k;
            if (mode == SteerMode.Tilt)
            {
                Shape(_boostBtn, 170 * k, 170 * k, 50 * k, 16);
                _boostBtn.style.marginBottom = 12;
                Shape(_brakeBtn, 245 * k, 245 * k, 245 * k * 0.24f, 20);
                _ctlLeft.Add(_boostBtn);
                _ctlLeft.Add(_brakeBtn);
                // right thumb: (GEAR) BUSINA right above GAS
                Shape(_hornBtn, 170 * k, 170 * k, 85 * k, 20);
                _hornBtn.style.marginBottom = 12;
                Shape(_gasBtn, 245 * k, 245 * k, 245 * k * 0.24f, 20);
                _gearBtn.style.marginBottom = 12;
                _gearBtn.style.marginLeft = _gearBtn.style.marginRight = 0;
                _ctlRight.Add(_gearBtn);
                _ctlRight.Add(_hornBtn);
                _ctlRight.Add(_gasBtn);
            }
            else
            {
                // left thumb: TURBO above the steering control
                Shape(_boostBtn, small, small, 40 * k, 14);
                _boostBtn.style.marginBottom = gap;
                _ctlLeft.Add(_boostBtn);
                if (mode == SteerMode.Wheel)
                {
                    ApplyWheelSettings(d);
                    _ctlLeft.Add(_wheelEl);
                }
                else
                {
                    // ◀ LEFT and ▶ RIGHT side by side, at the same height (LEFT always on the left)
                    _steerRow ??= UiKit.Box("steerRow");
                    _steerRow.Clear();
                    _steerRow.style.flexDirection = FlexDirection.Row;
                    _steerRow.style.alignItems = Align.FlexEnd;
                    float sw = 205 * k, sh = 215 * k;
                    foreach (var b in new[] { _steerLeftBtn, _steerRightBtn }) { Shape(b, sw, sh, 40 * k, 16); b.style.marginTop = 0; }
                    _steerLeftBtn.style.marginRight = gap;
                    _steerRightBtn.style.marginLeft = 0;
                    _steerRow.Add(_steerLeftBtn);
                    _steerRow.Add(_steerRightBtn);
                    _ctlLeft.Add(_steerRow);
                }
                // right thumb: (GEAR) BUSINA above GAS above BRAKE
                Shape(_hornBtn, small, small, small / 2, 14);
                _hornBtn.style.marginBottom = 0;
                _gearBtn.style.marginBottom = 0;
                _gearBtn.style.marginRight = lefty ? 0 : 12;
                _gearBtn.style.marginLeft = lefty ? 12 : 0;
                _ctlTopRow.style.marginBottom = gap;
                _ctlTopRow.style.alignItems = Align.Center;
                _ctlTopRow.Add(_gearBtn);
                _ctlTopRow.Add(_hornBtn);
                _ctlRight.Add(_ctlTopRow);
                Shape(_gasBtn, w, h, 44 * k, 16);
                Shape(_brakeBtn, w, h, 44 * k, 16);
                _brakeBtn.style.marginTop = gap;
                _ctlRight.Add(_gasBtn);
                _ctlRight.Add(_brakeBtn);
            }
            ApplyControlOpacity();
        }

        private void ApplyControlOpacity()
        {
            float o = _ctlOpacity * (_driverView ? 0.72f : 1f); // keep the road visible from the driver's seat
            foreach (var b in _pedalButtons) if (b != null) b.style.opacity = o;
        }

        /// <summary>The gear selector only matters when stopped (to reverse) or while reversing.</summary>
        private void UpdateGearVisibility(int kmh, bool reverse)
        {
            if (_gearBtn == null) return;
            bool show = reverse || kmh <= 2;
            if ((_gearBtn.resolvedStyle.display == DisplayStyle.Flex) != show) Show(_gearBtn, show);
        }

        // ------------------------------------------------------------------ Settings ▸ Controls
        private Label _calibLabel;

        private void BuildControls()
        {
            _controls = ModalShell("controls", "CONTROLS", out var card);
            var sv = UiKit.Scroll(1260);
            card.Add(sv);
            var c = sv.contentContainer;
            var d = (System.Func<SaveData>)(() => Services.Save.Data);
            void Apply()
            {
                Services.Save.Save();
                var s = Services.Game?.Session;
                if (s != null) s.ApplyControlSettings();
                else ApplyControlLayout(GameplaySession.EffectiveSteerMode(d()));
            }
            c.Add(Heading("STEERING"));
            // the mode selector has its own colours (blue / green) so it stands apart from the settings below it
            c.Add(Segmented("Steering mode", new[] { "Buttons", "Tilt", "Wheel" }, () => d().steerMode, i =>
            {
                d().steerMode = i;
                if (i == 1 && !InputCompat.HasTilt) Float("NO TILT SENSOR · USING BUTTONS", FloatKind.Info);
                Apply();
            }, UiKit.Hex("#22D065"), UiKit.Hex("#DCE6F5"), UiKit.Ink, UiKit.Hex("#2F4A7A"), 112));
            c.Add(Segmented("Steering sensitivity", new[] { "Low", "Medium", "High" },
                () => d().swipeSensitivity < 0.85f ? 0 : d().swipeSensitivity > 1.15f ? 2 : 1,
                i => { d().swipeSensitivity = i == 0 ? 0.7f : i == 1 ? 1f : 1.3f; Apply(); }));
            c.Add(Segmented("Max steering", new[] { "80%", "90%", "100%" },
                () => d().maxSteer < 0.85f ? 0 : d().maxSteer < 0.95f ? 1 : 2,
                i => { d().maxSteer = i == 0 ? 0.8f : i == 1 ? 0.9f : 1f; Apply(); }));
            c.Add(Segmented("Return to centre", new[] { "Slow", "Normal", "Fast" },
                () => d().steerReturn < 0.8f ? 0 : d().steerReturn > 1.2f ? 2 : 1,
                i => { d().steerReturn = i == 0 ? 0.6f : i == 1 ? 1f : 1.6f; Apply(); }));
            c.Add(Toggle("Lane assist", () => d().laneAssist, v => { d().laneAssist = v; Apply(); }));

            c.Add(Heading("TILT"));
            c.Add(Note("Hold the phone the way you like to drive, then calibrate.", 28));
            c.Add(SmallButton("CALIBRATE TILT", () =>
            {
                if (InputCompat.HasTilt) { d().tiltAxesKnown = false; PlayerInputHandler.LearnTiltAxes(d()); } // calibrating re-checks the sensor axes
                float roll = InputCompat.HasTilt ? (Services.Game?.Session?.Input != null ? Services.Game.Session.Input.Recalibrate() : InputCompat.TiltRoll()) : 0f;
                if (float.IsNaN(roll)) roll = 0f;
                if (ScreenOrientationControl.IsLandscape) { d().tiltRollLand = roll; d().tiltRollLandCal = InputCompat.HasTilt; }
                else { d().tiltRoll = roll; d().tiltRollCal = InputCompat.HasTilt; }
                Apply();
                RefreshCalibLabel();
                Toast(InputCompat.HasTilt ? "NA-CALIBRATE NA! ✓ Ito na ang 'diretso'." : "No tilt sensor on this device.");
            }, UiKit.Yellow, UiKit.Ink));
            _calibLabel = Note("", 28);
            c.Add(_calibLabel);
            c.Add(Segmented("Tilt dead zone", new[] { "Small", "Medium", "Large" },
                () => d().tiltDeadZone < 0.028f ? 0 : d().tiltDeadZone > 0.045f ? 2 : 1,
                i => { d().tiltDeadZone = i == 0 ? 0.02f : i == 1 ? 0.035f : 0.06f; Apply(); }));
            c.Add(Segmented("Tilt smoothing", new[] { "Low", "Medium", "High" },
                () => d().tiltSmoothing > 18f ? 0 : d().tiltSmoothing < 10f ? 2 : 1,
                i => { d().tiltSmoothing = i == 0 ? 24f : i == 1 ? 14f : 7f; Apply(); }));

            c.Add(Heading("STEERING WHEEL"));
            c.Add(Segmented("Wheel size", new[] { "Small", "Medium", "Large" }, () => d().wheelSize, i => { d().wheelSize = i; Apply(); }));
            c.Add(Segmented("Wheel opacity", new[] { "60%", "80%", "100%" },
                () => d().wheelOpacity < 0.7f ? 0 : d().wheelOpacity < 0.95f ? 1 : 2,
                i => { d().wheelOpacity = i == 0 ? 0.6f : i == 1 ? 0.8f : 1f; Apply(); }));
            c.Add(Segmented("Wheel rotation", new[] { "90°", "135°", "180°" },
                () => d().wheelMaxAngle < 110f ? 0 : d().wheelMaxAngle > 160f ? 2 : 1,
                i => { d().wheelMaxAngle = i == 0 ? 90f : i == 1 ? 135f : 180f; Apply(); }));

            c.Add(Heading("BUTTONS"));
            c.Add(Segmented("Button size", new[] { "Small", "Normal", "Large" },
                () => d().buttonScale < 0.95f ? 0 : d().buttonScale > 1.05f ? 2 : 1,
                i => { d().buttonScale = i == 0 ? 0.85f : i == 1 ? 1f : 1.15f; Apply(); }));
            c.Add(Segmented("Button opacity", new[] { "60%", "80%", "100%" },
                () => d().buttonOpacity < 0.7f ? 0 : d().buttonOpacity < 0.95f ? 1 : 2,
                i => { d().buttonOpacity = i == 0 ? 0.6f : i == 1 ? 0.8f : 1f; Apply(); }));
            c.Add(Segmented("Steering hand", new[] { "Left thumb", "Right thumb" }, () => d().leftHanded ? 1 : 0, i => { d().leftHanded = i == 1; Apply(); }));
            card.Add(UiKit.Button("BACK", CloseModal, Color.white, UiKit.Ink, 46, 120));
        }

        private void RefreshCalibLabel()
        {
            if (_calibLabel == null || Services.Save == null) return;
            var d = Services.Save.Data;
            bool land = ScreenOrientationControl.IsLandscape;
            bool cal = land ? d.tiltRollLandCal : d.tiltRollCal;
            string which = land ? "landscape" : "portrait";
            _calibLabel.text = !InputCompat.HasTilt ? "This device has no tilt sensor." : cal ? $"✓ Calibrated ({which})" : $"Not calibrated for {which} yet (uses how you hold the phone at the start).";
            _calibLabel.style.color = cal ? UiKit.Hex("#2E9E5B") : UiKit.Grey;
        }

        /// <summary>Opens Settings ▸ Controls (from the settings or the pause menu – applies immediately).</summary>
        public void OpenControls()
        {
            SyncToggles();
            RefreshCalibLabel();
            OpenModal(_controls);
        }
    }
}
