using System;
using JeepneyRush.Logic;
using UnityEngine;

namespace JeepneyRush
{
    /// <summary>What a store adapter must do (Unity IAP / Google Play in builds, simulated in development).</summary>
    public interface IStoreProvider
    {
        string Name { get; }
        void Init(ProductDef[] products, Action<bool> ready);
        bool Ready { get; }
        /// <summary>Localized price from the store, or null.</summary>
        string Price(string productId);
        /// <param name="accountId">signed-in player id, attached to the Google purchase (anti-fraud)</param>
        void Purchase(string productId, string accountId, Action<bool, string, string> done); // ok, purchase token, error
        /// <summary>Called after the server granted the purchase (consumes / acknowledges on the device).</summary>
        void Finish(string productId);
        /// <summary>Purchases the store delivers on its own (e.g. completed while the app was closed).</summary>
        event Action<string, string> PendingPurchase; // productId, token
    }

    /// <summary>
    /// In-app purchases. Every real purchase is verified by the server (Google Play check in the
    /// verify-purchase Edge Function) before anything is granted; the device only finishes the
    /// transaction after the server said yes, so nothing is lost if the app dies in between.
    /// </summary>
    public sealed class StoreService : MonoBehaviour
    {
        private IStoreProvider _p;
        public bool Available => _p != null && _p.Ready;
        public bool Simulated { get; private set; }
        public string ProviderName => _p?.Name ?? "none";

        private bool _initRan;

        public void Init()
        {
            _initRan = true;
            var cfg = MonetizationConfig.Active;
            if (!cfg.iapEnabled) return;
#if JR_UNITY_IAP && UNITY_ANDROID && !UNITY_EDITOR
            _p = new UnityIapProvider();
#else
            if (cfg.SimulationAllowed) { _p = new SimulatedStoreProvider(); Simulated = true; }
#endif
            if (_p == null) { Debug.Log("[Store] no store (install Unity IAP + define JR_UNITY_IAP for Google Play billing)"); return; }
            _p.PendingPurchase += (id, token) => Verify(id, token, (ok, msg) => Debug.Log($"[Store] delivered pending {id}: {ok} {msg}"));
            _p.Init(Products.All, ok => Debug.Log($"[Store] {(ok ? "ready" : "unavailable")} ({_p.Name})"));
        }

        public string PriceOf(ProductDef p) => _p?.Price(p.Id) ?? p.FallbackPrice;

        /// <summary>Why buying isn't possible right now (null = it is).</summary>
        public string Blocker()
        {
            if (!_initRan) Bootstrap.StartServices(); // safety net: the shop was opened before startup finished
            if (_p == null) return WhyNoStore();
            if (!_p.Ready) return "Connecting to Google Play…";
            var o = Services.Online;
            if (o != null && o.Configured && !o.Authoritative) return "Go online and sign in (ACCOUNT) to buy.";
            if ((o == null || !o.Configured) && !Simulated) return "Purchases need the online server (see docs/SUPABASE_SETUP.md).";
            return null;
        }

        /// <summary>Says exactly why there is no store, so a tester knows what to change.</summary>
        private string WhyNoStore()
        {
            var cfg = MonetizationConfig.Active;
            if (!_initRan) return "The store didn't start. Check the Console for an error.";
            if (!cfg.iapEnabled) return "Purchases are turned off (MonetizationConfig ▸ IAP Enabled).";
#if JR_UNITY_IAP
            if (!Application.isEditor && Application.platform != RuntimePlatform.Android) return "The store works on Android only.";
#endif
            if (Application.isEditor || Debug.isDebugBuild)
                return "TEST store is off (MonetizationConfig ▸ Simulate In Development).";
#if JR_UNITY_IAP
            return "The store isn't set up in this build yet.";
#else
            return "Google Play billing isn't in this build. Add JR_UNITY_IAP to Scripting Define Symbols (with Unity In-App Purchasing installed) and rebuild.";
#endif
        }

        public void Buy(string productId, Action<bool, string> done)
        {
            string why = Blocker();
            if (why != null) { done?.Invoke(false, why); return; }
            var o = Services.Online;
            _p.Purchase(productId, o?.UserId, (ok, token, err) =>
            {
                if (!ok) { done?.Invoke(false, err ?? "Purchase cancelled."); return; }
                Verify(productId, token, done);
            });
        }

        private void Verify(string productId, string token, Action<bool, string> done)
        {
            var o = Services.Online;
            if (o != null && o.Configured)
            {
                o.VerifyPurchase(productId, token, (ok, msg) =>
                {
                    if (ok) { _p.Finish(productId); Services.Audio?.Play(Logic.Audio.Sfx.Purchase); }
                    done?.Invoke(ok, msg);
                });
                return;
            }
            // offline development build with the simulated store only
            if (Simulated)
            {
                Progress.GrantProductLocally(Services.Save.Data, Products.Get(productId));
                Services.Save.Save();
                _p.Finish(productId);
                Services.Audio?.Play(Logic.Audio.Sfx.Purchase);
                done?.Invoke(true, null);
                return;
            }
            done?.Invoke(false, "Purchases need the online server.");
        }

        /// <summary>Restores purchases tied to the account (and re-checks the store).</summary>
        public void Restore(Action<bool> done)
        {
            var o = Services.Online;
            if (o != null && o.SignedIn) o.Refresh(done);
            else done?.Invoke(false);
        }
    }

    /// <summary>Development store: a TEST PURCHASE dialog, tokens start with "TEST-" (refused by the server unless allowed).</summary>
    public sealed class SimulatedStoreProvider : IStoreProvider
    {
        public string Name => "simulated test store";
        public event Action<string, string> PendingPurchase { add { } remove { } }
        public bool Ready { get; private set; }
        public void Init(ProductDef[] products, Action<bool> ready) { Ready = true; ready?.Invoke(true); }
        public string Price(string productId) => null;
        public void Purchase(string productId, string accountId, Action<bool, string, string> done) =>
            Services.UI.ShowTestPurchase(Products.Get(productId), ok => done(ok, ok ? "TEST-" + Guid.NewGuid().ToString("N") : null, ok ? null : "Purchase cancelled."));
        public void Finish(string productId) { }
    }
}
