using JeepneyRush.Logic;
using UnityEngine;
using UnityEngine.UIElements;

namespace JeepneyRush
{
    /// <summary>
    /// Operator mode ("MY FLEET"): hire drivers for your jeepneys; they earn boundary while you are
    /// away (up to 8 hours). Opened from the career screen once you hold the OPERATOR license.
    /// </summary>
    public partial class UIManager
    {
        private VisualElement _fleet, _fleetBody;
        private int _fleetPicking = -1;

        private void BuildFleet()
        {
            _fleet = ModalShell("fleet", "MY FLEET", out var card);
            var scroll = UiKit.Scroll(1250);
            scroll.style.height = 1250;
            _fleetBody = UiKit.Stack();
            scroll.Add(_fleetBody);
            card.Add(scroll);
            card.Add(UiKit.Button("BACK", CloseModal, Color.white, UiKit.Ink, 46, 120));
        }

        private void OpenFleet()
        {
            if (!_modals.Contains(_fleet)) OpenModal(_fleet);
            _fleetBody.Clear();
            _fleetBody.Add(Note("Loading…"));
            FleetProgress.Status(DrawFleet);
        }

        private void DrawFleet(FleetInfo f, string err)
        {
            _fleetBody.Clear();
            if (f == null) { _fleetBody.Add(Note(err ?? "Could not load your fleet.", 32, UiKit.Red)); return; }
            if (!f.unlocked)
            {
                _fleetBody.Add(Note($"Get the {Career.Licenses[Operator.MinRank].Name} license in BUHAY TSUPER to start your own fleet. Your drivers then earn boundary for you while you're away!", 32, UiKit.Ink));
                return;
            }
            int total = 0;
            foreach (var s in f.fleet) total += s.pending;
            _fleetBody.Add(Note($"Your drivers earn boundary while you're away (up to {Operator.CapHours} hours, then they wait for you). Better jeepneys earn more.", 26));
            var collect = UiKit.Button(total > 0 ? $"COLLECT BOUNDARY · +{total:N0}" : "NO BOUNDARY YET", () => FleetProgress.Collect((ok, msg, coins) =>
            {
                if (ok) { Services.Audio.Play(Logic.Audio.Sfx.CoinRush); Float($"+{coins:N0} BOUNDARY!", FloatKind.Big); } else Toast(msg);
                RefreshMenuTop(); OpenFleet();
            }), total > 0 ? UiKit.Yellow : Color.white, UiKit.Ink, 40, 116);
            collect.SetEnabled(total > 0);
            _fleetBody.Add(collect);
            for (int slot = 0; slot < f.slots; slot++)
            {
                FleetSlot s = null;
                foreach (var x in f.fleet) if (x.slot == slot) s = x;
                _fleetBody.Add(FleetRow(slot, s));
                if (_fleetPicking == slot) _fleetBody.Add(FleetPicker(slot, f));
            }
            if (f.next_slot_price > 0)
                _fleetBody.Add(UiKit.Button($"BUY ANOTHER SLOT · {f.next_slot_price:N0} COINS", () => FleetProgress.BuySlot((ok, msg, _) => { Toast(ok ? "New slot! Hire a driver." : msg); RefreshMenuTop(); OpenFleet(); }),
                    Color.white, UiKit.Ink, 30, 100));
        }

        private VisualElement FleetRow(int slot, FleetSlot s)
        {
            var row = UiKit.Box();
            row.style.backgroundColor = s != null ? UiKit.Hex("#FFF8E1") : Color.white;
            UiKit.Border(row.style, 3, s != null ? UiKit.Yellow : UiKit.Hex("#E2DBC6"), 18);
            UiKit.Pad(row.style, 10, 14);
            row.style.marginBottom = 10;
            string driver = Operator.DriverFor(slot);
            if (s == null)
            {
                row.Add(UiKit.Text($"SLOT {slot + 1} · {driver} is waiting for a jeepney", 28, UiKit.Ink));
                row.Add(UiKit.Button("ASSIGN A JEEPNEY", () => { _fleetPicking = slot; OpenFleet(); }, UiKit.Yellow, UiKit.Ink, 28, 88));
                return row;
            }
            var j = JeepneyCatalog.Get(s.jeepney);
            row.Add(UiKit.Text($"{driver} · {j.DisplayName.ToUpperInvariant()}", 30, UiKit.Ink));
            row.Add(UiKit.Text($"{s.rate} coins/hour · +{s.pending:N0} waiting ({s.hours:0.#} h" + (s.hours >= Operator.CapHours ? ", FULL" : "") + ")", 24, s.hours >= Operator.CapHours ? UiKit.Red : UiKit.Grey, false));
            var buttons = UiKit.Box();
            buttons.style.flexDirection = FlexDirection.Row;
            var change = UiKit.Button("CHANGE", () => { _fleetPicking = slot; OpenFleet(); }, Color.white, UiKit.Ink, 26, 80);
            var remove = UiKit.Button("REMOVE", () => FleetProgress.Unassign(slot, (ok, msg, paid) => { Toast(ok ? (paid > 0 ? $"+{paid:N0} boundary paid" : "Driver off duty") : msg); RefreshMenuTop(); OpenFleet(); }), Color.white, UiKit.Red, 26, 80);
            change.style.flexGrow = remove.style.flexGrow = 1;
            change.style.marginRight = 8;
            buttons.Add(change);
            buttons.Add(remove);
            row.Add(buttons);
            return row;
        }

        private VisualElement FleetPicker(int slot, FleetInfo f)
        {
            var box = UiKit.Box();
            UiKit.Pad(box.style, 6, 10);
            box.style.marginBottom = 12;
            var d = Services.Save.Data;
            bool any = false;
            foreach (var j in JeepneyCatalog.All)
            {
                if (!d.Owns(j.Id)) continue;
                bool busy = false;
                foreach (var x in f.fleet) if (x.jeepney == j.Id && x.slot != slot) busy = true;
                if (busy) continue;
                any = true;
                string id = j.Id;
                box.Add(UiKit.Button($"{j.DisplayName} · {Operator.RatePerHour(j)}/h", () =>
                {
                    _fleetPicking = -1;
                    FleetProgress.Assign(slot, id, (ok, msg, paid) => { Toast(ok ? $"{Operator.DriverFor(slot)} drives the {JeepneyCatalog.Get(id).DisplayName}!" : msg); RefreshMenuTop(); OpenFleet(); });
                }, UiKit.Hex("#FFF1B8"), UiKit.Ink, 26, 80));
            }
            if (!any) box.Add(Note("All your jeepneys are already on the road. Buy another in the GARAGE!", 26, UiKit.Ink));
            box.Add(UiKit.Button("CANCEL", () => { _fleetPicking = -1; OpenFleet(); }, Color.white, UiKit.Grey, 24, 72));
            return box;
        }
    }
}
