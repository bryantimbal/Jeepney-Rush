using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace JeepneyRush
{
    /// <summary>
    /// Landscape layouts (portrait stays exactly as designed):
    /// • main menu: logo on the left, PLAY and the menu buttons on the right
    /// • cards (settings, garage, pause, ...): shrink only as much as needed to fit the height
    /// • garage: the card on the right, the jeepney turning on the left
    /// </summary>
    public partial class UIManager
    {
        private bool _landscapeUi;
        private VisualElement _menuLogo, _menuSpacer, _menuButtons, _menuRow, _garageOverlay;
        private readonly List<VisualElement> _fitWraps = new List<VisualElement>();

        /// <summary>Called when the screen turns (and once at start).</summary>
        private void ApplyOrientationLayouts(bool landscape)
        {
            if (landscape == _landscapeUi) { RefitCards(); return; } // (the UI is built for portrait)
            _landscapeUi = landscape;
            ApplyMenuLayout(landscape);
            ApplyGarageLayout(landscape);
            WrapCards(landscape);
        }

        private void ApplyMenuLayout(bool landscape)
        {
            if (_menu == null || _menuLogo == null || _menuButtons == null) return;
            if (_menuRow == null)
            {
                _menuRow = UiKit.Box("menuRow");
                _menuRow.style.flexDirection = FlexDirection.Row;
                _menuRow.style.alignItems = Align.Center;
                _menuRow.style.justifyContent = Justify.Center;
                _menuRow.style.flexGrow = 1;
                _menuRow.style.alignSelf = Align.Stretch;
            }
            if (landscape)
            {
                if (_menuRow.parent == null) _menu.Insert(_menu.IndexOf(_menuLogo), _menuRow);
                _menuRow.Add(_menuLogo);
                _menuRow.Add(_menuButtons);
                _menuLogo.style.width = 620;
                _menuLogo.style.height = 315;
                _menuLogo.style.marginRight = 70;
                _menuButtons.style.marginBottom = 0;
                if (_menuSpacer != null) _menuSpacer.style.display = DisplayStyle.None;
            }
            else
            {
                if (_menuRow.parent != null)
                {
                    int i = _menu.IndexOf(_menuRow);
                    _menuRow.RemoveFromHierarchy();
                    _menu.Insert(i, _menuLogo);
                }
                _menuLogo.style.width = 780;
                _menuLogo.style.height = 400;
                _menuLogo.style.marginRight = 0;
                _menuButtons.style.marginBottom = 24;
                if (_menuSpacer != null)
                {
                    _menuSpacer.style.display = DisplayStyle.Flex;
                    _menu.Insert(_menu.IndexOf(_menuSpacer) + 1, _menuButtons);
                }
            }
        }

        private void ApplyGarageLayout(bool landscape)
        {
            if (_garageOverlay == null) return;
            var s = _garageOverlay.style;
            if (landscape)
            {
                s.alignItems = Align.FlexEnd;            // card on the right, the jeepney turns on the left
                s.justifyContent = Justify.Center;
                s.paddingBottom = 0;
                s.paddingRight = 50;
            }
            else
            {
                s.alignItems = Align.Center;
                s.justifyContent = Justify.FlexEnd;
                s.paddingBottom = 40;
                s.paddingRight = 0;
            }
        }

        // ---- cards: in landscape each card sits in a wrapper that is scaled down only if the card is taller than the screen
        private void WrapCards(bool landscape)
        {
            if (!landscape)
            {
                foreach (var w in _fitWraps)
                {
                    if (w.childCount == 0 || w.parent == null) continue;
                    var card = w[0];
                    var parent = w.parent;
                    int i = parent.IndexOf(w);
                    w.RemoveFromHierarchy();
                    parent.Insert(i, card);
                    card.style.maxHeight = Length.Percent(94);
                }
                _fitWraps.Clear();
                return;
            }
            var cards = new List<VisualElement>();
            _root.Query<VisualElement>("card").ForEach(cards.Add);
            foreach (var card in cards)
            {
                var parent = card.parent;
                if (parent == null || parent.name == "cardFit") continue;
                var w = new VisualElement { name = "cardFit" };
                w.pickingMode = PickingMode.Ignore;
                w.style.alignItems = Align.Center;
                w.style.flexShrink = 0;
                int i = parent.IndexOf(card);
                parent.Insert(i, w);
                w.Add(card);
                card.style.maxHeight = StyleKeyword.None;
                w.RegisterCallback<GeometryChangedEvent>(_ => FitWrap(w));
                _fitWraps.Add(w);
            }
        }

        private void RefitCards() { foreach (var w in _fitWraps) FitWrap(w); }

        private static void FitWrap(VisualElement w)
        {
            var area = w.parent;
            if (area == null) return;
            float avail = area.resolvedStyle.height - area.resolvedStyle.paddingTop - area.resolvedStyle.paddingBottom;
            float h = w.resolvedStyle.height;
            if (float.IsNaN(avail) || float.IsNaN(h)) return;
            float s = Logic.ScreenLayout.FitScale(h, avail * 0.97f);
            w.style.scale = new Scale(new Vector3(s, s, 1));
        }
    }
}
