using System;
using JeepneyRush.Logic;
using UnityEngine;
#if JR_NOTIFICATIONS && UNITY_ANDROID && !UNITY_EDITOR
using Unity.Notifications.Android;
#endif

namespace JeepneyRush
{
    /// <summary>
    /// Local reminders (Logic/NotificationPlan): scheduled when the game goes to the background,
    /// cancelled when it comes back. Needs the "Mobile Notifications" package
    /// (com.unity.mobile.notifications) and the scripting define JR_NOTIFICATIONS; without them it does
    /// nothing. Android 13+ asks the player for permission the first time. Settings ▸ Reminders turns it off.
    /// The screens that know the state (streak, fleet, barangay) fill in the static inputs below.
    /// </summary>
    public sealed class NotificationService : MonoBehaviour
    {
        public static bool StreakClaimedToday = true;
        public static int Streak;
        public static DateTime? FleetFullAtUtc;
        public static bool InBarangay;

        private const string Channel = "jr_reminders";
        private bool _ready;

        private void Start()
        {
#if JR_NOTIFICATIONS && UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                AndroidNotificationCenter.Initialize();
                AndroidNotificationCenter.RegisterNotificationChannel(new AndroidNotificationChannel(Channel, "Jeepney Rush", "Daily Challenge, streak and fleet reminders", Importance.Default));
                AndroidNotificationCenter.CancelAllScheduledNotifications();
                _ready = true;
            }
            catch (Exception e) { Debug.LogWarning("[Notify] " + e.Message); }
#endif
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) Schedule(); else Cancel();
        }

        private void OnApplicationQuit() => Schedule();

        private void Schedule()
        {
            if (!_ready || Services.Save?.Data == null || Services.Save.Data.notificationsOff) return;
            var d = Services.Save.Data;
            var now = PhilippineTime.Now;
            // offline the save file knows the streak; online the streak screen filled the inputs
            bool online = Services.Online != null && Services.Online.Configured;
            int today = LoginStreak.DayNumber(now);
            var state = new NotificationPlan.State
            {
                Now = now,
                StreakClaimedToday = online ? StreakClaimedToday : d.streakLast == today,
                Streak = online ? Streak : d.streakCount,
                FleetFullAt = FleetFullAtUtc?.AddHours(8),
                EventActive = EventProgress.Active,
                InBarangay = InBarangay,
            };
            var plan = NotificationPlan.Plan(state);
#if JR_NOTIFICATIONS && UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                AndroidNotificationCenter.CancelAllScheduledNotifications();
                var offset = DateTime.Now - DateTime.UtcNow.AddHours(8); // PH time -> this phone's local time
                foreach (var n in plan)
                {
                    var note = new AndroidNotification(n.Title, n.Text, n.At + offset);
                    AndroidNotificationCenter.SendNotificationWithExplicitID(note, Channel, n.Id);
                }
            }
            catch (Exception e) { Debug.LogWarning("[Notify] " + e.Message); }
#endif
        }

        private void Cancel()
        {
#if JR_NOTIFICATIONS && UNITY_ANDROID && !UNITY_EDITOR
            if (!_ready) return;
            try { AndroidNotificationCenter.CancelAllScheduledNotifications(); AndroidNotificationCenter.CancelAllDisplayedNotifications(); }
            catch (Exception e) { Debug.LogWarning("[Notify] " + e.Message); }
#endif
        }

        /// <summary>Android 13+: ask once (from the main menu, after the first run) whether reminders may be shown.</summary>
        public static void AskPermissionOnce()
        {
#if JR_NOTIFICATIONS && UNITY_ANDROID && !UNITY_EDITOR
            var d = Services.Save.Data;
            if (d.notificationsAsked || d.notificationsOff) return;
            d.notificationsAsked = true;
            Services.Save.Save();
            try { new PermissionRequest(); } catch (Exception e) { Debug.LogWarning("[Notify] " + e.Message); }
#endif
        }
    }
}
