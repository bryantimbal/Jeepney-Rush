using JeepneyRush.Logic;
using NUnit.Framework;

namespace JeepneyRush.Tests
{
    public class GameModeTests
    {
        [Test]
        public void NightShiftStaysNight()
        {
            for (float t = 0; t < 3600; t += 7.3f)
            {
                float hour = DayNight.HourAt(GameModes.Clock(GameMode.NightShift, t));
                Assert.AreEqual(1f, DayNight.Night(hour), 1e-3, $"t={t} hour={hour}");
                Assert.IsTrue(DayNight.IsNightFor(GameModes.Clock(GameMode.NightShift, t)));
            }
            Assert.AreEqual(20f, DayNight.HourAt(GameModes.Clock(GameMode.NightShift, 0)), 0.01f, "starts at 8 PM");
        }

        [Test]
        public void OtherModesKeepTheNormalClock()
        {
            Assert.AreEqual(42f, GameModes.Clock(GameMode.RushHour, 42f));
            Assert.AreEqual(42f, GameModes.Clock(GameMode.FreeRide, 42f));
        }

        [Test]
        public void RushHourClock()
        {
            Assert.AreEqual(180f, GameModes.TimeLeft(GameMode.RushHour, 0));
            Assert.AreEqual(0f, GameModes.TimeLeft(GameMode.RushHour, 999));
            Assert.IsTrue(float.IsPositiveInfinity(GameModes.TimeLeft(GameMode.NightShift, 10)));
            Assert.Greater(GameModes.RushMaxDuration, GameModes.RushSeconds);
        }

        [Test]
        public void KeysRoundTrip()
        {
            foreach (GameMode m in System.Enum.GetValues(typeof(GameMode)))
                Assert.AreEqual(m, GameModes.FromKey(GameModes.Key(m)));
        }

        [Test]
        public void RushStopsAreCrowdedNightStopsQuieter()
        {
            Assert.Greater(GameModes.Density(GameMode.RushHour), 1f);
            Assert.Less(GameModes.Density(GameMode.NightShift), 1f);
            Assert.Greater(GameModes.ExtraWaiting(GameMode.RushHour), 0);
            Assert.Less(GameModes.ExtraWaiting(GameMode.NightShift), 0);
            Assert.Greater(GameModes.FareFactor(GameMode.NightShift), 1f);
        }
    }
}
