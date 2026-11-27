// API stand-ins for compile-checking only (never shipped). Mirrors the public surface used by
// AdMobProvider.cs (Google Mobile Ads Unity plugin 9.x) and UnityIapProvider.cs (Unity IAP 5.x).
using System;
namespace GoogleMobileAds.Api
{
    public class InitializationStatus { }
    public class AdRequest { }
    public class AdError { public string GetMessage() => ""; }
    public class LoadAdError : AdError { }
    public class Reward { public string Type; public double Amount; }
    public enum AdPosition { Top, Bottom }
    public class AdSize { public static readonly AdSize Banner = new AdSize(); }
    public static class MobileAds
    {
        public static bool RaiseAdEventsOnUnityMainThread { get; set; }
        public static void Initialize(Action<InitializationStatus> done) { }
    }
    public class BannerView
    {
        public BannerView(string adUnitId, AdSize size, AdPosition position) { }
        public void LoadAd(AdRequest r) { }
        public void Show() { }
        public void Hide() { }
        public void Destroy() { }
    }
    public class InterstitialAd
    {
        public static void Load(string adUnitId, AdRequest request, Action<InterstitialAd, LoadAdError> done) { }
        public event Action OnAdFullScreenContentClosed;
        public event Action<AdError> OnAdFullScreenContentFailed;
        public bool CanShowAd() => true;
        public void Show() { }
        public void Destroy() { }
    }
    public class ServerSideVerificationOptions
    {
        public class Builder
        {
            public Builder SetUserId(string id) => this;
            public Builder SetCustomData(string data) => this;
            public ServerSideVerificationOptions Build() => new ServerSideVerificationOptions();
        }
    }
    public class RewardedAd
    {
        public static void Load(string adUnitId, AdRequest request, Action<RewardedAd, LoadAdError> done) { }
        public event Action OnAdFullScreenContentClosed;
        public event Action<AdError> OnAdFullScreenContentFailed;
        public bool CanShowAd() => true;
        public void SetServerSideVerificationOptions(ServerSideVerificationOptions o) { }
        public void Show(Action<Reward> userRewardEarned) { }
        public void Destroy() { }
    }
}
namespace UnityEngine.Purchasing
{
    // Unity IAP 5.x (StoreController API)
    public enum ProductType { Consumable, NonConsumable, Subscription }
    public enum PurchaseFailureReason { PurchasingUnavailable, ExistingPurchasePending, ProductUnavailable, SignatureInvalid, UserCancelled, PaymentDeclined, DuplicateTransaction, Unknown }
    public class ProductDefinition { public ProductDefinition(string id, ProductType type) { this.id = id; } public string id; }
    public class ProductMetadata { public string localizedPriceString; }
    public class Product { public ProductDefinition definition; public bool availableToPurchase; public ProductMetadata metadata; }
    public class CartItem { public Product Product { get; } }
    public interface ICart { System.Collections.Generic.IReadOnlyList<CartItem> Items(); }
    public interface IOrderInfo { string Receipt { get; } string TransactionID { get; } }
    public abstract class Order { public ICart CartOrdered { get; } public IOrderInfo Info { get; } }
    public class PendingOrder : Order { }
    public class FailedOrder : Order { public PurchaseFailureReason FailureReason { get; } public string Details { get; } }
    public class Orders { public System.Collections.Generic.IReadOnlyList<PendingOrder> PendingOrders { get; } }
    public class ProductFetchFailed { }
    public class StoreController
    {
        public event System.Action<System.Collections.Generic.List<Product>> OnProductsFetched;
        public event System.Action<ProductFetchFailed> OnProductsFetchFailed;
        public event System.Action<Orders> OnPurchasesFetched;
        public event System.Action<PendingOrder> OnPurchasePending;
        public event System.Action<FailedOrder> OnPurchaseFailed;
        public System.Threading.Tasks.Task Connect() => System.Threading.Tasks.Task.CompletedTask;
        public void FetchProducts(System.Collections.Generic.List<ProductDefinition> products) { }
        public void FetchPurchases() { }
        public void PurchaseProduct(Product product) { }
        public void ConfirmPurchase(PendingOrder order) { }
    }
    public static class UnityIAPServices { public static StoreController StoreController() => new StoreController(); }
}
