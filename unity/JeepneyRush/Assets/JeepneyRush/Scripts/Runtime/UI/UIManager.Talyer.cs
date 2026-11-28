using JeepneyRush.Logic;
using UnityEngine;
using UnityEngine.UIElements;

namespace JeepneyRush
{
    /// <summary>
    /// Garage ▸ TALYER: customize the jeepney on the turntable – paint, windshield banner, rear
    /// sticker, sign board, horn, mudflaps, hood ornament, lights. Items are bought once with
    /// coins (or earned with a career license) and work on every jeepney; each jeepney keeps its
    /// own setup. Cosmetic only: stats and the stat class never change.
    /// </summary>
    public sealed partial class UIManager
    {
        private VisualElement _talyerPage, _talyerList;
        private Label _talyerHint;
        private readonly Button[] _talyerSlotBtns = new Button[Customization.Slots.Length];
        private CustomSlot _talyerSlot = CustomSlot.Paint;
        private Button _tabTalyer;
        private bool _garageTalyer;

        private void BuildTalyerPage(VisualElement card)
        {
            _talyerPage = UiKit.Box();
            for (int row = 0; row < 2; row++)
            {
                var r = UiKit.Box();
                r.style.flexDirection = FlexDirection.Row;
                r.style.marginTop = 6;
                for (int k = 0; k < 4; k++)
                {
                    int i = row * 4 + k;
                    var slot = Customization.Slots[i];
                    var b = UiKit.Button(Customization.SlotName(slot), () => { _talyerSlot = slot; RefreshTalyer(); }, Color.white, UiKit.Ink, 24, 70);
                    b.style.flexGrow = 1;
                    b.style.flexBasis = 0;
                    b.style.marginRight = k < 3 ? 6 : 0;
                    b.style.marginTop = b.style.marginBottom = 0;
                    _talyerSlotBtns[i] = b;
                    r.Add(b);
                }
                _talyerPage.Add(r);
            }
            _talyerHint = UiKit.Text("", 24, UiKit.Grey, false);
            _talyerHint.style.whiteSpace = WhiteSpace.Normal;
            _talyerHint.style.marginTop = 6;
            _talyerPage.Add(_talyerHint);
            var scroll = UiKit.Scroll(430);
            _talyerList = UiKit.Stack();
            scroll.Add(_talyerList);
            _talyerPage.Add(scroll);
            card.Add(_talyerPage);
        }

        private void RefreshTalyer()
        {
            var d = Services.Save.Data;
            var j = JeepneyCatalog.All[_garageIndex];
            for (int i = 0; i < _talyerSlotBtns.Length; i++)
            {
                bool on = Customization.Slots[i] == _talyerSlot;
                _talyerSlotBtns[i].style.backgroundColor = on ? UiKit.Hex("#F77F00") : Color.white;
                _talyerSlotBtns[i].style.color = on ? Color.white : UiKit.Ink;
            }
            _talyerList.Clear();
            if (!d.Owns(j.Id))
            {
                _talyerHint.text = $"Buy the {j.DisplayName} first, then fix it up here in the talyer.";
                return;
            }
            bool modern = j.Style == JeepneyStyle.Modern;
            _talyerHint.text = _talyerSlot == CustomSlot.Ornament && modern
                ? "Modern jeepneys have no long hood – ornaments show on classic jeepneys only."
                : $"{j.DisplayName}: bought items work on every jeepney (switch jeepney in the JEEPNEYS tab). Cosmetic lang – walang epekto sa stats.";
            var look = Customization.LookFor(d, j.Id);
            var equipped = Customization.Equipped(d, look, _talyerSlot);
            foreach (var item in Customization.ForSlot(_talyerSlot)) _talyerList.Add(TalyerRow(j, item, equipped == item, d));
        }

        private VisualElement TalyerRow(JeepneyLivery j, CosmeticItem item, bool equipped, SaveData d)
        {
            bool owned = Customization.Owns(d, item);
            var row = UiKit.Box();
            row.pickingMode = PickingMode.Position;
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.backgroundColor = equipped ? UiKit.Hex("#FFF3C4") : Color.white;
            UiKit.Border(row.style, 4, equipped ? UiKit.Yellow : UiKit.Hex("#E2DBC6"), 18);
            UiKit.Pad(row.style, 8, 14);
            row.style.marginBottom = 8;
            row.style.flexShrink = 0;
            // colour chip for paint, light colour for lights
            string chip = item.Body ?? item.LightColor ?? item.MudflapColor;
            if (chip != null)
            {
                var sw = UiKit.Box();
                sw.style.width = sw.style.height = 46;
                sw.style.marginRight = 12;
                sw.style.backgroundColor = UiKit.Hex(chip);
                UiKit.Border(sw.style, 3, UiKit.Ink, 10);
                row.Add(sw);
            }
            var name = UiKit.Text(item.Name, 30, UiKit.Ink, false);
            name.style.flexGrow = 1;
            name.style.flexShrink = 1;
            name.style.unityTextAlign = TextAnchor.MiddleLeft;
            row.Add(name);
            string status; Color col;
            if (equipped) { status = "SUOT NA"; col = UiKit.Green; }
            else if (owned) { status = "GAMITIN"; col = UiKit.Blue; }
            else if (item.Earned) { status = item.IsEvent ? (item.Id == PasadaPass.PremiumPaint || item.Id == PasadaPass.PremiumBanner ? "PASADA PASS reward" : "PASKONG PASADA reward") : Career.Licenses[item.RankNeeded].Name; col = UiKit.Grey; }
            else { status = $"{item.Price:N0}"; col = d.coins >= item.Price ? UiKit.Ink : UiKit.Red; }
            var st = UiKit.Text(status, 26, col, true);
            st.style.flexShrink = 0;
            st.style.marginLeft = 10;
            if (!owned && !item.Earned)
            {
                var price = UiKit.Box();
                price.style.flexDirection = FlexDirection.Row;
                price.style.alignItems = Align.Center;
                price.Add(UiKit.Icon("ui_coin", 34));
                price.Add(st);
                row.Add(price);
            }
            else row.Add(st);
            row.RegisterCallback<ClickEvent>(_ => TalyerTap(j, item));
            return row;
        }

        private void TalyerTap(JeepneyLivery j, CosmeticItem item)
        {
            var d = Services.Save.Data;
            Services.Audio.Click();
            if (item.Slot == CustomSlot.Horn) Services.Audio.Play(Customization.HornSound(item.Horn)); // hear it first
            if (Customization.Owns(d, item))
            {
                Customization.Equip(d, j.Id, item);
                Services.Save.Save();
                Services.Game.Menu?.ShowLivery(j.Id);
                RefreshTalyer();
                return;
            }
            // not owned: preview it on the turntable, then buy
            var preview = Customization.LookFor(d, j.Id);
            var tryLook = new JeepneyLook { jeepney = j.Id, paint = preview.paint, windshield = preview.windshield, rear = preview.rear, sign = preview.sign, horn = preview.horn, mudflap = preview.mudflap, ornament = preview.ornament, lights = preview.lights };
            tryLook.Set(item.Slot, item.Id);
            Services.Game.Menu?.ShowLivery(j.Id, tryLook);
            if (item.Earned)
            {
                Float(item.IsEvent ? (item.Id == PasadaPass.PremiumPaint || item.Id == PasadaPass.PremiumBanner ? "Premium PASADA PASS reward (MODES ▸ PASADA PASS)" : "Christmas event reward – collect parols in PASKONG PASADA!") : $"Kinikita ito sa career: {Career.Licenses[item.RankNeeded].Name}", FloatKind.Info);
                return;
            }
            if (d.coins < item.Price)
            {
                Float($"Kulang ang coins: {item.Price:N0} ang kailangan", FloatKind.Bad);
                return;
            }
            Confirm("TALYER", $"Bilhin ang {item.Name} for {item.Price:N0} coins? Pwede sa lahat ng jeepney mo.", () =>
            {
                void Bought()
                {
                    Customization.Equip(Services.Save.Data, j.Id, item);
                    Services.Save.Save();
                    Services.Audio.Play(Logic.Audio.Sfx.Purchase);
                    Float("BAGONG PORMA!", FloatKind.Good);
                    Services.Game.Menu?.ShowLivery(j.Id);
                    _garageCoins.text = Services.Save.Data.coins.ToString("N0");
                    RefreshTalyer();
                }
                if (UseServer())
                {
                    Services.Online.BuyCosmetic(item.Id, (ok, msg) => { if (ok || Customization.Owns(Services.Save.Data, item)) Bought(); else { Toast(msg); Services.Game.Menu?.ShowLivery(j.Id); RefreshTalyer(); } });
                }
                else if (Customization.Buy(d, item)) Bought();
            });
        }
    }
}
