using UnityEngine;
using UnityEngine.UIElements;

namespace JeepneyRush
{
    /// <summary>Passenger personalities on the HUD: the notable riders on board ("LOLA · NAGMAMADALI 12s").</summary>
    public sealed partial class UIManager
    {
        private Label _hudRiders;

        private void BuildRidersHud(VisualElement scoreCol)
        {
            _hudRiders = UiKit.Text("", 24, new Color(0.85f, 0.95f, 1f), true, 2f);
            _hudRiders.style.unityTextAlign = TextAnchor.MiddleLeft;
            _hudRiders.style.marginTop = 2;
            _hudRiders.style.display = DisplayStyle.None;
            scoreCol.Add(_hudRiders);
        }

        public void SetRiders(string text)
        {
            if (_hudRiders == null) return;
            if (_hudRiders.text != text) _hudRiders.text = text ?? "";
            _hudRiders.style.display = string.IsNullOrEmpty(text) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        /// <summary>Results line about the riders (tips, fare dodgers), or null if nothing happened.</summary>
        private static string RidersSummary(RunResult r)
        {
            int dodgers = r.DodgersCaught + r.DodgersEscaped;
            if (r.Tips <= 0 && dodgers <= 0) return null;
            string s = r.Tips > 0 ? $"Tips: +{r.Tips} coins" : "";
            if (dodgers > 0) s += (s.Length > 0 ? " · " : "") + $"fare dodgers caught {r.DodgersCaught}/{dodgers}";
            return s;
        }
    }
}
