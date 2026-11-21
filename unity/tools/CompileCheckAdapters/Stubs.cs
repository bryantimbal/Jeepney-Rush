// API stand-ins for compile-checking only (never shipped). Mirrors the public surface used by
// AdMobProvider.cs (Google Mobile Ads Unity plugin 9.x) and UnityIapProvider.cs (Unity IAP 4.x).
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
namespace UnityEngine.Purchasing.Extension
{
    public interface IPurchasingModule { }
    public class PurchaseFailureDescription { public UnityEngine.Purchasing.PurchaseFailureReason reason; public string message; public string productId; }
}
namespace UnityEngine.Purchasing
{
    using UnityEngine.Purchasing.Extension;
    public enum ProductType { Consumable, NonConsumable, Subscription }
    public enum PurchaseProcessingResult { Complete, Pending }
    public enum InitializationFailureReason { PurchasingUnavailable, NoProductsAvailable, AppNotKnown }
    public enum PurchaseFailureReason { PurchasingUnavailable, ExistingPurchasePending, ProductUnavailable, SignatureInvalid, UserCancelled, PaymentDeclined, DuplicateTransaction, Unknown }
    public class ProductDefinition { public string id; }
    public class ProductMetadata { public string localizedPriceString; }
    public class Product { public ProductDefinition definition; public bool availableToPurchase; public string transactionID; public string receipt; public ProductMetadata metadata; }
    public class ProductCollection { public Product WithID(string id) => null; }
    public interface IStoreController { ProductCollection products { get; } void InitiatePurchase(Product p); void ConfirmPendingPurchase(Product p); }
    public interface IStoreExtension { }
    public interface IExtensionProvider { T GetExtension<T>() where T : IStoreExtension; }
    public interface IGooglePlayStoreExtensions : IStoreExtension { void SetObfuscatedAccountId(string id); }
    public class PurchaseEventArgs { public Product purchasedProduct; }
    public interface IStoreListener
    {
        void OnInitializeFailed(InitializationFailureReason error);
        void OnInitializeFailed(InitializationFailureReason error, string message);
        PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs e);
        void OnPurchaseFailed(Product product, PurchaseFailureReason reason);
        void OnInitialized(IStoreController controller, IExtensionProvider extensions);
    }
    public interface IDetailedStoreListener : IStoreListener { void OnPurchaseFailed(Product product, PurchaseFailureDescription description); }
    public class StandardPurchasingModule : IPurchasingModule { public static StandardPurchasingModule Instance() => new StandardPurchasingModule(); }
    public class ConfigurationBuilder
    {
        public static ConfigurationBuilder Instance(IPurchasingModule m) => new ConfigurationBuilder();
        public ConfigurationBuilder AddProduct(string id, ProductType type) => this;
    }
    public static class UnityPurchasing { public static void Initialize(IDetailedStoreListener listener, ConfigurationBuilder builder) { } }
}
