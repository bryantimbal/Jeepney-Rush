using JeepneyRush.Logic.Release;
using NUnit.Framework;

namespace JeepneyRush.Tests
{
    public class ManifestPatchTests
    {
        [Test]
        public void PortraitScreenBecomesOptional()
        {
            const string xml = "<manifest>\n  <uses-feature android:glEsVersion=\"0x00030000\" />\n" +
                "  <uses-feature android:name=\"android.hardware.screen.portrait\" />\n" +
                "  <uses-feature android:name=\"android.hardware.touchscreen\" android:required=\"true\"/>\n" +
                "  <uses-feature android:name=\"android.hardware.vulkan.version\" android:required=\"true\" />\n</manifest>";
            string r = ManifestPatch.MakeFeaturesOptional(xml);
            StringAssert.Contains("<uses-feature android:name=\"android.hardware.screen.portrait\" android:required=\"false\" />", r);
            StringAssert.Contains("<uses-feature android:name=\"android.hardware.touchscreen\" android:required=\"false\"/>", r);
            StringAssert.Contains("android.hardware.vulkan.version\" android:required=\"true\"", r); // others untouched
            StringAssert.Contains("android:glEsVersion=\"0x00030000\" />", r);
            Assert.AreEqual(r, ManifestPatch.MakeFeaturesOptional(r), "idempotent");
        }
    }
}
