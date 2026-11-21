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
        [Tooltip("Project URL, e.g. https://abcdefgh.supabase.co")]
        public string supabaseUrl = "";
        [Tooltip("Project API key 'anon public' (safe to ship). NEVER the service_role key.")]
        [TextArea(2, 4)] public string anonKey = "";
        [Tooltip("Create a guest account automatically on first launch (needs Anonymous sign-ins enabled in Supabase).")]
        public bool autoGuest = true;
        [Tooltip("Seconds before a request is considered failed (game falls back to offline).")]
        public int timeoutSeconds = 12;

        private static OnlineConfig _active;

        public static OnlineConfig Active
        {
            get
            {
                if (_active == null)
                {
                    _active = Resources.Load<OnlineConfig>("OnlineConfig");
                    if (_active == null) { _active = CreateInstance<OnlineConfig>(); _active.name = "OnlineConfig (offline)"; }
                }
                return _active;
            }
        }

        public bool Configured => !string.IsNullOrWhiteSpace(supabaseUrl) && !string.IsNullOrWhiteSpace(anonKey) && supabaseUrl.StartsWith("https://");
        public string BaseUrl => (supabaseUrl ?? "").Trim().TrimEnd('/');
    }
}
