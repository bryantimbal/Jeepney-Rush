using System;
using System.Collections.Generic;
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
            UiKit.Pad(inner.style, 8, 24);
            inner.Add(UiKit.Icon("ui_brake", 54));
            _bannerLabel = UiKit.Text("", 34, Color.white, true, 2f);
            _bannerLabel.style.marginLeft = 16;
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
        public void Say(string text, Func<Vector3> anchor, bool angry = false, float seconds = 1.8f)
        {
            if (_bubbles.Count >= 3) { _bubbles[0].RemoveFromHierarchy(); _bubbles.RemoveAt(0); }
            var b = UiKit.Box("bubble");
            b.style.position = Position.Absolute;
            b.style.backgroundImage = UiKit.Tex("ui_bubble");
            b.style.unitySliceLeft = 60;
            b.style.unitySliceRight = 60;
            b.style.unitySliceTop = 40;
            b.style.unitySliceBottom = 50;
            b.style.paddingLeft = b.style.paddingRight = 34;
            b.style.paddingTop = 18;
            b.style.paddingBottom = 44;
            b.style.minWidth = 220;
            b.style.unityBackgroundImageTintColor = angry ? UiKit.Hex("#FFD6D6") : Color.white;
            var l = UiKit.Text(text, 38, angry ? UiKit.Red : UiKit.Ink, true);
            l.style.whiteSpace = WhiteSpace.NoWrap;
            b.Add(l);
            _fxLayer.Add(b);
            _bubbles.Add(b);
            float jitter = UnityEngine.Random.Range(-60f, 60f);
            Animate(b, seconds, k =>
            {
                var p = WorldToPanel(anchor());
                if (p == null) { b.style.display = DisplayStyle.None; return; }
                b.style.display = DisplayStyle.Flex;
                float w = b.resolvedStyle.width;
                if (float.IsNaN(w)) w = 300;
                b.style.left = p.Value.x - 60 + jitter;
                b.style.top = p.Value.y - 150 - k * 40f;
                float pop = k < 0.1f ? Mathf.Lerp(0.3f, 1.1f, k / 0.1f) : k < 0.18f ? Mathf.Lerp(1.1f, 1f, (k - 0.1f) / 0.08f) : 1f;
                b.style.scale = new Scale(new Vector3(pop, pop, 1));
                b.style.opacity = k > 0.85f ? (1 - k) / 0.15f : 1f;
            }, () =>
            {
                b.RemoveFromHierarchy();
                _bubbles.Remove(b);
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

        /// <summary>In-game clock under the weather ("🌙 9:15 PM" style, text only).</summary>
        public void SetClock(string text, bool night)
        {
            if (_clock == null) return;
            _clock.text = text ?? "";
            _clock.style.color = night ? new Color(0.75f, 0.85f, 1f) : new Color(1f, 0.95f, 0.8f);
        }

        /// <summary>"TRAFFIC JAM" / "RUSH HOUR" indicator while you're in a jam (empty hides it).</summary>
        public void SetJam(string text)
        {
            if (_jamLabel == null) return;
            _jamLabel.text = text ?? "";
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
            _fxLayer.Clear();
            _flyingCoins = 0;
            _lastCombo = -1;
            SetCombo(0);
            SetEnforcerBanner(null);
            SetWeather("", Color.white);
        }
    }
}
