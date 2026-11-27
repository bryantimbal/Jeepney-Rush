using JeepneyRush.Logic;
using NUnit.Framework;

namespace JeepneyRush.Tests
{
    public class BarangayTests
    {
        [Test]
        public void Codes()
        {
            Assert.AreEqual("BRGYAB12", Barangays.NormalizeCode("brgy-ab12"));
            Assert.AreEqual("BRGYAB12", Barangays.NormalizeCode(" BRGY AB12 "));
            Assert.AreEqual("BRGYAB12", Barangays.NormalizeCode("ab12"), "just the last four");
            Assert.IsNull(Barangays.NormalizeCode("hello"));
            Assert.IsNull(Barangays.NormalizeCode(""));
        }

        [Test]
        public void TextsAndInvite()
        {
            StringAssert.Contains("3", Barangays.ReasonText("cooldown"));
            StringAssert.Contains("BRGYAB12", Barangays.InviteText("Brgy. San Roque", "BRGYAB12", "ph.jeepneyrush.game"));
            Assert.IsNull(Barangays.ReasonText(null));
        }
    }
}
