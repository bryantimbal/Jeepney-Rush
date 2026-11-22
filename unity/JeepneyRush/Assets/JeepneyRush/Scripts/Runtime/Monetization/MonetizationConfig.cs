using UnityEngine;

namespace JeepneyRush
{
    /// <summary>
    /// Ads & in-app purchase settings (Resources/MonetizationConfig.asset, created by Setup Project).
    /// Ad unit ids default to Google's official TEST ids – keep them until the game is approved,
    /// then paste your own and untick "Use Test Ads" for the release build.
    /// </summary>
    [CreateAssetMenu(menuName = "Jeepney Rush/Monetization Config", fileName = "MonetizationConfig")]
    public sealed class MonetizationConfig : ScriptableObject
    {
        // Google's documented test ids (safe to click; never earn money; never get you banned)
        public const string TestAppId = "ca-app-pub-3940256099942544~3347511713";
        public const string TestBanner = "ca-app-pub-3940256099942544/6300978111";
        public const string TestInterstitial = "ca-app-pub-3940256099942544/1033173712";
        public const string TestRewarded = "ca-app-pub-3940256099942544/5224354917";

        [Header("Ads (AdMob)")]
        public bool adsEnabled = true;
        [Tooltip("Always use Google's test ad units (development). Untick only for the store release.")]
        public bool useTestAds = true;
        [Tooltip("AdMob App ID (also set in Assets > Google Mobile Ads > Settings).")]
        public string admobAppId = TestAppId;
        public string bannerUnitId = TestBanner;
        public string interstitialUnitId = TestInterstitial;
        public string rewardedUnitId = TestRewarded;

        [Header("In-app purchases (Google Play)")]
        public bool iapEnabled = true;
        [Tooltip("Play Console package name, e.g. com.yourstudio.jeepneyrush")]
        public string packageName = "ph.jeepneyrush.game"; // must equal the application id (Player Settings) and the server secret PLAY_PACKAGE_NAME

        [Header("Development")]
        [Tooltip("Without the AdMob / Unity IAP packages (or in the Editor), show simulated TEST ads and TEST purchases. Never in release builds.")]
        public bool simulateInDevelopment = true;

        public string Banner => useTestAds ? TestBanner : bannerUnitId;
        public string Interstitial => useTestAds ? TestInterstitial : interstitialUnitId;
        public string Rewarded => useTestAds ? TestRewarded : rewardedUnitId;

        /// <summary>Simulation is allowed only in the Editor and development builds.</summary>
        public bool SimulationAllowed => simulateInDevelopment && (Application.isEditor || Debug.isDebugBuild);

        private static MonetizationConfig _active;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _active = null;

        public static MonetizationConfig Active
        {
            get
            {
                if (_active == null)
                {
                    _active = Resources.Load<MonetizationConfig>("MonetizationConfig");
                    if (_active == null) { _active = CreateInstance<MonetizationConfig>(); _active.name = "MonetizationConfig (defaults)"; }
                }
                return _active;
            }
        }
    }
}
