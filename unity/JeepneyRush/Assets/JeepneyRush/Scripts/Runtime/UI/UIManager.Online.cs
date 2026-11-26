using JeepneyRush.Logic;
using UnityEngine;
using UnityEngine.UIElements;

namespace JeepneyRush
{
    /// <summary>
    /// Phase 3 screens: account/profile + friends, rankings (all-time, weekly, daily
    /// challenge; global or friends) and the Daily Challenge. Every button does something
    /// real: when no Supabase project is configured the screens explain how to enable it.
    /// </summary>
    public sealed partial class UIManager
    {
        private VisualElement _account, _ranks, _daily;
        private VisualElement _accountBody, _rankList;
        private Label _onlineChip, _rankInfo, _dailyInfo, _goOnline;
        private string _rankBoard = "all", _rankScope = "global";
        private static readonly string[] RankBoardKeys = { "all", "week", "daily", "rush", "night" };
        private readonly Button[] _rankBoardBtns = new Button[5];
        private readonly Button[] _rankScopeBtns = new Button[2];
        private readonly Button[] _rankClassBtns = new Button[JeepneyClasses.Count + 1];
        /// <summary>Stat class shown in RANKINGS (-1 = everyone); starts at the class of the jeepney you drive.</summary>
        private int _rankClass = -2;
        private DailyChallenge _dailyToday;

        private static bool UseServer() => Services.Online != null && Services.Online.Authoritative;

        /// <summary>Short message for the player (null = nothing).</summary>
        private void Toast(string msg)
        {
            if (!string.IsNullOrEmpty(msg)) Float(msg, FloatKind.Info);
        }

        private void BuildOnline()
        {
            BuildAccount();
            BuildRanks();
            BuildDaily();
            HookOnline();
        }

        private bool _onlineHooked;

        /// <summary>Subscribes to the online service (safe to call before it exists; retried from Start).</summary>
        private void HookOnline()
        {
            if (_onlineHooked || Services.Online == null) return;
            Services.Online.Changed += OnOnlineChanged;
            Services.Online.RunSubmitted += OnRunSubmitted;
            _onlineHooked = true;
            UpdateOnlineChip();
        }

        private void Start() => HookOnline();

        private void OnDestroy()
        {
            if (!_onlineHooked || Services.Online == null) return;
            Services.Online.Changed -= OnOnlineChanged;
            Services.Online.RunSubmitted -= OnRunSubmitted;
        }

        private void OnOnlineChanged()
        {
            UpdateOnlineChip();
            if (_menu != null && _menu.style.display == DisplayStyle.Flex) RefreshMenuTop();
            if (_account != null && _account.style.display == DisplayStyle.Flex) RefreshAccount();
        }

        /// <summary>Small status pill on the main menu.</summary>
        private VisualElement BuildOnlineChip()
        {
            _onlineChip = UiKit.Text("", 26, Color.white, false, 1f);
            _onlineChip.style.backgroundColor = new Color(UiKit.Ink.r, UiKit.Ink.g, UiKit.Ink.b, 0.75f);
            UiKit.Border(_onlineChip.style, 3, new Color(1, 1, 1, 0.25f), 26);
            UiKit.Pad(_onlineChip.style, 6, 18);
            _onlineChip.style.alignSelf = Align.FlexStart;
            _onlineChip.style.marginLeft = 30;
            UpdateOnlineChip();
            return _onlineChip;
        }

        private void UpdateOnlineChip()
        {
            if (_onlineChip == null || Services.Online == null) return;
            var o = Services.Online;
            string name = o.Snapshot?.display_name;
            switch (o.State)
            {
                case OnlineState.Disabled: _onlineChip.text = "OFFLINE MODE"; _onlineChip.style.color = new Color(1, 1, 1, 0.8f); break;
                case OnlineState.Connecting: _onlineChip.text = "CONNECTING…"; _onlineChip.style.color = UiKit.Yellow; break;
                case OnlineState.Online: _onlineChip.text = $"ONLINE · {name}{(o.IsGuest ? " (guest)" : "")}"; _onlineChip.style.color = UiKit.Hex("#3EE07A"); break;
                case OnlineState.Offline: _onlineChip.text = o.QueuedRuns > 0 ? $"OFFLINE · {o.QueuedRuns} run(s) waiting to upload" : "OFFLINE · will reconnect"; _onlineChip.style.color = UiKit.Hex("#FF9F43"); break;
                default: _onlineChip.text = "SIGNED OUT · tap ACCOUNT"; _onlineChip.style.color = UiKit.Hex("#FF9F43"); break;
            }
        }

        // ============================================================== account & friends
        private Label Note(string text, float size = 32, Color? color = null)
        {
            var l = UiKit.Text(text, size, color ?? UiKit.Grey, false);
            l.style.whiteSpace = WhiteSpace.Normal;
            l.style.marginBottom = 10;
            return l;
        }

        /// <summary>
        /// Text input styled by hand: the game's panel has no default theme, so an unstyled
        /// TextField has a zero-size input box. The caption is a separate label ABOVE the box
        /// (the field's built-in label overlapped the box and got cut in half).
        /// </summary>
        private TextField Field(string label, bool password = false) => Field(label, password, out _);

        private TextField Field(string label, bool password, out VisualElement wrapper)
        {
            wrapper = UiKit.Box("fieldWrap");
            wrapper.style.marginTop = 6;
            wrapper.style.marginBottom = 14;
            wrapper.style.alignItems = Align.Stretch;
            var caption = UiKit.Text(label, 30, UiKit.Ink, false);
            caption.style.unityTextAlign = TextAnchor.MiddleLeft;
            caption.style.marginBottom = 8;
            caption.style.marginLeft = 6;
            caption.style.minHeight = 40;
            caption.style.whiteSpace = WhiteSpace.Normal;
            wrapper.Add(caption);

            var f = new TextField { isPasswordField = password, maskChar = '*' };
            f.AddToClassList(InteractiveClass);
            f.pickingMode = PickingMode.Position;
            f.style.marginLeft = f.style.marginRight = f.style.marginTop = f.style.marginBottom = 0;
            f.labelElement.style.display = DisplayStyle.None;
            var input = f.Q(TextField.textInputUssName) ?? f.Q(className: "unity-base-text-field__input");
            if (input != null)
            {
                input.pickingMode = PickingMode.Position;
                input.AddToClassList(InteractiveClass);
                input.style.flexGrow = 1;
                input.style.minHeight = 96;
                input.style.backgroundColor = Color.white;
                UiKit.Border(input.style, 4, UiKit.Ink, 20);
                UiKit.Pad(input.style, 10, 22);
                input.style.fontSize = 38;
                input.style.color = UiKit.Ink;
                input.style.unityFontDefinition = FontDefinition.FromFont(UiKit.Body);
                input.style.unityTextAlign = TextAnchor.MiddleLeft;
                foreach (var t in input.Query<TextElement>().ToList())
                {
                    t.style.fontSize = 38;
                    t.style.color = UiKit.Ink;
                    t.style.unityFontDefinition = FontDefinition.FromFont(UiKit.Body);
                }
                void Edge(Color c) { input.style.borderTopColor = input.style.borderBottomColor = input.style.borderLeftColor = input.style.borderRightColor = c; }
                f.RegisterCallback<FocusInEvent>(_ => Edge(UiKit.Blue));
                f.RegisterCallback<FocusOutEvent>(_ => Edge(UiKit.Ink));
            }
            wrapper.Add(f);
            return f;
        }

        /// <summary>Adds a field (with its caption) to a container.</summary>
        private TextField AddField(VisualElement parent, string label, bool password = false)
        {
            var f = Field(label, password, out var wrap);
            parent.Add(wrap);
            return f;
        }

        /// <summary>Section heading with breathing room above and below.</summary>
        private Label Heading(string text)
        {
            var h = UiKit.Text(text, 42, UiKit.Red);
            h.style.marginTop = 30;
            h.style.marginBottom = 10;
            h.style.minHeight = 52;
            return h;
        }

        private Button SmallButton(string text, System.Action a, Color bg, Color fg) 
        {
            var b = UiKit.Button(text, a, bg, fg, 36, 100);
            b.style.marginTop = 4;
            b.style.marginBottom = 8;
            return b;
        }

        private void BuildAccount()
        {
            _account = ModalShell("account", "ACCOUNT", out var card);
            var scroll = UiKit.Scroll(1350);
            _accountBody = UiKit.Stack();
            scroll.Add(_accountBody);
            card.Add(scroll);
            card.Add(UiKit.Button("BACK", CloseModal, Color.white, UiKit.Ink, 46, 120));
        }

        private void OpenAccount()
        {
            RefreshAccount();
            OpenModal(_account);
        }

        private void RefreshAccount()
        {
            var o = Services.Online;
            var body = _accountBody;
            body.Clear();
            if (!o.Configured)
            {
                body.Add(Note("Online features (cloud save, rankings, friends) are not set up in this build.", 36, UiKit.Ink));
                string why = OnlineConfig.Active.Problem;
                if (why != null) body.Add(Note(why, 32, UiKit.Red));
                body.Add(Note("Developer: create a free Supabase project, run supabase/migrations, then paste the Project URL and anon key into Assets/JeepneyRush/Resources/OnlineConfig. See docs/SUPABASE_SETUP.md."));
                body.Add(Note("Your progress is saved on this phone and the whole game works offline."));
                return;
            }
            string status = o.State == OnlineState.Online ? "Online" : o.State == OnlineState.Connecting ? "Connecting…" : o.State == OnlineState.Offline ? "Offline – will retry" : "Signed out";
            body.Add(Note($"Status: {status}{(o.QueuedRuns > 0 ? $" · {o.QueuedRuns} run(s) waiting to upload" : "")}", 34, UiKit.Ink));
            if (!string.IsNullOrEmpty(o.LastError) && o.State != OnlineState.Online) body.Add(Note(OnlineService.Friendly(o.LastError), 28, UiKit.Red));

            if (!o.SignedIn)
            {
                body.Add(Note("Sign in to save your progress in the cloud and appear on the rankings."));
                var email = AddField(body, "Email");
                if (string.IsNullOrEmpty(email.value)) email.value = SupabaseClient.RememberedEmail; // re-signing in after a lost login
                var pass = AddField(body, "Password", true);
                body.Add(SmallButton("SIGN IN", () => o.SignIn(email.value, pass.value, (ok, msg) => { Toast(ok ? "Welcome back!" : msg); RefreshAccount(); }), UiKit.Yellow, UiKit.Ink));
                body.Add(SmallButton("CREATE ACCOUNT", () => o.CreateEmailAccount(email.value, pass.value, (ok, msg) => { Toast(ok ? "Account created!" : msg); RefreshAccount(); }), Color.white, UiKit.Ink));
                body.Add(SmallButton("PLAY AS GUEST", () => { o.PlayAsGuest(); Toast("Creating guest account…"); }, Color.white, UiKit.Ink));
                return;
            }

            var snap = o.Snapshot;
            // ---- profile
            body.Add(Heading("PROFILE"));
            var name = AddField(body, "Display name");
            name.value = snap?.display_name ?? "";
            body.Add(SmallButton("SAVE NAME", () =>
            {
                var clean = OnlineText.CleanName(name.value);
                if (clean == null) { Toast("Name: 3–16 letters, numbers, spaces."); return; }
                if (!o.Authoritative) { Toast("Kailangan ng internet."); return; }
                o.SetName(clean, (ok, msg) => { Toast(ok ? "Name saved!" : msg); RefreshAccount(); });
            }, Color.white, UiKit.Ink));
            if (snap != null)
            {
                body.Add(Note($"Level {snap.level} · best {snap.best_score:N0} pts · {snap.runs:N0} runs", 32, UiKit.Ink));
                var codeRow = UiKit.Box();
                codeRow.style.flexDirection = FlexDirection.Row;
                codeRow.style.alignItems = Align.Center;
                codeRow.style.justifyContent = Justify.Center;
                codeRow.style.marginTop = 8;
                codeRow.style.marginBottom = 8;
                codeRow.style.minHeight = 110;
                codeRow.Add(UiKit.Text($"Friend code: {snap.friend_code}", 40, UiKit.Blue));
                var copy = SmallButton("COPY", () => { GUIUtility.systemCopyBuffer = snap.friend_code; Toast("Code copied!"); }, Color.white, UiKit.Ink);
                copy.style.width = 180;
                copy.style.marginLeft = 16;
                codeRow.Add(copy);
                body.Add(codeRow);
            }

            // ---- account type
            body.Add(Heading(o.IsGuest ? "GUEST ACCOUNT" : "EMAIL ACCOUNT"));
            if (o.IsGuest)
            {
                body.Add(Note("Guest progress lives only on this phone's account. Add an email to keep it if you change or reset your phone."));
                var email = AddField(body, "Email");
                var pass = AddField(body, "Password (6+ characters)", true);
                body.Add(SmallButton("SAVE PROGRESS WITH EMAIL", () => o.CreateEmailAccount(email.value, pass.value, (ok, msg) => { Toast(ok ? "Progress saved to your email!" : msg); RefreshAccount(); }), UiKit.Yellow, UiKit.Ink));
                body.Add(SmallButton("SIGN IN TO ANOTHER ACCOUNT", () => Confirm("Switch account?", "This phone will show the other account's progress. Your guest progress stays on the guest account and can't be recovered without an email.",
                    () => o.SignIn(email.value, pass.value, (ok, msg) => { Toast(ok ? "Signed in!" : msg); RefreshAccount(); })), Color.white, UiKit.Ink));
            }
            else
            {
                body.Add(Note($"Signed in as {o.Email}", 32, UiKit.Ink));
                body.Add(SmallButton("SIGN OUT", () => Confirm("Sign out?", "Your progress stays safe on your account. This phone goes back to an empty garage until you sign in again.",
                    () => o.SignOut(() => { Toast("Signed out"); RefreshAccount(); ShowMainMenu(); })), UiKit.Hex("#E5484D"), Color.white));
            }

            // ---- friends
            body.Add(Heading("FRIENDS"));
            var code = AddField(body, "Friend's code");
            var list = UiKit.Box("friends");
            body.Add(SmallButton("ADD FRIEND", () => o.AddFriend(code.value, (ok, msg) => { Toast(ok ? "Friend added!" : msg); if (ok) LoadFriends(list); }), UiKit.Yellow, UiKit.Ink));
            body.Add(list);
            LoadFriends(list);

            // ---- privacy: delete the account (Google Play requires this in the app)
            body.Add(Heading("PRIVACY"));
            var rel = ReleaseConfig.Active;
            if (!string.IsNullOrWhiteSpace(rel.privacyPolicyUrl))
                body.Add(SmallButton("PRIVACY POLICY", () => Application.OpenURL(rel.privacyPolicyUrl.Trim()), Color.white, UiKit.Ink));
            body.Add(Note("Deleting your account erases your coins, gems, jeepneys, scores and friends from the server and this phone. Purchases can't be restored afterwards."));
            body.Add(SmallButton("DELETE ACCOUNT", () => Confirm("Delete account?", "Everything on this account will be erased for good: coins, gems, jeepneys, upgrades, scores, friends and purchases.",
                () => Confirm("Sigurado ka ba?", "This can't be undone. Tap YES to delete your account now.",
                    () => o.DeleteAccount((ok, msg) =>
                    {
                        Toast(ok ? "Account deleted" : msg);
                        if (ok) { RefreshAccount(); ShowMainMenu(); }
                    }))), UiKit.Hex("#E5484D"), Color.white));
        }

        private void LoadFriends(VisualElement list)
        {
            list.Clear();
            list.Add(Note("Loading friends…"));
            Services.Online.Friends((fl, err) =>
            {
                list.Clear();
                if (fl == null) { list.Add(Note(err ?? "Could not load friends.")); return; }
                if (fl.friends.Length == 0) { list.Add(Note("No friends yet – share your code!")); return; }
                foreach (var f in fl.friends)
                {
                    var row = UiKit.Box();
                    row.style.flexDirection = FlexDirection.Row;
                    row.style.alignItems = Align.Center;
                    row.style.backgroundColor = Color.white;
                    UiKit.Border(row.style, 3, UiKit.Hex("#E2DBC6"), 20);
                    UiKit.Pad(row.style, 8, 16);
                    row.style.marginBottom = 8;
                    var t = UiKit.Text($"{f.display_name} · LV {f.level} · {f.best_score:N0}", 32, UiKit.Ink, false);
                    t.style.flexGrow = 1;
                    t.style.unityTextAlign = TextAnchor.MiddleLeft;
                    row.Add(t);
                    var id = f.user_id;
                    var x = UiKit.Button("X", () => Services.Online.RemoveFriend(id, (ok, msg) => { if (ok) LoadFriends(list); else Toast(msg); }), UiKit.Hex("#FFE3E3"), UiKit.Red, 30, 70);
                    x.style.width = 90;
                    x.style.marginTop = x.style.marginBottom = 0;
                    row.Add(x);
                    list.Add(row);
                }
            });
        }

        // ============================================================== rankings
        private void BuildRanks()
        {
            _ranks = ModalShell("ranks", "RANKINGS", out var card);
            var boards = UiKit.Box();
            boards.style.flexDirection = FlexDirection.Row;
            string[] bnames = { "ALL-TIME", "WEEKLY", "DAILY", "RUSH", "NIGHT" }, bkeys = RankBoardKeys;
            for (int i = 0; i < bkeys.Length; i++)
            {
                string k = bkeys[i];
                var b = UiKit.Button(bnames[i], () => { _rankBoard = k; LoadRanks(); }, Color.white, UiKit.Ink, 24, 86);
                b.style.flexGrow = 1;
                b.style.flexBasis = 0;
                b.style.marginRight = i < bkeys.Length - 1 ? 6 : 0;
                _rankBoardBtns[i] = b;
                boards.Add(b);
            }
            card.Add(boards);
            var scopes = UiKit.Box();
            scopes.style.flexDirection = FlexDirection.Row;
            string[] snames = { "EVERYONE", "FRIENDS" }, skeys = { "global", "friends" };
            for (int i = 0; i < 2; i++)
            {
                string k = skeys[i];
                var b = UiKit.Button(snames[i], () => { _rankScope = k; LoadRanks(); }, Color.white, UiKit.Ink, 30, 84);
                b.style.flexGrow = 1;
                b.style.marginRight = i < 1 ? 8 : 0;
                _rankScopeBtns[i] = b;
                scopes.Add(b);
            }
            card.Add(scopes);
            // stat classes: boosted jeepneys are ranked with jeepneys of similar power
            var classes = UiKit.Box();
            classes.style.flexDirection = FlexDirection.Row;
            for (int i = 0; i <= JeepneyClasses.Count; i++)
            {
                int c = i < JeepneyClasses.Count ? i : -1;
                string label = c < 0 ? "EVERYONE" : JeepneyClasses.Name((JeepneyClass)c);
                var b = UiKit.Button(label, () => { _rankClass = c; LoadRanks(); }, Color.white, UiKit.Ink, 26, 80);
                b.style.flexGrow = 1;
                b.style.marginRight = i < JeepneyClasses.Count ? 6 : 0;
                _rankClassBtns[i] = b;
                classes.Add(b);
            }
            card.Add(classes);
            _rankInfo = Note("", 28);
            card.Add(_rankInfo);
            var scroll = UiKit.Scroll(900);
            scroll.style.height = 900;
            _rankList = UiKit.Stack();
            scroll.Add(_rankList);
            card.Add(scroll);
            card.Add(UiKit.Button("BACK", CloseModal, Color.white, UiKit.Ink, 46, 120));
        }

        private void OpenRanks(string board = "all")
        {
            _rankBoard = board;
            // always open on the class of the jeepney you're driving
            _rankClass = (int)JeepneyClasses.For(Services.Save.Data.selectedJeepney);
            OpenModal(_ranks);
            LoadRanks();
        }

        private void LoadRanks()
        {
            string[] bkeys = RankBoardKeys, skeys = { "global", "friends" };
            for (int i = 0; i < bkeys.Length; i++) _rankBoardBtns[i].style.backgroundColor = bkeys[i] == _rankBoard ? UiKit.Yellow : Color.white;
            for (int i = 0; i < 2; i++) _rankScopeBtns[i].style.backgroundColor = skeys[i] == _rankScope ? UiKit.Yellow : Color.white;
            if (_rankClass < -1) _rankClass = (int)JeepneyClasses.For(Services.Save.Data.selectedJeepney);
            for (int i = 0; i <= JeepneyClasses.Count; i++)
            {
                int c = i < JeepneyClasses.Count ? i : -1;
                bool on = c == _rankClass;
                _rankClassBtns[i].style.backgroundColor = on ? (c < 0 ? UiKit.Yellow : UiKit.Hex(JeepneyClasses.Colour((JeepneyClass)c))) : Color.white;
                _rankClassBtns[i].style.color = on && c >= 0 ? Color.white : UiKit.Ink;
            }
            _rankList.Clear();
            var o = Services.Online;
            _rankInfo.text = (_rankBoard == "week" ? "Best score this week (resets Monday, PH time)" : _rankBoard == "daily" ? "Today's Daily Challenge – same road for everyone"
                    : _rankBoard == "rush" ? "Rush Hour – best 3-minute score this week" : _rankBoard == "night" ? "Night Shift – best score this week" : "Best score ever")
                + (_rankClass < 0 ? "\nAll jeepneys together" : "\n" + JeepneyClasses.Blurb((JeepneyClass)_rankClass));
            if (!o.Configured) { _rankList.Add(Note("Rankings need the online backend – see ACCOUNT for how to enable it.", 34, UiKit.Ink)); return; }
            if (!o.SignedIn) { _rankList.Add(Note("Sign in (or play as guest) under ACCOUNT to see the rankings.", 34, UiKit.Ink)); return; }
            _rankList.Add(Note("Loading…"));
            int wantClass = _rankClass;
            o.Leaderboard(_rankBoard, _rankScope, wantClass, (page, err) =>
            {
                _rankList.Clear();
                if (page == null) { _rankList.Add(Note(err ?? "Could not load rankings.", 34, UiKit.Red)); return; }
                if (wantClass >= 0 && page.@class < 0) _rankList.Add(Note("Class rankings need the latest server update – showing everyone.", 26, UiKit.Grey));
                if (page.rows.Length == 0) _rankList.Add(Note(_rankScope == "friends" ? "No scores from friends yet." : "No scores yet – be the first!", 34, UiKit.Ink));
                bool meShown = false;
                foreach (var r in page.rows) { _rankList.Add(RankRow(r)); meShown |= r.is_me; }
                if (!meShown && page.me != null && page.me.rank > 0)
                {
                    _rankList.Add(Note("…", 34));
                    _rankList.Add(RankRow(page.me));
                }
            });
        }

        private VisualElement RankRow(LeaderboardRow r)
        {
            var row = UiKit.Box();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.backgroundColor = r.is_me ? UiKit.Hex("#FFF1B8") : Color.white;
            UiKit.Border(row.style, 3, r.is_me ? UiKit.Yellow : UiKit.Hex("#E2DBC6"), 20);
            UiKit.Pad(row.style, 8, 16);
            row.style.marginBottom = 8;
            var rank = UiKit.Text($"#{r.rank}", 40, r.rank == 1 ? UiKit.Hex("#D4A017") : r.rank <= 3 ? UiKit.Blue : UiKit.Ink);
            rank.style.width = 130;
            row.Add(rank);
            var name = UiKit.Text($"{r.display_name}  · LV {r.level}", 32, UiKit.Ink, false);
            name.style.flexGrow = 1;
            name.style.unityTextAlign = TextAnchor.MiddleLeft;
            row.Add(name);
            var score = UiKit.Text($"{r.score:N0}", 38, UiKit.Ink);
            row.Add(score);
            return row;
        }

        // ============================================================== daily challenge
        private void BuildDaily()
        {
            _daily = ModalShell("daily", "DAILY CHALLENGE", out var card);
            _dailyInfo = Note("", 36, UiKit.Ink);
            card.Add(_dailyInfo);
            card.Add(Note("Everyone in the Philippines gets the same road today: the same traffic, stops, weather and lights. Your best score counts on today's ranking."));
            card.Add(UiKit.Button("PLAY TODAY'S ROUTE", () => { CloseModal(); Services.Game.PlayDaily(_dailyToday); }, UiKit.Yellow, UiKit.Ink, 54, 150));
            card.Add(UiKit.Button("TODAY'S RANKING", () => { CloseModal(); OpenRanks("daily"); }, Color.white, UiKit.Ink, 44, 120));
            card.Add(UiKit.Button("BACK", CloseModal, Color.white, UiKit.Ink, 46, 120));
        }

        private void OpenDaily()
        {
            _dailyToday = DailyChallenge.Today;
            ShowDailyInfo();
            OpenModal(_daily);
            Services.Online.Daily(c => { _dailyToday = c; ShowDailyInfo(); });
        }

        private void ShowDailyInfo()
        {
            int k = _dailyToday.DayKey;
            var date = new System.DateTime(k / 10000, (k / 100) % 100, k % 100);
            _dailyInfo.text = $"{date:dddd, MMMM d}\nRoute: {Environments.Get(_dailyToday.Environment).Name}";
        }

        // ============================================================== game over
        private void PrepareOnlineGameOver()
        {
            if (_goOnline == null) return;
            var o = Services.Online;
            bool daily = Services.Game.Session != null && Services.Game.Session.IsDaily;
            if (o == null || !o.Configured) { _goOnline.text = daily ? "Daily Challenge (offline – not ranked)" : ""; return; }
            if (!o.SignedIn) { _goOnline.text = "Sign in under ACCOUNT to post your scores."; return; }
            _goOnline.text = o.State == OnlineState.Online ? "Uploading run…" : "Saved – will upload when you're online.";
        }

        private void OnRunSubmitted(SubmitResult r)
        {
            if (_goOnline == null) return;
            if (!r.accepted && !r.duplicate) { _goOnline.text = $"Run not counted online ({r.reason})."; return; }
            var parts = new System.Collections.Generic.List<string>();
            if (r.rank_daily > 0) parts.Add($"#{r.rank_daily} today");
            if (r.rank_week > 0) parts.Add($"#{r.rank_week} this week");
            if (r.rank_all > 0) parts.Add($"#{r.rank_all} all-time");
            if (!string.IsNullOrEmpty(r.mode) && r.mode_rank > 0)
            {
                var m = GameModes.FromKey(r.mode);
                _goOnline.text = $"{GameModes.Name(m)} THIS WEEK: #{r.mode_rank}"
                    + (r.mode_class_rank > 0 && r.@class >= 0 && r.@class < JeepneyClasses.Count ? $" · #{r.mode_class_rank} in {JeepneyClasses.Name((JeepneyClass)r.@class)}" : "");
            }
            else if (r.class_rank_all > 0 && r.@class >= 0 && r.@class < JeepneyClasses.Count)
            {
                // ranks within your jeepney's stat class first: that's who you're really racing
                parts.Clear();
                if (r.class_rank_daily > 0) parts.Add($"#{r.class_rank_daily} today");
                if (r.class_rank_week > 0) parts.Add($"#{r.class_rank_week} this week");
                parts.Add($"#{r.class_rank_all} all-time");
                _goOnline.text = JeepneyClasses.Name((JeepneyClass)r.@class) + " CLASS RANK: " + string.Join(" · ", parts);
            }
            else _goOnline.text = parts.Count > 0 ? "RANK: " + string.Join(" · ", parts) : "Run uploaded.";
            if (r.clamped) _goOnline.text += " (adjusted by the server)";
            if (_menu != null) RefreshMenuTop();
        }
    }
}
