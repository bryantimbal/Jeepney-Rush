using System;
using System.Globalization;
using JeepneyRush.Logic;
using UnityEngine;
using UnityEngine.UIElements;

namespace JeepneyRush
{
    /// <summary>
    /// BARANGAY VS BARANGAY: create or join a barangay team with a code; every week the barangays
    /// compete on the sum of their 10 best drivers' weekly scores. Last week's winner is the
    /// Barangay of the Week.
    /// </summary>
    public partial class UIManager
    {
        private VisualElement _brgy, _brgyBody;
        private bool _brgyShowBoard;

        private static readonly Color BrgyGreen = new Color32(0x2E, 0x7D, 0x32, 0xFF);

        private void BuildBarangay()
        {
            _brgy = ModalShell("barangay", "BARANGAY", out var card);
            var tabs = UiKit.Box();
            tabs.style.flexDirection = FlexDirection.Row;
            var mine = UiKit.Button("AMING BARANGAY", () => { _brgyShowBoard = false; LoadBarangay(); }, Color.white, UiKit.Ink, 28, 86);
            var board = UiKit.Button("RANKING NG LINGGO", () => { _brgyShowBoard = true; LoadBarangay(); }, Color.white, UiKit.Ink, 28, 86);
            mine.name = "brgyTabMine"; board.name = "brgyTabBoard";
            mine.style.flexGrow = board.style.flexGrow = 1;
            mine.style.flexBasis = board.style.flexBasis = 0;
            mine.style.marginRight = 8;
            tabs.Add(mine); tabs.Add(board);
            card.Add(tabs);
            var scroll = UiKit.Scroll(1100);
            scroll.style.height = 1100;
            _brgyBody = UiKit.Stack();
            scroll.Add(_brgyBody);
            card.Add(scroll);
            card.Add(UiKit.Button("BACK", CloseModal, Color.white, UiKit.Ink, 46, 120));
        }

        private void OpenBarangay()
        {
            _brgyShowBoard = false;
            OpenModal(_brgy);
            LoadBarangay();
        }

        private void LoadBarangay()
        {
            _brgy.Q<Button>("brgyTabMine").style.backgroundColor = _brgyShowBoard ? Color.white : UiKit.Yellow;
            _brgy.Q<Button>("brgyTabBoard").style.backgroundColor = _brgyShowBoard ? UiKit.Yellow : Color.white;
            _brgyBody.Clear();
            var o = Services.Online;
            if (o == null || !o.Configured) { _brgyBody.Add(Note("Barangay vs Barangay needs the online backend – see ACCOUNT.", 34, UiKit.Ink)); return; }
            if (!o.SignedIn) { _brgyBody.Add(Note("Sign in (or play as guest) under ACCOUNT to join a barangay.", 34, UiKit.Ink)); return; }
            _brgyBody.Add(Note("Loading…"));
            if (_brgyShowBoard) o.BarangayBoard(ShowBarangayBoard);
            else o.BarangayMine(ShowMyBarangay);
        }

        // ------------------------------------------------------------------ my barangay
        private void ShowMyBarangay(BarangayMine m, string err)
        {
            _brgyBody.Clear();
            if (m == null) { _brgyBody.Add(Note(err ?? "Could not load. (Is the barangay server update applied?)", 34, UiKit.Red)); return; }
            if (!m.found) { ShowBarangayJoin(m); return; }

            var head = UiKit.Box();
            head.style.backgroundColor = BrgyGreen;
            UiKit.Border(head.style, 4, UiKit.Ink, 26);
            UiKit.Pad(head.style, 18, 24);
            head.style.marginBottom = 14;
            var name = UiKit.Text(m.name.ToUpperInvariant(), 52, Color.white, true, 1.5f);
            name.style.whiteSpace = WhiteSpace.Normal;
            head.Add(name);
            head.Add(UiKit.Text(m.city, 32, UiKit.Hex("#DDF3DE"), false));
            head.Add(UiKit.Text($"TEAM SCORE NGAYONG LINGGO: {m.score:N0}", 34, UiKit.Yellow));
            head.Add(UiKit.Text(m.rank > 0 ? $"RANK #{m.rank} SA PILIPINAS" : "Wala pang score – mag-drive na!", 30, Color.white, false));
            _brgyBody.Add(head);

            var codeRow = UiKit.Box();
            codeRow.style.flexDirection = FlexDirection.Row;
            codeRow.style.alignItems = Align.Center;
            codeRow.style.marginBottom = 10;
            var code = UiKit.Text("CODE: " + m.code, 40, UiKit.Ink);
            code.style.flexGrow = 1;
            codeRow.Add(code);
            var invite = UiKit.Button("I-INVITE", () => Toast(ShareService.ShareText(Barangays.InviteText(m.name, m.code, Application.identifier))),
                UiKit.Hex("#1877F2"), Color.white, 30, 84);
            invite.style.width = 260;
            codeRow.Add(invite);
            _brgyBody.Add(codeRow);
            _brgyBody.Add(Note($"Ang score ng barangay = kabuuan ng {Barangays.TopMembers} pinakamagaling na driver ngayong linggo (resets Monday, PH time). Bilang ang free ride, career, Daily Challenge at Ghost Race (hindi ang Rush Hour at Night Shift).", 26));

            // weekly goal: everyone's passengers count; when reached, every member gets a prize
            var goalBox = UiKit.Stack();
            _brgyBody.Add(goalBox);
            Services.Online.BarangayGoal((g, e) =>
            {
                if (g == null || !g.member) return;
                goalBox.Add(Heading("LINGGUHANG GOAL"));
                goalBox.Add(Note($"{g.progress:N0} / {g.target:N0} pasahero ngayong linggo (lahat ng kabarangay). Prize: {g.coins} coins + {g.gems} gems bawat isa.", 26, UiKit.Ink));
                var bar = UiKit.Box();
                bar.style.height = 22;
                bar.style.backgroundColor = UiKit.Hex("#E2DBC6");
                UiKit.Border(bar.style, 0, Color.clear, 11);
                bar.style.overflow = Overflow.Hidden;
                bar.style.marginBottom = 10;
                var fill = UiKit.Box();
                fill.style.height = Length.Percent(100);
                fill.style.width = Length.Percent(Mathf.Clamp01(g.progress / (float)Mathf.Max(1, g.target)) * 100f);
                fill.style.backgroundColor = BrgyGreen;
                bar.Add(fill);
                goalBox.Add(bar);
                if (g.progress >= g.target && !g.claimed)
                    goalBox.Add(UiKit.Button($"CLAIM · {g.coins} COINS + {g.gems} GEMS", () => Services.Online.BarangayGoalClaim((ok, msg, _) => { Toast(ok ? "Salamat, kabarangay!" : msg); RefreshMenuTop(); LoadBarangay(); }), UiKit.Yellow, UiKit.Ink, 30, 92));
                else if (g.claimed) goalBox.Add(Note("Prize claimed this week!", 24, UiKit.Green));
            });
            _brgyBody.Add(Heading($"MGA KABARANGAY ({m.members.Length})"));
            for (int i = 0; i < m.members.Length; i++)
            {
                var r = m.members[i];
                _brgyBody.Add(BrgyRow($"#{i + 1}", $"{r.display_name}  · LV {r.level}", r.score > 0 ? r.score.ToString("N0") : "–",
                    r.is_me, i < Barangays.TopMembers && r.score > 0));
            }

            DateTime until = ParseTime(m.can_switch_at);
            bool locked = until > DateTime.UtcNow;
            if (locked) _brgyBody.Add(Note($"Puwede kang lumipat ng barangay simula {until.ToLocalTime():MMM d, h:mm tt}.", 26));
            var leave = UiKit.Button("UMALIS SA BARANGAY", () =>
            {
                if (locked) { Toast(Barangays.ReasonText("cooldown")); return; }
                Confirm("UMALIS?", $"Aalis ka sa {m.name}. Makakasali ka ulit sa ibang barangay, pero isang beses lang kada {Barangays.SwitchCooldownDays} araw.", () =>
                    Services.Online.BarangayLeave((r, e) =>
                    {
                        if (r != null && r.ok) Toast("Umalis ka na sa barangay.");
                        else Toast(r != null ? Barangays.ReasonText(r.reason) : e);
                        LoadBarangay();
                    }));
            }, Color.white, UiKit.Red, 30, 96);
            leave.style.marginTop = 16;
            _brgyBody.Add(leave);
        }

        private void ShowBarangayJoin(BarangayMine m)
        {
            _brgyBody.Add(Note("Wala ka pang barangay! Sumali gamit ang code ng kapitbahay mo, o gumawa ng sarili ninyo. Bawat linggo, naglalaban ang mga barangay: ang 10 pinakamagaling ninyong driver ang bilang.", 32, UiKit.Ink));

            _brgyBody.Add(Heading("SUMALI SA BARANGAY"));
            var code = AddField(_brgyBody, "Barangay code (hal. BRGYAB12)");
            code.maxLength = 12;
            _brgyBody.Add(UiKit.Button("SUMALI", () =>
            {
                string c = Barangays.NormalizeCode(code.value);
                if (c == null) { Toast(Barangays.ReasonText("unknown_code")); return; }
                Services.Online.BarangayJoin(c, (r, e) =>
                {
                    if (r != null && r.ok) { Toast($"Welcome sa {r.name}!"); LoadBarangay(); }
                    else Toast(r != null ? Barangays.ReasonText(r.reason) : e);
                });
            }, UiKit.Yellow, UiKit.Ink, 40, 110));

            _brgyBody.Add(Heading("GUMAWA NG BARANGAY"));
            var name = AddField(_brgyBody, "Pangalan (hal. Brgy. San Roque)");
            name.maxLength = Barangays.NameMax;
            var city = AddField(_brgyBody, "Lungsod / bayan (hal. Marikina)");
            city.maxLength = Barangays.NameMax;
            _brgyBody.Add(UiKit.Button("GUMAWA", () =>
            {
                Services.Online.BarangayCreate(name.value.Trim(), city.value.Trim(), (r, e) =>
                {
                    if (r != null && r.ok) { Toast($"Ginawa ang barangay! Code: {r.code}"); LoadBarangay(); }
                    else Toast(r != null ? Barangays.ReasonText(r.reason) : e);
                });
            }, BrgyGreen, Color.white, 40, 110));
            DateTime until = ParseTime(m.can_switch_at);
            if (until > DateTime.UtcNow) _brgyBody.Add(Note($"Kakalipat mo lang: puwede ulit simula {until.ToLocalTime():MMM d, h:mm tt}.", 26));
        }

        // ------------------------------------------------------------------ weekly board
        private void ShowBarangayBoard(BarangayBoard b, string err)
        {
            _brgyBody.Clear();
            if (b == null) { _brgyBody.Add(Note(err ?? "Could not load. (Is the barangay server update applied?)", 34, UiKit.Red)); return; }
            if (b.champion != null && !string.IsNullOrEmpty(b.champion.name))
            {
                var champ = UiKit.Box();
                champ.style.backgroundColor = UiKit.Hex("#FFF1B8");
                UiKit.Border(champ.style, 4, UiKit.Hex("#D4A017"), 26);
                UiKit.Pad(champ.style, 16, 22);
                champ.style.marginBottom = 14;
                champ.Add(UiKit.Text("BARANGAY OF THE WEEK", 30, UiKit.Hex("#B8860B")));
                var n = UiKit.Text(b.champion.name.ToUpperInvariant(), 46, UiKit.Ink, true, 1f);
                n.style.whiteSpace = WhiteSpace.Normal;
                champ.Add(n);
                champ.Add(UiKit.Text($"{b.champion.city} · {b.champion.score:N0} pts noong nakaraang linggo", 26, UiKit.Grey, false));
                _brgyBody.Add(champ);
            }
            _brgyBody.Add(Note("Ngayong linggo – score ng 10 pinakamagaling na driver ng bawat barangay.", 26));
            if (b.rows.Length == 0) _brgyBody.Add(Note("Wala pang barangay na may score ngayong linggo. Kayo na ang mauna!", 34, UiKit.Ink));
            bool meShown = false;
            foreach (var r in b.rows) { _brgyBody.Add(BoardRow(r)); meShown |= r.is_mine; }
            if (!meShown && b.me != null && b.me.rank > 0) { _brgyBody.Add(Note("…", 34)); _brgyBody.Add(BoardRow(b.me)); }
        }

        private VisualElement BoardRow(BarangayRow r) =>
            BrgyRow($"#{r.rank}", $"{r.name}\n{r.city} · {r.members} members", r.score.ToString("N0"), r.is_mine, false, r.rank);

        private VisualElement BrgyRow(string rankText, string label, string score, bool mine, bool counts, int rank = 0)
        {
            var row = UiKit.Box();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.backgroundColor = mine ? UiKit.Hex("#FFF1B8") : Color.white;
            UiKit.Border(row.style, 3, mine ? UiKit.Yellow : counts ? UiKit.Hex("#8BC48E") : UiKit.Hex("#E2DBC6"), 20);
            UiKit.Pad(row.style, 8, 16);
            row.style.marginBottom = 8;
            var rk = UiKit.Text(rankText, 38, rank == 1 ? UiKit.Hex("#D4A017") : rank > 0 && rank <= 3 ? UiKit.Blue : UiKit.Ink);
            rk.style.width = 120;
            row.Add(rk);
            var name = UiKit.Text(label, 30, UiKit.Ink, false);
            name.style.flexGrow = 1;
            name.style.flexShrink = 1;
            name.style.whiteSpace = WhiteSpace.Normal;
            name.style.unityTextAlign = TextAnchor.MiddleLeft;
            row.Add(name);
            row.Add(UiKit.Text(score, 36, UiKit.Ink));
            return row;
        }

        private static DateTime ParseTime(string s) =>
            !string.IsNullOrEmpty(s) && DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var t)
                ? t : DateTime.MinValue;
    }
}
