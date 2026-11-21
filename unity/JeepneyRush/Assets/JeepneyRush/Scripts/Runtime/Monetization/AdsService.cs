using System;
using JeepneyRush.Logic;
using UnityEngine;

namespace JeepneyRush
{
    /// <summary>What an ad network adapter must do (AdMob in builds, simulated in development).</summary>
    public interface IAdsProvider
    {
        string Name { get; }
        void Init(MonetizationConfig cfg, Action ready);
        void SetBanner(bool visible);
        bool InterstitialReady { get; }
        void ShowInterstitial(Action closed);
        bool RewardedReady { get; }
        /// <param name="userId">signed-in player (sent to Google's server-side verification)</param>
        /// <param name="customData">"kind:ref:nonce", signed by Google and checked by our server</param>
        void ShowRewarded(string userId, string customData, Action<bool> earned);
    }

    /// <summary>
    /// Ads with the design brief's rules (see <see cref="AdPolicy"/>): banner only on menus,
    /// a capped interstitial only BETWEEN runs, rewarded ads only when the player taps for them,
    /// nothing at all during gameplay, and no banner/interstitial after buying "No Ads".
    /// </summary>
    public sealed class AdsService : MonoBehaviour
    {
        private IAdsProvider _p;
        private MonetizationConfig _cfg;
        private bool _bannerWanted;
        public bool Available => _p != null;
        public bool Simulated { get; private set; }
        public string ProviderName => _p?.Name ?? "none";

        public void Init()
        {
            _cfg = MonetizationConfig.Active;
            if (!_cfg.adsEnabled) return;
#if JR_ADMOB && UNITY_ANDROID && !UNITY_EDITOR
            _p = new AdMobProvider();
#else
            if (_cfg.SimulationAllowed) { _p = new SimulatedAdsProvider(); Simulated = true; }
#endif
            if (_p == null) { Debug.Log("[Ads] no ad provider (install Google Mobile Ads + define JR_ADMOB for real ads)"); return; }
            _p.Init(_cfg, () => { Debug.Log($"[Ads] ready ({_p.Name})"); ApplyBanner(); });
        }

        private static AdState State => Services.Save.Data.ads;
        private static bool AdFree => Services.Save.Data.adFree;

        /// <summary>Menus call this with true, gameplay with false.</summary>
        public void ShowBannerOnMenus(bool menus)
        {
            _bannerWanted = menus;
            ApplyBanner();
        }

        private void ApplyBanner() => _p?.SetBanner(_bannerWanted && AdPolicy.BannerAllowed(AdFree, inGameplay: false));

        /// <summary>A run just ended (counts toward the interstitial cap).</summary>
        public void RunEnded()
        {
            AdPolicy.RunEnded(State);
            Services.Save.Save();
        }

        /// <summary>Leaving the Game Over screen: maybe show an interstitial, then continue.</summary>
        public void BetweenRuns(float lastRunSeconds, Action then)
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (_p == null || !_p.InterstitialReady || !AdPolicy.InterstitialDue(State, AdFree, now, lastRunSeconds)) { then?.Invoke(); return; }
            AdPolicy.InterstitialShown(State, now);
            Services.Save.Save();
            _p.ShowInterstitial(() => then?.Invoke());
        }

        public bool RewardedReady => _p != null && _p.RewardedReady;

        /// <summary>
        /// Shows a rewarded ad the player asked for. On completion returns the claim id: for real
        /// ads the server receives Google's signed callback under that id; simulated ads return a
        /// "test-…" id (accepted by the server only when test rewards are switched on).
        /// </summary>
        public void ShowRewarded(RewardedKind kind, string reference, Action<bool, string> done)
        {
            if (!RewardedReady) { done?.Invoke(false, null); return; }
            string nonce = Simulated ? AdPolicy.TestNonce(kind, reference) : Guid.NewGuid().ToString("N");
            string data = Simulated ? nonce : AdPolicy.RewardData(kind, reference, nonce);
            Services.Audio?.StopEngine();
            AudioListener.pause = true; // ads have their own sound
            _p.ShowRewarded(Services.Online?.UserId ?? "", data, earned =>
            {
                AudioListener.pause = false;
                done?.Invoke(earned, earned ? nonce : null);
            });
        }
    }

    /// <summary>Development ads drawn by the game's own UI (clearly labelled TEST AD). Never in release builds.</summary>
    public sealed class SimulatedAdsProvider : IAdsProvider
    {
        public string Name => "simulated test ads";
        public void Init(MonetizationConfig cfg, Action ready) => ready?.Invoke();
        public void SetBanner(bool visible) => Services.UI?.SetTestBanner(visible);
        public bool InterstitialReady => true;
        public void ShowInterstitial(Action closed) => Services.UI.ShowTestAd(false, ok => closed?.Invoke());
        public bool RewardedReady => true;
        public void ShowRewarded(string userId, string customData, Action<bool> earned) => Services.UI.ShowTestAd(true, earned);
    }
}
