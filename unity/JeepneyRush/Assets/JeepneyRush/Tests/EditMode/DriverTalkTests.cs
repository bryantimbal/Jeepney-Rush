using JeepneyRush.Logic;
using NUnit.Framework;

namespace JeepneyRush.Tests
{
    public class DriverTalkTests
    {
        [Test]
        public void EveryMomentHasPoliteVariedLines()
        {
            foreach (DriverMoment m in System.Enum.GetValues(typeof(DriverMoment)))
            {
                var lines = DriverLines.For(m);
                Assert.GreaterOrEqual(lines.Length, 3, $"{m}: variations so it doesn't repeat");
                foreach (var l in lines)
                {
                    Assert.LessOrEqual(l.Length, 40, $"short enough to read while driving: {l}");
                    Assert.AreEqual(l.ToUpperInvariant(), l, "HUD style is upper case");
                }
            }
            CollectionAssert.Contains(DriverLines.For(DriverMoment.AskFare), "PAKI-ABOT NA LANG PO NG BAYAD.");
            CollectionAssert.Contains(DriverLines.For(DriverMoment.VehicleFull), "PASENSYA NA PO, PUNO NA!");
            CollectionAssert.Contains(DriverLines.For(DriverMoment.NearMiss), "AY, MUNTIK NA!");
            CollectionAssert.Contains(DriverLines.For(DriverMoment.GameOver), "AY, PAHINGA MUNA TAYO!");
            // the old awkward phrase is gone
            foreach (DriverMoment m in System.Enum.GetValues(typeof(DriverMoment)))
                foreach (var l in DriverLines.For(m)) StringAssert.DoesNotContain("NATANGGAP", l);
        }

        [Test]
        public void NeverTheSameLineTwiceInARow()
        {
            var t = new DriverTalk(new GameRandom(3));
            foreach (DriverMoment m in System.Enum.GetValues(typeof(DriverMoment)))
            {
                string prev = t.Pick(m);
                for (int i = 0; i < 40; i++) { var s = t.Pick(m); Assert.AreNotEqual(prev, s); prev = s; }
            }
        }

        [Test]
        public void OneLineAtATimeAndWarningsCutIn()
        {
            var t = new DriverTalk(new GameRandom(1));
            Assert.IsNotNull(t.TrySay(DriverMoment.Depart, 0f));
            Assert.IsNull(t.TrySay(DriverMoment.Boarding, 0.5f), "no talking over himself");
            Assert.IsNull(t.TrySay(DriverMoment.PickupDone, 1.0f));
            Assert.IsNotNull(t.TrySay(DriverMoment.NearMiss, 1.1f), "a warning is more important than small talk");
            Assert.IsNull(t.TrySay(DriverMoment.Pothole, 1.5f), "but warnings don't pile up either");
            Assert.IsNotNull(t.TrySay(DriverMoment.Boarding, 1.1f + DriverTalk.LineTime + 0.01f), "after a short pause he can talk again");
        }
    }
}
