using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace JeepneyRush.Logic.Release
{
    /// <summary>Everything the preflight looks at, gathered by the editor from PlayerSettings and the configs.</summary>
    public sealed class ReleaseFacts
    {
        public string ApplicationId = "", VersionName = "";
        public int VersionCode;
        /// <summary>Version code of the last build uploaded to Play (0 = never uploaded).</summary>
        public int LastUploadedVersionCode;
        public bool Il2Cpp, Arm64, Development, KeystoreConfigured;
        /// <summary>0 = "Automatic (highest installed)".</summary>
        public int TargetSdk;
        public int MinSdk;
        public bool AdsEnabled, UseTestAds;
        public string AdMobAppId = "", BannerId = "", InterstitialId = "", RewardedId = "";
        public bool IapEnabled;
        /// <summary>Scripting define symbols that switch on the real SDK adapters (JR_ADMOB / JR_UNITY_IAP).</summary>
        public bool AdMobSymbol, IapSymbol;
        public string IapPackageName = "";
        public string SupabaseUrl = "", SupabaseKey = "";
        public string PrivacyPolicyUrl = "", AccountDeletionUrl = "";
        public string[] ObsoleteFiles = Array.Empty<string>();
    }

    public enum Severity { Error, Warning }

    public readonly struct Issue
    {
        public readonly Severity Severity;
        public readonly string Message;
        public Issue(Severity s, string m) { Severity = s; Message = m; }
        public override string ToString() => (Severity == Severity.Error ? "ERROR: " : "warning: ") + Message;
    }

    /// <summary>
    /// Release preflight: catches the mistakes that get a Play upload rejected, ship test ads
    /// or leak a secret key. Errors block the release build; warnings are printed.
    /// </summary>
    public static class ReleasePreflight
    {
        /// <summary>Lowest target API Google Play accepts for new apps and updates (check Play Console each year).</summary>
        public const int PlayMinTargetSdk = 35;
        /// <summary>Target API Play will require next; below it is a warning.</summary>
        public const int PlayNextTargetSdk = 36;
        public const string TestAdMobPublisher = "ca-app-pub-3940256099942544";

        private static readonly Regex AppIdRx = new Regex(@"^[a-zA-Z][a-zA-Z0-9_]*(\.[a-zA-Z][a-zA-Z0-9_]*)+$");
        private static readonly Regex VersionRx = new Regex(@"^\d+\.\d+\.\d+$");
        private static readonly Regex AdMobAppRx = new Regex(@"^ca-app-pub-\d{16}~\d{10}$");
        private static readonly Regex AdMobUnitRx = new Regex(@"^ca-app-pub-\d{16}/\d{10}$");

        public static List<Issue> Check(ReleaseFacts f)
        {
            var list = new List<Issue>();
            void Err(string m) => list.Add(new Issue(Severity.Error, m));
            void Warn(string m) => list.Add(new Issue(Severity.Warning, m));

            // ---- identity & version
            if (!AppIdRx.IsMatch(f.ApplicationId ?? "")) Err($"Application id '{f.ApplicationId}' is not a valid Android package name.");
            else if (f.ApplicationId.StartsWith("com.example") || f.ApplicationId.StartsWith("com.unity") || f.ApplicationId.StartsWith("com.DefaultCompany"))
                Err($"Application id '{f.ApplicationId}' is a template placeholder.");
            else if (f.LastUploadedVersionCode == 0)
                Warn($"First upload: the application id '{f.ApplicationId}' can NEVER be changed after it is on Google Play.");
            if (!VersionRx.IsMatch(f.VersionName ?? "")) Err($"Version '{f.VersionName}' should look like 1.0.0.");
            if (f.VersionCode < 1) Err("Bundle version code must be at least 1.");
            else if (f.LastUploadedVersionCode > 0 && f.VersionCode <= f.LastUploadedVersionCode)
                Err($"Version code {f.VersionCode} must be higher than the last upload ({f.LastUploadedVersionCode}). Use Jeepney Rush ▸ Release ▸ Bump Version.");

            // ---- build type
            if (f.Development) Err("Development Build is on: release builds must not be development builds.");
            if (!f.Il2Cpp) Err("Scripting backend must be IL2CPP (Mono can't build ARM64).");
            if (!f.Arm64) Err("ARM64 must be enabled: Google Play requires 64-bit.");
            if (!f.KeystoreConfigured) Err("No release keystore: set it in Player ▸ Publishing Settings or the JR_KEYSTORE* environment variables.");
            if (f.TargetSdk == 0) Warn($"Target API is 'Automatic (highest installed)': make sure Android SDK {PlayNextTargetSdk} (at least {PlayMinTargetSdk}) is installed.");
            else if (f.TargetSdk < PlayMinTargetSdk) Err($"Target API {f.TargetSdk} is too low: Google Play requires {PlayMinTargetSdk}+.");
            else if (f.TargetSdk < PlayNextTargetSdk) Warn($"Target API {f.TargetSdk}: Google Play will soon require {PlayNextTargetSdk}.");

            // ---- ads
            if (f.AdsEnabled)
            {
                if (f.UseTestAds) Err("Monetization: 'Use Test Ads' is on – a release would show Google test ads and earn nothing.");
                if ((f.AdMobAppId ?? "").StartsWith(TestAdMobPublisher)) Err("Monetization: AdMob App ID is Google's sample ID – use your own from AdMob.");
                else if (!AdMobAppRx.IsMatch(f.AdMobAppId ?? "")) Err($"Monetization: AdMob App ID '{f.AdMobAppId}' should look like ca-app-pub-0000000000000000~0000000000.");
                foreach (var (name, id) in new[] { ("Banner", f.BannerId), ("Interstitial", f.InterstitialId), ("Rewarded", f.RewardedId) })
                {
                    if ((id ?? "").StartsWith(TestAdMobPublisher)) Err($"Monetization: {name} unit is a Google test unit.");
                    else if (!AdMobUnitRx.IsMatch(id ?? "")) Err($"Monetization: {name} unit id '{id}' should look like ca-app-pub-0000000000000000/0000000000.");
                }
            }
            if (f.AdsEnabled && !f.AdMobSymbol)
                Err("Monetization: JR_ADMOB is missing from Player ▸ Android ▸ Scripting Define Symbols – the build would show no ads at all.");
            if (f.IapEnabled && !f.IapSymbol)
                Err("Monetization: JR_UNITY_IAP is missing from Player ▸ Android ▸ Scripting Define Symbols – the shop would say the store isn't set up.");
            if (f.IapEnabled && !string.Equals(f.IapPackageName, f.ApplicationId, StringComparison.Ordinal))
                Err($"Monetization: package name '{f.IapPackageName}' differs from the application id '{f.ApplicationId}' (purchase verification would fail). Set both, and the server secret PLAY_PACKAGE_NAME, to the same value.");

            // ---- online
            string key = (f.SupabaseKey ?? "").Trim();
            if (key.StartsWith("sb_secret_") || key.Contains("service_role") || JwtRole(key) == "service_role")
                Err("Online: the Supabase key is a SECRET / service_role key. Never ship it – use the publishable (anon) key.");
            if (string.IsNullOrWhiteSpace(f.SupabaseUrl)) Warn("Online: no Supabase URL – leaderboards, accounts and server-verified purchases are off in this build.");
            else if (!f.SupabaseUrl.Trim().StartsWith("https://")) Err("Online: Supabase URL must start with https://");

            // ---- store policy
            bool collectsData = f.AdsEnabled || f.IapEnabled || !string.IsNullOrWhiteSpace(f.SupabaseUrl);
            if (collectsData && !IsHttps(f.PrivacyPolicyUrl)) Err("Privacy policy URL is missing (Release Config): required by Google Play for apps with ads, purchases or accounts.");
            if (!string.IsNullOrWhiteSpace(f.SupabaseUrl) && !IsHttps(f.AccountDeletionUrl))
                Err("Account deletion web page URL is missing (Release Config): Google Play requires one for apps with accounts.");

            // ---- project hygiene
            foreach (var path in f.ObsoleteFiles ?? Array.Empty<string>())
                Warn($"Obsolete file still in the project (adds to the download size): {path}. Use Jeepney Rush ▸ Release ▸ Remove Obsolete Files.");
            return list;
        }

        public static bool HasErrors(IEnumerable<Issue> issues)
        {
            foreach (var i in issues) if (i.Severity == Severity.Error) return true;
            return false;
        }

        private static bool IsHttps(string url) => !string.IsNullOrWhiteSpace(url) && url.Trim().StartsWith("https://") && url.Trim().Length > 12;

        /// <summary>Role claim of a legacy JWT key ("anon" / "service_role"), or null.</summary>
        public static string JwtRole(string jwt)
        {
            try
            {
                var parts = (jwt ?? "").Split('.');
                if (parts.Length != 3) return null;
                string p = parts[1].Replace('-', '+').Replace('_', '/');
                p = p.PadRight(p.Length + (4 - p.Length % 4) % 4, '=');
                string json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(p));
                var m = Regex.Match(json, "\"role\"\\s*:\\s*\"([^\"]+)\"");
                return m.Success ? m.Groups[1].Value : null;
            }
            catch (FormatException) { return null; }
        }

        /// <summary>Next version: patch bump ("1.2.3" → "1.2.4") and code + 1.</summary>
        public static (string name, int code) Bump(string versionName, int versionCode)
        {
            var m = Regex.Match(versionName ?? "", @"^(\d+)\.(\d+)\.(\d+)$");
            string name = m.Success ? $"{m.Groups[1].Value}.{m.Groups[2].Value}.{int.Parse(m.Groups[3].Value) + 1}" : "1.0.0";
            return (name, Math.Max(1, versionCode + 1));
        }
    }
}
