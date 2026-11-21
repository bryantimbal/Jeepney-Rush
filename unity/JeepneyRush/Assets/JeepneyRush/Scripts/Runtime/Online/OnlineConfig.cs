using UnityEngine;

namespace JeepneyRush
{
    /// <summary>
    /// Supabase connection settings (Resources/OnlineConfig.asset, created by
    /// Jeepney Rush ▸ Setup Project). Leave the URL empty to keep the game fully offline.
    /// Only the PUBLIC anon key belongs here – never the service-role key.
    /// </summary>
    [CreateAssetMenu(menuName = "Jeepney Rush/Online Config", fileName = "OnlineConfig")]
    public sealed class OnlineConfig : ScriptableObject
    {
        [Tooltip("Project URL, e.g. https://abcdefgh.supabase.co (no /rest/v1 needed)")]
        public string supabaseUrl = "";
        [Tooltip("Public project key: Publishable key (sb_publishable_…) or legacy 'anon public' key (eyJ…). Safe to ship. NEVER the secret / service_role key.")]
        [TextArea(2, 4)] public string anonKey = "";
        [Tooltip("Create a guest account automatically on first launch (needs Anonymous sign-ins enabled in Supabase).")]
        public bool autoGuest = true;
        [Tooltip("Seconds before a request is considered failed (game falls back to offline).")]
        public int timeoutSeconds = 12;

        private static OnlineConfig _active;
        private static bool _fromAsset;

        // With "Enter Play Mode Options" (no domain reload) statics survive between plays: forget the
        // cached config so values pasted after the first Play are picked up.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { _active = null; _fromAsset = false; }

        public static OnlineConfig Active
        {
            get
            {
                if (_active == null)
                {
                    _active = Resources.Load<OnlineConfig>("OnlineConfig");
                    _fromAsset = _active != null;
                    if (_active == null) { _active = CreateInstance<OnlineConfig>(); _active.name = "OnlineConfig (offline)"; }
                }
                return _active;
            }
        }

        public bool Configured => !string.IsNullOrWhiteSpace(supabaseUrl) && !string.IsNullOrWhiteSpace(anonKey) && supabaseUrl.Trim().StartsWith("https://") && !IsSecretKey;

        /// <summary>Why online is off (shown on the ACCOUNT screen), or null when configured.</summary>
        public string Problem
        {
            get
            {
                if (!_fromAsset) return "No OnlineConfig asset found at Assets/JeepneyRush/Resources/OnlineConfig.asset – run Jeepney Rush ▸ Setup Project (it must be inside a folder named Resources).";
                if (string.IsNullOrWhiteSpace(supabaseUrl)) return "OnlineConfig: Supabase Url is empty.";
                if (!supabaseUrl.Trim().StartsWith("https://")) return $"OnlineConfig: Supabase Url must start with https:// (now: \"{supabaseUrl.Trim()}\").";
                if (string.IsNullOrWhiteSpace(anonKey)) return "OnlineConfig: Anon Key is empty – paste the Publishable key or the legacy anon public key.";
                if (IsSecretKey) return "OnlineConfig: that is a SECRET key – use the Publishable / anon public key instead.";
                return null;
            }
        }

        /// <summary>Refuse to run with a secret key pasted by mistake (it would bypass all security).</summary>
        public bool IsSecretKey => (anonKey ?? "").Trim().StartsWith("sb_secret_") || (anonKey ?? "").Contains("service_role");
        /// <summary>
        /// Project URL without any path – tolerates a pasted ".../rest/v1/" or ".../auth/v1".
        /// </summary>
        public string BaseUrl
        {
            get
            {
                string u = (supabaseUrl ?? "").Trim().TrimEnd('/');
                foreach (var suffix in new[] { "/rest/v1", "/auth/v1" })
                    if (u.EndsWith(suffix, System.StringComparison.OrdinalIgnoreCase)) u = u.Substring(0, u.Length - suffix.Length).TrimEnd('/');
                return u;
            }
        }
    }
}
