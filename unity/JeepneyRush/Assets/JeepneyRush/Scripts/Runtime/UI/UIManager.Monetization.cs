using System;
using JeepneyRush.Logic;
using UnityEngine;
using UnityEngine.UIElements;

namespace JeepneyRush
{
    /// <summary>
    /// Phase 4 screens: SHOP (coins, gems, packs, No Ads, restore), gem prices and ad trials in
    /// the garage, the daily ad bonus, the double-coins button on Game Over, the revive offer,
    /// and the clearly-labelled TEST ad / TEST purchase screens used in development builds.
    /// </summary>
    public sealed partial class UIManager
    {
        private VisualElement _shop, _shopList, _testAd, _testBanner, _revive;
        private Label _shopGems, _menuGems, _testAdLabel, _reviveCount;
        private Button _testAdClose, _goDouble;
        private Button _garageGems, _garageTrial;
        private Action<bool> _testAdDone;

        // ------------------------------------------------------------------ shared bits
        /// <summary>Small purple diamond used as the gem icon (no texture needed).</summary>
        private static VisualElement GemIcon(float size)
        {
            var wrap = UiKit.Box();
            wrap.style.width = wrap.style.height = size;
            wrap.style.alignItems = Align.Center;
            wrap.style.justifyContent = Justify.Center;
            var g = UiKit.Box();
            g.style.width = g.style.height = size * 0.62f;
            g.style.backgroundColor = UiKit.Hex("#B565F2");
            UiKit.Border(g.style, 4, UiKit.Hex("#5B1E99"), 6);
            g.style.rotate = new Rotate(45);
            wrap.Add(g);
            return wrap;
        }

        private VisualElement BuildMenuGems()
        {
            _menuGems = UiKit.Text("0", 40, Color.white);
            var pill = UiKit.Pill(GemIcon(54), _menuGems);
            pill.style.marginTop = 10;
            pill.pickingMode = PickingMode.Position;
            pill.RegisterCallback<ClickEvent>(_ => OpenShop());
            return pill;
        }

        private void RefreshGems()
        {
            var d = Services.Save.Data;
            if (_menuGems != null) _menuGems.text = d.gems.ToString("N0");
            if (_shopGems != null) _shopGems.text = $"{d.gems:N0} gems · {d.coins:N0} coins";
        }

        // ------------------------------------------------------------------ SHOP
        private void BuildShop()
        {
            _shop = ModalShell("shop", "SHOP", out var card);
            _shopGems = UiKit.Text("", 36, UiKit.Ink);
            card.Add(_shopGems);
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.maxHeight = 1150;
            _shopList = UiKit.Box();
            scroll.Add(_shopList);
            card.Add(scroll);
            card.Add(UiKit.Button("RESTORE PURCHASES", () => Services.Store.Restore(ok => { Toast(ok ? "Purchases restored from your account." : "Sign in to the account you bought with (ACCOUNT)."); RefreshShop(); }), Color.white, UiKit.Ink, 34, 96));
            card.Add(UiKit.Button("BACK", CloseModal, Color.white, UiKit.Ink, 46, 120));
        }

        private void OpenShop()
        {
            RefreshShop();
            if (_modals.Count == 0 || _modals.Peek() != _shop) OpenModal(_shop);
        }

        private void RefreshShop()
        {
            RefreshGems();
            _shopList.Clear();
            var store = Services.Store;
            string blocker = store.Blocker();
            if (store.Simulated) _shopList.Add(Note("TEST STORE – purchases here are simulated (development build). Nothing is charged.", 28, UiKit.Red));
            if (blocker != null) _shopList.Add(Note(blocker, 30, UiKit.Red));
            var d = Services.Save.Data;
            void Section(string title) => _shopList.Add(Heading(title));
            void Row(ProductDef p)
            {
                bool owned = p.Kind == ProductKind.NonConsumable && d.ownedProducts.Contains(p.Id) || (p.AdFree && d.adFree);
                var row = UiKit.Box();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.style.backgroundColor = Color.white;
                UiKit.Border(row.style, 4, UiKit.Hex("#E2DBC6"), 24);
                UiKit.Pad(row.style, 10, 16);
                row.style.marginBottom = 10;
                var icon = p.Gems > 0 ? GemIcon(80) : p.Coins > 0 && p.Jeepneys.Length == 0 ? (VisualElement)UiKit.Icon("ui_coin", 80) : UiKit.Icon(p.AdFree ? "ui_pause" : "ui_star", 80);
                row.Add(icon);
                var col = UiKit.Box();
                col.style.flexGrow = 1;
                col.style.flexShrink = 1;
                col.style.marginLeft = 14;
                var t = UiKit.Text(p.Title.ToUpperInvariant(), 34, UiKit.Ink);
                t.style.unityTextAlign = TextAnchor.MiddleLeft;
                col.Add(t);
                var desc = UiKit.Text(p.Description, 26, UiKit.Grey, false);
                desc.style.unityTextAlign = TextAnchor.MiddleLeft;
                desc.style.whiteSpace = WhiteSpace.Normal;
                col.Add(desc);
                row.Add(col);
                var id = p.Id;
                var buy = UiKit.Button(owned ? "OWNED" : store.PriceOf(p), () =>
                {
                    store.Buy(id, (ok, msg) => { Toast(ok ? "Salamat! Purchase complete." : msg); RefreshShop(); RefreshMenuTop(); });
                }, owned ? UiKit.Hex("#B9B2C6") : UiKit.Yellow, UiKit.Ink, 34, 96);
                buy.style.width = 210;
                buy.style.marginTop = buy.style.marginBottom = 0;
                buy.SetEnabled(!owned && blocker == null);
                buy.style.opacity = buy.enabledSelf ? 1f : 0.6f;
                row.Add(buy);
                _shopList.Add(row);
            }
            Section("COINS");
            foreach (var p in Products.All) if (p.Coins > 0 && p.Gems == 0 && p.Jeepneys.Length == 0) Row(p);
            Section("GEMS");
            _shopList.Add(Note("Gems buy premium jeepneys in the GARAGE.", 26));
            foreach (var p in Products.All) if (p.Gems > 0) Row(p);
            Section("JEEPNEY PACKS");
            foreach (var p in Products.All) if (p.Jeepneys.Length > 0) Row(p);
            Section("NO ADS");
            foreach (var p in Products.All) if (p.AdFree) Row(p);
            _shopList.Add(Note("Purchases are checked with Google Play by our server before they are added to your account. Sign in with an email (ACCOUNT) to keep them on a new phone.", 24));
        }

        // ------------------------------------------------------------------ garage: gems & ad trial
        private void BuildGarageMonetization(VisualElement page)
        {
            var row = UiKit.Box();
            row.style.flexDirection = FlexDirection.Row;
            _garageGems = UiKit.Button("GEMS", GarageBuyWithGems, UiKit.Hex("#D9B8FA"), UiKit.Ink, 36, 100);
            _garageGems.style.flexGrow = 1;
            _garageGems.style.marginRight = 10;
            _garageTrial = UiKit.Button("TRY 1 RUN · AD", GarageTrial, Color.white, UiKit.Ink, 32, 100);
            _garageTrial.style.flexGrow = 1;
            row.Add(_garageGems);
            row.Add(_garageTrial);
            page.Add(row);
        }

        private void RefreshGarageMonetization(JeepneyLivery j)
        {
            if (_garageGems == null) return;
            var d = Services.Save.Data;
            bool owned = d.Owns(j.Id);
            Show(_garageGems, !owned && j.GemPrice > 0);
            _garageGems.text = $"BUY · {j.GemPrice:N0} GEMS";
            _garageGems.SetEnabled(d.gems >= j.GemPrice);
            _garageGems.style.opacity = _garageGems.enabledSelf ? 1f : 0.55f;
            bool trial = !owned && Services.Ads.RewardedReady && AdPolicy.CanTrial(d.ads, PhilippineTime.DayKey, j.Id, owned);
            Show(_garageTrial, trial);
        }

        private void GarageBuyWithGems()
        {
            var j = JeepneyCatalog.All[_garageIndex];
            var d = Services.Save.Data;
            if (UseServer())
            {
                Services.Online.BuyJeepneyWithGems(j.Id, (ok, msg) =>
                {
                    if (!ok) { Toast(msg); RefreshGarage(); return; }
                    Services.Save.Data.selectedJeepney = j.Id;
                    Services.Save.Save();
                    Services.Audio.Play(Logic.Audio.Sfx.Purchase);
                    RefreshGarage();
                });
                return;
            }
            if (Services.Online != null && Services.Online.SignedIn) { Toast("Kailangan ng internet para bumili."); return; }
            if (!Progress.BuyJeepneyWithGems(d, j.Id)) return;
            d.selectedJeepney = j.Id;
            Services.Save.Save();
            Services.Audio.Play(Logic.Audio.Sfx.Purchase);
            RefreshGarage();
        }

        private void GarageTrial()
        {
            var j = JeepneyCatalog.All[_garageIndex];
            Services.Ads.ShowRewarded(RewardedKind.Trial, j.Id, (earned, nonce) =>
            {
                if (!earned) { Toast("Ad not finished – no trial."); return; }
                AdPolicy.TrialUsed(Services.Save.Data.ads, PhilippineTime.DayKey, j.Id);
                Services.Save.Save();
                GameplaySession.TrialJeepney = j.Id;
                Toast($"{j.DisplayName}: yours for the next run!");
                CloseModal();
                Services.Game.Menu?.SetGarageMode(false);
                Services.Game.Play();
            });
        }

        // ------------------------------------------------------------------ missions: daily ad bonus
        private VisualElement DailyBonusRow()
        {
            var d = Services.Save.Data;
            bool online = Services.Online != null && Services.Online.Configured;
            bool available = AdPolicy.DailyBonusAvailable(d.ads, PhilippineTime.DayKey) && Services.Ads.RewardedReady && (!online || Services.Online.Authoritative);
            if (!available) return null;
            return ProgressRow("DAILY BONUS", $"Watch a short ad for +{AdPolicy.DailyBonusCoins} coins (once a day, optional)", 1f, "", true, false, () =>
            {
                Services.Ads.ShowRewarded(RewardedKind.DailyBonus, "", (earned, nonce) =>
                {
                    if (!earned) { Toast("Ad not finished – no bonus."); return; }
                    d.ads.dailyBonusDayKey = PhilippineTime.DayKey;
                    if (UseServer())
                        Services.Online.ClaimAdReward(nonce, (ok, msg, reward) => { Toast(ok ? $"+{reward} COINS!" : msg); RefreshMissions(); RefreshMenuTop(); });
                    else { d.coins += AdPolicy.DailyBonusCoins; Toast($"+{AdPolicy.DailyBonusCoins} COINS!"); }
                    Services.Audio.Play(Logic.Audio.Sfx.Purchase);
                    Services.Save.Save();
                    RefreshMissions();
                });
            });
        }

        // ------------------------------------------------------------------ game over: double coins
        private void BuildDoubleCoins(VisualElement card)
        {
            _goDouble = UiKit.Button("x2 COINS · WATCH AD", DoubleCoins, UiKit.Hex("#D9B8FA"), UiKit.Ink, 40, 110);
            card.Add(_goDouble);
        }

        private void PrepareDoubleCoins(RunResult r)
        {
            if (_goDouble == null) return;
            var s = Services.Game.Session;
            bool online = Services.Online != null && Services.Online.Configured;
            bool can = s != null && AdPolicy.CanDoubleCoins(r.Coins, s.CoinsDoubled) && Services.Ads.RewardedReady && (!online || Services.Online.SignedIn);
            Show(_goDouble, can);
            _goDouble.text = $"x2 COINS (+{r.Coins:N0}) · WATCH AD";
            _goDouble.SetEnabled(true);
        }

        private void DoubleCoins()
        {
            var s = Services.Game.Session;
            if (s == null || s.CoinsDoubled) return;
            int coins = s.LastRunCoins;
            string runId = s.LastRunId;
            _goDouble.SetEnabled(false);
            Services.Ads.ShowRewarded(RewardedKind.DoubleCoins, runId, (earned, nonce) =>
            {
                if (!earned) { Toast("Ad not finished – no bonus."); _goDouble.SetEnabled(true); return; }
                s.CoinsDoubled = true;
                Show(_goDouble, false);
                if (Services.Online != null && Services.Online.Configured)
                    Services.Online.ClaimAdReward(nonce, (ok, msg, reward) => Toast(ok ? $"+{reward:N0} COINS!" : msg));
                else
                {
                    Services.Save.Data.coins += coins;
                    Services.Save.Save();
                    Toast($"+{coins:N0} COINS!");
                }
                Services.Audio.Play(Logic.Audio.Sfx.CoinRush);
            });
        }

        // ------------------------------------------------------------------ revive offer
        private void BuildRevive()
        {
            _revive = ModalShell("revive", "BUHAYIN?", out var card);
            card.Add(Note("Watch a short ad to keep driving with 1 heart. One revive per run.", 34, UiKit.Ink));
            _reviveCount = UiKit.Text("5", 120, UiKit.Red, true, 3f);
            card.Add(_reviveCount);
            card.Add(UiKit.Button("REVIVE · WATCH AD", () => ResolveRevive(true), UiKit.Yellow, UiKit.Ink, 50, 140));
            card.Add(UiKit.Button("NO THANKS", () => ResolveRevive(false), Color.white, UiKit.Ink, 40, 110));
        }

        private Action<bool> _reviveDone;
        private IVisualElementScheduledItem _reviveTimer;

        /// <summary>Offered once per run right after the last heart is lost (the game is paused meanwhile).</summary>
        public void ShowRevive(float seconds, Action<bool> done)
        {
            _reviveDone = done;
            Show(_revive, true);
            UiKit.PopIn(_revive.Q("card"));
            float left = seconds;
            _reviveCount.text = Mathf.CeilToInt(left).ToString();
            _reviveTimer?.Pause();
            _reviveTimer = _revive.schedule.Execute(() =>
            {
                left -= 0.1f;
                _reviveCount.text = Mathf.Max(0, Mathf.CeilToInt(left)).ToString();
                if (left <= 0) ResolveRevive(false);
            }).Every(100);
        }

        private void ResolveRevive(bool watch)
        {
            if (_reviveDone == null) return;
            _reviveTimer?.Pause();
            Show(_revive, false);
            var cb = _reviveDone;
            _reviveDone = null;
            cb(watch);
        }

        // ------------------------------------------------------------------ development: TEST ads / purchases
        private void BuildTestScreens()
        {
            _testAd = MakeScreen("testAd", false);
            _testAd.style.backgroundColor = new Color(0.05f, 0.05f, 0.08f, 0.97f);
            _testAd.style.alignItems = Align.Center;
            _testAd.style.justifyContent = Justify.Center;
            _testAd.pickingMode = PickingMode.Position;
            _testAd.Add(UiKit.Text("TEST AD", 110, UiKit.Yellow, true, 3f));
            _testAdLabel = UiKit.Text("", 40, Color.white, false);
            _testAdLabel.style.whiteSpace = WhiteSpace.Normal;
            _testAdLabel.style.marginTop = 20;
            _testAdLabel.style.marginBottom = 30;
            _testAd.Add(_testAdLabel);
            _testAdClose = UiKit.Button("CLOSE", () => FinishTestAd(false), Color.white, UiKit.Ink, 44, 120);
            _testAdClose.style.width = 600;
            _testAd.Add(_testAdClose);

            _testBanner = UiKit.Box("testBanner");
            _testBanner.style.position = Position.Absolute;
            _testBanner.style.left = _testBanner.style.right = 0;
            _testBanner.style.bottom = 0;
            _testBanner.style.height = 120;
            _testBanner.style.backgroundColor = new Color(0.1f, 0.1f, 0.12f, 0.95f);
            _testBanner.style.alignItems = Align.Center;
            _testBanner.style.justifyContent = Justify.Center;
            _testBanner.Add(UiKit.Text("TEST BANNER AD · 320x50", 30, UiKit.Yellow, false));
            _root.Add(_testBanner);
            Show(_testBanner, false);
            Show(_testAd, false);
        }

        public void SetTestBanner(bool visible)
        {
            if (_testBanner == null) return;
            Show(_testBanner, visible);
            if (_menu != null) _menu.style.paddingBottom = visible ? 130 : 0; // keep menu buttons above the banner
        }

        /// <summary>Simulated full-screen ad: rewarded ones must run to the end to pay out.</summary>
        public void ShowTestAd(bool rewarded, Action<bool> done)
        {
            _testAdDone = done;
            Show(_testAd, true);
            _testAd.BringToFront();
            float left = rewarded ? 5f : 3f;
            _testAdClose.SetEnabled(!rewarded);
            _testAdClose.text = rewarded ? "SKIP (NO REWARD)" : "CLOSE";
            _testAdClose.SetEnabled(true);
            _testAd.schedule.Execute(t =>
            {
                left -= 0.1f;
                _testAdLabel.text = rewarded
                    ? (left > 0 ? $"Rewarded test ad · reward in {Mathf.CeilToInt(left)} s" : "Reward earned!")
                    : $"Interstitial test ad (between runs only)";
                if (rewarded && left <= 0) FinishTestAd(true);
            }).Every(100).Until(() => _testAdDone == null);
        }

        private void FinishTestAd(bool earned)
        {
            if (_testAdDone == null) return;
            Show(_testAd, false);
            var cb = _testAdDone;
            _testAdDone = null;
            cb(earned);
        }

        /// <summary>Simulated store dialog (development only).</summary>
        public void ShowTestPurchase(ProductDef p, Action<bool> done)
        {
            Confirm("TEST PURCHASE", $"{p?.Title}\n{p?.Description}\n\nSimulated – nothing is charged.", () => done(true));
            // cancelling the confirm dialog = no purchase
            _confirmCancel = () => done(false);
        }
    }
}
