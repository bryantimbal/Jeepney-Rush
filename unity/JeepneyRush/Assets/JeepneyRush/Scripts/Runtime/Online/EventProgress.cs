using System;
using JeepneyRush.Logic;

namespace JeepneyRush
{
    /// <summary>
    /// Christmas event progress for the UI: from the server when online, else from the save file
    /// (same rules as Logic/ChristmasEvent and the jr_event_* functions).
    /// </summary>
    public static class EventProgress
    {
        public static bool Online => Services.Online != null && Services.Online.Configured;

        /// <summary>Last answer from the server (it may use a test date before December): event on, Simbang Gabi night.</summary>
        public static bool ServerActive;
        public static int ServerNight;

        /// <summary>Is the event on right now (the phone's date, or the server says so)?</summary>
        public static bool Active => ChristmasEvent.Active(PhilippineTime.Now) || ServerActive;
        public static int Night => ServerActive && ServerNight > 0 ? ServerNight : ChristmasEvent.SimbangGabiNight(PhilippineTime.Now);

        /// <summary>Offline: add a run's tokens (daily cap) and Simbang Gabi night to the save file.</summary>
        public static void CreditLocal(int tokens, int simbangGabiNight)
        {
            var d = Services.Save.Data;
            var now = PhilippineTime.Now;
            int season = ChristmasEvent.Season(now);
            if (d.eventSeason != season) { d.eventSeason = season; d.eventTokens = 0; d.eventNights = 0; d.eventClaimed = 0; d.eventGolden = false; d.eventDayTokens = 0; }
            ChristmasEvent.AddTokens(ref d.eventTokens, ref d.eventDayKey, ref d.eventDayTokens, PhilippineTime.DayKey, tokens);
            if (simbangGabiNight > 0) d.eventNights |= 1 << (simbangGabiNight - 1);
            Services.Save.Save();
        }

        public static void Status(Action<EventInfo> done)
        {
            var now = PhilippineTime.Now;
            if (Online)
            {
                if (!Services.Online.SignedIn) { done(null); return; }
                Services.Online.EventStatus((i, e) => { if (i != null) { ServerActive = i.active; ServerNight = i.night; } done(i); });
                return;
            }
            var d = Services.Save.Data;
            bool same = d.eventSeason == ChristmasEvent.Season(now);
            done(new EventInfo
            {
                active = ChristmasEvent.Active(now), season = ChristmasEvent.Season(now), days_left = ChristmasEvent.DaysLeft(now),
                tokens = same ? d.eventTokens : 0, nights = same ? d.eventNights : 0, claimed = same ? d.eventClaimed : 0, golden = same && d.eventGolden,
                today_tokens = same && d.eventDayKey == PhilippineTime.DayKey ? d.eventDayTokens : 0,
            });
        }

        /// <param name="index">reward index; <see cref="ChristmasEvent.Rewards"/>.Length = the Golden Parol</param>
        public static void Claim(int index, Action<bool, string> done)
        {
            if (Online) { Services.Online.EventClaimReward(index, done); return; }
            var d = Services.Save.Data;
            if (!ChristmasEvent.Active(PhilippineTime.Now)) { done(false, "The Christmas event is over."); return; }
            bool golden = index == ChristmasEvent.Rewards.Length;
            if (golden)
            {
                if (d.eventGolden) { done(false, "Na-claim na."); return; }
                if ((d.eventNights & ChristmasEvent.AllNights) != ChristmasEvent.AllNights) { done(false, "Complete all 9 Simbang Gabi nights first."); return; }
                d.eventGolden = true;
                Grant(d, ChristmasEvent.GoldenParol);
            }
            else
            {
                if (index < 0 || index >= ChristmasEvent.Rewards.Length || (d.eventClaimed & (1 << index)) != 0) { done(false, "Na-claim na."); return; }
                var r = ChristmasEvent.Rewards[index];
                if (d.eventTokens < r.Need) { done(false, "Kulang pa ang parols."); return; }
                d.eventClaimed |= 1 << index;
                Grant(d, r);
            }
            Services.Save.Save();
            done(true, null);
        }

        private static void Grant(SaveData d, EventReward r)
        {
            switch (r.Kind)
            {
                case EventRewardKind.Coins: d.coins += r.Amount; break;
                case EventRewardKind.Gems: d.gems += r.Amount; break;
                case EventRewardKind.Cosmetic: if (!d.ownedCosmetics.Contains(r.Id)) d.ownedCosmetics.Add(r.Id); break;
                case EventRewardKind.Jeepney: if (!d.ownedJeepneys.Contains(r.Id)) d.ownedJeepneys.Add(r.Id); break;
            }
        }
    }
}
