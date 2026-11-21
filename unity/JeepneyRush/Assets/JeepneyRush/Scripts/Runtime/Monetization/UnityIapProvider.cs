#if JR_UNITY_IAP
using System;
using System.Collections.Generic;
using JeepneyRush.Logic;
using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Extension;

namespace JeepneyRush
{
    /// <summary>
    /// Google Play Billing through Unity IAP 4.x. Compiled only when the package is installed and
    /// the scripting define JR_UNITY_IAP is set. Purchases stay "pending" on the device until our
    /// server verified and granted them (then <see cref="Finish"/> confirms / consumes them).
    /// </summary>
    public sealed class UnityIapProvider : IStoreProvider, IDetailedStoreListener
    {
        private IStoreController _store;
        private IGooglePlayStoreExtensions _google;
        private Action<bool> _ready;
        private readonly Dictionary<string, Action<bool, string, string>> _waiting = new Dictionary<string, Action<bool, string, string>>();

        public string Name => "Google Play (Unity IAP)";
        public bool Ready => _store != null;
        public event Action<string, string> PendingPurchase;

        public void Init(ProductDef[] products, Action<bool> ready)
        {
            _ready = ready;
            var builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance());
            foreach (var p in products) builder.AddProduct(p.Id, p.Kind == ProductKind.Consumable ? ProductType.Consumable : ProductType.NonConsumable);
            UnityPurchasing.Initialize(this, builder);
        }

        public void OnInitialized(IStoreController controller, IExtensionProvider extensions)
        {
            _store = controller;
            _google = extensions.GetExtension<IGooglePlayStoreExtensions>();
            _ready?.Invoke(true);
        }

        public void OnInitializeFailed(InitializationFailureReason error) { Debug.LogWarning("[Store] init failed: " + error); _ready?.Invoke(false); }
        public void OnInitializeFailed(InitializationFailureReason error, string message) { Debug.LogWarning($"[Store] init failed: {error} {message}"); _ready?.Invoke(false); }

        public string Price(string productId) => _store?.products.WithID(productId)?.metadata?.localizedPriceString;

        public void Purchase(string productId, string accountId, Action<bool, string, string> done)
        {
            var product = _store?.products.WithID(productId);
            if (product == null || !product.availableToPurchase) { done(false, null, "Not available right now."); return; }
            if (!string.IsNullOrEmpty(accountId)) _google?.SetObfuscatedAccountId(accountId); // server checks it matches
            _waiting[productId] = done;
            _store.InitiatePurchase(product);
        }

        public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs e)
        {
            var p = e.purchasedProduct;
            string token = p.transactionID; // Google Play purchase token
            if (_waiting.TryGetValue(p.definition.id, out var cb)) { _waiting.Remove(p.definition.id); cb(true, token, null); }
            else PendingPurchase?.Invoke(p.definition.id, token); // delivered at start-up / after a crash
            return PurchaseProcessingResult.Pending; // finished in Finish() after the server granted it
        }

        public void Finish(string productId)
        {
            var product = _store?.products.WithID(productId);
            if (product != null) _store.ConfirmPendingPurchase(product);
        }

        public void OnPurchaseFailed(Product product, PurchaseFailureReason reason) => Fail(product, reason.ToString());
        public void OnPurchaseFailed(Product product, PurchaseFailureDescription description) => Fail(product, description.reason + " " + description.message);

        private void Fail(Product product, string why)
        {
            if (product != null && _waiting.TryGetValue(product.definition.id, out var cb)) { _waiting.Remove(product.definition.id); cb(false, null, why.StartsWith("UserCancelled") ? "Purchase cancelled." : why); }
        }
    }
}
#endif
