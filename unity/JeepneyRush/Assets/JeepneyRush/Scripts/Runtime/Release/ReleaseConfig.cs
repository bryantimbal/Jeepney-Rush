using UnityEngine;

namespace JeepneyRush
{
    /// <summary>
    /// Store-policy links shown in the game and checked by the release preflight
    /// (Resources/ReleaseConfig.asset, created by Jeepney Rush ▸ Setup ▸ Config Assets).
    /// </summary>
    [CreateAssetMenu(menuName = "Jeepney Rush/Release Config", fileName = "ReleaseConfig")]
    public sealed class ReleaseConfig : ScriptableObject
    {
        [Tooltip("Public privacy policy page (https://…). Also paste it in Play Console ▸ App content.")]
        public string privacyPolicyUrl = "";
        [Tooltip("Public web page explaining how to delete an account and its data (Play Console ▸ Data safety).")]
        public string accountDeletionUrl = "";
        [Tooltip("Contact e-mail shown in Settings ▸ About (optional).")]
        public string supportEmail = "";
        [Tooltip("Version code of the last build uploaded to Google Play – the preflight makes sure the next one is higher.")]
        public int lastUploadedVersionCode = 0;

        private static ReleaseConfig _active;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _active = null;

        public static ReleaseConfig Active
        {
            get
            {
                if (_active == null)
                {
                    _active = Resources.Load<ReleaseConfig>("ReleaseConfig");
                    if (_active == null) { _active = CreateInstance<ReleaseConfig>(); _active.name = "ReleaseConfig (empty)"; }
                }
                return _active;
            }
        }
    }
}
