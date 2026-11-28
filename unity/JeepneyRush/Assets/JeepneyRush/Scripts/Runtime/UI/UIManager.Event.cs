using JeepneyRush.Logic;
using UnityEngine;
using UnityEngine.UIElements;

namespace JeepneyRush
{
    /// <summary>
    /// "Paskong Pasada" Christmas event: banner on the main menu (December – 6 January) and the event
    /// page with the parol reward track and the Simbang Gabi tracker.
    /// </summary>
    public partial class UIManager
    {
        private VisualElement _event, _eventBody, _eventBanner;
        private Label _eventBannerInfo;
        private VisualElement _eventBannerFill;
        private float _eventAskedAt = -1f;

        private static readonly Color Pasko = new Color32(0xC8, 0x10, 0x2E, 0xFF);

        private VisualElement BuildEventBanner()
        {
            var b = _eventBanner = UiKit.Box();
            b.pickingMode = PickingMode.Position;
            b.AddToClassList(InteractiveClass);
            b.style.width = 900;
            b.style.backgroundColor = Pasko;
            UiKit.Border(b.style, 5, UiKit.Ink, 28);
            UiKit.Pad(b.style, 14, 22);
            b.style.marginTop = 6;
            var title = UiKit.Text("PASKONG PASADA", 44, UiKit.Yellow, true, 1.5f);
            title.style.unityTextAlign = TextAnchor.MiddleLeft;
            b.Add(title);
            _eventBannerInfo = UiKit.Text("", 26, Color.white, false);
            _eventBannerInfo.style.unityTextAlign = TextAnchor.MiddleLeft;
            b.Add(_eventBannerInfo);
            var bar = UiKit.Box();
            bar.style.height = 14;
            bar.style.marginTop = 6;
            bar.style.backgroundColor = new Color(0, 0, 0, 0.3f);
            UiKit.Border(bar.style, 0, Color.clear, 7);
            bar.style.overflow = Overflow.Hidden;
            _eventBannerFill = UiKit.Box();
            _eventBannerFill.style.height = Length.Percent(100);
            _eventBannerFill.style.backgroundColor = UiKit.Yellow;
            bar.Add(_eventBannerFill);
            b.Add(bar);
            b.RegisterCallback<ClickEvent>(_ => { Services.Audio.Click(); OpenEvent(); });
            b.style.display = DisplayStyle.None;
            return b;
        }

        private void RefreshEventBanner()
        {
            if (_eventBanner == null) return;
            if (!EventProgress.Active && !(EventProgress.Online && Services.Online.SignedIn)) { _eventBanner.style.display = DisplayStyle.None; return; }
            if (EventProgress.Online && Time.realtimeSinceStartup - _eventAskedAt < 20f && _eventAskedAt > 0) return; // don't ask the server on every redraw
            _eventAskedAt = Time.realtimeSinceStartup;
            EventProgress.Status(i =>
            {
                bool on = i != null && i.active;
                _eventBanner.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
                if (!on) return;
                int top = ChristmasEvent.Rewards[ChristmasEvent.Rewards.Length - 1].Need;
                _eventBannerInfo.text = $"{i.tokens} / {top} PAROLS · {i.days_left} DAYS LEFT" + (i.night > 0 ? $" · SIMBANG GABI {ChristmasEvent.CountNights(i.nights)}/9" : "");
                _eventBannerFill.style.width = Length.Percent(Mathf.Clamp01(i.tokens / (float)top) * 100f);
            });
        }

        private void BuildEvent()
        {
            _event = ModalShell("event", "PASKONG PASADA", out var card);
            var scroll = UiKit.Scroll(1200);
            scroll.style.height = 1200;
            _eventBody = UiKit.Stack();
            scroll.Add(_eventBody);
            card.Add(scroll);
            card.Add(UiKit.Button("BACK", CloseModal, Color.white, UiKit.Ink, 46, 120));
            if (Services.Online != null) Services.Online.EventChanged += RefreshEventBanner;
        }

        private void OpenEvent()
        {
            if (!_modals.Contains(_event)) OpenModal(_event);
            _eventBody.Clear();
            _eventBody.Add(Note("Loading…"));
            EventProgress.Status(DrawEvent);
        }

        private void DrawEvent(EventInfo i)
        {
            _eventBody.Clear();
            if (i == null) { _eventBody.Add(Note("Sign in (or play as guest) under ACCOUNT to join the event.", 32, UiKit.Ink)); return; }
            if (!i.active) { _eventBody.Add(Note("The Christmas event runs from 1 December to 6 January. See you then!", 32, UiKit.Ink)); return; }
            _eventBody.Add(Note($"Maligayang Pasko! {i.days_left} days left. Collect parols on the road and pick up carolers. Up to {ChristmasEvent.DailyCap} parols a day.", 28, UiKit.Ink));
            _eventBody.Add(UiKit.Text($"{i.tokens} PAROLS", 52, Pasko));
            for (int k = 0; k < ChristmasEvent.Rewards.Length; k++)
            {
                var r = ChristmasEvent.Rewards[k];
                bool claimed = (i.claimed & (1 << k)) != 0, ready = !claimed && i.tokens >= r.Need;
                int idx = k;
                _eventBody.Add(EventRow(r.Name + (r.Kind == EventRewardKind.Jeepney ? "  (event-only!)" : ""), claimed ? "NAKUHA NA" : $"{r.Need} parols", claimed, ready, () => ClaimEvent(idx)));
            }
            // Simbang Gabi tracker
            _eventBody.Add(Heading($"SIMBANG GABI · {ChristmasEvent.CountNights(i.nights)} / 9"));
            _eventBody.Add(Note($"16–24 December: drive a NIGHT SHIFT of {ChristmasEvent.SimbangGabiDistance / 1000f:0.#} km each night. All nine win the GOLDEN PAROL roof sign.", 26));
            var row = UiKit.Box();
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.SpaceBetween;
            row.style.marginBottom = 10;
            for (int n = 1; n <= ChristmasEvent.Nights; n++)
            {
                bool lit = (i.nights & (1 << (n - 1))) != 0, tonight = n == i.night;
                var cell = UiKit.Text(lit ? "OK" : n.ToString(), 30, lit ? UiKit.Ink : tonight ? Color.white : UiKit.Grey);
                cell.style.width = 80;
                cell.style.height = 70;
                cell.style.backgroundColor = lit ? UiKit.Yellow : tonight ? Pasko : Color.white;
                UiKit.Border(cell.style, tonight ? 5 : 3, UiKit.Ink, 14);
                row.Add(cell);
            }
            _eventBody.Add(row);
            bool allNights = (i.nights & ChristmasEvent.AllNights) == ChristmasEvent.AllNights;
            _eventBody.Add(EventRow(ChristmasEvent.GoldenParol.Name, i.golden ? "NAKUHA NA" : "all 9 nights", i.golden, allNights && !i.golden, () => ClaimEvent(ChristmasEvent.Rewards.Length)));
            if (i.night > 0 && (i.nights & (1 << (i.night - 1))) == 0)
                _eventBody.Add(UiKit.Button($"GABI {i.night}: DRIVE NIGHT SHIFT NOW", () => { _modals.Clear(); HideAll(); Services.Game.PlayMode(GameMode.NightShift); }, Pasko, Color.white, 36, 116));
        }

        private VisualElement EventRow(string name, string need, bool claimed, bool ready, System.Action claim)
        {
            var row = UiKit.Box();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.backgroundColor = claimed ? UiKit.Hex("#D9F7E3") : Color.white;
            UiKit.Border(row.style, 3, ready ? Pasko : UiKit.Hex("#E2DBC6"), 18);
            UiKit.Pad(row.style, 8, 14);
            row.style.marginBottom = 8;
            row.style.minHeight = 100;
            var col = UiKit.Box();
            col.style.flexGrow = 1;
            col.style.flexShrink = 1;
            var n = UiKit.Text(name, 30, UiKit.Ink);
            n.style.unityTextAlign = TextAnchor.MiddleLeft;
            n.style.whiteSpace = WhiteSpace.Normal;
            col.Add(n);
            var sub = UiKit.Text(need, 22, UiKit.Grey, false);
            sub.style.unityTextAlign = TextAnchor.MiddleLeft;
            col.Add(sub);
            row.Add(col);
            if (ready)
            {
                var b = UiKit.Button("KUNIN!", claim, UiKit.Yellow, UiKit.Ink, 30, 84);
                b.style.width = 200;
                row.Add(b);
            }
            return row;
        }

        private void ClaimEvent(int index)
        {
            EventProgress.Claim(index, (ok, msg) =>
            {
                if (ok) { Services.Audio.Play(Logic.Audio.Sfx.Purchase); Float("MALIGAYANG PASKO! REWARD CLAIMED", FloatKind.Big); }
                else Toast(msg);
                _eventAskedAt = -1f;
                RefreshMenuTop();
                OpenEvent();
            });
        }
    }
}
