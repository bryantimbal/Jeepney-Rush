using JeepneyRush.Logic;
using NUnit.Framework;

namespace JeepneyRush.Tests
{
    public class LoginStreakTests
    {
        [Test]
        public void SevenDayCycle()
        {
            int last = -1, streak = 0;
            for (int d = 0; d < 7; d++) Assert.AreEqual(d + 1, LoginStreak.Claim(100 + d, ref last, ref streak));
            Assert.AreEqual(1, LoginStreak.Claim(107, ref last, ref streak), "after day 7 the calendar starts again");
            Assert.AreEqual(8, streak, "but the streak keeps counting");
            Assert.AreEqual(0, LoginStreak.Claim(107, ref last, ref streak), "once a day");
            Assert.AreEqual((500, 15), LoginStreak.Reward(7));
        }

        [Test]
        public void MissedDayBreaksUnlessSaved()
        {
            int last = 104, streak = 5, week = 0;
            var s = LoginStreak.Status(106, last, streak, week, -1);
            Assert.IsTrue(s.CanSave);
            Assert.AreEqual(6, s.Day, "saving keeps the calendar going");
            Assert.IsTrue(LoginStreak.Save(106, ref last, streak, ref week));
            Assert.AreEqual(6, LoginStreak.Claim(106, ref last, ref streak));
            // a second miss in the same week can't be saved
            var s2 = LoginStreak.Status(108, last, streak, week, -1);
            Assert.IsFalse(s2.CanSave);
            Assert.IsTrue(s2.Broken);
            Assert.AreEqual(1, LoginStreak.Claim(108, ref last, ref streak), "starts over");
            // two missed days: gone
            var s3 = LoginStreak.Status(111, last, streak, 0, -1);
            Assert.IsTrue(s3.Broken);
            Assert.IsFalse(s3.CanSave);
        }

        [Test]
        public void StatusToday()
        {
            var s = LoginStreak.Status(50, 50, 3, 0, 50);
            Assert.IsTrue(s.Claimed);
            Assert.IsTrue(s.Doubled);
            Assert.AreEqual(3, s.Day);
            Assert.AreEqual(1, LoginStreak.Status(50, -1, 0, 0, -1).Day, "first ever");
            Assert.IsFalse(LoginStreak.Status(50, -1, 0, 0, -1).Broken);
        }
    }
}
