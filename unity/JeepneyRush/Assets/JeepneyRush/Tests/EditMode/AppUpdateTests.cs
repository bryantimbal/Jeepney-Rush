using JeepneyRush.Logic;
using NUnit.Framework;

namespace JeepneyRush.Tests
{
    public class AppUpdateTests
    {
        [Test]
        public void Decide()
        {
            Assert.AreEqual(UpdateNeed.None, AppUpdates.Decide(5, 5, 1, 0), "up to date");
            Assert.AreEqual(UpdateNeed.Optional, AppUpdates.Decide(5, 6, 1, 0), "newer version out");
            Assert.AreEqual(UpdateNeed.None, AppUpdates.Decide(5, 6, 1, 6), "MAMAYA NA for this version");
            Assert.AreEqual(UpdateNeed.Optional, AppUpdates.Decide(5, 7, 1, 6), "asked again for an even newer one");
            Assert.AreEqual(UpdateNeed.Required, AppUpdates.Decide(5, 7, 6, 7), "below the minimum: must update, even if dismissed");
            Assert.AreEqual(UpdateNeed.None, AppUpdates.Decide(0, 9, 9, 0), "unknown version never nags");
            Assert.AreEqual(UpdateNeed.None, AppUpdates.Decide(8, 7, 6, 0), "a newer test build than Play");
        }

        [Test]
        public void Texts()
        {
            var v = new AppVersionInfo { latest_code = 7, latest_name = "1.2.0", message = "Bagong Ghost Race!" };
            StringAssert.Contains("1.2.0", AppUpdates.Text(UpdateNeed.Optional, v));
            StringAssert.Contains("Bagong Ghost Race!", AppUpdates.Text(UpdateNeed.Optional, v));
            StringAssert.Contains("KAILANGAN", AppUpdates.Title(UpdateNeed.Required));
        }
    }
}
