using System;
using JeepneyRush.Logic;
using UnityEngine;
using UnityEngine.UIElements;

namespace JeepneyRush
{
    /// <summary>Missions (daily, weekly), achievements and route selection screens.</summary>
    public sealed partial class UIManager
    {
        private int _missionTab; // 0 daily, 1 weekly, 2 achievements
        private VisualElement _missionList;
        private Button[] _missionTabs;
        private Label _missionInfo;
        private VisualElement _routeList;

        private bool HasClaimable()
        {
            var d = Services.Save.Data;
            var daily = Missions.Daily(d.dailyKey);
            for (int i = 0; i < daily.Length; i++) if (!d.dailyClaimed[i] && d.dailyProgress[i] >= daily[i].Target) return true;
            var weekly = Missions.Weekly(d.weeklyKey);
            for (int i = 0; i < weekly.Length; i++) if (!d.weeklyClaimed[i] && d.weeklyProgress[i] >= weekly[i].Target) return true;
            foreach (var a in d.achievementsUnlocked) if (!d.achievementsClaimed.Contains(a)) return true;
            return false;
        }

        private void BuildMissions()
        {
            _missions = ModalShell("missions", "MISSIONS", out var card);
            var tabs = UiKit.Box();
            tabs.style.flexDirection = FlexDirection.Row;
            string[] names = { "DAILY", "WEEKLY", "AWARDS" };
            _missionTabs = new Button[3];
            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                var b = UiKit.Button(names[i], () => { _missionTab = idx; RefreshMissions(); }, Color.white, UiKit.Ink, 34, 92);
                b.style.flexGrow = 1;
                b.style.flexBasis = 0;
                if (i < 2) b.style.marginRight = 10;
                tabs.Add(b);
                _missionTabs[i] = b;
            }
            card.Add(tabs);
            _missionInfo = UiKit.Text("", 28, UiKit.Grey, false);
            card.Add(_missionInfo);
            _missionList = UiKit.Box();
            card.Add(_missionList);
            card.Add(UiKit.Button("BACK", () => { CloseModal(); ShowMainMenu(); }, Color.white, UiKit.Ink, 44, 110));
        }

        private void OpenMissions()
        {
            Services.Save.RefreshMissions();
            _missionTab = 0;
            OpenModal(_missions);
            RefreshMissions();
        }

        private VisualElement ProgressRow(string title, string subtitle, float fraction, string status, bool claimable, bool claimed, Action claim)
        {
            var row = UiKit.Box();
            row.style.backgroundColor = Color.white;
            UiKit.Border(row.style, 4, claimable ? UiKit.Green : UiKit.Hex("#E2DBC6"), 24);
            UiKit.Pad(row.style, 10, 16);
            row.style.marginTop = 10;
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            var col = UiKit.Box();
            col.style.flexGrow = 1;
            col.style.flexShrink = 1;
            var t = UiKit.Text(title, 32, UiKit.Ink, false);
            t.style.unityTextAlign = TextAnchor.MiddleLeft;
            col.Add(t);
            if (!string.IsNullOrEmpty(subtitle))
            {
                var st = UiKit.Text(subtitle, 24, UiKit.Grey, false);
                st.style.unityTextAlign = TextAnchor.MiddleLeft;
                col.Add(st);
            }
            var bar = UiKit.Box();
            bar.style.height = 18;
            bar.style.marginTop = 6;
            bar.style.backgroundColor = UiKit.Hex("#E2DBC6");
            UiKit.Border(bar.style, 0, Color.clear, 9);
            bar.style.overflow = Overflow.Hidden;
            var fill = UiKit.Box();
            fill.style.height = Length.Percent(100);
            fill.style.width = Length.Percent(Mathf.Clamp01(fraction) * 100f);
            fill.style.backgroundColor = fraction >= 1 ? UiKit.Green : UiKit.Yellow;
            bar.Add(fill);
            col.Add(bar);
            var s = UiKit.Text(status, 24, UiKit.Grey, false);
            s.style.unityTextAlign = TextAnchor.MiddleLeft;
            col.Add(s);
            row.Add(col);
            if (claimed) row.Add(UiKit.Icon("ui_check", 80));
            else
            {
                var b = UiKit.Button("CLAIM", claim, claimable ? UiKit.Green : UiKit.Hex("#B9B2C6"), claimable ? Color.white : UiKit.Ink, 32, 90);
                b.style.width = 200;
                b.style.marginLeft = 12;
                b.style.marginTop = b.style.marginBottom = 0;
                b.SetEnabled(claimable);
                b.style.opacity = claimable ? 1f : 0.55f;
                row.Add(b);
            }
            return row;
        }

        private void RefreshMissions()
        {
            var d = Services.Save.Data;
            for (int i = 0; i < _missionTabs.Length; i++) _missionTabs[i].style.backgroundColor = i == _missionTab ? UiKit.Yellow : Color.white;
            _missionList.Clear();
            var now = DateTime.Now;
            if (_missionTab < 2)
            {
                bool weekly = _missionTab == 1;
                var defs = weekly ? Missions.Weekly(d.weeklyKey) : Missions.Daily(d.dailyKey);
                var progress = weekly ? d.weeklyProgress : d.dailyProgress;
                var claimed = weekly ? d.weeklyClaimed : d.dailyClaimed;
                var reset = weekly ? now.Date.AddDays(7 - ((int)now.DayOfWeek + 6) % 7) - now : now.Date.AddDays(1) - now;
                _missionInfo.text = $"New {(weekly ? "weekly" : "daily")} missions in {(reset.Days > 0 ? reset.Days + "d " : "")}{reset.Hours}h {reset.Minutes}m";
                for (int i = 0; i < defs.Length; i++)
                {
                    int idx = i;
                    var m = defs[i];
                    bool done = progress[i] >= m.Target;
                    _missionList.Add(ProgressRow(m.Text, $"Reward: {m.Reward} coins", progress[i] / (float)m.Target, $"{progress[i]:N0} / {m.Target:N0}", done && !claimed[i], claimed[i], () =>
                    {
                        if (Missions.Claim(Services.Save.Data, weekly, idx) > 0) { Services.Audio.Play(Logic.Audio.Sfx.Purchase); Services.Save.Save(); }
                        RefreshMissions();
                    }));
                }
            }
            else
            {
                _missionInfo.text = $"{d.achievementsUnlocked.Count} / {Achievements.All.Length} unlocked";
                foreach (var a in Achievements.All)
                {
                    var ach = a;
                    bool unlocked = d.achievementsUnlocked.Contains(a.Id), claimedA = d.achievementsClaimed.Contains(a.Id);
                    float val = Mathf.Min(a.Stat(d), a.Target);
                    _missionList.Add(ProgressRow(a.Name, $"{a.Description} · {a.Reward} coins", Achievements.Progress(d, a), $"{val:N0} / {a.Target:N0}", unlocked && !claimedA, claimedA, () =>
                    {
                        if (Achievements.Claim(Services.Save.Data, ach.Id) > 0) { Services.Audio.Play(Logic.Audio.Sfx.Purchase); Services.Save.Save(); }
                        RefreshMissions();
                    }));
                }
            }
        }

        // ======================================================================= routes
        private void BuildRoutes()
        {
            _routes = ModalShell("routes", "ROUTES", out var card);
            _routeList = UiKit.Box();
            card.Add(_routeList);
            card.Add(UiKit.Button("BACK", () => { CloseModal(); ShowMainMenu(); }, Color.white, UiKit.Ink, 44, 110));
        }

        private void OpenRoutes()
        {
            OpenModal(_routes);
            RefreshRoutes();
        }

        private void RefreshRoutes()
        {
            var d = Services.Save.Data;
            _routeList.Clear();
            foreach (var e in Environments.All)
            {
                var env = e;
                bool unlocked = Environments.IsUnlocked(e.Kind, d.bestDistance);
                bool selected = d.selectedEnvironment == (int)e.Kind;
                var row = UiKit.Box();
                row.pickingMode = PickingMode.Position;
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.style.backgroundColor = selected ? UiKit.Hex("#FFF3C4") : Color.white;
                UiKit.Border(row.style, 5, selected ? UiKit.Yellow : UiKit.Hex("#E2DBC6"), 26);
                UiKit.Pad(row.style, 10, 12);
                row.style.marginBottom = 12;
                var thumb = UiKit.Icon("ui_route_" + e.Kind.ToString().ToLowerInvariant(), 250);
                thumb.style.height = 140;
                UiKit.Border(thumb.style, 4, UiKit.Ink, 18);
                if (!unlocked) thumb.style.opacity = 0.45f;
                row.Add(thumb);
                var col = UiKit.Box();
                col.style.flexGrow = 1;
                col.style.flexShrink = 1;
                col.style.marginLeft = 16;
                var n = UiKit.Text(e.Name.ToUpperInvariant(), 40, UiKit.Ink);
                n.style.unityTextAlign = TextAnchor.MiddleLeft;
                col.Add(n);
                var t = UiKit.Text(unlocked ? e.Tagline : $"Drive {e.UnlockDistance:N0} m in one run to unlock (best: {Mathf.FloorToInt(d.bestDistance):N0} m)", 26, unlocked ? UiKit.Grey : UiKit.Red, false);
                t.style.unityTextAlign = TextAnchor.MiddleLeft;
                col.Add(t);
                var status = UiKit.Text(selected ? "SELECTED" : unlocked ? "TAP TO DRIVE HERE" : "LOCKED", 28, selected ? UiKit.Green : unlocked ? UiKit.Blue : UiKit.Grey, false);
                status.style.unityTextAlign = TextAnchor.MiddleLeft;
                status.style.marginTop = 4;
                col.Add(status);
                row.Add(col);
                if (!unlocked) row.Add(UiKit.Icon("ui_lock", 70));
                row.RegisterCallback<ClickEvent>(_ =>
                {
                    if (!unlocked || selected) return;
                    Services.Audio.Click();
                    Services.Save.Data.selectedEnvironment = (int)env.Kind;
                    Services.Save.Save();
                    Services.Audio.SetRoute(env.Kind);
                    Services.Game.Menu?.ShowRoute(env.Kind);
                    RefreshRoutes();
                });
                _routeList.Add(row);
            }
        }
    }
}
