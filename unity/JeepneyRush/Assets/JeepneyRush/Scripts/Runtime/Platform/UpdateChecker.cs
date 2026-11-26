using System.Collections;
using JeepneyRush.Logic;
using UnityEngine;

namespace JeepneyRush
{
    /// <summary>
    /// Detects a new version on Google Play (once per app start, at the main menu):
    ///  1. Google Play In-App Updates (when the plugin is installed and JR_PLAY_UPDATES is defined,
    ///     see docs/APP_UPDATES.md): Play itself says whether a newer version exists. An optional
    ///     update downloads in the background while you play, then I-RESTART installs it; a required
    ///     one shows Play's full-screen update.
    ///  2. Otherwise (editor, plugin not installed, not installed from Play): the server's
    ///     jr_app_version (newest + oldest allowed version code) and a button to the Play Store page.
    /// The server's minimum version always applies (a too-old app must update).
    /// </summary>
    public sealed class UpdateChecker : MonoBehaviour
    {
        private bool _checked;
        public UpdateNeed Need { get; private set; }
        public AppVersionInfo Server { get; private set; }

        /// <summary>This build's version code (Android) – 0 when unknown.</summary>
        public static int CurrentCode
        {
            get
            {
#if UNITY_EDITOR
                return UnityEditor.PlayerSettings.Android.bundleVersionCode;
#elif UNITY_ANDROID
                try
                {
                    using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                    using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                    using var pm = activity.Call<AndroidJavaObject>("getPackageManager");
                    using var info = pm.Call<AndroidJavaObject>("getPackageInfo", activity.Call<string>("getPackageName"), 0);
                    return info.Get<int>("versionCode");
                }
                catch (System.Exception e) { Debug.LogWarning("[Update] version code: " + e.Message); return 0; }
#else
                return 0;
#endif
            }
        }

        public static string StoreUrl => "market://details?id=" + Application.identifier;
        public static string WebStoreUrl => "https://play.google.com/store/apps/details?id=" + Application.identifier;

        public void CheckOnce()
        {
            if (_checked) return;
            _checked = true;
            StartCoroutine(Check());
        }

        private IEnumerator Check()
        {
            yield return new WaitForSecondsRealtime(1.5f); // let the menu settle first
            int current = CurrentCode;
            // the server's minimum version (and the backup "latest")
            AppVersionInfo server = null;
            bool serverDone = false;
            if (Services.Online != null) Services.Online.AppVersion(v => { server = v; serverDone = true; });
            else serverDone = true;
            float wait = 8f;
            while (!serverDone && (wait -= Time.unscaledDeltaTime) > 0) yield return null;
            Server = server;
            var d = Services.Save.Data;
            bool required = server != null && AppUpdates.Decide(current, server.latest_code, server.min_code, d.updateDismissedCode) == UpdateNeed.Required;
#if JR_PLAY_UPDATES && UNITY_ANDROID && !UNITY_EDITOR
            // Google Play knows best: ask it first
            bool handled = false;
            yield return PlayAppUpdate.Run(required, d.updateDismissedCode, h => handled = h);
            if (handled) yield break;
#endif
            if (server == null) yield break;
            Need = AppUpdates.Decide(current, server.latest_code, server.min_code, d.updateDismissedCode);
            if (Need == UpdateNeed.None) yield break;
            Services.UI?.ShowUpdate(Need, server, later: () =>
            {
                d.updateDismissedCode = server.latest_code;
                Services.Save.Save();
            });
        }

        /// <summary>Opens the game's Play Store page.</summary>
        public static void OpenStore()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            Application.OpenURL(StoreUrl);
#else
            Application.OpenURL(WebStoreUrl);
#endif
        }
    }
}
