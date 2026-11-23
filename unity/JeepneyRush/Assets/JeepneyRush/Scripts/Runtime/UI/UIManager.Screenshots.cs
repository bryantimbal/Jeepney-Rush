using UnityEngine;
using UnityEngine.UIElements;

namespace JeepneyRush
{
    /// <summary>Screenshot UI: HUD camera button, pause-menu clean photo, 9:16 frame bars.</summary>
    public sealed partial class UIManager
    {
        private Button _shotBtn, _pausePhoto;
        private VisualElement[] _frameBars;

        private void BuildScreenshotUi()
        {
            // black bars around the 9:16 store frame (the 3D camera is letterboxed to match)
            _frameBars = new VisualElement[4];
            for (int i = 0; i < 4; i++)
            {
                var b = UiKit.Box("frameBar" + i);
                b.style.position = Position.Absolute;
                b.style.backgroundColor = Color.black;
                b.pickingMode = PickingMode.Ignore;
                _root.Insert(0, b); // behind every screen
                _frameBars[i] = b;
            }
            RefreshScreenshotUi();
        }

        /// <summary>Called by <see cref="Screenshots"/> once both exist.</summary>
        public void HookScreenshots(Screenshots s)
        {
            s.HideUi = hide => { if (_root != null) _root.visible = !hide; };
            s.Saved = msg => { Toast(msg); FlashWhite(); };
            s.HideButton = hide => { if (_shotBtn != null) _shotBtn.style.visibility = hide ? Visibility.Hidden : Visibility.Visible; };
        }

        public void RefreshScreenshotUi()
        {
            var d = Services.Save?.Data;
            if (_shotBtn != null) Show(_shotBtn, d != null && d.screenshotButton);
            ApplySafeArea();
        }

        private void LayoutFrameBars(float k)
        {
            if (_frameBars == null) return;
            var f = Screenshots.Frame;
            float W = Screen.width, H = Screen.height;
            // left, right, top, bottom (UI space: origin top-left, scaled by k)
            Set(_frameBars[0], 0, 0, f.X, H);
            Set(_frameBars[1], f.X + f.W, 0, W - f.X - f.W, H);
            Set(_frameBars[2], 0, 0, W, H - f.Y - f.H);
            Set(_frameBars[3], 0, H - f.Y, W, f.Y);
            void Set(VisualElement e, float x, float y, float w, float h)
            {
                bool on = w > 0.5f && h > 0.5f;
                e.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
                e.style.left = x * k; e.style.top = y * k; e.style.width = w * k; e.style.height = h * k;
            }
        }

        /// <summary>Camera-shutter flash.</summary>
        private void FlashWhite()
        {
            var fl = UiKit.Box("shutter");
            fl.style.position = Position.Absolute;
            fl.style.left = fl.style.right = fl.style.top = fl.style.bottom = 0;
            fl.style.backgroundColor = Color.white;
            fl.pickingMode = PickingMode.Ignore;
            _root.Add(fl);
            long start = 0;
            fl.schedule.Execute(t =>
            {
                if (start == 0) start = t.now;
                float a = 0.85f * (1f - (t.now - start) / 260f);
                fl.style.opacity = Mathf.Max(0, a);
                if (a <= 0) fl.RemoveFromHierarchy();
            }).Every(16).Until(() => fl.parent == null);
        }
    }
}
