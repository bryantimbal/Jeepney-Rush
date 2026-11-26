using UnityEngine;
using UnityEngine.UIElements;

namespace JeepneyRush
{
    /// <summary>Passenger personalities on the HUD: the notable riders on board ("LOLA · NAGMAMADALI 12s").</summary>
    public sealed partial class UIManager
    {
        private Label _hudRiders, _hudFares;
        private int _lastFares = -1, _lastBoundary = -2, _lastGas = -1;

        private void BuildRidersHud(VisualElement scoreCol)
        {
            // fare counter: passenger money only (the boundary is paid from this, never from road coins)
            _hudFares = UiKit.Text("PAMASAHE 0", 30, new Color(0.55f, 1f, 0.6f), true, 2f);
            _hudFares.style.unityTextAlign = TextAnchor.MiddleLeft;
            _hudFares.style.marginTop = 2;
            scoreCol.Add(_hudFares);
            _hudRiders = UiKit.Text("", 24, new Color(0.85f, 0.95f, 1f), true, 2f);
            _hudRiders.style.unityTextAlign = TextAnchor.MiddleLeft;
            _hudRiders.style.marginTop = 2;
            _hudRiders.style.display = DisplayStyle.None;
            scoreCol.Add(_hudRiders);
        }

        /// <summary>Fare counter on the HUD; with a boundary (career) it also shows what's left to collect / the take-home so far.</summary>
        public void SetFares(int fares, int gas, int boundary)
        {
            if (_hudFares == null || (fares == _lastFares && boundary == _lastBoundary && gas == _lastGas)) return;
            _lastFares = fares; _lastBoundary = boundary; _lastGas = gas;
            int kita = fares - gas - Mathf.Max(0, boundary);
            string gasPart = gas > 0 ? $" · GAS -{gas:N0}" : "";
            if (boundary >= 0 && fares < boundary) _hudFares.text = $"PAMASAHE {fares:N0}/{boundary:N0}{gasPart}";
            else _hudFares.text = $"PAMASAHE {fares:N0}{gasPart} · KITA {kita:N0}";
            _hudFares.style.color = kita < 0 ? new Color(1f, 0.55f, 0.45f) : new Color(0.55f, 1f, 0.6f);
        }

        public void SetRiders(string text)
        {
            if (_hudRiders == null) return;
            if (_hudRiders.text != text) _hudRiders.text = text ?? "";
            _hudRiders.style.display = string.IsNullOrEmpty(text) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        /// <summary>Results line about the riders (tips, fare dodgers), or null if nothing happened.</summary>
        /// <summary>Results line: fares (passenger money) vs road coins.</summary>
        private static string MoneySummary(RunResult r) =>
            r.Fares <= 0 && r.RoadCoins <= 0 && r.Gas <= 0 ? null
            : $"Pamasahe {r.Fares:N0} − gas {r.Gas:N0} = kita {r.Fares - r.Gas:N0} · road coins {r.RoadCoins:N0}";

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
