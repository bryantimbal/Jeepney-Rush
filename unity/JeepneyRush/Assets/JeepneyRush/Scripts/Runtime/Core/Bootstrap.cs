using System.Collections;
using JeepneyRush.Logic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JeepneyRush
{
    /// <summary>
    /// Creates the persistent managers exactly once (even when Play is pressed in the
    /// MainMenu or Gameplay scene in the editor) and runs the splash/loading sequence
    /// in the Bootstrap scene.
    /// </summary>
    public sealed class Bootstrap : MonoBehaviour
    {
        private static GameObject _systems;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _systems = null; // fast enter-play-mode support

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CreateSystems()
        {
            if (_systems != null) return;
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Screen.orientation = ScreenOrientation.Portrait;

            _systems = new GameObject("[Systems]");
            DontDestroyOnLoad(_systems);
            Services.Save = _systems.AddComponent<SaveManager>();
            Services.Save.Load();
            Services.Pools = _systems.AddComponent<PoolManager>();
            Services.Scenes = _systems.AddComponent<SceneLoader>();
            Services.Audio = _systems.AddComponent<AudioManager>();
            Services.Game = _systems.AddComponent<GameManager>();
            Services.UI = _systems.AddComponent<UIManager>();
            Services.Online = _systems.AddComponent<OnlineService>();
            _systems.AddComponent<Haptics>();
            _systems.AddComponent<BackButtonListener>();
            GraphicsQuality.Apply(Services.Save.Data.graphicsQuality);
        }

        /// <summary>
        /// If Play is pressed in a scene that is not part of the game (e.g. the template's
        /// SampleScene), run the normal splash → main menu flow anyway.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartFromAnyScene()
        {
            if (FindFirstObjectByType<Bootstrap>() != null || FindFirstObjectByType<GameplaySession>() != null || FindFirstObjectByType<MainMenuController>() != null) return;
            Debug.Log("[Jeepney Rush] No game scene open – starting the normal flow. (Tip: open Assets/JeepneyRush/Scenes/Bootstrap.unity.)");
            _systems.AddComponent<Bootstrap>();
        }

        private IEnumerator Start()
        {
            var ui = Services.UI;
            ui.ShowSplash();
            yield return null;
            ui.SetSplashProgress(0.05f, "Starting engine…");
            yield return null;

            // Generate audio (synthesised in code) a piece per frame so the bar animates.
            ui.SetSplashProgress(0.15f, "Tuning the horn…");
            yield return Services.Audio.LoadRoutine((p) => ui.SetSplashProgress(0.15f + p * 0.35f, "Tuning the horn…"));

            ui.SetSplashProgress(0.5f, "Painting the jeepney…");
            yield return null;
            ModelLibrary.Jeepney(JeepneyCatalog.Get(Services.Save.Data.selectedJeepney).Id);
            ui.SetSplashProgress(0.6f, "Calling the traffic…");
            yield return null;
            ModelLibrary.WarmUpTraffic();
            ui.SetSplashProgress(0.75f, "Building Manila streets…");
            yield return null;
            ModelLibrary.WarmUpScenery((EnvironmentKind)Services.Save.Data.selectedEnvironment);
            ui.SetSplashProgress(0.85f, "Opening the terminal…");
            yield return null;

            bool done = false;
            Services.Scenes.Load(SceneLoader.MainMenuScene, p => ui.SetSplashProgress(0.85f + p * 0.15f, "Opening the terminal…"), () => done = true);
            while (!done && Services.Scenes.IsLoading) yield return null;
            if (SceneManager.GetActiveScene().name == SceneLoader.MainMenuScene) Services.Game.SetState(GameState.MainMenu);
            // Phase 3: sign in (guest or saved account) in the background – the game never waits for it
            Services.Online.Connect();
        }
    }

    /// <summary>Polls the Android back button / Escape key.</summary>
    public sealed class BackButtonListener : MonoBehaviour
    {
        private void Update()
        {
            if (InputCompat.BackPressedThisFrame()) Services.Game?.HandleBack();
        }
    }
}
