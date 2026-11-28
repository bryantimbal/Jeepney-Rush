using System;

namespace JeepneyRush.Logic
{
    /// <summary>What the "Arawang Pasada" calendar shows today.</summary>
    public struct StreakStatus
    {
        /// <summary>Today's reward was already claimed.</summary>
        public bool Claimed;
        /// <summary>Calendar day (1..7) of today's reward (the one claimed today, or the one to claim).</summary>
        public int Day;
        /// <summary>Days in a row so far (before today's claim if not claimed yet).</summary>
        public int Streak;
        /// <summary>Exactly one day was missed and the streak can still be saved this week.</summary>
        public bool CanSave;
        /// <summary>The streak is lost (missed day and no save, or the save was already used this week).</summary>
        public bool Broken;
        /// <summary>Today's reward was already doubled with an ad.</summary>
        public bool Doubled;
    }

    /// <summary>
    /// Daily login streak ("Arawang Pasada"): one reward per PH calendar day, growing over 7 days and
    /// then starting the cycle again. Missing one day breaks the streak unless it is saved (once a
    /// week: watch an ad or pay <see cref="SaveGems"/> gems). Today's reward can be doubled once by
    /// watching an ad. Online the server applies the same rules (jr_streak_*); offline the save file.
    /// Day numbers are days since 1 Jan 2024 (PH time).
    /// </summary>
    public static class LoginStreak
    {
        public const int Days = 7, SaveGems = 10;
        public static readonly (int coins, int gems)[] Rewards = { (100, 0), (150, 0), (0, 5), (250, 0), (300, 0), (0, 10), (500, 15) };

        public static int DayNumber(DateTime phLocal) => (int)Math.Floor((phLocal.Date - new DateTime(2024, 1, 1)).TotalDays);
        public static int WeekOf(int day) => day / 7 + 1;

        public static (int coins, int gems) Reward(int calendarDay) => Rewards[Math.Max(1, Math.Min(Days, calendarDay)) - 1];
        public static int CalendarDay(int streak) => (Math.Max(1, streak) - 1) % Days + 1;

        /// <param name="last">day number of the last claim (-1 = never)</param>
        public static StreakStatus Status(int today, int last, int streak, int saveWeek, int doubledDay)
        {
            var s = new StreakStatus { Streak = Math.Max(0, streak) };
            if (last == today) { s.Claimed = true; s.Day = CalendarDay(streak); s.Doubled = doubledDay == today; return s; }
            if (last == today - 1) { s.Day = CalendarDay(streak + 1); return s; }
            if (last >= 0 && last == today - 2 && streak > 0)
            {
                s.CanSave = saveWeek != WeekOf(today);
                s.Broken = !s.CanSave;
                s.Day = s.CanSave ? CalendarDay(streak + 1) : 1;
                return s;
            }
            s.Broken = last >= 0 && streak > 0;
            s.Day = 1;
            return s;
        }

        /// <summary>Claims today's reward. Returns the calendar day claimed (0 = already claimed today).</summary>
        public static int Claim(int today, ref int last, ref int streak)
        {
            if (last == today) return 0;
            streak = last == today - 1 ? streak + 1 : 1;
            last = today;
            return CalendarDay(streak);
        }

        /// <summary>Saves a streak with one missed day (the missed day counts as played). False if it can't be saved.</summary>
        public static bool Save(int today, ref int last, int streak, ref int saveWeek)
        {
            if (!Status(today, last, streak, saveWeek, -1).CanSave) return false;
            last = today - 1;
            saveWeek = WeekOf(today);
            return true;
        }
    }
}
