using System;
using System.Collections.Generic;
using JeepneyRush.Logic;
using UnityEngine;
using UnityEngine.UIElements;

namespace JeepneyRush
{
    /// <summary>
    /// Game-feel layer: coins flying into the counter, combo meter, speech bubbles from
    /// passengers / enforcers, and the MMDA checkpoint warning banner.
    /// </summary>
    public sealed partial class UIManager
    {
        private VisualElement _fxLayer, _comboBox, _banner;
        private Label _comboLabel, _bannerLabel;
        private int _flyingCoins, _lastCombo;
        private readonly List<VisualElement> _bubbles = new List<VisualElement>();

        private void BuildJuice()
        {
            _fxLayer = UiKit.Box("fx");
            _fxLayer.style.position = Position.Absolute;
            _fxLayer.style.left = _fxLayer.style.right = _fxLayer.style.top = _fxLayer.style.bottom = 0;
            _root.Add(_fxLayer);

            // combo meter under the coin counter
            _comboBox = UiKit.Box("combo");
            _comboBox.style.position = Position.Absolute;
            _comboBox.style.right = 40;
            _comboBox.style.top = 230;
            _comboBox.style.alignItems = Align.FlexEnd;
            _comboLabel = UiKit.Text("", 58, UiKit.Yellow, true, 3f);
            _comboBox.Add(_comboLabel);
            Show(_comboBox, false);
            _hud.Add(_comboBox);

            // MMDA checkpoint warning
            _banner = UiKit.Box("mmdaBanner");
            _banner.style.position = Position.Absolute;
            _banner.style.left = _banner.style.right = 0;
            // above the horizon so the road stays visible (just under the stop panel / power-ups)
            _banner.style.top = Length.Percent(30.5f);
            _banner.style.alignItems = Align.Center;
            var inner = UiKit.Box();
            inner.style.flexDirection = FlexDirection.Row;
            inner.style.alignItems = Align.Center;
            inner.style.backgroundColor = UiKit.Hex("#0038A8");
            UiKit.Border(inner.style, 5, UiKit.Yellow, 26);
            UiKit.Pad(inner.style, 7, 22);
            inner.style.maxWidth = Length.Percent(92f);
            inner.Add(UiKit.Icon("ui_brake", 48));
            _bannerLabel = UiKit.Text("", 31, Color.white, true, 2f);
            _bannerLabel.style.marginLeft = 14;
            _bannerLabel.style.whiteSpace = WhiteSpace.Normal;
            _bannerLabel.style.flexShrink = 1;
            inner.Add(_bannerLabel);
            _banner.Add(inner);
            Show(_banner, false);
            _hud.Add(_banner);
        }

        private Vector2? WorldToPanel(Vector3 world)
        {
            var cam = Camera.main;
            var panel = _root?.panel;
            if (cam == null || panel == null) return null;
            if (cam.WorldToViewportPoint(world).z <= 0) return null;
            return RuntimePanelUtils.CameraTransformWorldToPanel(panel, world, cam);
        }

        private static void Animate(VisualElement e, float seconds, Action<float> step, Action done = null)
        {
            long start = 0;
            bool finished = false;
            e.schedule.Execute(t =>
            {
                if (start == 0) start = t.now;
                float k = Mathf.Clamp01((t.now - start) / (seconds * 1000f));
                step(k);
                if (k >= 1f && !finished) { finished = true; done?.Invoke(); }
            }).Every(16).Until(() => finished);
        }

        /// <summary>A coin icon flies from the collected coin into the HUD coin counter, which bumps.</summary>
        public void CoinFly(Vector3 world)
        {
            if (_flyingCoins >= 14) { BumpCoins(); return; }
            var from = WorldToPanel(world);
            var target = _coins.worldBound.center;
            if (from == null || float.IsNaN(target.x) || target == Vector2.zero) { BumpCoins(); return; }
            _flyingCoins++;
            var icon = UiKit.Icon("ui_coin", 62);
            icon.style.position = Position.Absolute;
            _fxLayer.Add(icon);
            var a = from.Value;
            var ctrl = new Vector2((a.x + target.x) / 2 - 120, Mathf.Min(a.y, target.y) - 80); // curved path
            Animate(icon, 0.5f, k =>
            {
                float e = k * k; // accelerate into the counter
                var p = (1 - e) * (1 - e) * a + 2 * (1 - e) * e * ctrl + e * e * target;
                icon.style.left = p.x - 31;
                icon.style.top = p.y - 31;
                float sc = Mathf.Lerp(1.2f, 0.6f, k);
                icon.style.scale = new Scale(new Vector3(sc, sc, 1));
                icon.style.rotate = new Rotate(k * 540f);
            }, () =>
            {
                icon.RemoveFromHierarchy();
                _flyingCoins--;
                BumpCoins();
            });
        }

        private void BumpCoins()
        {
            var pill = _coins.parent;
            Animate(pill, 0.18f, k =>
            {
                float s = 1f + Mathf.Sin(k * Mathf.PI) * 0.18f;
                pill.style.scale = new Scale(new Vector3(s, s, 1));
            });
        }

        /// <summary>Shows "COMBO xN" while a coin streak is going.</summary>
        // arcade combo (near misses + overtakes): "x3 · 7" chip with a draining timer bar under the score
        private VisualElement _arcadeBox, _arcadeFill;
        private Label _arcadeLabel;
        private int _arcadeChain = -1, _arcadeMult = -1;

        private void BuildArcade(VisualElement parent)
        {
            _arcadeBox = UiKit.Box("arcade");
            _arcadeBox.style.position = Position.Absolute;
            _arcadeBox.style.top = Length.Percent(17);
            _arcadeBox.style.left = 0; _arcadeBox.style.right = 0;
            _arcadeBox.style.alignItems = Align.Center;
            _arcadeBox.pickingMode = PickingMode.Ignore;
            var chip = UiKit.Box();
            chip.style.backgroundColor = new Color(UiKit.Ink.r, UiKit.Ink.g, UiKit.Ink.b, 0.85f);
            UiKit.Border(chip.style, 4, UiKit.Yellow, 30);
            UiKit.Pad(chip.style, 6, 26);
            chip.style.alignItems = Align.Center;
            _arcadeLabel = UiKit.Text("", 44, UiKit.Yellow, true, 2f);
            chip.Add(_arcadeLabel);
            var track = UiKit.Box();
            track.style.width = 220; track.style.height = 10; track.style.marginTop = 4;
            track.style.backgroundColor = new Color(1, 1, 1, 0.2f);
            UiKit.Border(track.style, 0, Color.clear, 5);
            _arcadeFill = UiKit.Box();
            _arcadeFill.style.height = Length.Percent(100);
            _arcadeFill.style.backgroundColor = UiKit.Yellow;
            UiKit.Border(_arcadeFill.style, 0, Color.clear, 5);
            track.Add(_arcadeFill);
            chip.Add(track);
            _arcadeBox.Add(chip);
            Show(_arcadeBox, false);
            parent.Add(_arcadeBox);
        }

        public void SetArcade(int chain, int multiplier, float remaining01)
        {
            if (_arcadeBox == null) return;
            if (chain != _arcadeChain || multiplier != _arcadeMult)
            {
                bool up = multiplier > _arcadeMult && _arcadeMult > 0;
                _arcadeChain = chain; _arcadeMult = multiplier;
                Show(_arcadeBox, chain > 0);
                if (chain > 0)
                {
                    _arcadeLabel.text = multiplier > 1 ? $"x{multiplier}  ·  {chain} CHAIN" : $"{chain} CHAIN";
                    var col = multiplier >= 5 ? UiKit.Hex("#FF5AF0") : multiplier >= 4 ? UiKit.Hex("#FF8A1F") : multiplier >= 3 ? UiKit.Hex("#3EE07A") : UiKit.Yellow;
                    _arcadeLabel.style.color = col;
                    _arcadeFill.style.backgroundColor = col;
                    if (up) UiKit.PopIn(_arcadeBox, 260);
                }
            }
            if (chain > 0) _arcadeFill.style.width = Length.Percent(remaining01 * 100f);
        }

        public void SetCombo(int combo)
        {
            if (combo == _lastCombo) return;
            bool rising = combo > _lastCombo;
            _lastCombo = combo;
            bool show = combo >= 3;
            Show(_comboBox, show);
            if (!show) return;
            _comboLabel.text = $"COMBO x{combo}";
            _comboLabel.style.color = combo >= 30 ? UiKit.Hex("#FF5AF0") : combo >= 20 ? UiKit.Hex("#FF8A1F") : combo >= 10 ? UiKit.Hex("#3EE07A") : UiKit.Yellow;
            if (rising)
                Animate(_comboLabel, 0.15f, k =>
                {
                    float s = 1f + Mathf.Sin(k * Mathf.PI) * 0.25f;
                    _comboLabel.style.scale = new Scale(new Vector3(s, s, 1));
                });
        }

        /// <summary>
        /// Comic speech bubble that follows a point in the world (a passenger in the jeepney,
        /// the MMDA enforcer…). Angry lines use a red tint.
        /// </summary>
        private readonly Dictionary<string, float> _saidAt = new Dictionary<string, float>();
        private readonly Dictionary<VisualElement, Rect> _bubbleRects = new Dictionary<VisualElement, Rect>();
        private readonly Dictionary<VisualElement, int> _bubbleOrder = new Dictionary<VisualElement, int>();
        private int _bubbleSerial;
        /// <summary>The same line isn't said again within this many seconds.</summary>
        public const float RepeatGap = 10f;

        public void Say(string text, Func<Vector3> anchor, bool angry = false, float seconds = 1.8f)
        {
            if (string.IsNullOrEmpty(text) || anchor == null) return;
            // no doubles: a line that is on screen, or was said a moment ago, isn't said again
            foreach (var other in _bubbles) if (other.userData is string t && t == text) return;
            float now = Time.unscaledTime;
            if (_saidAt.TryGetValue(text, out float at) && now - at < RepeatGap) return;
            _saidAt[text] = now;
            if (_saidAt.Count > 64) _saidAt.Clear();
            if (_bubbles.Count >= 3) { _bubbleRects.Remove(_bubbles[0]); _bubbleOrder.Remove(_bubbles[0]); _bubbles[0].RemoveFromHierarchy(); _bubbles.RemoveAt(0); }
            var b = UiKit.Box("bubble");
            b.userData = text;
            int order = ++_bubbleSerial;
            _bubbleOrder[b] = order;
            b.style.position = Position.Absolute;
            b.style.opacity = 0; // shown once its size is known (no jump on the first frame)
            b.style.backgroundImage = UiKit.Tex("ui_bubble");
            b.style.unitySliceLeft = 60;
            b.style.unitySliceRight = 60;
            b.style.unitySliceTop = 40;
            b.style.unitySliceBottom = 50;
            b.style.paddingLeft = b.style.paddingRight = 30;
            b.style.paddingTop = 15;
            b.style.paddingBottom = 40;
            b.style.minWidth = 200;
            b.style.unityBackgroundImageTintColor = angry ? UiKit.Hex("#FFD6D6") : Color.white;
            var l = UiKit.Text(text, 34, angry ? UiKit.Red : UiKit.Ink, true);
            l.style.whiteSpace = WhiteSpace.Normal; // long lines wrap instead of running off the screen
            b.style.maxWidth = 660;
            // a little comic tilt, and angry bubbles shake
            float tilt = UnityEngine.Random.Range(-3.5f, 3.5f);
            b.Add(l);
            _fxLayer.Add(b);
            _bubbles.Add(b);
            float jitter = UnityEngine.Random.Range(-60f, 60f);
            Animate(b, seconds, k =>
            {
                var p = WorldToPanel(anchor());
                if (p == null) { b.style.display = DisplayStyle.None; return; }
                b.style.display = DisplayStyle.Flex;
                float w = b.resolvedStyle.width, h = b.resolvedStyle.height;
                bool sized = !(float.IsNaN(w) || w <= 0 || float.IsNaN(h) || h <= 0);
                if (!sized) { w = 300; h = 130; }
                // keep the whole bubble on screen (the driver sits at the left edge of the view)
                float pw = _fxLayer.resolvedStyle.width, ph = _fxLayer.resolvedStyle.height;
                if (float.IsNaN(pw) || pw <= 0) pw = 1080;
                if (float.IsNaN(ph) || ph <= 0) ph = 1920;
                const float margin = 24f;
                float x = Mathf.Clamp(p.Value.x - 60 + jitter, margin, Mathf.Max(margin, pw - w - margin));
                float y = Mathf.Clamp(p.Value.y - 150 - k * 40f, margin + 140f, Mathf.Max(margin + 140f, ph - h - margin));
                // never on top of another bubble: move up above it
                for (int pass = 0; pass < 3; pass++)
                {
                    bool moved = false;
                    foreach (var kv in _bubbleRects)
                    {
                        // only the newer bubble moves out of the way (older ones never jump)
                        if (kv.Key == b || !_bubbleOrder.TryGetValue(kv.Key, out int ko) || ko > order) continue;
                        var o = kv.Value;
                        if (x < o.xMax && x + w > o.xMin && y < o.yMax && y + h > o.yMin) { y = Mathf.Max(margin + 140f, o.yMin - h - 6f); moved = true; }
                    }
                    if (!moved) break;
                }
                _bubbleRects[b] = new Rect(x, y, w, h);
                b.style.left = x;
                b.style.top = y;
                float pop = k < 0.1f ? Mathf.Lerp(0.3f, 1.1f, k / 0.1f) : k < 0.18f ? Mathf.Lerp(1.1f, 1f, (k - 0.1f) / 0.08f) : 1f;
                b.style.scale = new Scale(new Vector3(pop, pop, 1));
                b.style.rotate = new Rotate(tilt + (angry && k < 0.35f ? Mathf.Sin(k * 90f) * 4f * (1f - k / 0.35f) : 0f));
                b.style.opacity = !sized ? 0f : k > 0.85f ? (1 - k) / 0.15f : 1f;
            }, () =>
            {
                b.RemoveFromHierarchy();
                _bubbles.Remove(b);
                _bubbleRects.Remove(b);
                _bubbleOrder.Remove(b);
            });
        }

        /// <summary>MMDA checkpoint warning (null hides it).</summary>
        public void SetEnforcerBanner(string text)
        {
            bool on = !string.IsNullOrEmpty(text);
            if ((_banner.style.display == DisplayStyle.Flex) != on) Show(_banner, on);
            if (on)
            {
                _bannerLabel.text = text;
                _banner.style.opacity = 0.75f + 0.25f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 6f));
            }
        }

        private Label _gearD, _gearR;
        private VisualElement _gearBtn;

        /// <summary>Gear shifter: tap to switch D (drive) ↔ R (reverse). Shows the active gear lit.</summary>
        private VisualElement BuildGearButton()
        {
            var b = new VisualElement { name = "gear" };
            b.pickingMode = PickingMode.Position;
            b.AddToClassList(InteractiveClass);
            b.style.flexDirection = FlexDirection.Row;
            b.style.alignItems = Align.Center;
            b.style.alignSelf = Align.FlexStart;
            b.style.marginTop = 14;
            b.style.backgroundColor = new Color(0.1f, 0.1f, 0.12f, 0.95f);
            UiKit.Border(b.style, 7, UiKit.Ink, 34);
            b.style.borderBottomWidth = 12;
            UiKit.Pad(b.style, 8, 18);
            b.Add(UiKit.Text("GEAR", 26, Color.white, true, 1.5f));
            Label Cell(string t)
            {
                var l = UiKit.Text(t, 66, Color.white, true, 2f);
                l.style.width = 88;
                l.style.height = 84;
                l.style.marginLeft = 8;
                l.style.unityTextAlign = TextAnchor.MiddleCenter;
                UiKit.Border(l.style, 0, Color.clear, 16);
                return l;
            }
            _gearD = Cell("D");
            _gearR = Cell("R");
            b.Add(_gearD);
            b.Add(_gearR);
            b.RegisterCallback<PointerDownEvent>(e =>
            {
                e.StopPropagation();
                b.style.translate = new Translate(0, 5, 0);
                Services.Game.Session?.ToggleGear();
            });
            b.RegisterCallback<PointerUpEvent>(_ => b.style.translate = new Translate(0, 0, 0));
            b.RegisterCallback<PointerLeaveEvent>(_ => b.style.translate = new Translate(0, 0, 0));
            _gearBtn = b;
            SetGear(false);
            return b;
        }

        public void SetGear(bool reverse)
        {
            if (_gearD == null) return;
            _gearD.style.backgroundColor = reverse ? Color.clear : UiKit.Hex("#3EE07A");
            _gearD.style.color = reverse ? new Color(1, 1, 1, 0.35f) : UiKit.Ink;
            _gearR.style.backgroundColor = reverse ? UiKit.Hex("#FF6B6B") : Color.clear;
            _gearR.style.color = reverse ? UiKit.Ink : new Color(1, 1, 1, 0.35f);
        }

        private Label _weather, _fuelLabel, _fuelStation;
        private VisualElement _fuelFill, _fuelBox;

        /// <summary>Fuel gauge: "E ▕████░░▏ F" bar plus distance to the next gas station.</summary>
        private VisualElement BuildFuelGauge()
        {
            _fuelBox = UiKit.Box("fuel");
            _fuelBox.style.alignItems = Align.FlexEnd;
            _fuelBox.style.marginTop = 12;
            var row = UiKit.Box();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.backgroundColor = new Color(UiKit.Ink.r, UiKit.Ink.g, UiKit.Ink.b, 0.75f);
            UiKit.Border(row.style, 3, new Color(1, 1, 1, 0.25f), 22);
            UiKit.Pad(row.style, 8, 16);
            _fuelLabel = UiKit.Text("GAS", 26, Color.white, true, 1.5f);
            _fuelLabel.style.marginRight = 10;
            row.Add(_fuelLabel);
            row.Add(UiKit.Text("E", 22, UiKit.Hex("#FF6B6B"), true, 1f));
            var bar = UiKit.Box();
            bar.style.width = 220;
            bar.style.height = 22;
            bar.style.marginLeft = 6;
            bar.style.marginRight = 6;
            bar.style.backgroundColor = new Color(0, 0, 0, 0.5f);
            UiKit.Border(bar.style, 0, Color.clear, 8);
            bar.style.overflow = Overflow.Hidden;
            _fuelFill = UiKit.Box();
            _fuelFill.style.height = Length.Percent(100);
            _fuelFill.style.width = Length.Percent(100);
            _fuelFill.style.backgroundColor = UiKit.Hex("#3EE07A");
            bar.Add(_fuelFill);
            row.Add(bar);
            row.Add(UiKit.Text("F", 22, Color.white, true, 1f));
            _fuelBox.Add(row);
            _fuelStation = UiKit.Text("", 24, UiKit.Hex("#D8D3FF"), false, 1.5f);
            _fuelStation.style.marginTop = 4;
            _fuelBox.Add(_fuelStation);
            return _fuelBox;
        }

        /// <summary>Updates the gauge (green → yellow → red, blinking when low).</summary>
        public void SetFuel(float fraction, float stationMetres, bool low)
        {
            if (_fuelFill == null) return;
            fraction = Mathf.Clamp01(fraction);
            _fuelFill.style.width = Length.Percent(fraction * 100f);
            var c = fraction > 0.5f ? Color.Lerp(UiKit.Hex("#FFD166"), UiKit.Hex("#3EE07A"), (fraction - 0.5f) * 2f)
                                    : Color.Lerp(UiKit.Hex("#FF4D4D"), UiKit.Hex("#FFD166"), fraction * 2f);
            _fuelFill.style.backgroundColor = c;
            float blink = low ? 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 6f)) : 1f;
            _fuelBox.style.opacity = blink;
            _fuelLabel.style.color = low ? UiKit.Hex("#FF6B6B") : Color.white;
            _fuelStation.text = stationMetres >= 0 ? $"GAS STATION {Mathf.CeilToInt(stationMetres):N0} m" : "";
        }
        private VisualElement _lightning;

        /// <summary>Weather readout under the distance ("UMUULAN · madulas").</summary>
        private Label _clock, _jamLabel;
        // old-style analog speedometer (HUD, behind view)
        private VisualElement _speedo, _needle;
        private Label _odo;
        private Button _viewBtn;
        private float _speedTarget, _speedShown;

        private const float SpeedoSize = 175f;

        private VisualElement _speedCol, _speedPill;
        private Label _speedPillText, _speedoKmh;

        /// <summary>
        /// Speed readout under the hearts and fuel gauge (top right). LIKOD view: a compact digital
        /// "63 km/h". DRIVER view: the analog dashboard gauge (needle + km/h in the middle). Both read
        /// the same speed value; switching views swaps them instantly.
        /// </summary>
        private VisualElement BuildSpeedometer()
        {
            _speedCol = UiKit.Box("speedCol");
            _speedCol.style.alignItems = Align.FlexEnd;
            _speedPill = UiKit.Box("speedPill");
            _speedPill.style.flexDirection = FlexDirection.Row;
            _speedPill.style.alignItems = Align.FlexEnd;
            _speedPill.style.marginTop = 10;
            _speedPill.style.backgroundColor = new Color(UiKit.Ink.r, UiKit.Ink.g, UiKit.Ink.b, 0.78f);
            UiKit.Border(_speedPill.style, 4, new Color(1, 1, 1, 0.25f), 26);
            UiKit.Pad(_speedPill.style, 6, 18);
            _speedPillText = UiKit.Text("0", 52, Color.white, true, 2f);
            _speedPill.Add(_speedPillText);
            var unit = UiKit.Text("km/h", 24, new Color(1, 1, 1, 0.8f), false);
            unit.style.marginLeft = 8;
            unit.style.marginBottom = 8;
            _speedPill.Add(unit);
            _speedCol.Add(_speedPill);
            if (_debug != null) { _debug.style.unityTextAlign = TextAnchor.MiddleRight; _speedCol.Add(_debug); }
            _speedo = UiKit.Box("speedo");
            _speedo.style.width = _speedo.style.height = SpeedoSize;
            _speedo.style.marginTop = 10;
            _speedo.style.backgroundImage = UiKit.Tex("gauge_face");
            _odo = UiKit.Text("000000", 14, new Color(0.93f, 0.93f, 0.93f), false);
            _odo.style.position = Position.Absolute;
            _odo.style.left = SpeedoSize * 0.364f;
            _odo.style.width = SpeedoSize * 0.274f;
            _odo.style.top = SpeedoSize * 0.688f;
            _odo.style.height = SpeedoSize * 0.066f;
            _odo.style.letterSpacing = 2.2f;
            _odo.style.fontSize = SpeedoSize * 0.058f;
            _speedo.Add(_odo);
            _needle = UiKit.Box("needle");
            _needle.style.position = Position.Absolute;
            _needle.style.left = _needle.style.top = 0;
            _needle.style.width = _needle.style.height = SpeedoSize;
            _needle.style.backgroundImage = UiKit.Tex("gauge_needle");
            _speedo.Add(_needle);
            // digital km/h in the middle of the dial
            _speedoKmh = UiKit.Text("0", SpeedoSize * 0.17f, Color.white, true, 1.5f);
            _speedoKmh.style.position = Position.Absolute;
            _speedoKmh.style.left = 0;
            _speedoKmh.style.right = 0;
            _speedoKmh.style.top = SpeedoSize * 0.46f;
            _speedoKmh.style.unityTextAlign = TextAnchor.MiddleCenter;
            _speedoKmh.pickingMode = PickingMode.Ignore;
            _speedo.Add(_speedoKmh);
            // the needle lags and shakes a little, like an old cable-driven gauge
            _speedo.schedule.Execute(() =>
            {
                if (_speedo.resolvedStyle.display == DisplayStyle.None) return;
                float dt = Mathf.Min(0.05f, Time.unscaledDeltaTime);
                _speedShown = Mathf.Lerp(_speedShown, _speedTarget, 1f - Mathf.Exp(-dt * 4f));
                float t = Time.time;
                float wobble = _speedShown > 2f ? Mathf.Sin(t * 23f) * 0.9f + Mathf.Sin(t * 7.3f) * 0.6f : 0f;
                _needle.style.rotate = new Rotate(Logic.Speedometer.NeedleAngle(_speedShown + wobble));
            }).Every(16);
            _speedCol.Add(_speedo);
            SetViewMode(false);
            return _speedCol;
        }

        /// <summary>Speed (km/h) and the lifetime odometer (metres) for the analog speedometer.</summary>
        public void SetSpeedo(float kmh, double odometerMetres)
        {
            _speedTarget = kmh;
            if (_speedoKmh != null) _speedoKmh.text = Mathf.RoundToInt(kmh).ToString();
            if (_odo != null) _odo.text = Logic.Speedometer.Odometer(odometerMetres);
        }

        /// <summary>Driver view hides the HUD speedometer (the one on the dashboard takes over).</summary>
        public void SetViewMode(bool driver)
        {
            // DRIVER view: the cockpit's own dashboard gauge shows the speed – no HUD speedometer at all.
            // LIKOD view: the compact digital readout under the hearts and fuel.
            if (_speedo != null) _speedo.style.display = DisplayStyle.None;
            if (_speedPill != null) _speedPill.style.display = driver ? DisplayStyle.None : DisplayStyle.Flex;
            _driverView = driver;
            if (_viewBtn != null) _viewBtn.text = driver ? "VIEW: DRIVER" : "VIEW: LIKOD";
            // keep the road visible: pedals slightly see-through from the driver's seat
            ApplyControlOpacity();
        }

        private readonly System.Collections.Generic.List<VisualElement> _pedalButtons = new System.Collections.Generic.List<VisualElement>();

        /// <summary>In-game clock under the weather ("🌙 9:15 PM" style, text only).</summary>
        public void SetClock(string text, bool night)
        {
            if (_clock == null) return;
            _clock.text = text ?? "";
            _clock.style.color = night ? new Color(0.75f, 0.85f, 1f) : new Color(1f, 0.95f, 0.8f);
        }

        /// <summary>"TRAFFIC JAM" / "RUSH HOUR" indicator while you're in a jam (empty hides it).</summary>
        private VisualElement _boostBtn;
        private Label _boostCount;
        private int _boostsShown = -1;

        /// <summary>TURBO button: hidden with no boosters, pops in when the first one is collected.</summary>
        public void SetBoosts(int count)
        {
            if (_boostBtn == null || count == _boostsShown) return;
            bool was = _boostsShown > 0;
            _boostsShown = count;
            Show(_boostBtn, count > 0);
            _boostCount.text = "x" + count;
            if (count > 0 && !was) UiKit.PopIn(_boostBtn, 320);
        }

        /// <summary>Special passenger (contract) line under the score: destination, time, condition.</summary>
        public void SetContract(string text)
        {
            if (_contractLabel == null) return;
            _contractLabel.text = text ?? "";
            _contractLabel.style.display = string.IsNullOrEmpty(text) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private Label _contractLabel, _radioLabel;
        private float _radioShownAt;

        /// <summary>The radio DJ's line on the HUD (shown for a few seconds).</summary>
        public void SetRadioLine(string text)
        {
            if (_radioLabel == null) return;
            _radioLabel.text = string.IsNullOrEmpty(text) ? "" : text;
            _radioLabel.style.display = string.IsNullOrEmpty(text) ? DisplayStyle.None : DisplayStyle.Flex;
            _radioShownAt = Time.unscaledTime;
            _radioLabel.schedule.Execute(() => { if (Time.unscaledTime - _radioShownAt >= 7.9f) _radioLabel.style.display = DisplayStyle.None; }).StartingIn(8000);
        }

        private void CycleRadio()
        {
            var d = Services.Save.Data;
            d.radioStation = Radio.Next(d.radioStation);
            Services.Save.Save();
            Services.Audio.SetRadio(d.radioStation, true);
            Float(Radio.Names[d.radioStation], FloatKind.Info);
            Services.Game.Session?.RadioChanged();
        }

        public void SetJam(string text)
        {
            if (_jamLabel == null) return;
            _jamLabel.text = text ?? "";
        }

        private Label _ratingLabel;
        private VisualElement _ratingRow;

        /// <summary>Driver rating on the HUD (★ icon + "4.0"): green when good, amber, red when the riders are angry.</summary>
        public void SetRating(string display, float value, bool panic)
        {
            if (_ratingLabel == null) return;
            _ratingLabel.text = $"RATING {display}";
            _ratingLabel.style.color = panic || value < 2f ? new Color(1f, 0.45f, 0.4f) : value < 3.2f ? new Color(1f, 0.8f, 0.35f) : new Color(0.6f, 1f, 0.6f);
        }

        public void SetWeather(string text, Color color)
        {
            if (_weather == null) return;
            _weather.text = text ?? "";
            _weather.style.color = color;
        }

        /// <summary>Full-screen white lightning flash (double flicker).</summary>
        public void LightningFlash()
        {
            if (_lightning == null)
            {
                _lightning = UiKit.Box("lightning");
                _lightning.style.position = Position.Absolute;
                _lightning.style.left = _lightning.style.right = _lightning.style.top = _lightning.style.bottom = 0;
                _lightning.style.backgroundColor = new Color(0.92f, 0.95f, 1f, 1f);
                _lightning.pickingMode = PickingMode.Ignore;
                _lightning.style.opacity = 0;
                _root.Add(_lightning);
            }
            long start = 0;
            _lightning.schedule.Execute(t =>
            {
                if (start == 0) start = t.now;
                float ms = t.now - start;
                // flash, dip, second flash, fade
                float o = ms < 70 ? 0.55f : ms < 130 ? 0.1f : ms < 200 ? 0.45f : Mathf.Max(0, 0.45f - (ms - 200) / 500f);
                _lightning.style.opacity = o;
            }).Every(16).Until(() => start != 0 && _lightning.style.opacity.value <= 0f);
        }

        private void ClearJuice()
        {
            foreach (var b in _bubbles) b.RemoveFromHierarchy();
            _bubbles.Clear();
            _bubbleRects.Clear();
            _bubbleOrder.Clear();
            _saidAt.Clear();
            _fxLayer.Clear();
            _flyingCoins = 0;
            _lastCombo = -1;
            SetCombo(0);
            SetEnforcerBanner(null);
            SetWeather("", Color.white);
        }
    }
}
