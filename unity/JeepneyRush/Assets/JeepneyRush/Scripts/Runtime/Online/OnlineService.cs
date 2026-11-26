using System;
using System.Collections;
using System.IO;
using JeepneyRush.Logic;
using UnityEngine;

namespace JeepneyRush
{
    public enum OnlineState { Disabled, SignedOut, Connecting, Online, Offline }

    /// <summary>
    /// Phase 3 online layer. Offline-first: the game always works locally; when a Supabase
    /// project is configured and the player is signed in (guest or email), the server is the
    /// authority for coins, purchases, missions, achievements and leaderboards. Finished runs
    /// are queued on disk and uploaded when a connection is available (idempotent per run id).
    /// </summary>
    public sealed class OnlineService : MonoBehaviour
    {
        private OnlineConfig _cfg;
        private SupabaseClient _client;
        private OfflineQueue _queue = new OfflineQueue();
        private bool _flushing, _connecting;
        private float _retryT;

        public OnlineState State { get; private set; } = OnlineState.Disabled;
        public ServerSnapshot Snapshot { get; private set; }
        public string LastError { get; private set; }
        public int QueuedRuns => _queue.Count;

        /// <summary>A Supabase project is set up in OnlineConfig.</summary>
        public bool Configured => _cfg != null && _cfg.Configured;
        /// <summary>There is a signed-in account (possibly offline right now).</summary>
        public bool SignedIn => Configured && _client != null && _client.HasSession;
        /// <summary>The server owns the economy right now (purchases/claims go through it).</summary>
        public bool Authoritative => SignedIn && State == OnlineState.Online;
        public bool IsGuest => _client == null || _client.IsAnonymous;
        public string Email => _client?.Email;

        public event Action Changed;
        public event Action<SubmitResult> RunSubmitted;

        private string QueuePath => Path.Combine(Application.persistentDataPath, "jeepneyrush_runqueue.json");

        private void Awake()
        {
            _cfg = OnlineConfig.Active;
            if (!_cfg.Configured) { State = OnlineState.Disabled; return; }
            _client = new SupabaseClient(_cfg);
            try { if (File.Exists(QueuePath)) _queue = JsonUtility.FromJson<OfflineQueue>(File.ReadAllText(QueuePath)) ?? new OfflineQueue(); }
            catch (Exception e) { Debug.LogWarning("[Online] queue unreadable: " + e.Message); _queue = new OfflineQueue(); }
            State = _client.HasSession ? OnlineState.Offline : OnlineState.SignedOut;
        }

        private void SaveQueue()
        {
            try { File.WriteAllText(QueuePath, JsonUtility.ToJson(_queue)); }
            catch (Exception e) { Debug.LogWarning("[Online] could not save queue: " + e.Message); }
        }

        private void SetState(OnlineState s, string error = null)
        {
            State = s;
            LastError = error;
            Changed?.Invoke();
        }

        private void Update()
        {
            // reconnect every 30 s while offline (never during a run: it would cost frames)
            if (State != OnlineState.Offline || _connecting || Services.Game == null || Services.Game.State == GameState.Playing) return;
            _retryT += Time.unscaledDeltaTime;
            if (_retryT > 30f) { _retryT = 0; Connect(); }
        }

        private void OnApplicationPause(bool paused)
        {
            if (!paused && Configured && State == OnlineState.Offline) Connect();
        }

        // ================================================================== connect / accounts
        public void Connect()
        {
            if (!Configured || _connecting) return;
            StartCoroutine(ConnectRoutine());
        }

        private IEnumerator ConnectRoutine()
        {
            _connecting = true;
            var save = Services.Save.Data;
            SetState(OnlineState.Connecting);
            if (_client.HasSession)
            {
                string err = null;
                yield return _client.Refresh(e => err = e);
                // Only a definite "this login is gone" from the server drops the session. Anything else
                // (no internet, server hiccup) keeps it – otherwise a brand-new guest account would be
                // created and the player's progress would appear to vanish.
                bool revoked = err != null && (err.Contains("Invalid Refresh Token") || err.Contains("refresh_token_not_found") || err.Contains("invalid_grant") || err.Contains("User from sub claim in JWT does not exist"));
                if (err != null && !revoked) { _connecting = false; SetState(OnlineState.Offline, err); yield break; }
                if (revoked)
                {
                    Debug.LogWarning("[Online] saved login is no longer valid: " + err);
                    bool wasEmail = !_client.IsAnonymous;
                    _client.ClearSession();
                    if (wasEmail)
                    {
                        // an email account must never silently turn into a new guest: ask to sign in again
                        save.signedOut = true;
                        Services.Save.Save();
                        _connecting = false;
                        SetState(OnlineState.SignedOut, "session_expired");
                        yield break;
                    }
                }
            }
            if (!_client.HasSession)
            {
                if (!_cfg.autoGuest || save.signedOut) { _connecting = false; SetState(OnlineState.SignedOut); yield break; }
                string err = null;
                yield return _client.SignInAnonymously(e => err = e);
                if (err != null) { _connecting = false; SetState(OnlineState.SignedOut, err); yield break; }
            }
            yield return Pull(true);
            _connecting = false;
            if (State == OnlineState.Online) StartCoroutine(Flush());
        }

        /// <summary>Downloads the server state; on the first link of this device, uploads its progress once.</summary>
        private IEnumerator Pull(bool allowImport)
        {
            string json = null, err = null;
            yield return _client.Rpc("jr_get_state", "{}", (j, e) => { json = j; err = e; });
            if (err != null) { SetState(OnlineState.Offline, err); yield break; }
            var snap = JsonUtility.FromJson<ServerSnapshot>(json);
            var save = Services.Save.Data;
            bool localProgress = save.runs > 0 || save.coins > 0 || save.xp > 0;
            if (allowImport && !save.onlineLinked && localProgress && snap.runs == 0)
            {
                string body = "{\"p_data\":" + JsonUtility.ToJson(ServerSync.ToImport(save)) + "}";
                string importErr = null;
                yield return _client.Rpc("jr_import_legacy", body, (j, e) =>
                {
                    importErr = e;
                    if (e == null) { var r = JsonUtility.FromJson<OpResult>(j); if (r?.snapshot != null) snap = r.snapshot; }
                });
                if (importErr != null)
                {
                    // never overwrite the phone's progress with an empty account: try again later
                    Debug.LogWarning("[Online] could not upload this phone's progress yet: " + importErr);
                    SetState(OnlineState.Offline, importErr);
                    yield break;
                }
            }
            // switching to a different account while this phone holds progress: keep a backup file
            if (localProgress && !string.IsNullOrEmpty(snap.user_id) && snap.user_id != save.lastUserId) Services.Save.Backup("before-" + snap.user_id.Substring(0, 8));
            Apply(snap);
            save.onlineLinked = true;
            save.signedOut = false;
            save.lastUserId = snap.user_id;
            Services.Save.Save();
            SetState(OnlineState.Online);
        }

        private void Apply(ServerSnapshot s)
        {
            if (s == null) return;
            Snapshot = s;
            ServerSync.Apply(Services.Save.Data, s);
            Services.Save.Save();
            Changed?.Invoke();
        }

        /// <summary>Guest → email account (progress stays: same user id), or a brand-new email account.</summary>
        public void CreateEmailAccount(string email, string password, Action<bool, string> done) => StartCoroutine(LinkRoutine(email, password, done));

        private IEnumerator LinkRoutine(string email, string password, Action<bool, string> done)
        {
            string err = null;
            if (_client.HasSession && _client.IsAnonymous) yield return _client.LinkEmail(email, password, e => err = e);
            else yield return _client.SignUp(email, password, e => err = e);
            if (err == null) yield return Pull(false);
            done?.Invoke(err == null, Friendly(err));
            Changed?.Invoke();
        }

        /// <summary>Signs in to an existing email account. This device then shows that account's progress.</summary>
        public void SignIn(string email, string password, Action<bool, string> done) => StartCoroutine(SignInRoutine(email, password, done));

        private IEnumerator SignInRoutine(string email, string password, Action<bool, string> done)
        {
            string previous = _client.UserId;
            string err = null;
            yield return _client.SignIn(email, password, e => err = e);
            if (err == null)
            {
                if (previous != _client.UserId) { _queue = new OfflineQueue(); SaveQueue(); } // runs belonged to the old account
                yield return Pull(false);
            }
            done?.Invoke(err == null, Friendly(err));
            Changed?.Invoke();
        }

        /// <summary>Signs out. Local coins/progress are cleared (they live on the account) – settings stay.</summary>
        public void SignOut(Action done)
        {
            if (!Configured) { done?.Invoke(); return; }
            StartCoroutine(_client.SignOut(() =>
            {
                _queue = new OfflineQueue();
                SaveQueue();
                Snapshot = null;
                Services.Save.ResetProgress();
                Services.Save.Data.onlineLinked = true;
                Services.Save.Data.signedOut = true;
                Services.Save.Save();
                SetState(OnlineState.SignedOut);
                done?.Invoke();
            }));
        }

        /// <summary>
        /// Permanently deletes the account and everything stored with it on the server, then
        /// wipes local progress (settings stay). The game stays signed out until the player
        /// chooses to play as a guest again.
        /// </summary>
        public void DeleteAccount(Action<bool, string> done)
        {
            if (!SignedIn) { done?.Invoke(false, "Walang account na naka-sign in."); return; }
            Op("jr_delete_account", "{}", (ok, msg, _) =>
            {
                if (!ok) { done?.Invoke(false, msg ?? "Hindi na-delete. Subukan ulit mamaya."); return; }
                _client.ClearSession();
                _queue = new OfflineQueue();
                SaveQueue();
                Snapshot = null;
                Services.Save.ResetProgress();
                Services.Save.Data.onlineLinked = true;
                Services.Save.Data.signedOut = true;
                Services.Save.Save();
                SetState(OnlineState.SignedOut);
                done?.Invoke(true, null);
            });
        }

        /// <summary>After signing out: start a fresh guest account.</summary>
        public void PlayAsGuest()
        {
            if (!Configured) return;
            Services.Save.Data.signedOut = false;
            Connect();
        }

        public static string Friendly(string err)
        {
            if (string.IsNullOrEmpty(err)) return null;
            if (err == "offline") return "Walang internet. Try again later.";
            if (err == "session_expired") return "Your login expired. Please sign in again.";
            if (err == "confirm_email") return "Check your email to confirm the account, then sign in.";
            if (err.Contains("Invalid login")) return "Wrong email or password.";
            if (err.Contains("already") && err.Contains("registered")) return "That email already has an account – sign in instead.";
            if (err.Contains("Password")) return err;
            if (err.Contains("Anonymous sign-ins are disabled")) return "Guest accounts are disabled on the server.";
            return err;
        }

        // ================================================================== runs
        /// <summary>Queues a finished run for upload (kept on disk until the server answers).</summary>
        public void SubmitRun(RunReport report)
        {
            if (!SignedIn || report == null) return;
            _queue.Add(report);
            SaveQueue();
            if (!_flushing) StartCoroutine(Flush());
        }

        private IEnumerator Flush()
        {
            if (_flushing || !SignedIn) yield break;
            _flushing = true;
            while (_queue.Count > 0)
            {
                var r = _queue.runs[0];
                string json = null, err = null;
                yield return _client.Rpc("jr_submit_run", "{\"p_report\":" + JsonUtility.ToJson(r) + "}", (j, e) => { json = j; err = e; });
                if (err != null)
                {
                    // network/server trouble: keep the run and try again later
                    SetState(OnlineState.Offline, err);
                    break;
                }
                _queue.Remove(r.run_id);
                SaveQueue();
                var res = JsonUtility.FromJson<SubmitResult>(json);
                if (res != null)
                {
                    if (!res.accepted && !res.duplicate) Debug.LogWarning($"[Online] run {r.run_id} rejected: {res.reason}");
                    if (res.snapshot != null) Apply(res.snapshot);
                    if (State != OnlineState.Online) SetState(OnlineState.Online);
                    RunSubmitted?.Invoke(res);
                }
            }
            _flushing = false;
        }

        // ================================================================== economy (server-side)
        private void Op(string fn, string args, Action<bool, string, int> done) => StartCoroutine(OpRoutine(fn, args, done));

        private IEnumerator OpRoutine(string fn, string args, Action<bool, string, int> done)
        {
            string json = null, err = null;
            yield return _client.Rpc(fn, args, (j, e) => { json = j; err = e; });
            if (err != null)
            {
                if (err == "offline") SetState(OnlineState.Offline, err);
                done?.Invoke(false, Friendly(err), 0);
                yield break;
            }
            var r = JsonUtility.FromJson<OpResult>(json);
            if (r?.snapshot != null) Apply(r.snapshot);
            done?.Invoke(r != null && r.ok, ReasonText(r?.reason), r?.reward ?? 0);
        }

        public static string ReasonText(string reason)
        {
            switch (reason)
            {
                case null: return null;
                case "coins": return "Kulang ang coins.";
                case "owned": return "Owned na.";
                case "not_for_sale": return "Hindi nabibili – kinikita sa career.";
                case "max_level": return "Max level na.";
                case "not_done": return "Hindi pa tapos.";
                case "claimed": return "Na-claim na.";
                case "invalid_name": return "Name: 3–16 letters, numbers, spaces.";
                case "blocked_name": return "Please choose another name.";
                case "unknown_code": return "Walang ganyang friend code.";
                case "self": return "That's your own code!";
                case "limit": return "Friend list is full (100).";
                case "gems": return "Kulang ang gems.";
                case "test_not_allowed": return "Test purchases are switched off on the server.";
                case "token_used": return "This purchase belongs to another account.";
                case "pending": return "Payment pending – it will arrive when Google confirms it.";
                case "canceled": return "The purchase was cancelled.";
                case "wrong_account": return "This purchase was made for another account.";
                case "already_today": return "Daily bonus already claimed today.";
                case "run_not_found": return "That run can't be doubled any more.";
                default: return reason;
            }
        }

        public void BuyJeepney(string id, Action<bool, string> done) => Op("jr_buy_jeepney", "{\"p_id\":" + Json.Str(id) + "}", (ok, m, _) => done?.Invoke(ok, m));
        public void BuyUpgrade(UpgradeKind k, Action<bool, string> done) => Op("jr_buy_upgrade", "{\"p_kind\":" + (int)k + "}", (ok, m, _) => done?.Invoke(ok, m));
        public void ClaimMission(bool weekly, int slot, Action<bool, string, int> done) => Op("jr_claim_mission", "{\"p_weekly\":" + Json.Bool(weekly) + ",\"p_slot\":" + slot + "}", done);
        public void ClaimAchievement(string id, Action<bool, string, int> done) => Op("jr_claim_achievement", "{\"p_id\":" + Json.Str(id) + "}", done);
        public void SetName(string name, Action<bool, string> done) => Op("jr_set_name", "{\"p_name\":" + Json.Str(name) + "}", (ok, m, _) => done?.Invoke(ok, m));

        // ================================================================== Phase 4: gems, purchases, ad rewards
        public void BuyCosmetic(string id, Action<bool, string> done) => Op("jr_buy_cosmetic", "{\"p_id\":" + Json.Str(id) + "}", (ok, m, _) => done?.Invoke(ok, m));
        public void BuyJeepneyWithGems(string id, Action<bool, string> done) => Op("jr_buy_jeepney_gems", "{\"p_id\":" + Json.Str(id) + "}", (ok, m, _) => done?.Invoke(ok, m));

        /// <summary>Sends a store purchase token to the verify-purchase Edge Function (Google Play check + grant).</summary>
        public void VerifyPurchase(string productId, string token, Action<bool, string> done) => StartCoroutine(VerifyRoutine(productId, token, done));

        private IEnumerator VerifyRoutine(string productId, string token, Action<bool, string> done)
        {
            if (!SignedIn) { done?.Invoke(false, "Sign in first (ACCOUNT)."); yield break; }
            string json = null, err = null;
            string body = "{\"product_id\":" + Json.Str(productId) + ",\"purchase_token\":" + Json.Str(token) + "}";
            yield return _client.Function("verify-purchase", body, (j, e) => { json = j; err = e; });
            if (err != null) { done?.Invoke(false, Friendly(err)); yield break; }
            var r = JsonUtility.FromJson<OpResult>(json);
            if (r?.snapshot != null) Apply(r.snapshot);
            done?.Invoke(r != null && r.ok, ReasonText(r?.reason));
        }

        /// <summary>
        /// Claims a rewarded-ad reward. Google's verification callback can arrive a few seconds after
        /// the ad closes, so "not verified yet" is retried for a while.
        /// </summary>
        public void ClaimAdReward(string nonce, Action<bool, string, int> done) => StartCoroutine(ClaimRoutine(nonce, done));

        private IEnumerator ClaimRoutine(string nonce, Action<bool, string, int> done)
        {
            for (int attempt = 0; attempt < 8; attempt++)
            {
                string json = null, err = null;
                yield return _client.Rpc("jr_claim_ad_reward", "{\"p_nonce\":" + Json.Str(nonce) + "}", (j, e) => { json = j; err = e; });
                if (err != null) { done?.Invoke(false, Friendly(err), 0); yield break; }
                var r = JsonUtility.FromJson<OpResult>(json);
                if (r?.snapshot != null) Apply(r.snapshot);
                // Google's callback (or this run's upload) may still be on its way: retry for a while
                if (r != null && (r.reason == "not_verified" || r.reason == "run_not_found")) { yield return new WaitForSecondsRealtime(1.5f); continue; }
                done?.Invoke(r != null && r.ok, ReasonText(r?.reason), r?.reward ?? 0);
                yield break;
            }
            done?.Invoke(false, "Reward not confirmed by Google yet – try again in a minute.", 0);
        }

        /// <summary>Re-downloads the account (restores purchases made on another phone with the same account).</summary>
        public void Refresh(Action<bool> done) => StartCoroutine(RefreshRoutine(done));

        private IEnumerator RefreshRoutine(Action<bool> done)
        {
            if (!SignedIn) { done?.Invoke(false); yield break; }
            yield return Pull(false);
            done?.Invoke(State == OnlineState.Online);
        }

        public string UserId => _client?.UserId;

        // ================================================================== social
        /// <summary>One leaderboard page. statClass 0..2 = only that jeepney stat class, -1 = everyone.</summary>
        public void Leaderboard(string board, string scope, int statClass, Action<LeaderboardPage, string> done)
        {
            string args = "{\"p_board\":" + Json.Str(board) + ",\"p_scope\":" + Json.Str(scope) + ",\"p_limit\":50";
            if (statClass < 0) { StartCoroutine(Get("jr_leaderboard", args + "}", done)); return; }
            // a backend without the stat-class migration doesn't know p_class: fall back to everyone
            StartCoroutine(Get<LeaderboardPage>("jr_leaderboard", args + ",\"p_class\":" + statClass + "}", (page, err) =>
            {
                if (page != null || !SignedIn) { done?.Invoke(page, err); return; }
                StartCoroutine(Get<LeaderboardPage>("jr_leaderboard", args + "}", (p2, e2) => { if (p2 != null) p2.@class = -1; done?.Invoke(p2, e2); }));
            }));
        }

        public void Friends(Action<FriendList, string> done) => StartCoroutine(Get("jr_list_friends", "{}", done));

        public void AddFriend(string code, Action<bool, string> done)
        {
            string norm = OnlineText.NormalizeCode(code);
            if (norm == null) { done?.Invoke(false, "A friend code has 8 letters/numbers."); return; }
            Op("jr_add_friend", "{\"p_code\":" + Json.Str(norm) + "}", (ok, m, _) => done?.Invoke(ok, m));
        }

        public void RemoveFriend(string userId, Action<bool, string> done) => Op("jr_remove_friend", "{\"p_user\":" + Json.Str(userId) + "}", (ok, m, _) => done?.Invoke(ok, m));

        /// <summary>Today's challenge: from the server when online, otherwise computed locally (same algorithm).</summary>
        public void Daily(Action<DailyChallenge> done)
        {
            if (!Authoritative) { done?.Invoke(DailyChallenge.Today); return; }
            StartCoroutine(Get<DailyInfo>("jr_daily_challenge", "{}", (d, err) =>
                done?.Invoke(d != null && err == null ? new DailyChallenge { DayKey = d.day_key, Seed = d.seed, Environment = (EnvironmentKind)d.env } : DailyChallenge.Today)));
        }

        private IEnumerator Get<T>(string fn, string args, Action<T, string> done) where T : class
        {
            if (!SignedIn) { done?.Invoke(null, Configured ? "Sign in first." : "Online features are not set up."); yield break; }
            string json = null, err = null;
            yield return _client.Rpc(fn, args, (j, e) => { json = j; err = e; });
            if (err != null) { if (err == "offline") SetState(OnlineState.Offline, err); done?.Invoke(null, Friendly(err)); yield break; }
            T value = null;
            try { value = JsonUtility.FromJson<T>(json); } catch (Exception e) { err = e.Message; }
            done?.Invoke(value, err);
        }
    }
}
