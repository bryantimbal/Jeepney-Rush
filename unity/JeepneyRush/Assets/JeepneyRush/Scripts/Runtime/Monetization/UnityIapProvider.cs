#if JR_UNITY_IAP
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using JeepneyRush.Logic;
using UnityEngine;
using UnityEngine.Purchasing;

namespace JeepneyRush
{
    /// <summary>
    /// Google Play Billing through Unity IAP 5.x (StoreController API). Compiled only when the
    /// package is installed and the scripting define JR_UNITY_IAP is set. Purchases stay "pending"
    /// on the device until our server verified and granted them; <see cref="Finish"/> then confirms
    /// (consumes / acknowledges) them, so nothing is lost if the app dies in between.
    /// </summary>
    public sealed class UnityIapProvider : IStoreProvider
    {
        private StoreController _store;
        private Action<bool> _ready;
        private bool _readyCalled;
        private readonly Dictionary<string, Product> _products = new Dictionary<string, Product>();
        private readonly Dictionary<string, Action<bool, string, string>> _waiting = new Dictionary<string, Action<bool, string, string>>();
        /// <summary>Orders waiting for the server's OK, by product id (confirmed in Finish).</summary>
        private readonly Dictionary<string, PendingOrder> _pending = new Dictionary<string, PendingOrder>();
        private readonly HashSet<string> _reported = new HashSet<string>();

        public string Name => "Google Play (Unity IAP 5)";
        public bool Ready { get; private set; }
        public event Action<string, string> PendingPurchase;

        public void Init(ProductDef[] products, Action<bool> ready)
        {
            _ready = ready;
            _store = UnityIAPServices.StoreController();
            _store.OnProductsFetched += OnProductsFetched;
            _store.OnProductsFetchFailed += f => { Debug.LogWarning("[Store] products not fetched: " + f); Done(false); };
            _store.OnPurchasesFetched += OnPurchasesFetched;
            _store.OnPurchasePending += OnPurchasePending;
            _store.OnPurchaseFailed += OnPurchaseFailed;
            Connect(products);
        }

        private async void Connect(ProductDef[] products)
        {
            try
            {
                await _store.Connect();
                var defs = new List<ProductDefinition>();
                foreach (var p in products) defs.Add(new ProductDefinition(p.Id, p.Kind == ProductKind.Consumable ? ProductType.Consumable : ProductType.NonConsumable));
                _store.FetchProducts(defs);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Store] could not connect to Google Play: " + e.Message);
                Done(false);
            }
        }

        private void Done(bool ok)
        {
            Ready = ok;
            if (_readyCalled) return;
            _readyCalled = true;
            _ready?.Invoke(ok);
        }

        private void OnProductsFetched(List<Product> products)
        {
            foreach (var p in products) if (p?.definition != null) _products[p.definition.id] = p;
            Done(true);
            _store.FetchPurchases(); // purchases finished while the app was closed come back here
        }

        private void OnPurchasesFetched(Orders orders)
        {
            if (orders?.PendingOrders == null) return;
            foreach (var o in orders.PendingOrders) OnPurchasePending(o);
        }

        public string Price(string productId) => _products.TryGetValue(productId, out var p) ? p.metadata?.localizedPriceString : null;

        public void Purchase(string productId, string accountId, Action<bool, string, string> done)
        {
            // accountId: IAP 5 sets no obfuscated account id here; the server only rejects a purchase tagged with someone else's id.
            if (!Ready || !_products.TryGetValue(productId, out var product) || !product.availableToPurchase) { done(false, null, "Not available right now."); return; }
            _waiting[productId] = done;
            _store.PurchaseProduct(product);
        }

        private void OnPurchasePending(PendingOrder order)
        {
            string id = ProductId(order?.CartOrdered);
            if (id == null) return;
            string token = Token(order.Info);
            _pending[id] = order;
            if (_waiting.TryGetValue(id, out var cb)) { _waiting.Remove(id); _reported.Add(token); cb(true, token, null); }
            else if (_reported.Add(token)) PendingPurchase?.Invoke(id, token); // delivered at start-up / after a crash
        }

        public void Finish(string productId)
        {
            if (_pending.TryGetValue(productId, out var order)) { _pending.Remove(productId); _store.ConfirmPurchase(order); }
        }

        private void OnPurchaseFailed(FailedOrder order)
        {
            string id = ProductId(order?.CartOrdered);
            if (id == null || !_waiting.TryGetValue(id, out var cb)) return;
            _waiting.Remove(id);
            cb(false, null, order.FailureReason == PurchaseFailureReason.UserCancelled ? "Purchase cancelled." : $"{order.FailureReason} {order.Details}".Trim());
        }

        private static string ProductId(ICart cart)
        {
            if (cart == null) return null;
            foreach (var item in cart.Items()) if (item?.Product?.definition != null) return item.Product.definition.id;
            return null;
        }

        private static readonly Regex TokenRx = new Regex("\\\\*\"purchaseToken\\\\*\"\\s*:\\s*\\\\*\"([^\"\\\\]+)");

        /// <summary>The Google Play purchase token (what the server verifies).</summary>
        private static string Token(IOrderInfo info)
        {
            if (info == null) return null;
            // On Google Play the transaction id is the purchase token; the receipt holds it too.
            var m = string.IsNullOrEmpty(info.Receipt) ? null : TokenRx.Match(info.Receipt);
            if (m != null && m.Success) return m.Groups[1].Value;
            return info.TransactionID;
        }
    }
}
#endif
