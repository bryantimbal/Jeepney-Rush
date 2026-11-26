#if JR_PLAY_UPDATES && UNITY_ANDROID && !UNITY_EDITOR
using System;
using System.Collections;
using Google.Play.AppUpdate;
using Google.Play.Common;
using UnityEngine;

namespace JeepneyRush
{
    /// <summary>
    /// Google Play In-App Updates (Google's "Play In-App Update" Unity plugin, com.google.play.appupdate).
    /// Compiled only with the scripting define JR_PLAY_UPDATES (see docs/APP_UPDATES.md).
    /// Required (server minimum) or high-priority (Play Console priority 4-5) updates use Play's
    /// full-screen IMMEDIATE flow; others ask first, then download FLEXIBLY in the background and
    /// offer I-RESTART when ready.
    /// </summary>
    public static class PlayAppUpdate
    {
        private static AppUpdateManager _mgr;

        /// <param name="done">true if Play handled it (an update was found and offered), false to fall back to the server check.</param>
        public static IEnumerator Run(bool required, int dismissedCode, Action<bool> done)
        {
            _mgr ??= new AppUpdateManager();
            var infoOp = _mgr.GetAppUpdateInfo();
            yield return infoOp;
            if (!infoOp.IsSuccessful) { Debug.Log("[Update] Play check failed: " + infoOp.Error); done(false); yield break; }
            var info = infoOp.GetResult();
            if (info.UpdateAvailability == UpdateAvailability.DeveloperTriggeredUpdateInProgress)
            {
                // an immediate update was interrupted: resume it
                yield return _mgr.StartUpdate(info, AppUpdateOptions.ImmediateAppUpdateOptions());
                done(true); yield break;
            }
            if (info.UpdateAvailability != UpdateAvailability.UpdateAvailable) { done(false); yield break; }
            bool immediate = required || info.UpdatePriority >= 4;
            var immediateOpts = AppUpdateOptions.ImmediateAppUpdateOptions();
            if (immediate && info.IsUpdateTypeAllowed(immediateOpts))
            {
                var op = _mgr.StartUpdate(info, immediateOpts);
                yield return op;
                done(true); yield break;
            }
            if (info.AvailableVersionCode <= dismissedCode) { done(true); yield break; } // MAMAYA NA was said for this one
            var flexOpts = AppUpdateOptions.FlexibleAppUpdateOptions();
            if (!info.IsUpdateTypeAllowed(flexOpts)) { done(false); yield break; }
            bool? answer = null;
            Services.UI?.ShowUpdate(Logic.UpdateNeed.Optional, new Logic.AppVersionInfo { latest_code = info.AvailableVersionCode },
                later: () => { answer = false; Services.Save.Data.updateDismissedCode = info.AvailableVersionCode; Services.Save.Save(); },
                update: () => answer = true);
            while (answer == null) yield return null;
            done(true);
            if (answer == false) yield break;
            var flex = _mgr.StartUpdate(info, flexOpts);
            while (!flex.IsDone) yield return null; // downloads in the background while you play
            if (flex.Status == AppUpdateStatus.Downloaded)
                Services.UI?.ShowUpdateReady(() => { _mgr.CompleteUpdate(); });
        }
    }
}
#endif
