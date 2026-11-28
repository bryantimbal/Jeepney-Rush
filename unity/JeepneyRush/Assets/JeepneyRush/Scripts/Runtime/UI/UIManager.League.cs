using JeepneyRush.Logic;
using UnityEngine;
using UnityEngine.UIElements;

namespace JeepneyRush
{
    /// <summary>
    /// Weekly league: your group of up to 30 drivers of the same tier, ranked by this week's best
    /// score. Top 7 move up, bottom 5 move down when the week ends; last week's prize is claimed here.
    /// </summary>
    public partial class UIManager
    {
        private VisualElement _league, _leagueBody;

        private void BuildLeague()
        {
            _league = ModalShell("league", "WEEKLY LEAGUE", out var card);
            var scroll = UiKit.Scroll(1250);
            scroll.style.height = 1250;
            _leagueBody = UiKit.Stack();
            scroll.Add(_leagueBody);
            card.Add(scroll);
            card.Add(UiKit.Button("BACK", CloseModal, Color.white, UiKit.Ink, 46, 120));
        }

        private void OpenLeague()
        {
            if (!_modals.Contains(_league)) OpenModal(_league);
            _leagueBody.Clear();
            var o = Services.Online;
            if (o == null || !o.Configured) { _leagueBody.Add(Note("Leagues need the online backend – see ACCOUNT.", 32, UiKit.Ink)); return; }
            if (!o.SignedIn) { _leagueBody.Add(Note("Sign in (or play as guest) under ACCOUNT to join the weekly league.", 32, UiKit.Ink)); return; }
            _leagueBody.Add(Note("Loading…"));
            o.LeagueStatus(DrawLeague);
        }

        private void DrawLeague(LeagueInfo i, string err)
        {
            _leagueBody.Clear();
            if (i == null) { _leagueBody.Add(Note(err ?? "Could not load the league. (Is the league server update applied?)", 32, UiKit.Red)); return; }
            // tier badge
            var badge = UiKit.Text(Leagues.Name(i.tier) + " LEAGUE", 46, Color.white, true, 1.5f);
            badge.style.backgroundColor = UiKit.Hex(Leagues.Colour(i.tier));
            UiKit.Border(badge.style, 5, UiKit.Ink, 24);
            UiKit.Pad(badge.style, 12, 20);
            _leagueBody.Add(badge);
            _leagueBody.Add(Note($"Ends in {i.days_left} day{(i.days_left == 1 ? "" : "s")} (Monday, PH time). Top {Leagues.Promote} move up, bottom {Leagues.Demote} move down. Your best score this week counts.", 26));
            // last week's result
            if (i.last != null && i.last.week > 0 && i.last.rank > 0)
            {
                bool up = i.last.result_tier > i.last.tier, down = i.last.result_tier < i.last.tier;
                var box = UiKit.Box();
                box.style.backgroundColor = up ? UiKit.Hex("#D9F7E3") : down ? UiKit.Hex("#FFE3E3") : UiKit.Hex("#F1F3F5");
                UiKit.Border(box.style, 3, UiKit.Ink, 18);
                UiKit.Pad(box.style, 10, 14);
                box.style.marginBottom = 12;
                box.Add(UiKit.Text($"LAST WEEK: #{i.last.rank} · " + (up ? "PROMOTED!" : down ? "DEMOTED" : "STAYED"), 30, UiKit.Ink));
                if (i.last.reward > 0)
                {
                    if (i.last.claimed) box.Add(UiKit.Text($"+{i.last.reward:N0} coins claimed", 24, UiKit.Green, false));
                    else box.Add(UiKit.Button($"CLAIM {i.last.reward:N0} COINS", () => Services.Online.LeagueClaim((ok, msg, reward) =>
                        { Toast(ok ? $"+{reward:N0} COINS!" : msg); if (ok) Services.Audio.Play(Logic.Audio.Sfx.Purchase); RefreshMenuTop(); OpenLeague(); }), UiKit.Yellow, UiKit.Ink, 32, 96));
                }
                _leagueBody.Add(box);
            }
            if (!i.joined) { _leagueBody.Add(Note("Play a run to join this week's league!", 34, UiKit.Ink)); return; }
            _leagueBody.Add(Heading($"YOUR GROUP · #{i.rank} OF {i.rows.Length}"));
            int scored = i.players;
            foreach (var r in i.rows)
            {
                bool promote = r.rank <= Leagues.Promote && r.score > 0 && i.tier < Leagues.Tiers - 1;
                bool demote = scored >= Leagues.MinForDemotion && r.rank > scored - Leagues.Demote && r.score > 0 && i.tier > 0;
                var row = UiKit.Box();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.style.backgroundColor = r.is_me ? UiKit.Hex("#FFF1B8") : promote ? UiKit.Hex("#EAF8EE") : demote ? UiKit.Hex("#FDECEC") : Color.white;
                UiKit.Border(row.style, 3, r.is_me ? UiKit.Yellow : UiKit.Hex("#E2DBC6"), 16);
                UiKit.Pad(row.style, 6, 14);
                row.style.marginBottom = 6;
                var rk = UiKit.Text($"#{r.rank}", 32, promote ? UiKit.Green : demote ? UiKit.Red : UiKit.Ink);
                rk.style.width = 100;
                row.Add(rk);
                var n = UiKit.Text($"{r.display_name}  · LV {r.level}", 28, UiKit.Ink, false);
                n.style.flexGrow = 1;
                n.style.unityTextAlign = TextAnchor.MiddleLeft;
                row.Add(n);
                row.Add(UiKit.Text(r.score > 0 ? r.score.ToString("N0") : "–", 30, UiKit.Ink));
                _leagueBody.Add(row);
            }
        }
    }
}
