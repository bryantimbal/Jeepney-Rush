using JeepneyRush.Logic;
using UnityEngine;
using UnityEngine.UIElements;

namespace JeepneyRush
{
    /// <summary>Garage ▸ MY DRIVER: the driver's look (shirt, headwear, towel / shades) and hiring the konduktor.</summary>
    public partial class UIManager
    {
        private VisualElement _driverModal, _driverBody;

        private void BuildDriver()
        {
            _driverModal = ModalShell("driver", "MY DRIVER", out var card);
            var scroll = UiKit.Scroll(1250);
            scroll.style.height = 1250;
            _driverBody = UiKit.Stack();
            scroll.Add(_driverBody);
            card.Add(scroll);
            card.Add(UiKit.Button("BACK", CloseModal, Color.white, UiKit.Ink, 46, 120));
        }

        private void OpenDriver()
        {
            if (!_modals.Contains(_driverModal)) OpenModal(_driverModal);
            DrawDriver();
        }

        private void DrawDriver()
        {
            _driverBody.Clear();
            var d = Services.Save.Data;
            _driverBody.Add(Note("Looks are bought once and worn in every jeepney. They never change stats.", 26));
            foreach (var part in new[] { DriverPart.Shirt, DriverPart.Hat, DriverPart.Extra })
            {
                _driverBody.Add(Heading(part == DriverPart.Shirt ? "DAMIT" : part == DriverPart.Hat ? "SOMBRERO" : "EXTRA"));
                var worn = DriverStyle.Worn(d, part);
                foreach (var item in DriverStyle.ForPart(part)) _driverBody.Add(DriverRow(item, worn == item));
            }
            _driverBody.Add(Heading("KONDUKTOR"));
            _driverBody.Add(Note($"Rides on the rear step: fare dodgers can't escape, and he collects every fare (+{DriverStyle.KonduktorFare * 100:0}%).", 26));
            var k = DriverStyle.Get(DriverStyle.KonduktorId);
            if (!DriverStyle.Owns(d, k)) _driverBody.Add(DriverRow(k, false));
            else _driverBody.Add(UiKit.Button(d.konduktorOff ? "KONDUKTOR: OFF DUTY (tap to bring him back)" : "KONDUKTOR: ON DUTY (tap to give him a day off)",
                () => { d.konduktorOff = !d.konduktorOff; Services.Save.Save(); DrawDriver(); }, d.konduktorOff ? Color.white : UiKit.Hex("#D9F7E3"), UiKit.Ink, 28, 96));
        }

        private VisualElement DriverRow(DriverItem item, bool wearing)
        {
            var d = Services.Save.Data;
            bool owned = DriverStyle.Owns(d, item);
            string label = wearing ? "SUOT" : owned ? "ISUOT" : $"BUY · {item.Price:N0}";
            var b = UiKit.Button($"{item.Name}  ·  {label}", () => DriverTap(item), wearing ? UiKit.Yellow : owned ? Color.white : UiKit.Hex("#FFF1B8"), UiKit.Ink, 28, 90);
            return b;
        }

        private void DriverTap(DriverItem item)
        {
            var d = Services.Save.Data;
            if (DriverStyle.Owns(d, item))
            {
                if (item.Part == DriverPart.Shirt) d.driverShirt = item.Id;
                else if (item.Part == DriverPart.Hat) d.driverHat = item.Id;
                else if (item.Part == DriverPart.Extra) d.driverExtra = item.Id;
                Services.Save.Save();
                DrawDriver();
                return;
            }
            Confirm("MY DRIVER", $"Bilhin ang {item.Name} for {item.Price:N0} coins?", () =>
            {
                void Bought()
                {
                    Services.Audio.Play(Logic.Audio.Sfx.Purchase);
                    if (item.Part == DriverPart.Shirt) d.driverShirt = item.Id;
                    else if (item.Part == DriverPart.Hat) d.driverHat = item.Id;
                    else if (item.Part == DriverPart.Extra) d.driverExtra = item.Id;
                    else d.konduktorOff = false;
                    Services.Save.Save();
                    Toast(item.Part == DriverPart.Konduktor ? "May konduktor ka na!" : "Bagay sa'yo!");
                    RefreshMenuTop();
                    DrawDriver();
                }
                if (UseServer()) { Services.Online.BuyCosmetic(item.Id, (ok, msg) => { if (ok) Bought(); else Toast(msg); }); return; }
                if (d.coins < item.Price) { Toast("Kulang ang coins."); return; }
                d.coins -= item.Price;
                if (!d.ownedCosmetics.Contains(item.Id)) d.ownedCosmetics.Add(item.Id);
                Bought();
            });
        }
    }
}
