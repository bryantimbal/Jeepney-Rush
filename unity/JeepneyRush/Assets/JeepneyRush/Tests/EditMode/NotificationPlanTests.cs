using System;
using System.Linq;
using JeepneyRush.Logic;
using NUnit.Framework;

namespace JeepneyRush.Tests
{
    public class NotificationPlanTests
    {
        [Test]
        public void FewAndUseful()
        {
            var s = new NotificationPlan.State { Now = new DateTime(2026, 12, 18, 14, 0, 0), Streak = 5, StreakClaimedToday = false, FleetFullAt = new DateTime(2026, 12, 18, 21, 30, 0), EventActive = true, InBarangay = true };
            var p = NotificationPlan.Plan(s);
            Assert.IsTrue(p.Any(n => n.Id == NotificationPlan.DailyId && n.At == new DateTime(2026, 12, 19, 9, 0, 0)));
            Assert.IsTrue(p.Any(n => n.Id == NotificationPlan.StreakId && n.At.Hour == 19));
            Assert.IsTrue(p.Any(n => n.Id == NotificationPlan.FleetId));
            Assert.IsTrue(p.Any(n => n.Id == NotificationPlan.SimbangGabiId && n.Text.Contains("Golden Parol")));
            Assert.IsTrue(p.Any(n => n.Id == NotificationPlan.BarangayId && n.At.DayOfWeek == DayOfWeek.Sunday));
            for (int i = 1; i < p.Count; i++) Assert.GreaterOrEqual((p[i].At - p[i - 1].At).TotalMinutes, 60, "spaced out");
        }

        [Test]
        public void NothingNeedlessly()
        {
            var s = new NotificationPlan.State { Now = new DateTime(2026, 11, 10, 22, 0, 0), Streak = 1, StreakClaimedToday = false };
            var p = NotificationPlan.Plan(s);
            Assert.AreEqual(1, p.Count, "only tomorrow's daily challenge");
            Assert.AreEqual(NotificationPlan.DailyId, p[0].Id);
        }
    }
}
