using JeepneyRush.Logic;
using UnityEngine;
using UnityEngine.UIElements;

namespace JeepneyRush
{
    /// <summary>Career mode (BUHAY TSUPER): license progress, shift list, pre-shift briefing, HUD line and results.</summary>
    public sealed partial class UIManager
    {
        private VisualElement _career, _careerList, _brief, _briefCard;
        private Label _careerLicense, _careerStars, _careerNext;
        private Label _hudCareer;
        private VisualElement _goCareer;
        private Button _goNext;
        private CareerShift _goNextShift;

        // ============================================================== career screen
        private void BuildCareer()
        {
            _career = ModalShell("career", "BUHAY TSUPER", out var card);
            var head = UiKit.Box();
            head.style.backgroundColor = UiKit.Hex("#FFF3C4");
            UiKit.Border(head.style, 4, UiKit.Yellow, 22);
            UiKit.Pad(head.style, 12, 18);
            head.style.marginBottom = 12;
            var top = UiKit.Box();
            top.style.flexDirection = FlexDirection.Row;
            top.style.alignItems = Align.Center;
            top.Add(UiKit.Icon("ui_m_career", 84));
            var col = UiKit.Box();
            col.style.flexGrow = 1;
            col.style.marginLeft = 14;
            _careerLicense = UiKit.Text("", 40, UiKit.Ink);
            _careerLicense.style.unityTextAlign = TextAnchor.MiddleLeft;
            col.Add(_careerLicense);
            _careerStars = UiKit.Text("", 30, UiKit.Ink, false);
            _careerStars.style.unityTextAlign = TextAnchor.MiddleLeft;
            col.Add(_careerStars);
            top.Add(col);
            head.Add(top);
            _careerNext = UiKit.Text("", 26, UiKit.Grey, false);
            _careerNext.style.unityTextAlign = TextAnchor.MiddleLeft;
            _careerNext.style.whiteSpace = WhiteSpace.Normal;
            _careerNext.style.marginTop = 6;
            head.Add(_careerNext);
            card.Add(head);
            var scroll = UiKit.Scroll(1150);
            _careerList = UiKit.Stack();
            scroll.Add(_careerList);
            card.Add(scroll);
            card.Add(UiKit.Button("BACK", CloseModal, Color.white, UiKit.Ink, 44, 110));

            // pre-shift briefing from Mang Kanor
            _brief = ModalShell("brief", "BIYAHE", out _briefCard);
        }

        private void OpenCareer()
        {
            OpenModal(_career);
            RefreshCareer();
        }

        private void RefreshCareer()
        {
            var d = Services.Save.Data;
            SaveValidation.Sanitize(d);
            int stars = Career.TotalStars(d.careerMasks);
            int rank = Career.RankFor(stars);
            var lic = Career.Licenses[rank];
            _careerLicense.text = lic.Name;
            _careerStars.text = $"{stars} / {Career.MaxStars} stars";
            _careerNext.text = rank + 1 < Career.Licenses.Length
                ? $"Next: {Career.Licenses[rank + 1].Name} at {Career.Licenses[rank + 1].StarsNeeded} stars (+{Career.Licenses[rank + 1].RewardCoins:N0} coins, +{Career.Licenses[rank + 1].RewardGems} gems)"
                : "You are the HARI NG KALSADA. Get every star!";
            _careerList.Clear();
            int chapter = -1;
            for (int i = 0; i < Career.Shifts.Length; i++)
            {
                var s = Career.Shifts[i];
                if (s.Chapter != chapter)
                {
                    chapter = s.Chapter;
                    bool open = rank >= chapter;
                    var h = UiKit.Text(Career.Licenses[chapter].Name + (open ? "" : $"  ·  {Career.Licenses[chapter].StarsNeeded} STARS"), 32, open ? UiKit.Red : UiKit.Grey);
                    h.style.unityTextAlign = TextAnchor.MiddleLeft;
                    h.style.marginTop = 10;
                    h.style.marginBottom = 6;
                    _careerList.Add(h);
                }
                _careerList.Add(ShiftRow(i, d));
            }
        }

        private VisualElement ShiftRow(int index, SaveData d)
        {
            var s = Career.Shifts[index];
            bool unlocked = Career.IsUnlocked(index, d.careerMasks);
            int mask = Career.Mask(d.careerMasks, index);
            var row = UiKit.Box();
            row.pickingMode = PickingMode.Position;
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.backgroundColor = Color.white;
            UiKit.Border(row.style, 4, mask == 7 ? UiKit.Yellow : UiKit.Hex("#E2DBC6"), 22);
            UiKit.Pad(row.style, 8, 12);
            row.style.marginBottom = 10;
            row.style.flexShrink = 0;
            var thumb = UiKit.Icon("ui_route_" + s.Env.ToString().ToLowerInvariant(), 170);
            thumb.style.height = 96;
            UiKit.Border(thumb.style, 3, UiKit.Ink, 14);
            if (!unlocked) thumb.style.opacity = 0.4f;
            row.Add(thumb);
            var col = UiKit.Box();
            col.style.flexGrow = 1;
            col.style.flexShrink = 1;
            col.style.marginLeft = 14;
            var t = UiKit.Text(s.Title.ToUpperInvariant(), 34, unlocked ? UiKit.Ink : UiKit.Grey);
            t.style.unityTextAlign = TextAnchor.MiddleLeft;
            col.Add(t);
            var sub = UiKit.Text(unlocked ? $"{s.From} – {s.To} · {s.Length:N0} m · boundary {s.Boundary}" : "Get a star on the shift before", 24, UiKit.Grey, false);
            sub.style.unityTextAlign = TextAnchor.MiddleLeft;
            col.Add(sub);
            row.Add(col);
            if (unlocked) row.Add(Stars(mask, 40));
            else row.Add(UiKit.Icon("ui_lock", 56));
            row.RegisterCallback<ClickEvent>(_ =>
            {
                if (!unlocked) return;
                Services.Audio.Click();
                OpenBrief(s, mask);
            });
            return row;
        }

        /// <summary>Three stars, earned ones bright.</summary>
        private static VisualElement Stars(int mask, float size)
        {
            var box = UiKit.Box();
            box.style.flexDirection = FlexDirection.Row;
            box.style.flexShrink = 0;
            for (int b = 0; b < 3; b++)
            {
                var st = UiKit.Icon("ui_star", size);
                bool on = (mask & (1 << b)) != 0;
                st.style.opacity = on ? 1f : 0.2f;
                st.style.marginLeft = 2;
                box.Add(st);
            }
            return box;
        }

        private void OpenBrief(CareerShift s, int mask)
        {
            _briefCard.Clear();
            var title = UiKit.Text(s.Title.ToUpperInvariant(), 64, UiKit.Red, true, 2.5f);
            title.style.marginBottom = 6;
            _briefCard.Add(title);
            _briefCard.Add(UiKit.Text($"{Environments.Get(s.Env).Name} · {s.From} – {s.To}", 30, UiKit.Ink, false));
            var quote = UiKit.Text($"“{s.Intro}”\n– Mang Kanor", 32, UiKit.Ink, false);
            quote.style.whiteSpace = WhiteSpace.Normal;
            quote.style.backgroundColor = UiKit.Hex("#FFF3C4");
            UiKit.Border(quote.style, 3, UiKit.Yellow, 18);
            UiKit.Pad(quote.style, 12, 16);
            quote.style.marginTop = 12;
            quote.style.marginBottom = 12;
            _briefCard.Add(quote);
            void Goal(int bit, string text)
            {
                var r = UiKit.Box();
                r.style.flexDirection = FlexDirection.Row;
                r.style.alignItems = Align.Center;
                r.style.marginBottom = 6;
                var st = UiKit.Icon("ui_star", 44);
                st.style.opacity = (mask & bit) != 0 ? 1f : 0.3f;
                r.Add(st);
                var l = UiKit.Text(text, 32, UiKit.Ink, false);
                l.style.marginLeft = 10;
                l.style.unityTextAlign = TextAnchor.MiddleLeft;
                r.Add(l);
                _briefCard.Add(r);
            }
            Goal(Career.StarFinish, $"Reach {s.To} ({s.Length:N0} m)");
            Goal(Career.StarBoundary, $"Bring home the boundary: {s.Boundary} coins");
            Goal(Career.StarGoal, Career.GoalText(s.Goal, s.Target));
            var pay = UiKit.Text($"Every new star: +{Career.StarReward(s.Chapter)} coins. Crash before the terminal and the shift doesn't count.", 24, UiKit.Grey, false);
            pay.style.whiteSpace = WhiteSpace.Normal;
            pay.style.marginTop = 6;
            _briefCard.Add(pay);
            _briefCard.Add(UiKit.Button("ARANGKADA!", () => { _modals.Clear(); HideAll(); Services.Game.PlayShift(s); }, UiKit.Yellow, UiKit.Ink, 60, 150));
            _briefCard.Add(UiKit.Button("BACK", CloseModal, Color.white, UiKit.Ink, 44, 110));
            OpenModal(_brief);
        }

        // ============================================================== HUD
        private void BuildCareerHud(VisualElement scoreCol)
        {
            _hudCareer = UiKit.Text("", 28, UiKit.Yellow, true, 2f);
            _hudCareer.style.unityTextAlign = TextAnchor.MiddleLeft;
            _hudCareer.style.marginTop = 2;
            scoreCol.Add(_hudCareer);
        }

        /// <summary>Career line on the HUD ("CUBAO: 420 m · BOUNDARY 23/40"); empty hides it.</summary>
        public void SetCareer(string text)
        {
            if (_hudCareer == null) return;
            _hudCareer.text = text ?? "";
            _hudCareer.style.display = string.IsNullOrEmpty(text) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        // ============================================================== game over
        private void BuildCareerGameOver(VisualElement card)
        {
            _goCareer = UiKit.Box("career");
            _goCareer.style.marginTop = 6;
            card.Add(_goCareer);
        }

        private void BuildNextShiftButton(VisualElement card)
        {
            _goNext = UiKit.Button("NEXT BIYAHE", () =>
            {
                var next = _goNextShift;
                if (next == null) return;
                Show(_gameOver, false);
                Services.Game.PlayShift(next);
            }, UiKit.Green, Color.white, 52, 136);
            _goNext.style.display = DisplayStyle.None;
            card.Add(_goNext);
        }

        /// <summary>Fills the career part of the results (stars, rewards, promotion) – hidden for free rides.</summary>
        private void ShowCareerResult(ShiftResult c, Label title)
        {
            _goCareer?.Clear();
            _goNextShift = null;
            if (_goNext != null) _goNext.style.display = DisplayStyle.None;
            if (title != null)
            {
                bool done = c != null && c.Finished;
                title.text = c == null ? "GAME OVER" : done ? "BIYAHE COMPLETE!" : "HINDI NAKARATING";
                title.style.color = done ? UiKit.Green : UiKit.Red;
            }
            if (c == null || _goCareer == null) return;
            var s = c.Shift;
            var head = UiKit.Box();
            head.style.flexDirection = FlexDirection.Row;
            head.style.alignItems = Align.Center;
            head.style.justifyContent = Justify.Center;
            head.Add(Stars(c.Mask, 72));
            _goCareer.Add(head);
            var d = Services.Save.Data;
            void Check(bool ok, string text)
            {
                var l = UiKit.Text((ok ? "OK  " : "–  ") + text, 28, ok ? UiKit.Green : UiKit.Grey, false);
                l.style.unityTextAlign = TextAnchor.MiddleLeft;
                _goCareer.Add(l);
            }
            Check(c.Finished, $"Reached {s.To}");
            Check(c.MadeBoundary, $"Boundary {s.Boundary} coins");
            Check(c.GoalMet, Career.GoalText(s.Goal, s.Target));
            if (c.StarCoins > 0)
            {
                var l = UiKit.Text($"New stars: +{c.StarCoins} coins", 32, UiKit.Ink, true);
                l.style.marginTop = 4;
                _goCareer.Add(l);
            }
            if (c.NewRank > 0)
            {
                var lic = Career.Licenses[c.NewRank];
                var promo = UiKit.Text($"NEW LICENSE: {lic.Name}!\n+{c.RankCoins:N0} coins · +{c.RankGems} gems", 36, UiKit.Ink, true);
                promo.style.whiteSpace = WhiteSpace.Normal;
                promo.style.backgroundColor = UiKit.Yellow;
                UiKit.Border(promo.style, 4, UiKit.Ink, 18);
                UiKit.Pad(promo.style, 10, 16);
                promo.style.marginTop = 8;
                _goCareer.Add(promo);
                Services.Audio.Play(Logic.Audio.Sfx.LevelUp);
            }
            // next shift, if this one opened it
            int i = Career.IndexOf(s.Id);
            for (int k = i + 1; k < Career.Shifts.Length; k++)
            {
                if (!Career.IsUnlocked(k, d.careerMasks)) break;
                if (Career.Mask(d.careerMasks, k) == 0) { _goNextShift = Career.Shifts[k]; break; }
            }
            if (_goNextShift != null && _goNext != null && c.Finished)
            {
                _goNext.text = "NEXT: " + _goNextShift.Title.ToUpperInvariant();
                _goNext.style.display = DisplayStyle.Flex;
            }
        }
    }
}
