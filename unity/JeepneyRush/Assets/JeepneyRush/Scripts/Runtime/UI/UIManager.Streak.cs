using System;
using JeepneyRush.Logic;
using UnityEngine;
using UnityEngine.UIElements;

namespace JeepneyRush
{
    /// <summary>
    /// Login streak "Arawang Pasada": a 7-day calendar that opens on the first visit to the main menu
    /// each day. Claim today's reward, double it with an ad, or save a streak with one missed day
    /// (ad or 10 gems, once a week). Online the server keeps the streak; offline the save file.
    /// </summary>
    public partial class UIManager
    {
        private VisualElement _streak, _streakBody;
        private int _streakShownDay = -1;

        private void BuildStreak()
        {
            _streak = ModalShell("streak", "ARAWANG PASADA", out var card);
            _streakBody = UiKit.Stack();
            card.Add(_streakBody);
            card.Add(UiKit.Button("LATER", CloseModal, Color.white, UiKit.Ink, 36, 100));
        }

        private bool StreakOnline => UseServer();
        private static int TodayNumber => LoginStreak.DayNumber(PhilippineTime.Now);

        /// <summary>From ShowMainMenu: open the calendar once a day if today's reward is waiting.</summary>
        private bool _streakHooked;

        private void MaybeShowStreak()
        {
            if (!_streakHooked && Services.Online != null)
            {
                // the menu may show before sign-in finished: try again when the connection is ready
                _streakHooked = true;
                Services.Online.Changed += () => { if (_modals.Count == 0 && _menu != null && _menu.resolvedStyle.display == DisplayStyle.Flex) MaybeShowStreak(); };
            }
            if (_streak == null || _streakShownDay == TodayNumber) return;
            if (Services.Online != null && Services.Online.Configured && !Services.Online.Authoritative) return; // wait until online (server keeps the streak)
            GetStreak(s =>
            {
                if (s == null || s.Value.Claimed) { _streakShownDay = TodayNumber; return; }
                _streakShownDay = TodayNumber;
                OpenStreak(s);
            });
        }

        private void GetStreak(Action<StreakStatus?> done)
        {
            if (StreakOnline)
                Services.Online.StreakStatus((i, e) => done(i == null ? (StreakStatus?)null : new StreakStatus
                    { Claimed = i.claimed, Day = i.day, Streak = i.streak, CanSave = i.can_save, Broken = i.broken, Doubled = i.doubled }));
            else
            {
                var d = Services.Save.Data;
                done(LoginStreak.Status(TodayNumber, d.streakLast, d.streakCount, d.streakSaveWeek, d.streakDoubled));
            }
        }

        private void OpenStreak(StreakStatus? s = null)
        {
            if (s == null) { GetStreak(x => { if (x != null) OpenStreak(x); }); return; }
            if (!_modals.Contains(_streak)) OpenModal(_streak);
            DrawStreak(s.Value);
        }

        private void DrawStreak(StreakStatus s)
        {
            _streakBody.Clear();
            if (s.CanSave && !s.Claimed) { DrawStreakSaver(s); return; }
            int shown = s.Claimed ? s.Streak : s.Day == 1 ? 0 : s.Streak;
            var head = UiKit.Text(shown > 0 ? $"{shown} DAYS IN A ROW" : s.Broken ? "NAG-RESET ANG STREAK" : "DAY 1 – WELCOME!", 34, UiKit.Ink);
            _streakBody.Add(head);
            _streakBody.Add(Note(s.Claimed ? "See you tomorrow – bigger reward!" : "Come back every day for bigger rewards. Day 7 is the big one!", 26));
            // calendar: 3 x 2 grid + day 7 across the bottom
            var grid = UiKit.Box();
            grid.style.flexDirection = FlexDirection.Row;
            grid.style.flexWrap = Wrap.Wrap;
            grid.style.justifyContent = Justify.SpaceBetween;
            for (int day = 1; day <= LoginStreak.Days; day++)
            {
                bool done = day < s.Day || (s.Claimed && day == s.Day), today = day == s.Day;
                var (coins, gems) = LoginStreak.Reward(day);
                var cell = UiKit.Box();
                cell.style.width = day == 7 ? Length.Percent(100) : Length.Percent(31.5f);
                cell.style.minHeight = 150;
                cell.style.marginBottom = 10;
                cell.style.alignItems = Align.Center;
                cell.style.justifyContent = Justify.Center;
                cell.style.backgroundColor = today && !s.Claimed ? UiKit.Yellow : done ? UiKit.Hex("#D9F7E3") : Color.white;
                UiKit.Border(cell.style, today ? 6 : 4, today ? UiKit.Red : UiKit.Ink, 20);
                cell.Add(UiKit.Text($"ARAW {day}", 24, today ? UiKit.Red : UiKit.Grey));
                var row = UiKit.Box();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                if (coins > 0) { row.Add(UiKit.Icon("ui_coin", 46)); row.Add(UiKit.Text($" {coins}", 30, UiKit.Ink)); }
                if (gems > 0) { if (coins > 0) row.Add(UiKit.Text("  +", 26, UiKit.Ink)); row.Add(GemIcon(46)); row.Add(UiKit.Text($" {gems}", 30, UiKit.Ink)); }
                cell.Add(row);
                if (done) cell.Add(UiKit.Text("NA-CLAIM", 20, UiKit.Green));
                grid.Add(cell);
            }
            _streakBody.Add(grid);
            if (!s.Claimed)
                _streakBody.Add(UiKit.Button("KUNIN!  (CLAIM)", ClaimStreak, UiKit.Yellow, UiKit.Ink, 46, 120));
            else if (!s.Doubled && Services.Ads != null && Services.Ads.RewardedReady)
                _streakBody.Add(UiKit.Button("WATCH AD = DOUBLE TODAY'S REWARD", () => StreakAd(RewardedKind.StreakDouble), UiKit.Hex("#D9B8FA"), UiKit.Ink, 30, 104));
        }

        private void DrawStreakSaver(StreakStatus s)
        {
            _streakBody.Add(UiKit.Text("NA-MISS MO KAMI!", 44, UiKit.Red));
            _streakBody.Add(Note($"You missed a day. Save your {s.Streak}-day streak before it resets? (one save a week)", 30, UiKit.Ink));
            if (Services.Ads != null && Services.Ads.RewardedReady)
                _streakBody.Add(UiKit.Button("SAVE WITH AN AD", () => StreakAd(RewardedKind.StreakSave), UiKit.Yellow, UiKit.Ink, 40, 116));
            _streakBody.Add(UiKit.Button($"SAVE FOR {LoginStreak.SaveGems} GEMS", SaveStreakWithGems, Color.white, UiKit.Ink, 36, 108));
            _streakBody.Add(UiKit.Button("START AGAIN FROM DAY 1", () => DrawStreak(new StreakStatus { Day = 1, Broken = true }), Color.white, UiKit.Grey, 28, 92));
        }

        private void ClaimStreak()
        {
            Services.Audio.Play(Logic.Audio.Sfx.Purchase);
            if (StreakOnline)
            {
                Services.Online.StreakClaim((r, err) =>
                {
                    if (r == null || !r.ok) { Toast(err ?? "Hindi na-claim. Subukan ulit."); return; }
                    StreakToast(r.reward, r.gems, r.day);
                    RefreshMenuTop();
                    OpenStreak();
                });
                return;
            }
            var d = Services.Save.Data;
            int day = LoginStreak.Claim(TodayNumber, ref d.streakLast, ref d.streakCount);
            if (day == 0) return;
            var (coins, gems) = LoginStreak.Reward(day);
            d.coins += coins; d.gems += gems;
            Services.Save.Save();
            StreakToast(coins, gems, day);
            RefreshMenuTop();
            OpenStreak();
        }

        private void StreakToast(int coins, int gems, int day) =>
            Float(day == 7 ? "ARAW 7! JACKPOT!" : $"ARAW {day}: " + (coins > 0 ? $"+{coins} COINS" : "") + (gems > 0 ? $" +{gems} GEMS" : ""), FloatKind.Big);

        private void StreakAd(RewardedKind kind)
        {
            Services.Ads.ShowRewarded(kind, "", (earned, nonce) =>
            {
                if (!earned) { Toast("Ad not finished."); return; }
                if (StreakOnline)
                {
                    Services.Online.ClaimAdReward(nonce, (ok, msg, reward) =>
                    {
                        Toast(ok ? (kind == RewardedKind.StreakSave ? "Streak saved!" : "Doubled!") : msg);
                        RefreshMenuTop();
                        OpenStreak();
                    });
                    return;
                }
                var d = Services.Save.Data;
                if (kind == RewardedKind.StreakSave) { if (LoginStreak.Save(TodayNumber, ref d.streakLast, d.streakCount, ref d.streakSaveWeek)) Toast("Streak saved!"); }
                else if (d.streakLast == TodayNumber && d.streakDoubled != TodayNumber)
                {
                    var (c, g) = LoginStreak.Reward(LoginStreak.CalendarDay(d.streakCount));
                    d.coins += c; d.gems += g; d.streakDoubled = TodayNumber;
                    Toast("Doubled!");
                }
                Services.Save.Save();
                RefreshMenuTop();
                OpenStreak();
            });
        }

        private void SaveStreakWithGems()
        {
            if (StreakOnline)
            {
                Services.Online.StreakSaveWithGems((ok, msg) => { Toast(ok ? "Streak saved!" : msg); RefreshMenuTop(); OpenStreak(); });
                return;
            }
            var d = Services.Save.Data;
            if (d.gems < LoginStreak.SaveGems) { Toast("Kulang ang gems."); return; }
            if (!LoginStreak.Save(TodayNumber, ref d.streakLast, d.streakCount, ref d.streakSaveWeek)) { Toast("This streak can't be saved any more."); return; }
            d.gems -= LoginStreak.SaveGems;
            Services.Save.Save();
            Toast("Streak saved!");
            RefreshMenuTop();
            OpenStreak();
        }
    }
}
