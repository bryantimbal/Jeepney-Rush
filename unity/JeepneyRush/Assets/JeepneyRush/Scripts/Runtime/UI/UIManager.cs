using System.Collections.Generic;
using JeepneyRush.Logic;
using UnityEngine;
using UnityEngine.UIElements;

namespace JeepneyRush
{
    /// <summary>
    /// Every screen of the game, built with UI Toolkit in code: splash, main menu,
    /// garage, settings, gameplay HUD, pause, game over, tutorial and floating feedback.
    /// Portrait reference resolution 1080x1920, safe-area aware.
    /// </summary>
    public sealed partial class UIManager : MonoBehaviour
    {
        public enum FloatKind { Good, Bad, Info, Big }

        private UIDocument _doc;
        private VisualElement _root, _safe;
        private VisualElement _splash, _menu, _hud, _pause, _gameOver, _settings, _garage, _confirm, _loading, _tutorial, _floats, _flash, _missions, _routes;
        private VisualElement _splashBar, _loadingBar, _splashJeep;
        private Label _splashText, _menuCoins, _menuBest;
        private Label _score, _distance, _coins, _pax, _stopName, _stopMeta, _speed, _debug;
        private Image _stopArrowL, _stopArrowR;
        private readonly List<Image> _hearts = new List<Image>();
        private int _lastHealth = -1, _lastScore = -1, _lastCoins = -1, _lastPax = -1, _lastDist = -1, _lastSpeed = -1;
        private long _lastStop = long.MaxValue;
        private readonly Stack<VisualElement> _modals = new Stack<VisualElement>();
        private System.Action _confirmYes, _confirmCancel;
        private Label _confirmTitle, _confirmText;
        private bool _showDebug;

        // ======================================================================= setup
        private void Awake()
        {
            var host = new GameObject("UI");
            host.transform.SetParent(transform, false);
            host.SetActive(false);
            var ps = ScriptableObject.CreateInstance<PanelSettings>();
            ps.name = "JR_PanelSettings";
            ps.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            ps.referenceResolution = new Vector2Int(1080, 1920);
            ps.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            ps.match = 0f; // match width: portrait layouts scale with screen width
            ps.themeStyleSheet = Resources.Load<ThemeStyleSheet>("UI/JR_Theme");
            if (ps.themeStyleSheet == null) ps.themeStyleSheet = ScriptableObject.CreateInstance<ThemeStyleSheet>();
            ps.sortingOrder = 10;
            _doc = host.AddComponent<UIDocument>();
            _doc.panelSettings = ps;
            host.SetActive(true);
            _root = _doc.rootVisualElement;
            _root.style.flexGrow = 1;
            _safe = UiKit.Box("safe");
            _safe.style.position = Position.Absolute;
            _safe.style.left = _safe.style.right = _safe.style.top = _safe.style.bottom = 0;
            _root.Add(_safe);
            _root.RegisterCallback<GeometryChangedEvent>(_ => ApplySafeArea());

            // each screen is built in isolation: a bug in one must never leave the others
            // stacked on top of each other (HideAll below always runs)
            foreach (var build in new System.Action[] { BuildHud, BuildJuice, BuildMenu, BuildPause, BuildGameOver, BuildSettings, BuildControls,
                         BuildGarage, BuildMissions, BuildRoutes, BuildOnline, BuildShop, BuildRevive, BuildConfirm, BuildLoading, BuildSplash, BuildDevSplash, BuildTestScreens })
            {
                try { build(); }
                catch (System.Exception e) { Debug.LogException(e); }
            }
            _floats = UiKit.Box("floats");
            _floats.style.position = Position.Absolute;
            _floats.style.left = _floats.style.right = 0;
            _floats.style.top = Length.Percent(25f); // above the warning banner, clear of the road
            _floats.style.alignItems = Align.Center;
            _root.Add(_floats);
            _flash = UiKit.Box("flash");
            _flash.style.position = Position.Absolute;
            _flash.style.left = _flash.style.right = _flash.style.top = _flash.style.bottom = 0;
            UiKit.Border(_flash.style, 60, new Color(1, 0, 0, 0.6f), 0);
            _flash.style.opacity = 0;
            _root.Add(_flash);
            BuildScreenshotUi();
            HideAll();
            GameEvents.StateChanged += OnState;
        }

        private void ApplySafeArea()
        {
            float w = _root.resolvedStyle.width;
            if (float.IsNaN(w) || w <= 0 || Screen.width <= 0) return;
            float k = w / Screen.width;
            var sa = Screen.safeArea;
            // while the 9:16 store frame is on, the UI lives inside the frame too
            var f = Screenshots.Frame;
            float xMin = Mathf.Max(sa.xMin, f.X), xMax = Mathf.Min(sa.xMax, f.X + f.W);
            float yMin = Mathf.Max(sa.yMin, f.Y), yMax = Mathf.Min(sa.yMax, f.Y + f.H);
            _safe.style.left = xMin * k;
            _safe.style.right = (Screen.width - xMax) * k;
            _safe.style.top = (Screen.height - yMax) * k;
            _safe.style.bottom = yMin * k;
            LayoutFrameBars(k);
        }

        private VisualElement MakeScreen(string name, bool safe = true)
        {
            var s = UiKit.Box(name);
            s.style.position = Position.Absolute;
            s.style.left = s.style.right = s.style.top = s.style.bottom = 0;
            (safe ? _safe : _root).Add(s);
            return s;
        }

        private void HideAll()
        {
            foreach (var e in new[] { _splash, _dev, _menu, _hud, _pause, _gameOver, _settings, _controls, _garage, _confirm, _loading, _missions, _routes, _account, _ranks, _daily, _shop, _revive })
                if (e != null) e.style.display = DisplayStyle.None;
            _modals.Clear();
        }

        private static void Show(VisualElement e, bool on) => e.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;

        private void OnState(GameState from, GameState to)
        {
            Show(_pause, to == GameState.Paused);
            if (to == GameState.Paused) { SyncToggles(); UiKit.PopIn(_pause.Q("card")); }
            if (to == GameState.MainMenu) ShowMainMenu();
            // banner ads only on menus / game over, never while driving (AdPolicy)
            Services.Ads?.ShowBannerOnMenus(to == GameState.MainMenu || to == GameState.GameOver);
            if (to == GameState.Playing || to == GameState.Starting) Show(_loading, false);
        }

        // ======================================================================= splash & loading
        private void BuildSplash()
        {
            _splash = MakeScreen("splash", false);
            _splash.style.backgroundImage = UiKit.Tex("ui_splash_bg");
            _splash.style.alignItems = Align.Center;
            _splash.style.justifyContent = Justify.Center;
            // smaller, centred logo; height from the texture's own aspect ratio so it never stretches
            const float jrLogoW = 680f;
            var logo = UiKit.Icon("logo", jrLogoW);
            var jrTex = UiKit.Tex("logo");
            logo.style.height = jrTex != null && jrTex.width > 0 ? jrLogoW * jrTex.height / jrTex.width : jrLogoW * 520f / 1024f;
            logo.style.marginBottom = 24;
            _splash.Add(logo);
            var tag = UiKit.Text("The Ultimate Pinoy Driving Challenge!", 40, UiKit.Ink, false);
            tag.style.backgroundColor = UiKit.Yellow;
            UiKit.Border(tag.style, 6, UiKit.Ink, 50);
            UiKit.Pad(tag.style, 14, 34);
            _splash.Add(tag);
            _splashJeep = UiKit.Icon("ui_jeepney", 440);
            _splashJeep.style.height = 220;
            _splashJeep.style.marginTop = 80;
            _splash.Add(_splashJeep);
            var bar = UiKit.Box();
            bar.style.width = 640;
            bar.style.height = 44;
            bar.style.marginTop = 70;
            bar.style.backgroundColor = new Color(1, 1, 1, 0.35f);
            UiKit.Border(bar.style, 6, UiKit.Ink, 30);
            bar.style.overflow = Overflow.Hidden;
            _splashBar = UiKit.Box();
            _splashBar.style.backgroundColor = UiKit.Yellow;
            _splashBar.style.height = Length.Percent(100);
            _splashBar.style.width = Length.Percent(0);
            bar.Add(_splashBar);
            _splash.Add(bar);
            _splashText = UiKit.Text("Starting engine…", 36, Color.white, false);
            _splashText.style.marginTop = 20;
            _splash.Add(_splashText);
            // drive the little jeepney back and forth
            _splashJeep.schedule.Execute(t =>
            {
                float s = t.now / 1000f;
                _splashJeep.style.translate = new Translate(Mathf.Sin(s * 2.6f) * 40f, Mathf.Abs(Mathf.Sin(s * 18f)) * -6f, 0);
            }).Every(16);
        }

        // ------------------------------------------------------------------ developer splash (Ninja Tech)
        private VisualElement _dev, _devLogo, _devFill;
        private Label _devText;
        private float _devTarget, _devShown, _devStart;
        private static readonly Color DevNavy = new Color32(10, 24, 63, 255);

        /// <summary>
        /// Startup screen: the Ninja Tech logo (fades and settles in like the studio's intro video)
        /// with a REAL loading bar that follows the game's actual loading progress.
        /// </summary>
        private void BuildDevSplash()
        {
            _dev = MakeScreen("devSplash", false);
            _dev.style.backgroundColor = DevNavy;
            _dev.style.alignItems = Align.Center;
            _dev.style.justifyContent = Justify.Center;
            // size from the texture's own aspect ratio, so the logo can never be stretched
            var logoTex = UiKit.Tex("ninjatech_logo");
            const float logoW = 560f; // a little smaller and better balanced on every screen
            _devLogo = UiKit.Icon("ninjatech_logo", logoW);
            _devLogo.style.height = logoTex != null && logoTex.width > 0 ? logoW * logoTex.height / logoTex.width : logoW * 1024f / 902f;
            _dev.Add(_devLogo);
            var track = UiKit.Box();
            track.style.width = 646;
            track.style.height = 20;
            track.style.marginTop = 190;
            track.style.backgroundColor = (Color)new Color32(36, 49, 93, 255);
            UiKit.Border(track.style, 0, Color.clear, 10);
            track.style.overflow = Overflow.Hidden;
            _devFill = UiKit.Box();
            _devFill.style.height = Length.Percent(100);
            _devFill.style.width = Length.Percent(0);
            _devFill.style.backgroundColor = (Color)new Color32(205, 15, 36, 255);
            UiKit.Border(_devFill.style, 0, Color.clear, 10);
            track.Add(_devFill);
            _dev.Add(track);
            _devText = UiKit.Text("LOADING", 28, (Color)new Color32(205, 206, 238, 255), false);
            _devText.style.letterSpacing = 8;
            _devText.style.marginTop = 46;
            _dev.Add(_devText);
            // Philippine flag stripe along the bottom (blue · yellow · red)
            var stripe = UiKit.Box();
            stripe.style.position = Position.Absolute;
            stripe.style.left = stripe.style.right = stripe.style.bottom = 0;
            stripe.style.height = 36;
            stripe.style.flexDirection = FlexDirection.Row;
            foreach (var (c, w) in new[] { (new Color32(10, 58, 167, 255), 47f), (new Color32(253, 208, 30, 255), 6f), (new Color32(205, 15, 36, 255), 47f) })
            {
                var seg = UiKit.Box();
                seg.style.width = Length.Percent(w);
                seg.style.backgroundColor = (Color)c;
                stripe.Add(seg);
            }
            _dev.Add(stripe);
            // logo intro + a bar that glides toward the real progress (never jumps, never fakes it)
            _dev.schedule.Execute(() =>
            {
                if (_dev.style.display == DisplayStyle.None) return;
                float t = Time.realtimeSinceStartup - _devStart;
                float k = Mathf.Clamp01(t / 0.7f);
                float ease = 1f - Mathf.Pow(1f - k, 3f);
                _devLogo.style.opacity = ease;
                _devLogo.style.scale = new Scale(new Vector3(0.86f + 0.14f * ease, 0.86f + 0.14f * ease, 1));
                _devShown = Mathf.MoveTowards(_devShown, _devTarget, Mathf.Max(0.004f, (_devTarget - _devShown) * 0.12f));
                _devFill.style.width = Length.Percent(_devShown * 100f);
                _devText.text = $"LOADING  {Mathf.RoundToInt(_devShown * 100f)}%";
            }).Every(16);
        }

        public void ShowSplash()
        {
            HideAll();
            _devTarget = _devShown = 0;
            _devStart = Time.realtimeSinceStartup;
            Show(_dev, true);
        }

        /// <summary>Fades the developer splash out (unscaled time), then hides it.</summary>
        public System.Collections.IEnumerator FadeOutSplash(float seconds = 0.35f)
        {
            if (_dev == null || _dev.style.display == DisplayStyle.None) yield break;
            float t = 0;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                _dev.style.opacity = 1f - Mathf.Clamp01(t / seconds);
                yield return null;
            }
            Show(_dev, false);
            _dev.style.opacity = 1f;
        }

        /// <summary>Seconds the developer splash has been on screen.</summary>
        public float SplashSeconds => Time.realtimeSinceStartup - _devStart;
        /// <summary>True once the on-screen bar has caught up with 100 %.</summary>
        public bool SplashBarFull => _devShown >= 0.995f;

        public void SetSplashProgress(float p, string text)
        {
            _devTarget = Mathf.Max(_devTarget, Mathf.Clamp01(p)); // a loading bar only ever moves forward
            if (_splashBar != null) _splashBar.style.width = Length.Percent(Mathf.Clamp01(p) * 100f);
            if (_splashText != null) _splashText.text = text;
        }

        private void BuildLoading()
        {
            _loading = MakeScreen("loading", false);
            _loading.style.backgroundColor = new Color(UiKit.Ink.r, UiKit.Ink.g, UiKit.Ink.b, 0.92f);
            _loading.style.alignItems = Align.Center;
            _loading.style.justifyContent = Justify.Center;
            _loading.Add(UiKit.Text("BIYAHE NA!", 90, UiKit.Yellow, true, 3));
            var bar = UiKit.Box();
            bar.style.width = 560;
            bar.style.height = 36;
            bar.style.marginTop = 40;
            bar.style.backgroundColor = new Color(1, 1, 1, 0.2f);
            UiKit.Border(bar.style, 5, UiKit.Yellow, 24);
            _loadingBar = UiKit.Box();
            _loadingBar.style.backgroundColor = UiKit.Yellow;
            _loadingBar.style.height = Length.Percent(100);
            bar.Add(_loadingBar);
            _loading.Add(bar);
        }

        public void ShowLoading(float p)
        {
            Show(_loading, p < 1f);
            _loadingBar.style.width = Length.Percent(Mathf.Clamp01(p) * 100f);
        }

        // ======================================================================= main menu
        private Label _menuLevel;
        private VisualElement _menuXpFill, _missionDot;

        private Label _playSub;

        private void BuildMenu()
        {
            _menu = MakeScreen("menu");
            _menu.style.alignItems = Align.Center;
            var top = UiKit.Box();
            top.style.flexDirection = FlexDirection.Row;
            top.style.justifyContent = Justify.SpaceBetween;
            top.style.alignItems = Align.Center;
            top.style.alignSelf = Align.Stretch;
            UiKit.Pad(top.style, 26, 30);
            // level badge with XP bar
            var lvl = UiKit.Box();
            lvl.style.flexDirection = FlexDirection.Row;
            lvl.style.alignItems = Align.Center;
            lvl.style.backgroundColor = new Color(UiKit.Ink.r, UiKit.Ink.g, UiKit.Ink.b, 0.82f);
            UiKit.Border(lvl.style, 4, new Color(1, 1, 1, 0.25f), 60);
            UiKit.Pad(lvl.style, 8, 22);
            lvl.style.paddingLeft = 8;
            lvl.Add(UiKit.Icon("ui_star", 62));
            var lvlCol = UiKit.Box();
            lvlCol.style.marginLeft = 10;
            _menuLevel = UiKit.Text("LV 1", 38, Color.white);
            _menuLevel.style.unityTextAlign = TextAnchor.MiddleLeft;
            lvlCol.Add(_menuLevel);
            var xpBar = UiKit.Box();
            xpBar.style.width = 150;
            xpBar.style.height = 14;
            xpBar.style.backgroundColor = new Color(1, 1, 1, 0.2f);
            UiKit.Border(xpBar.style, 0, Color.clear, 7);
            xpBar.style.overflow = Overflow.Hidden;
            _menuXpFill = UiKit.Box();
            _menuXpFill.style.height = Length.Percent(100);
            _menuXpFill.style.backgroundColor = UiKit.Yellow;
            xpBar.Add(_menuXpFill);
            lvlCol.Add(xpBar);
            lvl.Add(lvlCol);
            top.Add(lvl);
            _menuCoins = UiKit.Text("0", 46, Color.white);
            var wallet = UiKit.Box();
            wallet.style.alignItems = Align.FlexEnd;
            wallet.Add(UiKit.Pill(UiKit.Icon("ui_coin", 60), _menuCoins));
            wallet.Add(BuildMenuGems());
            top.Add(wallet);
            _menu.Add(top);
            var bestRow = UiKit.Box();
            bestRow.style.flexDirection = FlexDirection.Row;
            bestRow.style.alignSelf = Align.FlexEnd;
            bestRow.style.marginRight = 30;
            var bestLabel = UiKit.Text("BEST", 28, UiKit.Yellow, false);
            _menuBest = UiKit.Text("0 m", 40, Color.white);
            bestRow.Add(UiKit.Pill(bestLabel, _menuBest));
            _menu.Add(bestRow);
            _menu.Add(BuildOnlineChip());
            var logo = UiKit.Icon("logo", 780);
            logo.style.height = 400;
            _menu.Add(logo);
            var spacer = UiKit.Box();
            spacer.style.flexGrow = 1;
            _menu.Add(spacer);
            var buttons = UiKit.Box();
            buttons.style.width = 900;
            buttons.style.marginBottom = 24;
            // PLAY: big, glossy and gently "breathing" so it's the obvious thing to tap
            var playWrap = UiKit.Box();
            var play = UiKit.MenuButton("PLAY", "ui_m_play", UiKit.Yellow, () => Services.Game.Play(), 210, false, 104);
            play.Q<Label>().style.color = UiKit.Ink;
            play.Q<Label>().style.unityTextOutlineWidth = 0f;
            var playCol = UiKit.Box();
            playCol.style.alignItems = Align.FlexStart;
            var playLabel = play.Q<Label>();
            play.Remove(playLabel);
            playCol.Add(playLabel);
            _playSub = UiKit.Text("", 28, UiKit.Ink, false);
            _playSub.style.unityTextAlign = TextAnchor.MiddleLeft;
            _playSub.style.whiteSpace = WhiteSpace.NoWrap;
            _playSub.style.opacity = 0.8f;
            playCol.Add(_playSub);
            play.Add(playCol);
            playWrap.Add(play);
            float t0 = Time.realtimeSinceStartup;
            playWrap.schedule.Execute(() =>
            {
                float k = 1f + 0.022f * Mathf.Sin((Time.realtimeSinceStartup - t0) * 3.2f);
                playWrap.style.scale = new Scale(new Vector3(k, k, 1));
            }).Every(33);
            buttons.Add(playWrap);
            Button Small(string text, string icon, Color col, System.Action a, VisualElement row, bool last = false, bool three = false)
            {
                var b = UiKit.MenuButton(text, icon, col, a, three ? 150 : 140, three, three ? 36 : 46);
                b.style.flexGrow = 1;
                b.style.flexBasis = 0;
                if (!last) b.style.marginRight = 14;
                row.Add(b);
                return b;
            }
            var row1 = UiKit.Box();
            row1.style.flexDirection = FlexDirection.Row;
            Small("GARAGE", "ui_m_garage", UiKit.Hex("#E63946"), OpenGarage, row1);
            var missions = Small("MISSIONS", "ui_m_missions", UiKit.Hex("#2A9D8F"), OpenMissions, row1, true);
            _missionDot = UiKit.Box();
            _missionDot.style.position = Position.Absolute;
            _missionDot.style.right = 10;
            _missionDot.style.top = 10;
            _missionDot.style.width = _missionDot.style.height = 34;
            _missionDot.style.backgroundColor = UiKit.Red;
            UiKit.Border(_missionDot.style, 4, Color.white, 17);
            missions.Add(_missionDot);
            buttons.Add(row1);
            var row2 = UiKit.Box();
            row2.style.flexDirection = FlexDirection.Row;
            Small("ROUTES", "ui_m_routes", UiKit.Hex("#3A86FF"), OpenRoutes, row2, false, true);
            Small("SHOP", "ui_m_shop", UiKit.Hex("#9B5DE5"), OpenShop, row2, false, true);
            Small("SETTINGS", "ui_m_settings", UiKit.Hex("#5C677D"), () => OpenModal(_settings), row2, true, true);
            buttons.Add(row2);
            // Phase 3: online
            var row3 = UiKit.Box();
            row3.style.flexDirection = FlexDirection.Row;
            Small("DAILY", "ui_m_daily", UiKit.Hex("#FF8C42"), OpenDaily, row3, false, true);
            Small("RANKINGS", "ui_m_rank", UiKit.Hex("#E0A100"), () => OpenRanks(), row3, false, true);
            Small("ACCOUNT", "ui_m_account", UiKit.Hex("#06AED5"), OpenAccount, row3, true, true);
            buttons.Add(row3);
            _menu.Add(buttons);
            var ver = UiKit.Text($"v{Application.version} · Beep beep! Tap the horn on the road.", 26, new Color(1, 1, 1, 0.85f), false);
            ver.style.marginBottom = 16;
            _menu.Add(ver);
        }

        public void ShowMainMenu()
        {
            HideAll();
            Services.Save.RefreshMissions();
            RefreshMenuTop();
            Show(_menu, true);
            UiKit.PopIn(_menu.Q<Button>());
        }

        private void RefreshMenuTop()
        {
            var d = Services.Save.Data;
            _menuCoins.text = d.coins.ToString("N0");
            _menuBest.text = $"{Mathf.FloorToInt(d.bestDistance):N0} m";
            int lv = PlayerLevel.LevelFor(d.xp);
            int a = PlayerLevel.XpForLevel(lv), b = PlayerLevel.XpForLevel(lv + 1);
            _menuLevel.text = $"LV {lv}";
            _menuXpFill.style.width = Length.Percent(Mathf.Clamp01((d.xp - a) / (float)Mathf.Max(1, b - a)) * 100f);
            Show(_missionDot, HasClaimable());
            RefreshGems();
            if (_playSub != null)
            {
                var env = Logic.Environments.Get((Logic.EnvironmentKind)d.selectedEnvironment);
                var jeep = JeepneyCatalog.Get(d.selectedJeepney);
                _playSub.text = $"{env.Name.ToUpperInvariant()} · {jeep.DisplayName}{(jeep.SoundSystem && d.soundTrip ? " · SOUND TRIP" : "")}";
            }
        }

        // ======================================================================= modals
        private void OpenModal(VisualElement m)
        {
            if (m == _settings) SyncToggles();
            Show(m, true);
            _modals.Push(m);
            UiKit.PopIn(m.Q("card"));
        }

        private void CloseModal()
        {
            if (_modals.Count == 0) return;
            Show(_modals.Pop(), false);
        }

        /// <summary>Android back: close the top modal. Returns true if handled.</summary>
        public bool HandleBack()
        {
            if (_modals.Count == 0) return false;
            Services.Audio?.Click();
            bool garage = _modals.Peek() == _garage;
            CloseModal();
            if (garage)
            {
                Services.Game.Menu?.SetGarageMode(false);
                Services.Game.Menu?.ShowLivery(Services.Save.Data.selectedJeepney);
                ShowMainMenu();
            }
            return true;
        }

        private VisualElement ModalShell(string name, string title, out VisualElement card)
        {
            var screen = MakeScreen(name, false);
            var overlay = UiKit.Overlay(new Color(0.06f, 0.03f, 0.13f, 0.6f));
            screen.Add(overlay);
            card = UiKit.Card();
            var t = UiKit.Text(title, 84, UiKit.Red, true, 2.5f);
            t.style.marginBottom = 20;
            card.Add(t);
            overlay.Add(card);
            return screen;
        }

        private readonly List<(Label label, System.Func<bool> get)> _toggles = new List<(Label, System.Func<bool>)>();

        private VisualElement Toggle(string label, System.Func<bool> get, System.Action<bool> set)
        {
            var row = UiKit.Box();
            row.pickingMode = PickingMode.Position;
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.SpaceBetween;
            row.style.alignItems = Align.Center;
            row.style.backgroundColor = Color.white;
            UiKit.Border(row.style, 4, UiKit.Hex("#E2DBC6"), 26);
            UiKit.Pad(row.style, 18, 28);
            row.style.marginBottom = 14;
            var l = UiKit.Text(label, 44, UiKit.Ink, false);
            row.Add(l);
            var pill = UiKit.Text("", 38, Color.white);
            pill.style.width = 170;
            pill.style.height = 76;
            UiKit.Border(pill.style, 4, UiKit.Ink, 38);
            row.Add(pill);
            _toggles.Add((pill, get));
            row.RegisterCallback<ClickEvent>(_ =>
            {
                Services.Audio?.Click();
                set(!get());
                Services.Save.Save();
                SyncToggles();
            });
            return row;
        }

        private void SyncToggles()
        {
            foreach (var (label, get) in _toggles)
            {
                bool on = get();
                label.text = on ? "ON" : "OFF";
                label.style.backgroundColor = on ? UiKit.Green : UiKit.Hex("#B9B2C6");
            }
            foreach (var seg in _segments) seg();
        }

        private readonly List<System.Action> _segments = new List<System.Action>();

        private VisualElement Segmented(string title, string[] options, System.Func<int> get, System.Action<int> set)
        {
            var wrap = UiKit.Box();
            wrap.style.marginBottom = 14;
            var t = UiKit.Text(title.ToUpperInvariant(), 30, UiKit.Grey, false);
            t.style.unityTextAlign = TextAnchor.MiddleLeft;
            t.style.marginBottom = 6;
            wrap.Add(t);
            var row = UiKit.Box();
            row.style.flexDirection = FlexDirection.Row;
            var buttons = new List<Button>();
            for (int i = 0; i < options.Length; i++)
            {
                int idx = i;
                var b = UiKit.Button(options[i], () => { set(idx); Services.Save.Save(); SyncToggles(); }, Color.white, UiKit.Ink, 36, 96);
                b.style.flexGrow = 1;
                b.style.marginRight = i < options.Length - 1 ? 10 : 0;
                b.style.marginTop = b.style.marginBottom = 0;
                buttons.Add(b);
                row.Add(b);
            }
            _segments.Add(() => { for (int i = 0; i < buttons.Count; i++) buttons[i].style.backgroundColor = i == get() ? UiKit.Yellow : Color.white; });
            wrap.Add(row);
            return wrap;
        }

        private void BuildSettings()
        {
            _settings = ModalShell("settings", "SETTINGS", out var card);
            var d = (System.Func<SaveData>)(() => Services.Save.Data);
            card.Add(Toggle("Music", () => d().music, v => { d().music = v; Services.Audio.ApplySettings(); }));
            card.Add(Toggle("Sound effects", () => d().sfx, v => { d().sfx = v; Services.Audio.ApplySettings(); }));
            card.Add(Toggle("Patok sound trip", () => d().soundTrip, v => d().soundTrip = v));
            card.Add(Toggle("Vibration", () => d().vibration, v => d().vibration = v));
            card.Add(UiKit.Button("CONTROLS & STEERING", OpenControls, UiKit.Yellow, UiKit.Ink, 40, 110));
            card.Add(Segmented("Graphics", new[] { "Low", "Medium", "High", "Auto" }, () => d().graphicsQuality, i => { d().graphicsQuality = i; GraphicsQuality.Apply(i); }));
            var rel = ReleaseConfig.Active;
            if (!string.IsNullOrWhiteSpace(rel.privacyPolicyUrl))
                card.Add(UiKit.Button("PRIVACY POLICY", () => Application.OpenURL(rel.privacyPolicyUrl.Trim()), Color.white, UiKit.Ink, 36, 96));
            card.Add(Toggle("Camera effects", () => d().cameraEffects, v => d().cameraEffects = v));
            card.Add(Toggle("Screenshot button", () => d().screenshotButton, v => { d().screenshotButton = v; RefreshScreenshotUi(); }));
            card.Add(Toggle("Store frame 9:16", () => d().storeFrame, v => { d().storeFrame = v; RefreshScreenshotUi(); }));
            card.Add(Toggle("Show FPS", () => _showDebug, v => { _showDebug = v; if (_debug != null) Show(_debug, v); }));
            card.Add(UiKit.Button("RESET PROGRESS", () => Confirm("Reset progress?", "Erases coins, best scores and stats on this device.", () =>
            {
                Services.Save.ResetProgress();
                ShowMainMenu();
            }), UiKit.Hex("#E5484D"), Color.white, 40, 110));
            card.Add(UiKit.Button("BACK", CloseModal, Color.white, UiKit.Ink, 46, 120));
        }

        private void BuildConfirm()
        {
            _confirm = ModalShell("confirm", "SURE?", out var card);
            _confirmTitle = card.Q<Label>();
            _confirmText = UiKit.Text("", 40, UiKit.Grey, false);
            _confirmText.style.marginBottom = 20;
            card.Add(_confirmText);
            card.Add(UiKit.Button("YES", () => { CloseModal(); var a = _confirmYes; _confirmYes = null; _confirmCancel = null; a?.Invoke(); }, UiKit.Hex("#E5484D"), Color.white, 50, 130));
            card.Add(UiKit.Button("CANCEL", () => { CloseModal(); var c = _confirmCancel; _confirmCancel = null; _confirmYes = null; c?.Invoke(); }, Color.white, UiKit.Ink, 50, 130));
        }

        private void Confirm(string title, string text, System.Action yes)
        {
            _confirmTitle.text = title;
            _confirmText.text = text;
            _confirmYes = yes;
            _confirmCancel = null;
            OpenModal(_confirm);
        }

        // ======================================================================= HUD
        /// <summary>USS class marking non-Button elements that should block swipes (hold pedals).</summary>
        public const string InteractiveClass = "jr-interactive";
        private VisualElement _powerRow;
        private readonly (VisualElement cell, VisualElement fill)[] _powerCells = new (VisualElement, VisualElement)[PowerUpState.KindCount];

        /// <summary>
        /// Press-and-hold pedal. Uses pointer capture so it keeps working while another
        /// finger swipes (UI Toolkit tracks each pointer id separately).
        /// </summary>
        private VisualElement HoldButton(string label, string icon, Color color, System.Action<bool> set)
        {
            // a plain element (not a Button) so no click manipulator competes for the pointer
            var b = new VisualElement { name = label.ToLowerInvariant() };
            b.pickingMode = PickingMode.Position;
            b.AddToClassList(InteractiveClass);
            b.style.width = 210;
            b.style.height = 210;
            b.style.backgroundColor = color;
            UiKit.Border(b.style, 7, UiKit.Ink, 46);
            b.style.borderBottomWidth = 16;
            b.style.alignItems = Align.Center;
            b.style.justifyContent = Justify.Center;
            b.style.paddingTop = b.style.paddingBottom = 0;
            b.style.marginTop = b.style.marginBottom = 0;
            var ic = UiKit.Icon(icon, 96);
            b.Add(ic);
            b.Add(UiKit.Text(label, 40, UiKit.Ink));
            int pointer = -1;
            void Down(int id)
            {
                pointer = id;
                b.style.translate = new Translate(0, 8, 0);
                b.style.borderBottomWidth = 8;
                set(true);
            }
            void Up()
            {
                pointer = -1;
                b.style.translate = new Translate(0, 0, 0);
                b.style.borderBottomWidth = 16;
                set(false);
            }
            b.RegisterCallback<PointerDownEvent>(e => { if (pointer >= 0) return; Down(e.pointerId); b.CapturePointer(e.pointerId); e.StopPropagation(); });
            b.RegisterCallback<PointerUpEvent>(e => { if (e.pointerId == pointer) { b.ReleasePointer(e.pointerId); Up(); } });
            b.RegisterCallback<PointerCancelEvent>(e => { if (e.pointerId == pointer) Up(); });
            b.RegisterCallback<PointerCaptureOutEvent>(_ => { if (pointer >= 0) Up(); });
            b.RegisterCallback<DetachFromPanelEvent>(_ => { if (pointer >= 0) Up(); });
            _holdReleases.Add(() => { if (pointer >= 0) { if (b.HasPointerCapture(pointer)) b.ReleasePointer(pointer); Up(); } });
            return b;
        }

        private void BuildHud()
        {
            _hud = MakeScreen("hud");
            var top = UiKit.Box();
            top.style.flexDirection = FlexDirection.Row;
            top.style.justifyContent = Justify.SpaceBetween;
            UiKit.Pad(top.style, 20, 30);
            var scoreCol = UiKit.Box();
            var sl = UiKit.Text("SCORE", 28, Color.white, false, 1.5f);
            sl.style.unityTextAlign = TextAnchor.MiddleLeft;
            scoreCol.Add(sl);
            _score = UiKit.Text("0", 86, Color.white, true, 3f);
            _score.style.unityTextAlign = TextAnchor.MiddleLeft;
            scoreCol.Add(_score);
            _distance = UiKit.Text("0 m", 40, UiKit.Yellow, true, 2f);
            _distance.style.unityTextAlign = TextAnchor.MiddleLeft;
            scoreCol.Add(_distance);
            _weather = UiKit.Text("", 30, Color.white, true, 2f);
            _weather.style.unityTextAlign = TextAnchor.MiddleLeft;
            _weather.style.marginTop = 4;
            scoreCol.Add(_weather);
            _clock = UiKit.Text("", 28, new Color(1f, 1f, 1f, 0.92f), true, 2f);
            _clock.style.unityTextAlign = TextAnchor.MiddleLeft;
            _clock.style.marginTop = 2;
            scoreCol.Add(_clock);
            _ratingRow = UiKit.Box();
            _ratingRow.style.flexDirection = FlexDirection.Row;
            _ratingRow.style.alignItems = Align.Center;
            _ratingRow.style.marginTop = 2;
            _ratingRow.Add(UiKit.Icon("ui_star", 34));
            _ratingLabel = UiKit.Text("RATING 4.0", 30, new Color(0.6f, 1f, 0.6f), true, 2f);
            _ratingLabel.style.marginLeft = 6;
            _ratingRow.Add(_ratingLabel);
            scoreCol.Add(_ratingRow);
            _jamLabel = UiKit.Text("", 28, new Color(1f, 0.55f, 0.45f), true, 2f);
            _jamLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            _jamLabel.style.marginTop = 2;
            scoreCol.Add(_jamLabel);
            top.Add(scoreCol);
            var right = UiKit.Box();
            right.style.alignItems = Align.FlexEnd;
            var rowR = UiKit.Box();
            rowR.style.flexDirection = FlexDirection.Row;
            rowR.style.alignItems = Align.Center;
            _coins = UiKit.Text("0", 46, Color.white);
            rowR.Add(UiKit.Pill(UiKit.Icon("ui_coin", 58), _coins));
            _shotBtn = UiKit.IconButton("ui_camera", () => Screenshots.Instance?.Take(false), 112, Color.white);
            _shotBtn.name = "screenshot";
            _shotBtn.style.marginLeft = 18;
            rowR.Add(_shotBtn);
            var pause = UiKit.IconButton("ui_pause", () => Services.Game.Pause(), 112, Color.white);
            pause.name = "pause";
            pause.style.marginLeft = 18;
            rowR.Add(pause);
            right.Add(rowR);
            var hearts = UiKit.Box();
            hearts.style.flexDirection = FlexDirection.Row;
            hearts.style.marginTop = 14;
            for (int i = 0; i < 5; i++)
            {
                var h = UiKit.Icon("ui_heart", 64);
                h.style.marginLeft = 6;
                _hearts.Add(h);
                hearts.Add(h);
            }
            right.Add(hearts);
            right.Add(BuildFuelGauge());
            right.Add(BuildSpeedometer());
            _viewBtn = UiKit.Button("VIEW: LIKOD", () => Services.Game.Session?.ToggleView(), new Color(UiKit.Ink.r, UiKit.Ink.g, UiKit.Ink.b, 0.85f), Color.white, 26, 72);
            _viewBtn.style.width = 230;
            _viewBtn.style.marginTop = 10;
            _viewBtn.style.borderBottomWidth = 7;
            right.Add(_viewBtn);
            top.Add(right);
            _hud.Add(top);

            // passenger + next stop panel (compact, left-aligned so the road stays clear)
            var panel = UiKit.Box("stopPanel");
            panel.style.flexDirection = FlexDirection.Row;
            panel.style.alignSelf = Align.FlexStart;
            panel.style.marginTop = 12;
            panel.style.maxWidth = 620;
            panel.style.backgroundColor = new Color(UiKit.Ink.r, UiKit.Ink.g, UiKit.Ink.b, 0.8f);
            UiKit.Border(panel.style, 4, new Color(1, 1, 1, 0.2f), 30);
            UiKit.Pad(panel.style, 12, 22);
            var paxCol = UiKit.Box();
            paxCol.style.alignItems = Align.Center;
            paxCol.style.paddingRight = 18;
            paxCol.style.borderRightWidth = 3;
            paxCol.style.borderRightColor = new Color(1, 1, 1, 0.2f);
            paxCol.Add(UiKit.Icon("ui_person", 52));
            _pax = UiKit.Text("0/5", 44, Color.white);
            paxCol.Add(_pax);
            panel.Add(paxCol);
            var stopCol = UiKit.Box();
            stopCol.style.paddingLeft = 18;
            var st = UiKit.Text("SUSUNOD NA SAKAYAN", 24, UiKit.Yellow, false);
            st.style.unityTextAlign = TextAnchor.MiddleLeft;
            stopCol.Add(st);
            var nameRow = UiKit.Box();
            nameRow.style.flexDirection = FlexDirection.Row;
            nameRow.style.alignItems = Align.Center;
            _stopArrowL = UiKit.Icon("ui_arrow_left", 46);
            _stopName = UiKit.Text("-", 40, Color.white);
            _stopName.style.marginLeft = _stopName.style.marginRight = 8;
            _stopArrowR = UiKit.Icon("ui_arrow_right", 46);
            nameRow.Add(_stopArrowL);
            nameRow.Add(_stopName);
            nameRow.Add(_stopArrowR);
            stopCol.Add(nameRow);
            _stopMeta = UiKit.Text("", 28, UiKit.Hex("#D8D3FF"), false);
            _stopMeta.style.unityTextAlign = TextAnchor.MiddleLeft;
            stopCol.Add(_stopMeta);
            panel.Add(stopCol);
            // sits in the left column right under the rating, level with the speedometer on the right
            var leftCol = _ratingRow.parent;
            leftCol.Insert(leftCol.IndexOf(_ratingRow) + 1, panel);
            leftCol.style.flexShrink = 1;

            // active power-ups with draining timer bars
            _powerRow = UiKit.Box("powerups");
            _powerRow.style.flexDirection = FlexDirection.Row;
            _powerRow.style.marginLeft = 30;
            _powerRow.style.marginTop = 12;
            foreach (PowerUpKind k in System.Enum.GetValues(typeof(PowerUpKind)))
            {
                if (k == PowerUpKind.RepairKit) continue;
                var cell = UiKit.Box();
                cell.style.marginRight = 12;
                cell.style.alignItems = Align.Center;
                cell.Add(UiKit.Icon("ui_pu_" + k, 84));
                var bar = UiKit.Box();
                bar.style.width = 84;
                bar.style.height = 12;
                bar.style.marginTop = 4;
                bar.style.backgroundColor = new Color(0, 0, 0, 0.45f);
                UiKit.Border(bar.style, 0, Color.clear, 6);
                bar.style.overflow = Overflow.Hidden;
                var fill = UiKit.Box();
                fill.style.height = Length.Percent(100);
                fill.style.backgroundColor = UiKit.Yellow;
                bar.Add(fill);
                cell.Add(bar);
                Show(cell, false);
                _powerRow.Add(cell);
                _powerCells[(int)k] = (cell, fill);
            }
            _hud.Add(_powerRow);

            var spacer = UiKit.Box();
            spacer.style.flexGrow = 1;
            _hud.Add(spacer);
            var bottom = UiKit.Box();
            bottom.style.flexDirection = FlexDirection.Row;
            bottom.style.justifyContent = Justify.SpaceBetween;
            bottom.style.alignItems = Align.FlexEnd;
            UiKit.Pad(bottom.style, 26, 30);
            _ctlBottom = bottom;
            _ctlLeft = UiKit.Box("ctlLeft");
            _ctlLeft.style.alignItems = Align.FlexStart;
            _speedBox = UiKit.Box("speedBox");
            _speed = UiKit.Text("0", 72, Color.white, true, 3f);
            _speed.style.unityTextAlign = TextAnchor.MiddleLeft;
            _speedBox.Add(_speed);
            var kmh = UiKit.Text("km/h", 26, Color.white, false, 1.5f);
            kmh.style.unityTextAlign = TextAnchor.MiddleLeft;
            _speedBox.Add(kmh);
            _debug = UiKit.Text("", 22, UiKit.Hex("#7CFC7C"), false, 1f);
            _debug.style.unityTextAlign = TextAnchor.MiddleLeft;
            Show(_debug, false);
            (_speedCol ?? _speedBox).Add(_debug); // FPS readout under the speed (top right)
            bottom.Add(_ctlLeft);
            _wheelEl = BuildWheel();
            _pedalButtons.Add(_wheelEl);
            // TURBO: fires a stored booster (shown once one has been collected)
            _boostBtn = HoldButton("TURBO", "ui_pu_Turbo", UiKit.Hex("#FF8A00"), v => { if (v) Services.Game.Session?.UseBoost(); });
            _boostBtn.name = "turbo";
            _boostCount = UiKit.Text("x1", 34, Color.white, true, 2f);
            _boostCount.name = "count";
            _boostCount.style.position = Position.Absolute;
            _boostCount.style.right = -14;
            _boostCount.style.top = -16;
            _boostCount.style.backgroundColor = UiKit.Ink;
            UiKit.Border(_boostCount.style, 4, Color.white, 22);
            UiKit.Pad(_boostCount.style, 2, 12);
            _boostCount.pickingMode = PickingMode.Ignore;
            _boostBtn.Add(_boostCount);
            Show(_boostBtn, false);
            _pedalButtons.Add(_boostBtn);
            _brakeBtn = HoldButton("BRAKE", "ui_brake", UiKit.Hex("#FF3B3B"), v => { var s = Services.Game.Session; if (s != null) s.Input.BrakeButton = v; });
            _pedalButtons.Add(_brakeBtn);
            // BUSINA: honks the instant it is pressed
            _hornBtn = HoldButton("BUSINA", "ui_horn", UiKit.Hex("#FFD000"), v => { if (v) Services.Game.Session?.Honk(); });
            _hornBtn.name = "horn";
            _pedalButtons.Add(_hornBtn);
            _gasBtn = HoldButton("GAS", "ui_gas", UiKit.Hex("#22D065"), v => { var s = Services.Game.Session; if (s != null) s.Input.GasButton = v; });
            _pedalButtons.Add(_gasBtn);
            BuildGearButton();
            _pedalButtons.Add(_gearBtn);
            // steering buttons (Buttons mode): left thumb steers, right thumb drives
            _steerLeftBtn = HoldButton("LEFT", "ui_steer_left", UiKit.Hex("#4A5568"), v => { var s = Services.Game.Session; if (s != null) s.Input.SteerLeftButton = v; });
            _steerRightBtn = HoldButton("RIGHT", "ui_steer_right", UiKit.Hex("#4A5568"), v => { var s = Services.Game.Session; if (s != null) s.Input.SteerRightButton = v; });
            foreach (var sb in new[] { _steerLeftBtn, _steerRightBtn }) { var l = sb.Q<Label>(); if (l != null) l.style.color = Color.white; _pedalButtons.Add(sb); }

            // centred on the screen regardless of the side columns' widths (the strip itself ignores touches)
            _hornCentre = UiKit.Box("hornCol");
            _hornCentre.pickingMode = PickingMode.Ignore;
            _hornCentre.style.position = Position.Absolute;
            _hornCentre.style.left = _hornCentre.style.right = 0;
            _hornCentre.style.bottom = 52;
            _hornCentre.style.alignItems = Align.Center;
            bottom.Add(_hornCentre);

            _ctlRight = UiKit.Box("ctlRight");
            _ctlRight.style.alignItems = Align.FlexEnd;
            _ctlTopRow = UiKit.Box("ctlTopRow");
            _ctlTopRow.style.flexDirection = FlexDirection.Row;
            _ctlTopRow.style.alignItems = Align.FlexEnd;
            _ctlTopRow.style.justifyContent = Justify.FlexEnd;
            bottom.Add(_ctlRight);
            ApplyControlLayout(Services.Save != null ? GameplaySession.EffectiveSteerMode(Services.Save.Data) : Logic.SteerMode.Tap);
            _hud.Add(bottom);
            BuildArcade(_hud);

            _tutorial = UiKit.Box("tutorial");
            _tutorial.style.position = Position.Absolute;
            _tutorial.style.left = _tutorial.style.right = 0;
            _tutorial.style.bottom = Length.Percent(24);
            _tutorial.style.alignItems = Align.Center;
            var tc = UiKit.Box();
            tc.style.backgroundColor = new Color(UiKit.Ink.r, UiKit.Ink.g, UiKit.Ink.b, 0.88f);
            UiKit.Border(tc.style, 5, UiKit.Yellow, 34);
            UiKit.Pad(tc.style, 26, 36);
            foreach (var line in new[] { "Hold LEFT / RIGHT to steer (or TILT: Settings)", "Hold GAS to speed up · hold BRAKE to slow down", "BRAKE before speed HUMPS · STOP at RED lights", "BUSINA (horn): the car ahead moves over", "GEAR D/R: stop, tap R, hold GAS to back up", "Collect TURBO boosters · tap TURBO to use one", "Rain = slippery road · avoid floods (BAHA)", "Watch the GAS gauge · stop at GAS STATIONS to refuel", "PARA zone: hold BRAKE to a FULL STOP to load passengers", "GREEN arrow: stop there to drop them off for fares" })
            {
                var l = UiKit.Text(line, 36, Color.white, false);
                l.style.marginBottom = 8;
                tc.Add(l);
            }
            _tutorial.Add(tc);
            _hud.Add(_tutorial);
        }

        public void ShowHud(bool tutorial, bool controlsTutorial = false)
        {
            HideAll();
            Show(_hud, true);
            tutorial |= controlsTutorial;
            Show(_tutorial, tutorial);
            foreach (var c in _powerCells) if (c.cell != null) Show(c.cell, false);
            ClearJuice();
            _lastHealth = _lastScore = _lastCoins = _lastPax = _lastDist = _lastSpeed = -1;
            _lastStop = long.MaxValue;
            _floats.Clear();
            if (tutorial)
            {
                _tutorial.schedule.Execute(() =>
                {
                    Show(_tutorial, false);
                    Services.Save.Data.tutorialDone = true;
                    Services.Save.Data.controlsTutorialDone = true;
                    Services.Save.Save();
                }).StartingIn(7000);
            }
        }

        public void UpdateHud(GameplaySession s)
        {
            var score = s.Score;
            if (score.Score != _lastScore) { _lastScore = score.Score; _score.text = score.Score.ToString("N0"); }
            int dist = Mathf.FloorToInt(score.Distance);
            if (dist != _lastDist) { _lastDist = dist; _distance.text = $"{dist:N0} m"; }
            if (score.Coins != _lastCoins) { _lastCoins = score.Coins; _coins.text = score.Coins.ToString("N0"); }
            int kmh = Mathf.RoundToInt(Mathf.Abs(s.Speed) * 3.6f);
            if (kmh != _lastSpeed) { _lastSpeed = kmh; _speed.text = kmh.ToString(); if (_speedPillText != null) _speedPillText.text = kmh.ToString(); }
            UpdateGearVisibility(kmh, s.ReverseGear);
            var c = s.Player.Collisions;
            if (c.Health != _lastHealth)
            {
                for (int i = 0; i < _hearts.Count; i++)
                {
                    Show(_hearts[i], i < c.MaxHealth);
                    _hearts[i].image = UiKit.Tex(i < c.Health ? "ui_heart" : "ui_heart_empty");
                }
                if (_lastHealth > c.Health && c.Health >= 0 && c.Health < _hearts.Count) UiKit.PopIn(_hearts[c.Health], 300);
                _lastHealth = c.Health;
            }
            var route = s.Passengers.Route;
            int pax = route.Count * 100 + route.Capacity;
            if (pax != _lastPax)
            {
                _lastPax = pax;
                _pax.text = $"{route.Count}/{route.Capacity}";
                _pax.style.color = route.Count >= route.Capacity ? UiKit.Hex("#FF6B6B") : Color.white;
            }
            var next = s.Passengers.NextStop();
            // compare numbers, not a formatted string, so the HUD allocates nothing per frame
            long key = next == null ? long.MinValue : ((long)next.Stop.Id << 32) ^ ((long)Mathf.CeilToInt(next.transform.localPosition.z / 10f) << 8) ^ route.Count;
            if (key != _lastStop)
            {
                _lastStop = key;
                if (next == null) { _stopName.text = "-"; _stopMeta.text = ""; Show(_stopArrowL, false); Show(_stopArrowR, false); }
                else
                {
                    float m = Mathf.Max(0, next.transform.localPosition.z);
                    _stopName.text = next.Stop.Name;
                    int drop = s.Passengers.Route.AlightingAt(next.Stop.Id);
                    _stopMeta.text = $"{Mathf.CeilToInt(m)} m · {(next.Stop.Side < 0 ? "left" : "right")} · {next.Stop.Waiting} sakay" + (drop > 0 ? $" · {drop} bababa" : "");
                    _stopMeta.style.color = drop > 0 ? UiKit.Hex("#2BFF88") : UiKit.Hex("#D8D3FF");
                    Show(_stopArrowL, next.Stop.Side < 0);
                    Show(_stopArrowR, next.Stop.Side > 0);
                }
            }
            var pu = s.PowerUps.State;
            for (int i = 0; i < _powerCells.Length; i++)
            {
                var (cell, fill) = _powerCells[i];
                if (cell == null) continue;
                bool on = pu.IsActive((PowerUpKind)i);
                if ((cell.style.display == DisplayStyle.Flex) != on) Show(cell, on);
                if (on) fill.style.width = Length.Percent(pu.Fraction((PowerUpKind)i) * 100f);
            }
            if (_showDebug) _debug.text = $"{(1f / Mathf.Max(1e-4f, Time.smoothDeltaTime)):0} fps · {s.DebugText()}";
        }

        public void Float(string text, FloatKind kind)
        {
            if (_floats.childCount > 4) _floats.RemoveAt(0);
            var color = kind == FloatKind.Good ? UiKit.Hex("#3EE07A") : kind == FloatKind.Bad ? UiKit.Hex("#FF5A5A") : kind == FloatKind.Big ? UiKit.Yellow : UiKit.Yellow;
            var l = UiKit.Text(text, kind == FloatKind.Big ? 110 : 66, color, true, 3.5f);
            _floats.Add(l);
            long start = 0;
            l.schedule.Execute(t =>
            {
                if (start == 0) start = t.now;
                float k = (t.now - start) / 1300f;
                float sc = k < 0.15f ? Mathf.Lerp(0.4f, 1.15f, k / 0.15f) : k < 0.25f ? Mathf.Lerp(1.15f, 1f, (k - 0.15f) / 0.1f) : 1f;
                l.style.scale = new Scale(new Vector3(sc, sc, 1));
                l.style.opacity = k < 0.8f ? 1f : Mathf.Clamp01(1 - (k - 0.8f) / 0.2f);
                l.style.translate = new Translate(0, k > 0.8f ? -(k - 0.8f) * 250f : 0, 0);
                if (k >= 1f) l.RemoveFromHierarchy();
            }).Every(16).Until(() => l.parent == null);
        }

        public void FlashDamage()
        {
            long start = 0;
            _flash.schedule.Execute(t =>
            {
                if (start == 0) start = t.now;
                _flash.style.opacity = Mathf.Clamp01(1f - (t.now - start) / 450f);
            }).Every(16).Until(() => start != 0 && _flash.style.opacity.value <= 0f);
        }

        /// <summary>True when a screen position (pixels, origin bottom-left) is over a button.</summary>
        public bool IsOverInteractive(Vector2 screenPos)
        {
            var panel = _root?.panel;
            if (panel == null) return false;
            var p = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screenPos.x, UnityEngine.Screen.height - screenPos.y));
            var picked = panel.Pick(p);
            while (picked != null)
            {
                if (picked is Button || picked.ClassListContains(InteractiveClass)) return true;
                picked = picked.parent;
            }
            return false;
        }

        // ======================================================================= pause
        private void BuildPause()
        {
            _pause = ModalShell("pause", "PAHINGA MUNA", out var card);
            var sub = UiKit.Text("Game paused", 38, UiKit.Grey, false);
            sub.style.marginBottom = 16;
            card.Add(sub);
            card.Add(UiKit.Button("RESUME", () => Services.Game.Resume(), UiKit.Yellow, UiKit.Ink, 64, 160));
            card.Add(UiKit.Button("RESTART", () => Services.Game.Restart(), Color.white, UiKit.Ink, 48, 128));
            card.Add(UiKit.Button("MAIN MENU", () => Services.Game.GoToMainMenu(), Color.white, UiKit.Ink, 48, 128));
            card.Add(UiKit.Button("CONTROLS", OpenControls, UiKit.Hex("#D8D3FF"), UiKit.Ink, 40, 110));
            // the scene is frozen while paused: perfect moment for a clean store screenshot
            _pausePhoto = UiKit.Button("PHOTO WITHOUT HUD", () => Screenshots.Instance?.Take(true), UiKit.Hex("#D8D3FF"), UiKit.Ink, 40, 110);
            card.Add(_pausePhoto);
            var d = (System.Func<SaveData>)(() => Services.Save.Data);
            card.Add(Toggle("Music", () => d().music, v => { d().music = v; Services.Audio.ApplySettings(); }));
            card.Add(Toggle("Sound effects", () => d().sfx, v => { d().sfx = v; Services.Audio.ApplySettings(); }));
            card.Add(Toggle("Patok sound trip", () => d().soundTrip, v => d().soundTrip = v));
        }

        // ======================================================================= game over
        private Label _goScore, _goDist, _goCoins, _goPax, _goBest, _goBadge, _goDelivered, _goReason, _goRating;
        private VisualElement _goRewards;

        private void BuildGameOver()
        {
            _gameOver = ModalShell("gameover", "GAME OVER", out var card);
            _goBadge = UiKit.Text("NEW BEST!", 46, UiKit.Ink);
            _goBadge.style.backgroundColor = UiKit.Yellow;
            UiKit.Border(_goBadge.style, 5, UiKit.Ink, 20);
            UiKit.Pad(_goBadge.style, 8, 30);
            _goBadge.style.alignSelf = Align.Center;
            _goBadge.style.rotate = new Rotate(-4);
            card.Add(_goBadge);
            _goReason = UiKit.Text("", 40, UiKit.Red, true);
            _goReason.style.marginBottom = 6;
            card.Add(_goReason);
            _goScore = UiKit.Text("0", 150, UiKit.Blue, true, 4f);
            card.Add(_goScore);
            _goRating = UiKit.Text("", 34, UiKit.Ink, false);
            _goRating.style.marginBottom = 4;
            card.Add(_goRating);
            var grid = UiKit.Box();
            grid.style.flexDirection = FlexDirection.Row;
            grid.style.flexWrap = Wrap.Wrap;
            grid.style.justifyContent = Justify.SpaceBetween;
            grid.style.marginTop = 10;
            Label Cell(string title)
            {
                var c = UiKit.Box();
                c.style.width = Length.Percent(48.5f);
                c.style.backgroundColor = Color.white;
                UiKit.Border(c.style, 4, UiKit.Hex("#E2DBC6"), 24);
                UiKit.Pad(c.style, 12, 16);
                c.style.marginBottom = 14;
                c.Add(UiKit.Text(title.ToUpperInvariant(), 26, UiKit.Grey, false));
                var v = UiKit.Text("0", 50, UiKit.Ink);
                c.Add(v);
                grid.Add(c);
                return v;
            }
            _goDist = Cell("Distance");
            _goCoins = Cell("Coins");
            _goPax = Cell("Picked up");
            _goDelivered = Cell("Delivered");
            card.Add(grid);
            _goBest = UiKit.Text("", 32, UiKit.Grey, false);
            card.Add(_goBest);
            _goRewards = UiKit.Box("rewards");
            _goRewards.style.marginTop = 8;
            card.Add(_goRewards);
            _goOnline = UiKit.Text("", 32, UiKit.Blue, false);
            _goOnline.style.whiteSpace = WhiteSpace.Normal;
            _goOnline.style.marginBottom = 6;
            card.Add(_goOnline);
            BuildDoubleCoins(card);
            // an interstitial may play here – between runs, never during one (capped by AdPolicy)
            float LastRunSeconds() => Services.Game.Session != null ? Services.Game.Session.LastRunSeconds : 0f;
            card.Add(UiKit.Button("RESTART", () => Services.Ads.BetweenRuns(LastRunSeconds(), () => { Show(_gameOver, false); Services.Game.Restart(); }), UiKit.Yellow, UiKit.Ink, 64, 160));
            card.Add(UiKit.Button("HOME", () => Services.Ads.BetweenRuns(LastRunSeconds(), () => { Show(_gameOver, false); Services.Game.GoToMainMenu(); }), Color.white, UiKit.Ink, 48, 128));
        }

        public void ShowGameOver(RunResult r)
        {
            ClearJuice();
            Show(_hud, false);
            Show(_gameOver, true);
            Show(_goBadge, r.NewBestScore);
            _goDist.text = $"{Mathf.FloorToInt(r.Distance):N0} m";
            _goCoins.text = $"+{r.Coins:N0}";
            _goPax.text = r.Passengers.ToString();
            _goDelivered.text = r.Deliveries.ToString();
            _goBest.text = $"Best score {r.BestScore:N0} · best distance {Mathf.FloorToInt(r.BestDistance):N0} m";
            var sess = Services.Game.Session;
            string reason = sess != null ? sess.LastEndReason : null;
            _goReason.text = reason ?? "";
            Show(_goReason, !string.IsNullOrEmpty(reason));
            string rating = sess != null ? sess.LastRating : null;
            _goRating.text = string.IsNullOrEmpty(rating) ? "" : $"Driver rating: {rating} / 5";
            Show(_goRating, !string.IsNullOrEmpty(rating));
            _goRewards.Clear();
            PrepareOnlineGameOver();
            PrepareDoubleCoins(r);
            var rw = r.Rewards;
            void Line(string text, Color c)
            {
                if (_goRewards.childCount >= 5) return; // keep the card on screen
                var l = UiKit.Text(text, 34, c, false);
                l.style.backgroundColor = Color.white;
                UiKit.Border(l.style, 3, UiKit.Hex("#E2DBC6"), 18);
                UiKit.Pad(l.style, 8, 14);
                l.style.marginBottom = 8;
                _goRewards.Add(l);
            }
            if (rw != null)
            {
                if (rw.CoinBonus > 0) Line($"Coin Bonus upgrade: +{rw.CoinBonus} coins", UiKit.Ink);
                if (rw.Gems > 0) Line($"Gems found on the road: +{rw.Gems}", UiKit.Hex("#7C3AED"));
                Line($"+{rw.XpGained} XP", UiKit.Blue);
                if (rw.LevelsGained > 0) { Line($"LEVEL UP! Level {Services.Save.Data.level} · +{rw.LevelReward} coins", UiKit.Green); Services.Audio.Play(Logic.Audio.Sfx.LevelUp); }
                foreach (var m in rw.MissionsCompleted) Line($"Mission complete: {m.Text} (claim +{m.Reward})", UiKit.Green);
                foreach (var a in rw.AchievementsUnlocked) Line($"Achievement: {a.Name} (claim +{a.Reward})", UiKit.Hex("#B8860B"));
                foreach (var e in rw.RoutesUnlocked) Line($"New route unlocked: {Environments.Get(e).Name}!", UiKit.Red);
            }
            UiKit.PopIn(_gameOver.Q("card"), 320);
            // satisfying score count-up
            long start = 0;
            _goScore.text = "0";
            _goScore.schedule.Execute(t =>
            {
                if (start == 0) start = t.now;
                float k = Mathf.Clamp01((t.now - start) / 900f);
                k = 1 - Mathf.Pow(1 - k, 3);
                _goScore.text = Mathf.RoundToInt(r.Score * k).ToString("N0");
            }).Every(16).Until(() => start != 0 && _goScore.text == r.Score.ToString("N0"));
        }
    }
}
