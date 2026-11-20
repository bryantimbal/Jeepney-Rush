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
    public sealed class UIManager : MonoBehaviour
    {
        public enum FloatKind { Good, Bad, Info, Big }

        private UIDocument _doc;
        private VisualElement _root, _safe;
        private VisualElement _splash, _menu, _hud, _pause, _gameOver, _settings, _garage, _confirm, _loading, _tutorial, _floats, _flash;
        private VisualElement _splashBar, _loadingBar, _splashJeep;
        private Label _splashText, _menuCoins, _menuBest;
        private Label _score, _distance, _coins, _pax, _stopName, _stopMeta, _speed, _debug;
        private Image _stopArrowL, _stopArrowR;
        private readonly List<Image> _hearts = new List<Image>();
        private int _lastHealth = -1, _lastScore = -1, _lastCoins = -1, _lastPax = -1, _lastDist = -1, _lastSpeed = -1;
        private string _lastStop;
        private readonly Stack<VisualElement> _modals = new Stack<VisualElement>();
        private System.Action _confirmYes;
        private Label _confirmTitle, _confirmText;
        private int _garageIndex;
        private JeepneySpec[] _specs;
        private Label _garageName, _garageDesc, _garageStatus;
        private VisualElement _garageStats;
        private Button _garageSelect;
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

            BuildHud();
            BuildMenu();
            BuildPause();
            BuildGameOver();
            BuildSettings();
            BuildGarage();
            BuildConfirm();
            BuildLoading();
            BuildSplash();
            _floats = UiKit.Box("floats");
            _floats.style.position = Position.Absolute;
            _floats.style.left = _floats.style.right = 0;
            _floats.style.top = Length.Percent(30);
            _floats.style.alignItems = Align.Center;
            _root.Add(_floats);
            _flash = UiKit.Box("flash");
            _flash.style.position = Position.Absolute;
            _flash.style.left = _flash.style.right = _flash.style.top = _flash.style.bottom = 0;
            UiKit.Border(_flash.style, 60, new Color(1, 0, 0, 0.6f), 0);
            _flash.style.opacity = 0;
            _root.Add(_flash);
            HideAll();
            GameEvents.StateChanged += OnState;
        }

        private void ApplySafeArea()
        {
            float w = _root.resolvedStyle.width;
            if (float.IsNaN(w) || w <= 0 || Screen.width <= 0) return;
            float k = w / Screen.width;
            var sa = Screen.safeArea;
            _safe.style.left = sa.xMin * k;
            _safe.style.right = (Screen.width - sa.xMax) * k;
            _safe.style.top = (Screen.height - sa.yMax) * k;
            _safe.style.bottom = sa.yMin * k;
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
            foreach (var e in new[] { _splash, _menu, _hud, _pause, _gameOver, _settings, _garage, _confirm, _loading })
                e.style.display = DisplayStyle.None;
            _modals.Clear();
        }

        private static void Show(VisualElement e, bool on) => e.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;

        private void OnState(GameState from, GameState to)
        {
            Show(_pause, to == GameState.Paused);
            if (to == GameState.Paused) { SyncToggles(); UiKit.PopIn(_pause.Q("card")); }
            if (to == GameState.MainMenu) ShowMainMenu();
            if (to == GameState.Playing || to == GameState.Starting) Show(_loading, false);
        }

        // ======================================================================= splash & loading
        private void BuildSplash()
        {
            _splash = MakeScreen("splash", false);
            _splash.style.backgroundImage = UiKit.Tex("ui_splash_bg");
            _splash.style.alignItems = Align.Center;
            _splash.style.justifyContent = Justify.Center;
            var logo = UiKit.Icon("logo", 900);
            logo.style.height = 460;
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

        public void ShowSplash()
        {
            HideAll();
            Show(_splash, true);
        }

        public void SetSplashProgress(float p, string text)
        {
            _splashBar.style.width = Length.Percent(Mathf.Clamp01(p) * 100f);
            _splashText.text = text;
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
        private void BuildMenu()
        {
            _menu = MakeScreen("menu");
            _menu.style.alignItems = Align.Center;
            var top = UiKit.Box();
            top.style.flexDirection = FlexDirection.Row;
            top.style.justifyContent = Justify.SpaceBetween;
            top.style.alignSelf = Align.Stretch;
            UiKit.Pad(top.style, 30, 36);
            _menuCoins = UiKit.Text("0", 50, Color.white);
            _menuBest = UiKit.Text("0 m", 50, Color.white);
            top.Add(UiKit.Pill(UiKit.Icon("ui_coin", 64), _menuCoins));
            var bestLabel = UiKit.Box();
            bestLabel.style.flexDirection = FlexDirection.Row;
            bestLabel.style.alignItems = Align.Center;
            bestLabel.Add(UiKit.Text("BEST", 30, UiKit.Yellow, false));
            top.Add(UiKit.Pill(bestLabel, _menuBest));
            _menu.Add(top);
            var logo = UiKit.Icon("logo", 820);
            logo.style.height = 420;
            logo.style.marginTop = 10;
            _menu.Add(logo);
            var spacer = UiKit.Box();
            spacer.style.flexGrow = 1;
            _menu.Add(spacer);
            var buttons = UiKit.Box();
            buttons.style.width = 860;
            buttons.style.marginBottom = 30;
            var play = UiKit.Button("PLAY", () => Services.Game.Play(), UiKit.Yellow, UiKit.Ink, 96, 210);
            buttons.Add(play);
            var row = UiKit.Box();
            row.style.flexDirection = FlexDirection.Row;
            var garage = UiKit.Button("GARAGE", OpenGarage, Color.white, UiKit.Ink, 50, 140);
            garage.style.flexGrow = 1;
            garage.style.marginRight = 14;
            var settings = UiKit.Button("SETTINGS", () => OpenModal(_settings), Color.white, UiKit.Ink, 50, 140);
            settings.style.flexGrow = 1;
            row.Add(garage);
            row.Add(settings);
            buttons.Add(row);
            var honk = UiKit.Button("BEEP BEEP!", () => Services.Audio.Play(Logic.Audio.Sfx.Horn), UiKit.Hex("#E9EDF2"), UiKit.Ink, 40, 110);
            buttons.Add(honk);
            _menu.Add(buttons);
            var ver = UiKit.Text($"v{Application.version} · Endless Rush · Manila", 28, new Color(1, 1, 1, 0.85f), false);
            ver.style.marginBottom = 20;
            _menu.Add(ver);
        }

        public void ShowMainMenu()
        {
            HideAll();
            var d = Services.Save.Data;
            _menuCoins.text = d.coins.ToString("N0");
            _menuBest.text = $"{Mathf.FloorToInt(d.bestDistance):N0} m";
            Show(_menu, true);
            UiKit.PopIn(_menu.Q<Button>());
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
            CloseModal();
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
            card.Add(Toggle("Vibration", () => d().vibration, v => d().vibration = v));
            card.Add(Segmented("Swipe sensitivity", new[] { "Low", "Normal", "High" },
                () => d().swipeSensitivity < 0.85f ? 0 : d().swipeSensitivity > 1.15f ? 2 : 1,
                i => d().swipeSensitivity = i == 0 ? 0.7f : i == 1 ? 1f : 1.3f));
            card.Add(Segmented("Graphics", new[] { "Low", "Medium", "High" }, () => d().graphicsQuality, i => { d().graphicsQuality = i; GraphicsQuality.Apply(i); }));
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
            card.Add(UiKit.Button("YES", () => { CloseModal(); var a = _confirmYes; _confirmYes = null; a?.Invoke(); }, UiKit.Hex("#E5484D"), Color.white, 50, 130));
            card.Add(UiKit.Button("CANCEL", CloseModal, Color.white, UiKit.Ink, 50, 130));
        }

        private void Confirm(string title, string text, System.Action yes)
        {
            _confirmTitle.text = title;
            _confirmText.text = text;
            _confirmYes = yes;
            OpenModal(_confirm);
        }

        // ======================================================================= garage
        private void BuildGarage()
        {
            _garage = MakeScreen("garage", false);
            var overlay = UiKit.Overlay(new Color(0, 0, 0, 0));
            overlay.style.justifyContent = Justify.FlexEnd;
            overlay.style.paddingBottom = 60;
            _garage.Add(overlay);
            var card = UiKit.Card(980);
            overlay.Add(card);
            card.Add(UiKit.Text("GARAGE", 70, UiKit.Red, true, 2.5f));
            var nav = UiKit.Box();
            nav.style.flexDirection = FlexDirection.Row;
            nav.style.alignItems = Align.Center;
            nav.style.justifyContent = Justify.SpaceBetween;
            nav.Add(UiKit.IconButton("ui_arrow_left", () => CycleGarage(-1), 120, Color.white));
            _garageName = UiKit.Text("", 56, UiKit.Ink);
            _garageName.style.flexGrow = 1;
            nav.Add(_garageName);
            nav.Add(UiKit.IconButton("ui_arrow_right", () => CycleGarage(1), 120, Color.white));
            card.Add(nav);
            _garageDesc = UiKit.Text("", 34, UiKit.Grey, false);
            _garageDesc.style.marginTop = 10;
            card.Add(_garageDesc);
            _garageStats = UiKit.Box();
            _garageStats.style.marginTop = 16;
            card.Add(_garageStats);
            _garageStatus = UiKit.Text("", 34, UiKit.Green, false);
            _garageStatus.style.marginTop = 10;
            card.Add(_garageStatus);
            _garageSelect = UiKit.Button("SELECT", SelectGarage, UiKit.Yellow, UiKit.Ink, 56, 140);
            card.Add(_garageSelect);
            var note = UiKit.Text("More jeepneys, upgrades and custom paint arrive with the Phase 2 garage.", 28, UiKit.Grey, false);
            card.Add(note);
            card.Add(UiKit.Button("BACK", () => { CloseModal(); Show(_menu, true); Services.Game.Menu?.ShowLivery(Services.Save.Data.selectedJeepney); }, Color.white, UiKit.Ink, 46, 120));
        }

        private void OpenGarage()
        {
            _specs = JeepneySpec.LoadAll();
            _garageIndex = 0;
            for (int i = 0; i < _specs.Length; i++) if (_specs[i].liveryId == Services.Save.Data.selectedJeepney) _garageIndex = i;
            Show(_menu, false);
            OpenModal(_garage);
            RefreshGarage();
        }

        private void CycleGarage(int dir)
        {
            _garageIndex = (_garageIndex + dir + _specs.Length) % _specs.Length;
            RefreshGarage();
        }

        private void RefreshGarage()
        {
            var s = _specs[_garageIndex];
            _garageName.text = s.displayName.ToUpperInvariant();
            _garageDesc.text = s.description;
            _garageStats.Clear();
            void Stat(string n, int value, int max)
            {
                var row = UiKit.Box();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.style.marginBottom = 8;
                var l = UiKit.Text(n, 32, UiKit.Ink, false);
                l.style.width = 280;
                l.style.unityTextAlign = TextAnchor.MiddleLeft;
                row.Add(l);
                for (int i = 0; i < max; i++)
                {
                    var pip = UiKit.Box();
                    pip.style.width = 56;
                    pip.style.height = 30;
                    pip.style.marginRight = 8;
                    pip.style.backgroundColor = i < value ? UiKit.Yellow : UiKit.Hex("#E2DBC6");
                    UiKit.Border(pip.style, 3, UiKit.Ink, 10);
                    row.Add(pip);
                }
                _garageStats.Add(row);
            }
            Stat("Passengers", Mathf.CeilToInt(s.passengerCapacity / 3f), 5);
            Stat("Health", s.health, 5);
            Stat("Handling", s.handling, 5);
            bool selected = s.liveryId == Services.Save.Data.selectedJeepney;
            _garageStatus.text = selected ? "DRIVING THIS ONE" : "FREE";
            _garageSelect.SetEnabled(!selected);
            _garageSelect.text = selected ? "SELECTED" : "SELECT";
            Services.Game.Menu?.ShowLivery(s.liveryId);
        }

        private void SelectGarage()
        {
            Services.Save.Data.selectedJeepney = _specs[_garageIndex].liveryId;
            Services.Save.Save();
            Services.Audio.Play(Logic.Audio.Sfx.Horn);
            RefreshGarage();
        }

        // ======================================================================= HUD
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
            top.Add(scoreCol);
            var right = UiKit.Box();
            right.style.alignItems = Align.FlexEnd;
            var rowR = UiKit.Box();
            rowR.style.flexDirection = FlexDirection.Row;
            rowR.style.alignItems = Align.Center;
            _coins = UiKit.Text("0", 46, Color.white);
            rowR.Add(UiKit.Pill(UiKit.Icon("ui_coin", 58), _coins));
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
            top.Add(right);
            _hud.Add(top);

            // passenger + next stop panel (compact, left-aligned so the road stays clear)
            var panel = UiKit.Box("stopPanel");
            panel.style.flexDirection = FlexDirection.Row;
            panel.style.alignSelf = Align.FlexStart;
            panel.style.marginLeft = 30;
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
            _stopName = UiKit.Text("-", 46, Color.white);
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
            _hud.Add(panel);

            var spacer = UiKit.Box();
            spacer.style.flexGrow = 1;
            _hud.Add(spacer);
            var bottom = UiKit.Box();
            bottom.style.flexDirection = FlexDirection.Row;
            bottom.style.justifyContent = Justify.SpaceBetween;
            bottom.style.alignItems = Align.FlexEnd;
            UiKit.Pad(bottom.style, 26, 30);
            var speedCol = UiKit.Box();
            _speed = UiKit.Text("0", 72, Color.white, true, 3f);
            _speed.style.unityTextAlign = TextAnchor.MiddleLeft;
            speedCol.Add(_speed);
            var kmh = UiKit.Text("km/h", 26, Color.white, false, 1.5f);
            kmh.style.unityTextAlign = TextAnchor.MiddleLeft;
            speedCol.Add(kmh);
            _debug = UiKit.Text("", 22, UiKit.Hex("#7CFC7C"), false, 1f);
            _debug.style.unityTextAlign = TextAnchor.MiddleLeft;
            Show(_debug, false);
            speedCol.Add(_debug);
            bottom.Add(speedCol);
            var horn = UiKit.IconButton("ui_horn", () => Services.Game.Session?.Honk(), 150, UiKit.Hex("#E9EDF2"));
            horn.name = "horn";
            UiKit.Border(horn.style, 7, UiKit.Ink, 75);
            horn.style.borderBottomWidth = 14;
            bottom.Add(horn);
            _hud.Add(bottom);

            _tutorial = UiKit.Box("tutorial");
            _tutorial.style.position = Position.Absolute;
            _tutorial.style.left = _tutorial.style.right = 0;
            _tutorial.style.bottom = Length.Percent(24);
            _tutorial.style.alignItems = Align.Center;
            var tc = UiKit.Box();
            tc.style.backgroundColor = new Color(UiKit.Ink.r, UiKit.Ink.g, UiKit.Ink.b, 0.88f);
            UiKit.Border(tc.style, 5, UiKit.Yellow, 34);
            UiKit.Pad(tc.style, 26, 36);
            foreach (var line in new[] { "Swipe LEFT / RIGHT to change lanes", "Swipe UP to jump over cones & potholes", "Swipe DOWN to brake", "Drive through yellow PARA zones to pick up passengers" })
            {
                var l = UiKit.Text(line, 36, Color.white, false);
                l.style.marginBottom = 8;
                tc.Add(l);
            }
            _tutorial.Add(tc);
            _hud.Add(_tutorial);
        }

        public void ShowHud(bool tutorial)
        {
            HideAll();
            Show(_hud, true);
            Show(_tutorial, tutorial);
            _lastHealth = _lastScore = _lastCoins = _lastPax = _lastDist = _lastSpeed = -1;
            _lastStop = null;
            _floats.Clear();
            if (tutorial)
            {
                _tutorial.schedule.Execute(() =>
                {
                    Show(_tutorial, false);
                    Services.Save.Data.tutorialDone = true;
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
            int kmh = Mathf.RoundToInt(s.Speed * 3.6f);
            if (kmh != _lastSpeed) { _lastSpeed = kmh; _speed.text = kmh.ToString(); }
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
            string key = next == null ? "none" : $"{next.Stop.Name}|{Mathf.CeilToInt(next.transform.localPosition.z / 10f)}";
            if (key != _lastStop)
            {
                _lastStop = key;
                if (next == null) { _stopName.text = "-"; _stopMeta.text = ""; Show(_stopArrowL, false); Show(_stopArrowR, false); }
                else
                {
                    float m = Mathf.Max(0, next.transform.localPosition.z);
                    _stopName.text = next.Stop.Name;
                    _stopMeta.text = $"{Mathf.CeilToInt(m)} m · {(next.Stop.Side < 0 ? "left" : "right")} lane · {next.Stop.Waiting} waiting";
                    Show(_stopArrowL, next.Stop.Side < 0);
                    Show(_stopArrowR, next.Stop.Side > 0);
                }
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
                if (picked is Button) return true;
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
            var d = (System.Func<SaveData>)(() => Services.Save.Data);
            card.Add(Toggle("Music", () => d().music, v => { d().music = v; Services.Audio.ApplySettings(); }));
            card.Add(Toggle("Sound effects", () => d().sfx, v => { d().sfx = v; Services.Audio.ApplySettings(); }));
        }

        // ======================================================================= game over
        private Label _goScore, _goDist, _goCoins, _goPax, _goBest, _goBadge;

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
            _goScore = UiKit.Text("0", 150, UiKit.Blue, true, 4f);
            card.Add(_goScore);
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
            _goPax = Cell("Passengers");
            _goBest = Cell("Best score");
            card.Add(grid);
            card.Add(UiKit.Button("RESTART", () => { Show(_gameOver, false); Services.Game.Restart(); }, UiKit.Yellow, UiKit.Ink, 64, 160));
            card.Add(UiKit.Button("HOME", () => { Show(_gameOver, false); Services.Game.GoToMainMenu(); }, Color.white, UiKit.Ink, 48, 128));
        }

        public void ShowGameOver(RunResult r)
        {
            Show(_hud, false);
            Show(_gameOver, true);
            Show(_goBadge, r.NewBestScore);
            _goDist.text = $"{Mathf.FloorToInt(r.Distance):N0} m";
            _goCoins.text = $"+{r.Coins:N0}";
            _goPax.text = r.Passengers.ToString();
            _goBest.text = r.BestScore.ToString("N0");
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
