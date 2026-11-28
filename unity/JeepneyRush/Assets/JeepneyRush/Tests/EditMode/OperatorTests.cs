using JeepneyRush.Logic;
using NUnit.Framework;

namespace JeepneyRush.Tests
{
    public class OperatorTests
    {
        [Test]
        public void SlotsAndRates()
        {
            Assert.AreEqual(0, Operator.Slots(2, 3), "needs the operator license");
            Assert.AreEqual(1, Operator.Slots(3, 0));
            Assert.AreEqual(2, Operator.Slots(4, 0));
            Assert.AreEqual(5, Operator.Slots(4, 9), "at most 3 bought");
            Assert.AreEqual(3000, Operator.NextSlotPrice(0));
            Assert.AreEqual(0, Operator.NextSlotPrice(3));
            Assert.AreEqual(30, Operator.RatePerHour(JeepneyCatalog.Get("pilipinas")));
            Assert.AreEqual(Operator.MaxRate, Operator.RatePerHour(JeepneyCatalog.Get("pasko")), "capped");
            Assert.Greater(Operator.RatePerHour(JeepneyCatalog.Get("gold")), Operator.RatePerHour(JeepneyCatalog.Get("manila")));
        }

        [Test]
        public void EarningsCapAtEightHours()
        {
            Assert.AreEqual(0, Operator.Pending(60, 1000, 1000));
            Assert.AreEqual(30, Operator.Pending(60, 0, 1800));
            Assert.AreEqual(480, Operator.Pending(60, 0, 3600 * 20), "8 h cap");
            Assert.AreEqual(0, Operator.Pending(60, 5000, 1000), "clock going backwards");
        }
    }
}
