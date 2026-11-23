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
            b.focusable = false; // otherwise Space/Enter (brake on PC) would "click" the last pressed button
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

        /// <summary>
        /// Vertical scroll area that works without Unity's default theme: it shrinks to the space
        /// left in its card, while its content keeps its natural height (so rows never get squashed
        /// on top of each other) and scrolls by dragging.
        /// </summary>
        public static ScrollView Scroll(float maxHeight)
        {
            var sv = new ScrollView(ScrollViewMode.Vertical);
            sv.style.maxHeight = maxHeight;
            sv.style.flexShrink = 1;
            sv.style.minHeight = 160;
            sv.style.overflow = Overflow.Hidden;
            sv.touchScrollBehavior = ScrollView.TouchScrollBehavior.Clamped;
            sv.contentContainer.style.flexShrink = 0;
            sv.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            sv.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            return sv;
        }

        /// <summary>A box whose children keep their natural height (never squashed).</summary>
        public static VisualElement Stack()
        {
            var b = Box();
            b.style.flexShrink = 0;
            return b;
        }

        /// <summary>
        /// Colourful jeepney-style menu button: painted body, glossy highlight, darker 3D lip,
        /// icon and outlined white label. <paramref name="vertical"/> stacks the icon above the label
        /// (for narrow buttons).
        /// </summary>
        public static Button MenuButton(string text, string icon, Color bg, Action onClick, float height = 140, bool vertical = false, float fontSize = 44)
        {
            var b = Button("", onClick, bg, Color.white, fontSize, height);
            var s = b.style;
            Color.RGBToHSV(bg, out float h, out float sat, out float v);
            var lip = Color.HSVToRGB(h, Mathf.Min(1f, sat * 1.1f), v * 0.62f);
            s.borderBottomColor = lip;
            s.borderLeftColor = s.borderRightColor = s.borderTopColor = Ink;
            s.overflow = Overflow.Hidden;
            s.flexDirection = vertical ? FlexDirection.Column : FlexDirection.Row;
            s.alignItems = Align.Center;
            s.justifyContent = Justify.Center;
            s.paddingLeft = s.paddingRight = 8;
            // glossy highlight across the top half
            var gloss = Box();
            gloss.style.position = Position.Absolute;
            gloss.style.left = 14; gloss.style.right = 14; gloss.style.top = 6;
            gloss.style.height = Length.Percent(34);
            gloss.style.backgroundColor = new Color(1, 1, 1, 0.22f);
            Border(gloss.style, 0, Color.clear, 22);
            b.Add(gloss);
            // jeepney pinstripe (red / yellow / blue) along the bottom edge
            var stripe = Box();
            stripe.style.position = Position.Absolute;
            stripe.style.left = 0; stripe.style.right = 0; stripe.style.bottom = 6;
            stripe.style.height = 6;
            stripe.style.flexDirection = FlexDirection.Row;
            foreach (var c in new[] { Red, Yellow, Blue })
            {
                var seg = Box();
                seg.style.flexGrow = 1;
                seg.style.backgroundColor = new Color(c.r, c.g, c.b, 0.55f);
                stripe.Add(seg);
            }
            b.Add(stripe);
            if (!string.IsNullOrEmpty(icon))
            {
                var ic = Icon(icon, vertical ? height * 0.38f : height * 0.5f);
                if (vertical) ic.style.marginBottom = 2; else ic.style.marginRight = 14;
                b.Add(ic);
            }
            var label = Text(text, fontSize, Color.white, true, 3.5f);
            label.style.whiteSpace = WhiteSpace.NoWrap;
            label.style.marginTop = 4; // the display font sits a bit high
            b.Add(label);
            return b;
        }

        public static VisualElement Card(float width = 940)
        {
            var c = Box("card");
            c.pickingMode = PickingMode.Position;
            var s = c.style;
            s.width = width;
            s.maxWidth = Length.Percent(92);
            s.maxHeight = Length.Percent(94);
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
