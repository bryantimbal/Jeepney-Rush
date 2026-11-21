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
            _banner.style.top = Length.Percent(36);
            _banner.style.alignItems = Align.Center;
            var inner = UiKit.Box();
            inner.style.flexDirection = FlexDirection.Row;
            inner.style.alignItems = Align.Center;
            inner.style.backgroundColor = UiKit.Hex("#0038A8");
            UiKit.Border(inner.style, 6, UiKit.Yellow, 30);
            UiKit.Pad(inner.style, 14, 30);
            inner.Add(UiKit.Icon("ui_brake", 70));
            _bannerLabel = UiKit.Text("", 40, Color.white, true, 2f);
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

        private void ClearJuice()
        {
            foreach (var b in _bubbles) b.RemoveFromHierarchy();
            _bubbles.Clear();
            _fxLayer.Clear();
            _flyingCoins = 0;
            _lastCombo = -1;
            SetCombo(0);
            SetEnforcerBanner(null);
        }
    }
}
