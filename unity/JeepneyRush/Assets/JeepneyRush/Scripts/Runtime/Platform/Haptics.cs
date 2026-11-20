using UnityEngine;

namespace JeepneyRush
{
    /// <summary>Short vibrations on Android (respecting the Vibration setting).</summary>
    public sealed class Haptics : MonoBehaviour
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        private AndroidJavaObject _vibrator;
        private int _sdk;
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
            }
            catch (System.Exception e) { Debug.LogWarning("[Haptics] unavailable: " + e.Message); }
#endif
            GameEvents.Hit += (_, __) => Pulse(60);
            GameEvents.PassengersServed += r => { if (r.Boarded > 0) Pulse(20); };
            GameEvents.Landed += () => Pulse(12);
        }

        public static void Pulse(long ms)
        {
            if (Services.Save == null || !Services.Save.Data.vibration) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            var self = _instance;
            if (self == null || self._vibrator == null) return;
            try
            {
                if (self._sdk >= 26)
                {
                    using (var effectClass = new AndroidJavaClass("android.os.VibrationEffect"))
                    using (var effect = effectClass.CallStatic<AndroidJavaObject>("createOneShot", ms, -1))
                        self._vibrator.Call("vibrate", effect);
                }
                else self._vibrator.Call("vibrate", ms);
            }
            catch (System.Exception) { /* ignore: device without vibrator */ }
#endif
        }
    }
}
