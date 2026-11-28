using System;
using System.Collections.Generic;

namespace JeepneyRush.Logic
{
    /// <summary>A reminder to schedule (local notification).</summary>
    public struct PlannedNotification
    {
        public int Id;
        public string Title, Text;
        /// <summary>When it fires (PH local time).</summary>
        public DateTime At;
    }

    /// <summary>
    /// The few, useful reminders scheduled when the game goes to the background (all cancelled when
    /// it comes back): Daily Challenge ready (next morning), streak at risk (this evening, if today's
    /// reward is unclaimed), fleet boundary full (when the 8-hour cap is reached), Simbang Gabi tonight
    /// (16–24 Dec) and the barangay's last day of the week (Sunday evening). At most one per hour slot.
    /// </summary>
    public static class NotificationPlan
    {
        public const int DailyId = 1, StreakId = 2, FleetId = 3, SimbangGabiId = 4, BarangayId = 5;
        public const int MorningHour = 9, EveningHour = 19, NightHour = 18;

        public sealed class State
        {
            public DateTime Now;                  // PH local
            public bool StreakClaimedToday;
            public int Streak;
            public DateTime? FleetFullAt;         // PH local; null = no fleet
            public bool EventActive;
            public bool InBarangay;
        }

        public static List<PlannedNotification> Plan(State s)
        {
            var list = new List<PlannedNotification>();
            var today = s.Now.Date;
            // tomorrow morning: a new Daily Challenge
            list.Add(new PlannedNotification { Id = DailyId, Title = "BAGONG DAILY CHALLENGE!", Text = "Iba na ang ruta ngayon. Sino ang mangunguna? Pasada na!", At = today.AddDays(1).AddHours(MorningHour) });
            // this evening: the streak is at risk (only if there's a streak worth saving)
            var evening = today.AddHours(EveningHour);
            if (!s.StreakClaimedToday && s.Streak >= 2 && s.Now < evening)
                list.Add(new PlannedNotification { Id = StreakId, Title = $"{s.Streak} ARAW NA STREAK MO!", Text = "Huwag sayangin! Kunin ang reward bago mag-hatinggabi.", At = evening });
            // the fleet's boundary is full
            if (s.FleetFullAt.HasValue && s.FleetFullAt.Value > s.Now)
                list.Add(new PlannedNotification { Id = FleetId, Title = "PUNO NA ANG BOUNDARY!", Text = "Naghihintay na ang mga driver mo. I-collect na, boss!", At = s.FleetFullAt.Value });
            // Simbang Gabi tonight
            if (s.EventActive)
                for (int d = 0; d <= 1; d++)
                {
                    var day = today.AddDays(d);
                    int night = ChristmasEvent.SimbangGabiNight(day);
                    var at = day.AddHours(NightHour);
                    if (night > 0 && at > s.Now)
                    {
                        list.Add(new PlannedNotification { Id = SimbangGabiId, Title = $"SIMBANG GABI · GABI {night}", Text = "Mag-Night Shift ngayong gabi para sa Golden Parol!", At = at });
                        break;
                    }
                }
            // Sunday evening: the barangay's last chance this week
            if (s.InBarangay)
            {
                int toSunday = ((int)DayOfWeek.Sunday - (int)s.Now.DayOfWeek + 7) % 7;
                var sunday = today.AddDays(toSunday).AddHours(NightHour + 1);
                if (sunday <= s.Now) sunday = sunday.AddDays(7);
                list.Add(new PlannedNotification { Id = BarangayId, Title = "HULING ARAW NG LINGGO!", Text = "Tulungan ang barangay mo bago mag-reset ang ranking!", At = sunday });
            }
            // never two in the same hour (the earlier one wins)
            list.Sort((a, b) => a.At.CompareTo(b.At));
            for (int i = list.Count - 1; i > 0; i--) if ((list[i].At - list[i - 1].At).TotalMinutes < 60) list.RemoveAt(i);
            return list;
        }
    }
}
