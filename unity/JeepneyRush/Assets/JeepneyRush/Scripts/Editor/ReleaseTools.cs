using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using JeepneyRush.Logic.Release;
using UnityEditor;
using UnityEngine;

namespace JeepneyRush.EditorTools
{
    /// <summary>
    /// Release helpers: preflight checks, version bump, release player settings and the
    /// clean-up of files that older versions of the project left behind.
    /// </summary>
    public static class ReleaseTools
    {
        /// <summary>Files from earlier versions that are no longer used (copying a new version over does not delete them).</summary>
        public static readonly string[] ObsoleteFiles =
        {
            "Assets/JeepneyRush/Resources/Textures/stop_signs.png", // replaced by one texture per stop
        };

        public static string[] ObsoletePresent() => ObsoleteFiles.Where(File.Exists).ToArray();

        // ------------------------------------------------------------------ menus
        [MenuItem("Jeepney Rush/Release/Run Preflight Check", priority = 40)]
        public static void PreflightMenu()
        {
            var issues = ReleasePreflight.Check(GatherFacts(false));
            string report = Report(issues);
            LogEach(issues);
            EditorUtility.DisplayDialog("Release preflight", issues.Count == 0 ? "All checks passed. Ready for a release build." : report, "OK");
        }

        [MenuItem("Jeepney Rush/Release/Bump Version (patch + version code)", priority = 41)]
        public static void BumpVersion()
        {
            var (name, code) = ReleasePreflight.Bump(PlayerSettings.bundleVersion, PlayerSettings.Android.bundleVersionCode);
            PlayerSettings.bundleVersion = name;
            PlayerSettings.Android.bundleVersionCode = code;
            AssetDatabase.SaveAssets();
            Debug.Log($"[Jeepney Rush] Version is now {name} (code {code}).");
        }

        [MenuItem("Jeepney Rush/Release/Mark Current Version As Uploaded", priority = 42)]
        public static void MarkUploaded()
        {
            var cfg = Resources.Load<ReleaseConfig>("ReleaseConfig");
            if (cfg == null) { Debug.LogError("[Jeepney Rush] No Resources/ReleaseConfig.asset – run Jeepney Rush ▸ Setup ▸ Config Assets."); return; }
            cfg.lastUploadedVersionCode = PlayerSettings.Android.bundleVersionCode;
            EditorUtility.SetDirty(cfg);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Jeepney Rush] Recorded version code {cfg.lastUploadedVersionCode} as uploaded. Bump the version before the next release.");
            // once Google Play has published it, tell the game's update check (Supabase ▸ SQL Editor):
            string sql = $"update public.jr_app_release set latest_code = {cfg.lastUploadedVersionCode}, latest_name = '{PlayerSettings.bundleVersion}', message = '', updated_at = now() where id = 1;";
            EditorGUIUtility.systemCopyBuffer = sql;
            Debug.Log("[Jeepney Rush] When the update is live on Google Play, run this in Supabase ▸ SQL Editor (copied to the clipboard):\n" + sql
                + "\nTo force everyone onto it, also set min_code = " + cfg.lastUploadedVersionCode + " (only when old versions must stop).");
        }

        [MenuItem("Jeepney Rush/Release/Remove Obsolete Files", priority = 43)]
        public static void RemoveObsolete()
        {
            var present = ObsoletePresent();
            if (present.Length == 0) { Debug.Log("[Jeepney Rush] No obsolete files found."); return; }
            if (!Application.isBatchMode && !EditorUtility.DisplayDialog("Remove obsolete files", string.Join("\n", present), "Delete", "Cancel")) return;
            foreach (var p in present) AssetDatabase.DeleteAsset(p);
            AssetDatabase.Refresh();
            Debug.Log($"[Jeepney Rush] Removed {present.Length} obsolete file(s).");
        }

        [MenuItem("Jeepney Rush/Release/Apply Release Player Settings", priority = 44)]
        public static void ApplyReleaseSettingsMenu() { ApplyReleaseSettings(); Debug.Log("[Jeepney Rush] Release player settings applied."); }

        // ------------------------------------------------------------------ settings
        /// <summary>
        /// IL2CPP, 32 + 64-bit ARM (Play serves each phone only its own), medium code stripping
        /// (link.xml keeps what reflection needs), quiet logs, and the native symbol file Play
        /// Console uses to make crash reports readable.
        /// </summary>
        public static void ApplyReleaseSettings()
        {
            var android = BuildTargetGroup.Android;
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;
            PlayerSettings.SetManagedStrippingLevel(android, ManagedStrippingLevel.Medium);
            PlayerSettings.stripEngineCode = true;
            PlayerSettings.SetIl2CppCompilerConfiguration(android, Il2CppCompilerConfiguration.Release);
            // no stack traces for plain logs/warnings in release (they are costly); keep them for errors
            PlayerSettings.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
            PlayerSettings.SetStackTraceLogType(LogType.Warning, StackTraceLogType.None);
            PlayerSettings.SetStackTraceLogType(LogType.Error, StackTraceLogType.ScriptOnly);
            PlayerSettings.SetStackTraceLogType(LogType.Exception, StackTraceLogType.ScriptOnly);
            PlayerSettings.SetStackTraceLogType(LogType.Assert, StackTraceLogType.ScriptOnly);
            PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)26;
            EditorUserBuildSettings.development = false;
            EnableSymbols();
            string iconProblem = AppIcons.Apply();
            if (iconProblem != null) Debug.LogWarning("[Jeepney Rush] " + iconProblem);
        }

        /// <summary>symbols.zip for Play Console (Unity 6 API first, older API as fallback; reflection so both compile).</summary>
        private static void EnableSymbols()
        {
            try
            {
                var ubs = Type.GetType("UnityEditor.Android.UserBuildSettings, UnityEditor.Android.Extensions");
                var debugSymbols = ubs?.GetNestedType("DebugSymbols", BindingFlags.Public);
                var level = debugSymbols?.GetProperty("level", BindingFlags.Public | BindingFlags.Static);
                if (level != null)
                {
                    level.SetValue(null, Enum.Parse(level.PropertyType, "SymbolTable"));
                    var format = debugSymbols.GetProperty("format", BindingFlags.Public | BindingFlags.Static);
                    if (format != null) format.SetValue(null, Enum.Parse(format.PropertyType, "Zip"));
                    return;
                }
                var old = typeof(EditorUserBuildSettings).GetProperty("androidCreateSymbols", BindingFlags.Public | BindingFlags.Static);
                if (old != null) old.SetValue(null, Enum.Parse(old.PropertyType, "Public"));
            }
            catch (Exception e) { Debug.LogWarning("[Jeepney Rush] Could not enable the symbols file: " + e.Message); }
        }

        // ------------------------------------------------------------------ facts
        public static ReleaseFacts GatherFacts(bool forBuild)
        {
            var money = Resources.Load<MonetizationConfig>("MonetizationConfig");
            var online = Resources.Load<OnlineConfig>("OnlineConfig");
            var release = Resources.Load<ReleaseConfig>("ReleaseConfig");
            bool keystore = PlayerSettings.Android.useCustomKeystore && !string.IsNullOrEmpty(PlayerSettings.Android.keystoreName)
                            || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("JR_KEYSTORE"));
            return new ReleaseFacts
            {
                ApplicationId = PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android),
                VersionName = PlayerSettings.bundleVersion,
                VersionCode = PlayerSettings.Android.bundleVersionCode,
                LastUploadedVersionCode = release != null ? release.lastUploadedVersionCode : 0,
                Il2Cpp = PlayerSettings.GetScriptingBackend(BuildTargetGroup.Android) == ScriptingImplementation.IL2CPP,
                Arm64 = (PlayerSettings.Android.targetArchitectures & AndroidArchitecture.ARM64) != 0,
                // the release AAB build always switches Development Build off itself, so it isn't a blocker here
                Development = false,
                KeystoreConfigured = keystore,
                TargetSdk = (int)PlayerSettings.Android.targetSdkVersion,
                MinSdk = (int)PlayerSettings.Android.minSdkVersion,
                AdsEnabled = money != null && money.adsEnabled,
                UseTestAds = money == null || money.useTestAds,
                AdMobAppId = money != null ? money.admobAppId : "",
                BannerId = money != null ? money.bannerUnitId : "",
                InterstitialId = money != null ? money.interstitialUnitId : "",
                RewardedId = money != null ? money.rewardedUnitId : "",
                IapEnabled = money != null && money.iapEnabled,
                AdMobSymbol = HasSymbol("JR_ADMOB"),
                IapSymbol = HasSymbol("JR_UNITY_IAP"),
                IapPackageName = money != null ? money.packageName : "",
                SupabaseUrl = online != null ? online.supabaseUrl : "",
                SupabaseKey = online != null ? online.anonKey : "",
                PrivacyPolicyUrl = release != null ? release.privacyPolicyUrl : "",
                AccountDeletionUrl = release != null ? release.accountDeletionUrl : "",
                ObsoleteFiles = ObsoletePresent(),
            };
        }

        /// <summary>One Console entry per issue, so none is cut off in the Console's one-line view.</summary>
        private static void LogEach(System.Collections.Generic.List<Issue> issues)
        {
            int errors = issues.Count(i => i.Severity == Severity.Error);
            Debug.Log($"[Jeepney Rush] Release preflight: {errors} error(s), {issues.Count - errors} warning(s). Details below, one per line.");
            int n = 0;
            foreach (var i in issues.OrderBy(i => i.Severity))
            {
                n++;
                if (i.Severity == Severity.Error) Debug.LogError($"[Preflight {n}/{issues.Count}] {i.Message}");
                else Debug.LogWarning($"[Preflight {n}/{issues.Count}] {i.Message}");
            }
        }

        private static bool HasSymbol(string symbol)
        {
#pragma warning disable 618
            string defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.Android) ?? "";
#pragma warning restore 618
            foreach (var d in defines.Split(';')) if (d.Trim() == symbol) return true;
            return false;
        }

        public static string Report(System.Collections.Generic.List<Issue> issues)
        {
            var sb = new StringBuilder();
            int errors = issues.Count(i => i.Severity == Severity.Error);
            sb.AppendLine($"[Jeepney Rush] Release preflight: {errors} error(s), {issues.Count - errors} warning(s)");
            foreach (var i in issues.OrderBy(i => i.Severity)) sb.AppendLine("  • " + i);
            return sb.ToString();
        }

        /// <summary>Runs before a release build; returns false (and logs why) when the build must not go ahead.</summary>
        public static bool PreflightForBuild()
        {
            ApplyReleaseSettings();
            var issues = ReleasePreflight.Check(GatherFacts(true));
            LogEach(issues);
            return !ReleasePreflight.HasErrors(issues);
        }
    }
}
