using System;
using System.Linq;
using JeepneyRush.Logic.Release;
using NUnit.Framework;

namespace JeepneyRush.Tests
{
    public class ReleasePreflightTests
    {
        private static ReleaseFacts Good() => new ReleaseFacts
        {
            ApplicationId = "ph.jeepneyrush.game", VersionName = "1.0.0", VersionCode = 3, LastUploadedVersionCode = 2,
            Il2Cpp = true, Arm64 = true, KeystoreConfigured = true, TargetSdk = 36, MinSdk = 26,
            AdsEnabled = true, UseTestAds = false,
            AdMobAppId = "ca-app-pub-1234567890123456~1234567890",
            BannerId = "ca-app-pub-1234567890123456/1111111111", InterstitialId = "ca-app-pub-1234567890123456/2222222222", RewardedId = "ca-app-pub-1234567890123456/3333333333",
            IapEnabled = true, IapPackageName = "ph.jeepneyrush.game", AdMobSymbol = true, IapSymbol = true,
            SupabaseUrl = "https://abcdefgh.supabase.co", SupabaseKey = "sb_publishable_xyz",
            PrivacyPolicyUrl = "https://example.org/privacy", AccountDeletionUrl = "https://example.org/delete",
        };

        private static string[] Errors(ReleaseFacts f) => ReleasePreflight.Check(f).Where(i => i.Severity == Severity.Error).Select(i => i.Message).ToArray();

        [Test]
        public void AReadyProjectPasses()
        {
            Assert.IsEmpty(ReleasePreflight.Check(Good()));
        }

        [Test]
        public void TestAdsAndSampleIdsBlockTheRelease()
        {
            var f = Good(); f.UseTestAds = true;
            Assert.That(Errors(f), Has.Some.Contains("Test Ads"));
            f = Good(); f.AdMobAppId = "ca-app-pub-3940256099942544~3347511713";
            Assert.That(Errors(f), Has.Some.Contains("sample ID"));
            f = Good(); f.RewardedId = "ca-app-pub-3940256099942544/5224354917";
            Assert.That(Errors(f), Has.Some.Contains("Rewarded"));
            f = Good(); f.BannerId = "";
            Assert.That(Errors(f), Has.Some.Contains("Banner"));
            f = Good(); f.AdsEnabled = false; f.UseTestAds = true; f.BannerId = "";
            Assert.IsEmpty(Errors(f), "ads switched off: ad ids don't matter");
        }

        [Test]
        public void SecretKeysAreCaughtInEveryForm()
        {
            foreach (var key in new[] { "sb_secret_abc", Jwt("service_role") })
            {
                var f = Good(); f.SupabaseKey = key;
                Assert.That(Errors(f), Has.Some.Contains("SECRET"), key);
            }
            var ok = Good(); ok.SupabaseKey = Jwt("anon");
            Assert.IsEmpty(Errors(ok));
            Assert.AreEqual("anon", ReleasePreflight.JwtRole(Jwt("anon")));
            Assert.IsNull(ReleasePreflight.JwtRole("not-a-jwt"));
        }

        [Test]
        public void BuildSettingsAndVersioning()
        {
            var f = Good(); f.Development = true; f.Il2Cpp = false; f.Arm64 = false; f.KeystoreConfigured = false; f.TargetSdk = 33;
            var e = Errors(f);
            Assert.That(e, Has.Some.Contains("Development"));
            Assert.That(e, Has.Some.Contains("IL2CPP"));
            Assert.That(e, Has.Some.Contains("ARM64"));
            Assert.That(e, Has.Some.Contains("keystore"));
            Assert.That(e, Has.Some.Contains("Target API"));
            f = Good(); f.VersionCode = 2;
            Assert.That(Errors(f), Has.Some.Contains("higher than the last upload"));
            f = Good(); f.VersionName = "1.0";
            Assert.That(Errors(f), Has.Some.Contains("1.0.0"));
            Assert.AreEqual(("1.2.4", 8), ReleasePreflight.Bump("1.2.3", 7));
            Assert.AreEqual(("1.0.0", 1), ReleasePreflight.Bump("weird", 0));
        }

        [Test]
        public void StorePolicyLinksAndPackageNames()
        {
            var f = Good(); f.PrivacyPolicyUrl = ""; f.AccountDeletionUrl = "http://insecure";
            var e = Errors(f);
            Assert.That(e, Has.Some.Contains("Privacy policy"));
            Assert.That(e, Has.Some.Contains("Account deletion"));
            f = Good(); f.IapPackageName = "com.jeepneyrush.game";
            Assert.That(Errors(f), Has.Some.Contains("package name"));
            f = Good(); f.ApplicationId = "com.DefaultCompany.JeepneyRush";
            Assert.That(Errors(f), Has.Some.Contains("placeholder"));
            f = Good(); f.ObsoleteFiles = new[] { "Assets/x.png" };
            Assert.IsEmpty(Errors(f), "obsolete files are only a warning");
            Assert.AreEqual(1, ReleasePreflight.Check(f).Count);
        }

        [Test]
        public void MissingSdkDefineSymbolsBlockTheRelease()
        {
            var f = Good(); f.IapSymbol = false; f.AdMobSymbol = false;
            var e = Errors(f);
            Assert.That(e, Has.Some.Contains("JR_UNITY_IAP"));
            Assert.That(e, Has.Some.Contains("JR_ADMOB"));
            f = Good(); f.IapEnabled = false; f.IapSymbol = false; f.AdsEnabled = false; f.AdMobSymbol = false;
            Assert.IsEmpty(Errors(f), "not needed when ads / purchases are switched off");
        }

        private static string Jwt(string role)
        {
            string B64(string s) => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(s)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            return B64("{\"alg\":\"HS256\"}") + "." + B64("{\"iss\":\"supabase\",\"role\":\"" + role + "\"}") + ".sig";
        }
    }
}
