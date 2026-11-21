#if JR_ADMOB
using System;
using GoogleMobileAds.Api;
using UnityEngine;

namespace JeepneyRush
{
    /// <summary>
    /// Google AdMob adapter (Google Mobile Ads Unity plugin v9+). Compiled only when the plugin is
    /// installed and the scripting define JR_ADMOB is set (see docs/MONETIZATION_SETUP.md).
    /// Rewarded ads carry server-side-verification data so rewards are granted by our server.
    /// </summary>
    public sealed class AdMobProvider : IAdsProvider
    {
        private MonetizationConfig _cfg;
        private BannerView _banner;
        private InterstitialAd _inter;
        private RewardedAd _rewarded;
        private bool _bannerVisible;

        public string Name => "AdMob";

        public void Init(MonetizationConfig cfg, Action ready)
        {
            _cfg = cfg;
            MobileAds.RaiseAdEventsOnUnityMainThread = true;
            MobileAds.Initialize(_ =>
            {
                LoadInterstitial();
                LoadRewarded();
                ready?.Invoke();
            });
        }

        public void SetBanner(bool visible)
        {
            _bannerVisible = visible;
            if (visible)
            {
                if (_banner == null)
                {
                    _banner = new BannerView(_cfg.Banner, AdSize.Banner, AdPosition.Bottom);
                    _banner.LoadAd(new AdRequest());
                }
                _banner.Show();
            }
            else _banner?.Hide();
        }

        // ---------------------------------------------------------------- interstitial
        private void LoadInterstitial()
        {
            InterstitialAd.Load(_cfg.Interstitial, new AdRequest(), (ad, error) =>
            {
                if (error != null || ad == null) { Debug.LogWarning("[Ads] interstitial failed to load: " + error); return; }
                _inter = ad;
            });
        }

        public bool InterstitialReady => _inter != null && _inter.CanShowAd();

        public void ShowInterstitial(Action closed)
        {
            var ad = _inter;
            _inter = null;
            if (ad == null) { closed?.Invoke(); return; }
            _banner?.Hide();
            ad.OnAdFullScreenContentClosed += () => { ad.Destroy(); LoadInterstitial(); if (_bannerVisible) _banner?.Show(); closed?.Invoke(); };
            ad.OnAdFullScreenContentFailed += _ => { ad.Destroy(); LoadInterstitial(); closed?.Invoke(); };
            ad.Show();
        }

        // ---------------------------------------------------------------- rewarded
        private void LoadRewarded()
        {
            RewardedAd.Load(_cfg.Rewarded, new AdRequest(), (ad, error) =>
            {
                if (error != null || ad == null) { Debug.LogWarning("[Ads] rewarded failed to load: " + error); return; }
                _rewarded = ad;
            });
        }

        public bool RewardedReady => _rewarded != null && _rewarded.CanShowAd();

        public void ShowRewarded(string userId, string customData, Action<bool> earned)
        {
            var ad = _rewarded;
            _rewarded = null;
            if (ad == null) { earned?.Invoke(false); return; }
            bool got = false;
            ad.SetServerSideVerificationOptions(new ServerSideVerificationOptions.Builder().SetUserId(userId ?? "").SetCustomData(customData ?? "").Build());
            ad.OnAdFullScreenContentClosed += () => { ad.Destroy(); LoadRewarded(); earned?.Invoke(got); };
            ad.OnAdFullScreenContentFailed += _ => { ad.Destroy(); LoadRewarded(); earned?.Invoke(false); };
            ad.Show(_ => got = true);
        }
    }
}
#endif
