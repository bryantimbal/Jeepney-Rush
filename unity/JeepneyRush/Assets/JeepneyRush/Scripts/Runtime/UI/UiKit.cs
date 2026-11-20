using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace JeepneyRush
{
    /// <summary>Small styling toolkit for code-built UI Toolkit screens (reference width 1080).</summary>
    public static class UiKit
    {
        public static readonly Color Ink = Hex("#1B1033");
        public static readonly Color Yellow = Hex("#FCD116");
        public static readonly Color Red = Hex("#CE1126");
        public static readonly Color Blue = Hex("#0038A8");
        public static readonly Color Cream = Hex("#FFF8E7");
        public static readonly Color Green = Hex("#18C06A");
        public static readonly Color Grey = Hex("#8A8199");

        private static Font _display, _body;
        public static Font Display => _display != null ? _display : (_display = Resources.Load<Font>("Fonts/LuckiestGuy-Regular"));
        public static Font Body => _body != null ? _body : (_body = Resources.Load<Font>("Fonts/Nunito-Black"));

        public static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }

        public static Texture2D Tex(string name) => MaterialLibrary.LoadTexture(name);

        public static VisualElement Box(string name = null)
        {
            var v = new VisualElement();
            if (name != null) v.name = name;
            v.pickingMode = PickingMode.Ignore;
            return v;
        }

        public static Label Text(string text, float size, Color color, bool display = true, float outline = 0)
        {
            var l = new Label(text);
            l.pickingMode = PickingMode.Ignore;
            var f = display ? Display : Body;
            if (f != null) l.style.unityFontDefinition = new StyleFontDefinition(f);
            l.style.fontSize = size;
            l.style.color = color;
            l.style.unityTextAlign = TextAnchor.MiddleCenter;
            l.style.whiteSpace = WhiteSpace.Normal;
            l.style.marginBottom = l.style.marginTop = l.style.marginLeft = l.style.marginRight = 0;
            l.style.paddingBottom = l.style.paddingTop = l.style.paddingLeft = l.style.paddingRight = 0;
            if (outline > 0)
            {
                l.style.unityTextOutlineWidth = outline;
                l.style.unityTextOutlineColor = Ink;
            }
            return l;
        }

        public static void Border(IStyle s, float width, Color c, float radius)
        {
            s.borderTopWidth = s.borderBottomWidth = s.borderLeftWidth = s.borderRightWidth = width;
            s.borderTopColor = s.borderBottomColor = s.borderLeftColor = s.borderRightColor = c;
            s.borderTopLeftRadius = s.borderTopRightRadius = s.borderBottomLeftRadius = s.borderBottomRightRadius = radius;
        }

        public static void Pad(IStyle s, float v, float h)
        {
            s.paddingTop = s.paddingBottom = v;
            s.paddingLeft = s.paddingRight = h;
        }

        public static Image Icon(string tex, float size)
        {
            var i = new Image { image = Tex(tex), scaleMode = ScaleMode.ScaleToFit };
            i.pickingMode = PickingMode.Ignore;
            i.style.width = size;
            i.style.height = size;
            i.style.flexShrink = 0;
            return i;
        }

        /// <summary>Chunky arcade button with a pressed-down effect and click sound.</summary>
        public static Button Button(string text, Action onClick, Color bg, Color fg, float fontSize = 54, float height = 140)
        {
            var b = new Button(() =>
            {
                Services.Audio?.Click();
                onClick?.Invoke();
            });
            b.text = text;
            var s = b.style;
            if (Display != null) s.unityFontDefinition = new StyleFontDefinition(Display);
            s.fontSize = fontSize;
            s.color = fg;
            s.backgroundColor = bg;
            s.height = height;
            s.marginTop = s.marginBottom = 14;
            s.marginLeft = s.marginRight = 0;
            Border(s, 7, Ink, 36);
            s.borderBottomWidth = 16; // 3D lip
            s.unityTextAlign = TextAnchor.MiddleCenter;
            s.unityTextOutlineWidth = 0;
            b.RegisterCallback<PointerDownEvent>(_ => { b.style.translate = new Translate(0, 8, 0); b.style.borderBottomWidth = 8; }, TrickleDown.TrickleDown);
            b.RegisterCallback<PointerUpEvent>(_ => { b.style.translate = new Translate(0, 0, 0); b.style.borderBottomWidth = 16; }, TrickleDown.TrickleDown);
            b.RegisterCallback<PointerLeaveEvent>(_ => { b.style.translate = new Translate(0, 0, 0); b.style.borderBottomWidth = 16; });
            return b;
        }

        public static Button IconButton(string tex, Action onClick, float size, Color bg)
        {
            var b = Button("", onClick, bg, Ink, 10, size);
            b.style.width = size;
            Border(b.style, 6, Ink, size * 0.28f);
            b.style.borderBottomWidth = 12;
            b.style.marginTop = b.style.marginBottom = 0;
            var i = Icon(tex, size * 0.62f);
            i.style.alignSelf = Align.Center;
            b.Add(i);
            b.style.justifyContent = Justify.Center;
            b.style.alignItems = Align.Center;
            return b;
        }

        public static VisualElement Card(float width = 940)
        {
            var c = Box("card");
            c.pickingMode = PickingMode.Position;
            var s = c.style;
            s.width = width;
            s.maxWidth = Length.Percent(92);
            s.backgroundColor = Cream;
            Border(s, 8, Ink, 54);
            s.borderBottomWidth = 20;
            Pad(s, 44, 48);
            s.alignItems = Align.Stretch;
            return c;
        }

        public static VisualElement Overlay(Color tint)
        {
            var o = Box("overlay");
            o.pickingMode = PickingMode.Position;
            o.style.position = Position.Absolute;
            o.style.left = o.style.right = o.style.top = o.style.bottom = 0;
            o.style.backgroundColor = tint;
            o.style.alignItems = Align.Center;
            o.style.justifyContent = Justify.Center;
            return o;
        }

        public static VisualElement Pill(VisualElement icon, Label value)
        {
            var p = Box("pill");
            p.style.flexDirection = FlexDirection.Row;
            p.style.alignItems = Align.Center;
            p.style.backgroundColor = new Color(Ink.r, Ink.g, Ink.b, 0.82f);
            Border(p.style, 4, new Color(1, 1, 1, 0.25f), 60);
            Pad(p.style, 10, 26);
            p.style.paddingLeft = 12;
            p.Add(icon);
            value.style.marginLeft = 14;
            p.Add(value);
            return p;
        }

        /// <summary>Pop-in scale animation (unscaled time, so it also works while paused).</summary>
        public static void PopIn(VisualElement e, long ms = 260)
        {
            e.style.scale = new Scale(new Vector3(0.6f, 0.6f, 1));
            e.style.opacity = 0;
            long start = 0;
            e.schedule.Execute(t =>
            {
                if (start == 0) start = t.now;
                float k = Mathf.Clamp01((t.now - start) / (float)ms);
                float back = 1 + 2.2f * Mathf.Pow(k - 1, 3) + 1.2f * Mathf.Pow(k - 1, 2); // ease-out-back
                e.style.scale = new Scale(new Vector3(Mathf.LerpUnclamped(0.6f, 1f, back), Mathf.LerpUnclamped(0.6f, 1f, back), 1));
                e.style.opacity = k;
            }).Every(16).Until(() => start != 0 && e.style.opacity.value >= 1f);
        }
    }
}
