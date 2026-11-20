using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace JeepneyRush.EditorTools
{
    /// <summary>
    /// Android builds from the menu or the command line:
    ///   Unity -batchmode -quit -projectPath unity/JeepneyRush -executeMethod JeepneyRush.EditorTools.BuildTools.BuildApk
    ///   Unity -batchmode -quit -projectPath unity/JeepneyRush -executeMethod JeepneyRush.EditorTools.BuildTools.BuildAab
    /// Release signing: set the keystore in Project Settings ▸ Player ▸ Publishing Settings, or pass
    /// environment variables JR_KEYSTORE, JR_KEYSTORE_PASS, JR_KEY_ALIAS, JR_KEY_PASS (never commit them).
    /// </summary>
    public static class BuildTools
    {
        private static string[] EnabledScenes() => EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();

        [MenuItem("Jeepney Rush/Build/Android APK (development)", priority = 20)]
        public static void BuildApk() => Build(false, true);

        [MenuItem("Jeepney Rush/Build/Android AAB (release, Google Play)", priority = 21)]
        public static void BuildAab() => Build(true, false);

        private static void Build(bool aab, bool development)
        {
            if (EnabledScenes().Length == 0) ProjectSetup.CreateScenes();
            ApplySigningFromEnvironment();
            EditorUserBuildSettings.buildAppBundle = aab;
            Directory.CreateDirectory("Builds");
            string version = PlayerSettings.bundleVersion;
            string path = $"Builds/JeepneyRush-{version}{(development ? "-dev" : "")}.{(aab ? "aab" : "apk")}";
            var options = new BuildPlayerOptions
            {
                scenes = EnabledScenes(),
                locationPathName = path,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = development ? BuildOptions.Development : BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(options);
            var s = report.summary;
            Debug.Log($"[Jeepney Rush] Build {s.result}: {path} ({s.totalSize / (1024f * 1024f):0.0} MB, {s.totalErrors} errors, {s.totalTime})");
            if (Application.isBatchMode && s.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }

        private static void ApplySigningFromEnvironment()
        {
            string ks = Environment.GetEnvironmentVariable("JR_KEYSTORE");
            if (string.IsNullOrEmpty(ks)) return;
            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = ks;
            PlayerSettings.Android.keystorePass = Environment.GetEnvironmentVariable("JR_KEYSTORE_PASS");
            PlayerSettings.Android.keyaliasName = Environment.GetEnvironmentVariable("JR_KEY_ALIAS");
            PlayerSettings.Android.keyaliasPass = Environment.GetEnvironmentVariable("JR_KEY_PASS");
        }
    }
}
