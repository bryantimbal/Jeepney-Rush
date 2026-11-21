using JeepneyRush.Logic;
using UnityEngine;
using UnityEngine.UIElements;

namespace JeepneyRush
{
    /// <summary>Garage: browse/buy/select the 11 jeepneys (live 3D preview) and buy upgrades.</summary>
    public sealed partial class UIManager
    {
        private int _garageIndex;
        private bool _garageUpgrades;
        private Label _garageName, _garageDesc, _garageStatus, _garageCoins;
        private VisualElement _garageStats, _garageJeepPage, _garageUpgradePage;
        private Button _garageAction, _tabJeeps, _tabUpgrades;

        private void BuildGarage()
        {
            _garage = MakeScreen("garage", false);
            var overlay = UiKit.Overlay(new Color(0, 0, 0, 0));
            overlay.style.justifyContent = Justify.FlexEnd;
            overlay.style.paddingBottom = 40;
            _garage.Add(overlay);
            var card = UiKit.Card(1000);
            card.style.paddingTop = 30;
            overlay.Add(card);

            var head = UiKit.Box();
            head.style.flexDirection = FlexDirection.Row;
            head.style.justifyContent = Justify.SpaceBetween;
            head.style.alignItems = Align.Center;
            head.Add(UiKit.Text("GARAGE", 66, UiKit.Red, true, 2.5f));
            _garageCoins = UiKit.Text("0", 42, Color.white);
            head.Add(UiKit.Pill(UiKit.Icon("ui_coin", 52), _garageCoins));
            card.Add(head);

            var tabs = UiKit.Box();
            tabs.style.flexDirection = FlexDirection.Row;
            tabs.style.marginTop = 8;
            _tabJeeps = UiKit.Button("JEEPNEYS", () => { _garageUpgrades = false; RefreshGarage(); }, UiKit.Yellow, UiKit.Ink, 36, 96);
            _tabJeeps.style.flexGrow = 1;
            _tabJeeps.style.marginRight = 10;
            _tabUpgrades = UiKit.Button("UPGRADES", () => { _garageUpgrades = true; RefreshGarage(); }, Color.white, UiKit.Ink, 36, 96);
            _tabUpgrades.style.flexGrow = 1;
            tabs.Add(_tabJeeps);
            tabs.Add(_tabUpgrades);
            card.Add(tabs);

            // ---- jeepney page
            _garageJeepPage = UiKit.Box();
            var nav = UiKit.Box();
            nav.style.flexDirection = FlexDirection.Row;
            nav.style.alignItems = Align.Center;
            nav.style.justifyContent = Justify.SpaceBetween;
            nav.Add(UiKit.IconButton("ui_arrow_left", () => CycleGarage(-1), 110, Color.white));
            _garageName = UiKit.Text("", 50, UiKit.Ink);
            _garageName.style.flexGrow = 1;
            _garageName.style.flexShrink = 1;
            nav.Add(_garageName);
            nav.Add(UiKit.IconButton("ui_arrow_right", () => CycleGarage(1), 110, Color.white));
            _garageJeepPage.Add(nav);
            _garageDesc = UiKit.Text("", 28, UiKit.Grey, false);
            _garageDesc.style.marginTop = 6;
            _garageJeepPage.Add(_garageDesc);
            _garageStats = UiKit.Box();
            _garageStats.style.marginTop = 10;
            _garageJeepPage.Add(_garageStats);
            _garageStatus = UiKit.Text("", 32, UiKit.Green, false);
            _garageStatus.style.marginTop = 6;
            _garageJeepPage.Add(_garageStatus);
            _garageAction = UiKit.Button("SELECT", GarageAction, UiKit.Yellow, UiKit.Ink, 52, 130);
            _garageJeepPage.Add(_garageAction);
            card.Add(_garageJeepPage);

            // ---- upgrade page
            _garageUpgradePage = UiKit.Box();
            card.Add(_garageUpgradePage);

            card.Add(UiKit.Button("BACK", () =>
            {
                CloseModal();
                Services.Game.Menu?.SetGarageMode(false);
                Services.Game.Menu?.ShowLivery(Services.Save.Data.selectedJeepney);
                ShowMainMenu();
            }, Color.white, UiKit.Ink, 44, 110));
        }

        private void OpenGarage()
        {
            _garageIndex = 0;
            for (int i = 0; i < JeepneyCatalog.All.Length; i++) if (JeepneyCatalog.All[i].Id == Services.Save.Data.selectedJeepney) _garageIndex = i;
            _garageUpgrades = false;
            Show(_menu, false);
            Services.Game.Menu?.SetGarageMode(true);
            OpenModal(_garage);
            RefreshGarage();
        }

        private void CycleGarage(int dir)
        {
            int n = JeepneyCatalog.All.Length;
            _garageIndex = (_garageIndex + dir + n) % n;
            RefreshGarage();
        }

        private void RefreshGarage()
        {
            var d = Services.Save.Data;
            _garageCoins.text = d.coins.ToString("N0");
            _tabJeeps.style.backgroundColor = _garageUpgrades ? Color.white : UiKit.Yellow;
            _tabUpgrades.style.backgroundColor = _garageUpgrades ? UiKit.Yellow : Color.white;
            Show(_garageJeepPage, !_garageUpgrades);
            Show(_garageUpgradePage, _garageUpgrades);
            if (_garageUpgrades) { RefreshUpgrades(); return; }

            var j = JeepneyCatalog.All[_garageIndex];
            _garageName.text = $"{j.DisplayName.ToUpperInvariant()}  ({_garageIndex + 1}/{JeepneyCatalog.All.Length})";
            _garageDesc.text = j.Description;
            _garageStats.Clear();
            void Stat(string n, int value, string extra)
            {
                var row = UiKit.Box();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.style.marginBottom = 6;
                var l = UiKit.Text(n, 30, UiKit.Ink, false);
                l.style.width = 230;
                l.style.unityTextAlign = TextAnchor.MiddleLeft;
                row.Add(l);
                for (int i = 0; i < 5; i++)
                {
                    var pip = UiKit.Box();
                    pip.style.width = 50;
                    pip.style.height = 26;
                    pip.style.marginRight = 6;
                    pip.style.backgroundColor = i < value ? UiKit.Yellow : UiKit.Hex("#E2DBC6");
                    UiKit.Border(pip.style, 3, UiKit.Ink, 9);
                    row.Add(pip);
                }
                var e = UiKit.Text(extra, 26, UiKit.Grey, false);
                e.style.marginLeft = 8;
                row.Add(e);
                _garageStats.Add(row);
            }
            Stat("Seats", Mathf.Clamp(1 + j.CapacityBonus, 1, 5), j.CapacityBonus > 0 ? $"+{j.CapacityBonus}" : "");
            Stat("Health", Mathf.Clamp(2 + j.HealthBonus * 2, 1, 5), j.HealthBonus > 0 ? $"+{j.HealthBonus}" : "");
            Stat("Top speed", Mathf.Clamp(2 + Mathf.RoundToInt(j.SpeedBonus * 1.5f), 1, 5), j.SpeedBonus > 0 ? $"+{j.SpeedBonus:0.#} m/s" : "");
            Stat("Handling", Mathf.Clamp(3 + Mathf.RoundToInt(j.HandlingBonus * 100), 1, 5), "");
            bool owned = d.Owns(j.Id), selected = d.selectedJeepney == j.Id;
            _garageStatus.text = selected ? "DRIVING THIS ONE" : owned ? "OWNED" : $"PRICE: {j.Price:N0} COINS";
            _garageStatus.style.color = selected || owned ? UiKit.Green : UiKit.Ink;
            if (selected) { _garageAction.text = "SELECTED"; _garageAction.SetEnabled(false); }
            else if (owned) { _garageAction.text = "SELECT"; _garageAction.SetEnabled(true); }
            else { _garageAction.text = $"BUY · {j.Price:N0}"; _garageAction.SetEnabled(d.coins >= j.Price); }
            _garageAction.style.opacity = _garageAction.enabledSelf ? 1f : 0.55f;
            Services.Game.Menu?.ShowLivery(j.Id);
        }

        private void GarageAction()
        {
            var d = Services.Save.Data;
            var j = JeepneyCatalog.All[_garageIndex];
            if (!d.Owns(j.Id))
            {
                if (!Progress.BuyJeepney(d, j.Id)) return;
                Services.Audio.Play(Logic.Audio.Sfx.Purchase);
                Achievements.Evaluate(d);
            }
            d.selectedJeepney = j.Id;
            Services.Save.Save();
            Services.Audio.Play(Logic.Audio.Sfx.Horn);
            RefreshGarage();
        }

        private void RefreshUpgrades()
        {
            var d = Services.Save.Data;
            _garageUpgradePage.Clear();
            foreach (var k in Upgrades.All)
            {
                int lv = d.UpgradeLevel(k);
                int cost = Upgrades.Cost(k, lv);
                var row = UiKit.Box();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.style.backgroundColor = Color.white;
                UiKit.Border(row.style, 4, UiKit.Hex("#E2DBC6"), 24);
                UiKit.Pad(row.style, 10, 16);
                row.style.marginTop = 10;
                var col = UiKit.Box();
                col.style.flexGrow = 1;
                var name = UiKit.Text(Upgrades.Name(k), 36, UiKit.Ink);
                name.style.unityTextAlign = TextAnchor.MiddleLeft;
                col.Add(name);
                var pips = UiKit.Box();
                pips.style.flexDirection = FlexDirection.Row;
                pips.style.marginTop = 4;
                for (int i = 0; i < Upgrades.MaxLevel; i++)
                {
                    var pip = UiKit.Box();
                    pip.style.width = 44;
                    pip.style.height = 18;
                    pip.style.marginRight = 6;
                    pip.style.backgroundColor = i < lv ? UiKit.Green : UiKit.Hex("#E2DBC6");
                    UiKit.Border(pip.style, 3, UiKit.Ink, 7);
                    pips.Add(pip);
                }
                col.Add(pips);
                var eff = UiKit.Text(lv > 0 ? Upgrades.Effect(k, lv) : "Not upgraded", 24, UiKit.Grey, false);
                eff.style.unityTextAlign = TextAnchor.MiddleLeft;
                eff.style.marginTop = 4;
                col.Add(eff);
                row.Add(col);
                var kind = k;
                var buy = UiKit.Button(cost < 0 ? "MAX" : $"{cost:N0}", () =>
                {
                    if (!Progress.BuyUpgrade(Services.Save.Data, kind)) return;
                    Achievements.Evaluate(Services.Save.Data);
                    Services.Save.Save();
                    Services.Audio.Play(Logic.Audio.Sfx.Purchase);
                    RefreshGarage();
                }, cost < 0 ? UiKit.Hex("#B9B2C6") : UiKit.Yellow, UiKit.Ink, 36, 96);
                buy.style.width = 220;
                buy.style.marginTop = buy.style.marginBottom = 0;
                buy.SetEnabled(cost >= 0 && d.coins >= cost);
                buy.style.opacity = buy.enabledSelf ? 1f : 0.55f;
                if (cost >= 0) buy.Add(UiKit.Icon("ui_coin", 34));
                row.Add(buy);
                _garageUpgradePage.Add(row);
            }
            var note = UiKit.Text("Upgrades apply to every jeepney you own.", 26, UiKit.Grey, false);
            note.style.marginTop = 8;
            _garageUpgradePage.Add(note);
        }
    }
}
