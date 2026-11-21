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
        [Serializable] private class AuthUser { public string id; public string email; public bool is_anonymous; }
        [Serializable] private class AuthResponse { public string access_token; public string refresh_token; public long expires_at; public int expires_in; public AuthUser user; }
        [Serializable] private class ErrorBody { public string error; public string error_description; public string msg; public string message; public string error_code; public string code; }
        [Serializable] private class EmailPassword { public string email; public string password; }
        [Serializable] private class RefreshBody { public string refresh_token; }

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
            req.SetRequestHeader("Authorization", "Bearer " + (auth && _session != null ? _session.access_token : _cfg.anonKey.Trim()));
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

        public IEnumerator Refresh(Action<string> done)
        {
            if (!HasSession) { done("no_session"); yield break; }
            yield return Auth("/auth/v1/token?grant_type=refresh_token", JsonUtility.ToJson(new RefreshBody { refresh_token = _session.refresh_token }), false, done);
        }

        /// <summary>Turns the current guest into an email account (keeps all progress).</summary>
        public IEnumerator LinkEmail(string email, string password, Action<string> done)
        {
            yield return EnsureFresh();
            string error = null;
            yield return Send(Request("PUT", "/auth/v1/user", JsonUtility.ToJson(new EmailPassword { email = email, password = password }), true), (json, err) =>
            {
                error = err;
                if (err == null)
                {
                    var u = JsonUtility.FromJson<AuthUser>(json);
                    if (u != null && _session != null) { _session.email = u.email ?? email; _session.is_anonymous = false; PlayerPrefs.SetString(PrefKey, JsonUtility.ToJson(_session)); PlayerPrefs.Save(); }
                }
            });
            done(error);
        }

        public IEnumerator SignOut(Action done)
        {
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

        // ------------------------------------------------------------------ RPC
        /// <summary>POST /rest/v1/rpc/{fn} with a JSON object of named arguments. Returns the JSON result.</summary>
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
