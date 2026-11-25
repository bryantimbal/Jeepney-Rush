using UnityEngine;

namespace JeepneyRush
{
    /// <summary>
    /// Short vibrations on Android (respecting the Vibration setting).
    /// Needs android.permission.VIBRATE: the build adds it (AndroidManifestFixes) – without it
    /// Android refuses every vibration. Very short pulses are lengthened so they can be felt.
    /// </summary>
    public sealed class Haptics : MonoBehaviour
    {
        /// <summary>Shorter pulses can't be felt on many phones.</summary>
        public const long MinPulseMs = 28;
#if UNITY_ANDROID && !UNITY_EDITOR
        private AndroidJavaObject _vibrator;
        private int _sdk;
        private static bool _warned;
#endif

        private static Haptics _instance;

        private void Awake()
        {
            _instance = this;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unity.GetStatic<AndroidJavaObject>("currentActivity"))
                    _vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                using (var version = new AndroidJavaClass("android.os.Build$VERSION")) _sdk = version.GetStatic<int>("SDK_INT");
                if (_vibrator != null && !_vibrator.Call<bool>("hasVibrator")) { Debug.Log("[Haptics] this device has no vibration motor"); _vibrator = null; }
            }
            catch (System.Exception e) { Debug.LogWarning("[Haptics] unavailable: " + e.Message); _vibrator = null; }
#endif
            GameEvents.Hit += (_, __) => Pulse(70);
            GameEvents.PassengersServed += r => { if (r.Boarded > 0) Pulse(30); };
            GameEvents.Landed += () => Pulse(30);
        }

        public static void Pulse(long ms)
        {
            if (Services.Save == null || !Services.Save.Data.vibration) return;
            if (Application.platform != RuntimePlatform.Android) return;
            ms = System.Math.Max(MinPulseMs, ms);
#if UNITY_ANDROID && !UNITY_EDITOR
            var self = _instance;
            if (self != null && self._vibrator != null)
            {
                try
                {
                    if (self._sdk >= 26)
                    {
                        using (var effectClass = new AndroidJavaClass("android.os.VibrationEffect"))
                        using (var effect = effectClass.CallStatic<AndroidJavaObject>("createOneShot", ms, -1)) // -1 = the phone's default strength
                            self._vibrator.Call("vibrate", effect);
                    }
                    else self._vibrator.Call("vibrate", ms);
                    return;
                }
                catch (System.Exception e)
                {
                    // e.g. SecurityException when the VIBRATE permission is missing: say so once, then fall back
                    if (!_warned) { _warned = true; Debug.LogWarning("[Haptics] vibrate failed: " + e.Message); }
                }
            }
#endif
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            // fallback (and it makes Unity add the VIBRATE permission on its own): a standard buzz for strong events
            if (ms >= 60) Handheld.Vibrate();
#endif
        }
    }
}
