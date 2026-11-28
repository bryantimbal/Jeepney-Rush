using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace JeepneyRush
{
    /// <summary>
    /// Tiny Supabase client on UnityWebRequest (no SDK dependency): GoTrue auth (guest,
    /// email/password, refresh, link guest → email, sign-out) and PostgREST RPC calls.
    /// The session (access + refresh token) is kept in PlayerPrefs.
    /// </summary>
    public sealed class SupabaseClient
    {
        [Serializable] private class AuthUser { public string id; public string email; public string new_email; public bool is_anonymous; }
        [Serializable] private class AuthResponse { public string access_token; public string refresh_token; public long expires_at; public int expires_in; public AuthUser user; }
        [Serializable] private class ErrorBody { public string error; public string error_description; public string msg; public string message; public string error_code; public string code; }
        [Serializable] private class EmailPassword { public string email; public string password; }
        [Serializable] private class RefreshBody { public string refresh_token; }
        [Serializable] private class EmailOnly { public string email; }
        [Serializable] private class PasswordOnly { public string password; }

        private const string PrefKey = "jr_session_v1";
        [Serializable] private class Session { public string access_token; public string refresh_token; public long expires_at; public string user_id; public string email; public bool is_anonymous; }

        private readonly OnlineConfig _cfg;
        private Session _session;

        public SupabaseClient(OnlineConfig cfg)
        {
            _cfg = cfg;
            try { var json = PlayerPrefs.GetString(PrefKey, ""); if (!string.IsNullOrEmpty(json)) _session = JsonUtility.FromJson<Session>(json); }
            catch { _session = null; }
        }

        public bool HasSession => _session != null && !string.IsNullOrEmpty(_session.refresh_token);
        public string UserId => _session?.user_id;
        public string Email => _session?.email;
        public bool IsAnonymous => _session == null || _session.is_anonymous;

        private void Store(AuthResponse r)
        {
            if (r == null || string.IsNullOrEmpty(r.access_token)) return;
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            _session = new Session
            {
                access_token = r.access_token, refresh_token = r.refresh_token,
                expires_at = r.expires_at > 0 ? r.expires_at : now + Math.Max(60, r.expires_in),
                user_id = r.user?.id, email = r.user?.email, is_anonymous = r.user == null || r.user.is_anonymous || string.IsNullOrEmpty(r.user.email),
            };
            if (!_session.is_anonymous && !string.IsNullOrEmpty(_session.email)) PlayerPrefs.SetString(EmailKey, _session.email);
            PlayerPrefs.SetString(PrefKey, JsonUtility.ToJson(_session));
            PlayerPrefs.Save();
        }

        public void ClearSession()
        {
            _session = null;
            PlayerPrefs.DeleteKey(PrefKey);
            PlayerPrefs.Save();
        }

        // ------------------------------------------------------------------ HTTP
        private UnityWebRequest Request(string method, string path, string body, bool auth)
        {
            var req = new UnityWebRequest(_cfg.BaseUrl + path, method);
            if (body != null) req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body)) { contentType = "application/json" };
            req.downloadHandler = new DownloadHandlerBuffer();
            req.timeout = Mathf.Max(3, _cfg.timeoutSeconds);
            req.SetRequestHeader("apikey", _cfg.anonKey.Trim());
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Accept", "application/json");
            // Only a signed-in user's token goes in Authorization. The project key travels in
            // "apikey" – that works with both the legacy anon key (eyJ…) and the new
            // publishable key (sb_publishable_…), which is not a JWT.
            if (auth && _session != null && !string.IsNullOrEmpty(_session.access_token))
                req.SetRequestHeader("Authorization", "Bearer " + _session.access_token);
            return req;
        }

        /// <summary>Human-readable error from a failed request (null on success).</summary>
        private static string ErrorOf(UnityWebRequest req)
        {
            if (req.result == UnityWebRequest.Result.Success) return null;
            if (req.result == UnityWebRequest.Result.ConnectionError || req.result == UnityWebRequest.Result.DataProcessingError) return "offline";
            string text = req.downloadHandler?.text;
            try
            {
                var e = JsonUtility.FromJson<ErrorBody>(text);
                string m = e?.error_description ?? e?.msg ?? e?.message ?? e?.error ?? e?.error_code;
                if (!string.IsNullOrEmpty(m)) return m;
            }
            catch { /* not JSON */ }
            return $"http {req.responseCode}";
        }

        private IEnumerator Send(UnityWebRequest req, Action<string, string> done)
        {
            using (req)
            {
                yield return req.SendWebRequest();
                string err = ErrorOf(req);
                done(err == null ? req.downloadHandler.text : null, err);
            }
        }

        // ------------------------------------------------------------------ auth
        private IEnumerator Auth(string path, string body, bool withSession, Action<string> done)
        {
            string error = null;
            yield return Send(Request("POST", path, body, withSession), (json, err) =>
            {
                error = err;
                if (err == null)
                {
                    var r = JsonUtility.FromJson<AuthResponse>(json);
                    if (r == null || string.IsNullOrEmpty(r.access_token)) error = "confirm_email"; // sign-up that needs email confirmation
                    else Store(r);
                }
            });
            done(error);
        }

        /// <summary>Guest account (Supabase "Anonymous sign-ins" must be enabled).</summary>
        public IEnumerator SignInAnonymously(Action<string> done) => Auth("/auth/v1/signup", "{}", false, done);

        public IEnumerator SignUp(string email, string password, Action<string> done) =>
            Auth("/auth/v1/signup", JsonUtility.ToJson(new EmailPassword { email = email, password = password }), false, done);

        public IEnumerator SignIn(string email, string password, Action<string> done) =>
            Auth("/auth/v1/token?grant_type=password", JsonUtility.ToJson(new EmailPassword { email = email, password = password }), false, done);

        // Single-flight refresh: Supabase rotates the refresh token on every use, and reusing an
        // already-rotated token can make the server revoke the whole login. So only ONE refresh
        // may be in flight; anyone else who needs a fresh token waits for that one's result.
        private bool _refreshing;
        private string _refreshResult;
        private int _refreshGen;

        public IEnumerator Refresh(Action<string> done)
        {
            if (!HasSession) { done("no_session"); yield break; }
            if (_refreshing)
            {
                int gen = _refreshGen;
                while (_refreshing && gen == _refreshGen) yield return null;
                done(_refreshResult);
                yield break;
            }
            _refreshing = true;
            string err = null;
            yield return Auth("/auth/v1/token?grant_type=refresh_token", JsonUtility.ToJson(new RefreshBody { refresh_token = _session.refresh_token }), false, e => err = e);
            _refreshResult = err;
            _refreshGen++;
            _refreshing = false;
            done(err);
        }

        /// <summary>Email of the last signed-in (non-guest) account, kept so a lost login can be re-entered quickly.</summary>
        public static string RememberedEmail
        {
            get => PlayerPrefs.GetString(EmailKey, "");
            set { PlayerPrefs.SetString(EmailKey, value ?? ""); PlayerPrefs.Save(); }
        }
        private const string EmailKey = "jr_last_email";

        /// <summary>Email a guest asked to link; Supabase waits for the link in the confirmation mail ("" = none).</summary>
        public static string PendingEmail
        {
            get => PlayerPrefs.GetString(PendingKey, "");
            private set { PlayerPrefs.SetString(PendingKey, value ?? ""); PlayerPrefs.Save(); }
        }
        private const string PendingKey = "jr_pending_email";
        // kept in memory only (never saved): set once the email is confirmed while the game is still open
        private string _pendingPassword;

        /// <summary>
        /// Turns the current guest into an email account (same user id, so all progress stays).
        /// Trusts only Supabase's answer: when the project confirms emails the account stays a guest
        /// until the link in the mail is tapped ("confirm_email_change"). When the email is linked
        /// right away, the password is checked by really signing in with it.
        /// </summary>
        public IEnumerator LinkEmail(string email, string password, Action<string> done)
        {
            email = (email ?? "").Trim();
            yield return EnsureFresh();
            AuthUser user = null;
            string error = null;
            yield return Send(Request("PUT", "/auth/v1/user", JsonUtility.ToJson(new EmailPassword { email = email, password = password }), true), (json, err) =>
            {
                error = err;
                if (err == null) user = JsonUtility.FromJson<AuthUser>(json);
            });
            if (error != null) { done(error); yield break; }
            bool linked = user != null && !user.is_anonymous && string.Equals(user.email, email, StringComparison.OrdinalIgnoreCase);
            if (!linked)
            {
                PendingEmail = email;
                _pendingPassword = password;
                done("confirm_email_change");
                yield break;
            }
            yield return FinishLink(email, password, done);
        }

        /// <summary>The email is on the account: make sure email + password really sign in (sets the password again if needed).</summary>
        private IEnumerator FinishLink(string email, string password, Action<string> done)
        {
            string err = null;
            yield return SignIn(email, password, e => err = e);
            if (err != null && !string.IsNullOrEmpty(password))
            {
                yield return Send(Request("PUT", "/auth/v1/user", JsonUtility.ToJson(new PasswordOnly { password = password }), true), (_, e) => err = e);
                if (err == null) yield return SignIn(email, password, e => err = e);
            }
            if (err == null) { PendingEmail = ""; _pendingPassword = null; }
            done(err);
        }

        /// <summary>
        /// After the player tapped the confirmation link: asks Supabase again. Returns null once this
        /// login is an email account, "confirm_email_change" while it still waits for the link.
        /// </summary>
        public IEnumerator CheckPendingEmail(Action<string> done)
        {
            string pending = PendingEmail;
            if (string.IsNullOrEmpty(pending) || !HasSession) { done(null); yield break; }
            string err = null;
            yield return Refresh(e => err = e);
            if (err != null) { done(err); yield break; }
            if (IsAnonymous || !string.Equals(Email, pending, StringComparison.OrdinalIgnoreCase)) { done("confirm_email_change"); yield break; }
            if (_pendingPassword != null) { yield return FinishLink(pending, _pendingPassword, done); yield break; }
            PendingEmail = "";
            done(null);
        }

        /// <summary>Sends the confirmation mail again.</summary>
        public IEnumerator ResendPendingEmail(Action<string> done)
        {
            string pending = PendingEmail;
            if (string.IsNullOrEmpty(pending)) { done(null); yield break; }
            yield return EnsureFresh();
            string error = null;
            yield return Send(Request("PUT", "/auth/v1/user", JsonUtility.ToJson(new EmailOnly { email = pending }), true), (_, err) => error = err);
            done(error);
        }

        public static void ForgetPendingEmail() => PendingEmail = "";

        public IEnumerator SignOut(Action done)
        {
            PendingEmail = "";
            _pendingPassword = null;
            if (HasSession) yield return Send(Request("POST", "/auth/v1/logout", "{}", true), (_, __) => { });
            ClearSession();
            done?.Invoke();
        }

        private IEnumerator EnsureFresh()
        {
            if (_session == null) yield break;
            if (_session.expires_at - DateTimeOffset.UtcNow.ToUnixTimeSeconds() > 60) yield break;
            yield return Refresh(_ => { });
        }

        // ------------------------------------------------------------------ Edge Functions
        /// <summary>POST /functions/v1/{name} with the signed-in user's token.</summary>
        public IEnumerator Function(string name, string bodyJson, Action<string, string> done)
        {
            if (!HasSession) { done(null, "no_session"); yield break; }
            yield return EnsureFresh();
            yield return Send(Request("POST", "/functions/v1/" + name, string.IsNullOrEmpty(bodyJson) ? "{}" : bodyJson, true), done);
        }

        // ------------------------------------------------------------------ RPC
        /// <summary>POST /rest/v1/rpc/{fn} with a JSON object of named arguments. Returns the JSON result.</summary>
        /// <summary>RPC that needs no sign-in (e.g. the app version check, which runs before sign-in).</summary>
        public IEnumerator RpcPublic(string fn, string argsJson, Action<string, string> done)
        {
            string result = null, error = null;
            yield return Send(Request("POST", "/rest/v1/rpc/" + fn, string.IsNullOrEmpty(argsJson) ? "{}" : argsJson, false), (json, err) => { result = json; error = err; });
            done(result, error);
        }

        public IEnumerator Rpc(string fn, string argsJson, Action<string, string> done)
        {
            if (!HasSession) { done(null, "no_session"); yield break; }
            yield return EnsureFresh();
            string result = null, error = null;
            yield return Send(Request("POST", "/rest/v1/rpc/" + fn, string.IsNullOrEmpty(argsJson) ? "{}" : argsJson, true), (json, err) => { result = json; error = err; });
            if (error != null && error.Contains("JWT"))
            {
                // token expired between the check and the call: refresh once and retry
                string refreshErr = null;
                yield return Refresh(e => refreshErr = e);
                if (refreshErr == null)
                    yield return Send(Request("POST", "/rest/v1/rpc/" + fn, string.IsNullOrEmpty(argsJson) ? "{}" : argsJson, true), (json, err) => { result = json; error = err; });
            }
            done(result, error);
        }
    }
}
