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

        [Test]
        public void VibratePermissionIsAddedOnce()
        {
            string xml = "<?xml version=\"1.0\"?>\n<manifest xmlns:android=\"http://schemas.android.com/apk/res/android\" package=\"x\">\n  <application />\n</manifest>";
            string r = ManifestPatch.EnsurePermissions(xml);
            StringAssert.Contains("<uses-permission android:name=\"android.permission.VIBRATE\" />", r);
            Assert.AreEqual(r, ManifestPatch.EnsurePermissions(r), "idempotent");
            Assert.Less(r.IndexOf("android.permission.VIBRATE"), r.IndexOf("<application"), "declared before the application element");
            // already declared (e.g. by Unity): left alone
            string had = xml.Replace("<application />", "<uses-permission android:name=\"android.permission.VIBRATE\"/>\n  <application />");
            Assert.AreEqual(had, ManifestPatch.EnsurePermissions(had));
        }
    }
}
