using UnityEngine;
using UnityEngine.UIElements;

namespace JeepneyRush
{
    /// <summary>
    /// Keeps the road ahead visible: pop-up messages and the warning banner live in the sky band at
    /// the top, at most two messages / two speech bubbles at a time, and a bubble that would cover
    /// the lane ahead (the "driving corridor") slides to the side of the road. Portrait and landscape.
    /// </summary>
    public sealed partial class UIManager
    {
        public const int MaxFloats = Logic.HudLayout.MaxFloats, MaxBubbles = Logic.HudLayout.MaxBubbles;
        private float BubbleMaxWidthNow => Logic.HudLayout.BubbleMaxWidthFor(_hudLandscape);

        private bool _hudLandscape;

        private void ApplyHudClearance(bool landscape)
        {
            _hudLandscape = landscape;
            if (_floats != null) _floats.style.top = Length.Percent(landscape ? 10f : 15f);
            if (_banner != null)
            {
                _banner.style.top = Length.Percent(landscape ? 1.5f : 10.5f);
                var inner = _banner.childCount > 0 ? _banner[0] : null;
                if (inner != null) inner.style.maxWidth = Length.Percent(landscape ? 44f : 78f);
            }
        }

        /// <summary>Moves a bubble rect (panel px) out of the driving corridor, to the nearer side.</summary>
        private float ClearOfCorridor(float x, float y, float w, float h, float pw, float ph, float margin) =>
            Logic.HudLayout.ClearOfCorridor(x, y, w, h, pw, ph, _hudLandscape, margin);
    }
}
