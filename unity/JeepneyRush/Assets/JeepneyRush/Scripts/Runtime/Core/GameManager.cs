using JeepneyRush.Logic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JeepneyRush
{
    /// <summary>
    /// Central state machine (Loading → MainMenu → Starting → Playing ⇄ Paused → GameOver).
    /// Persistent across scenes; the active <see cref="GameplaySession"/> registers itself.
    /// </summary>
    public sealed class GameManager : MonoBehaviour
    {
        public GameState State { get; private set; } = GameState.Loading;
        public GameplaySession Session { get; private set; }
        public MainMenuController Menu { get; private set; }

        public void SetState(GameState next)
        {
            if (next == State) return;
            var prev = State;
            State = next;
            Time.timeScale = next == GameState.Paused ? 0f : 1f;
            GameEvents.RaiseStateChanged(prev, next);
        }

        internal void Register(GameplaySession s) => Session = s;
        internal void Unregister(GameplaySession s) { if (Session == s) Session = null; }
        internal void Register(MainMenuController m) => Menu = m;
        internal void Unregister(MainMenuController m) { if (Menu == m) Menu = null; }

        // ------------------------------------------------------------------ navigation
        public void Play()
        {
            if (Session == null) { GameplaySession.Challenge = null; GameplaySession.Shift = null; GameplaySession.PlayMode = GameMode.FreeRide; } // free ride from the menu
            if (Session != null)
            {
                Session.StartRun();
                return;
            }
            Services.Pools.ReleaseAll();
            SetState(GameState.Loading);
            Services.Scenes.Load(SceneLoader.GameplayScene, p => Services.UI.ShowLoading(p));
            // the GameplaySession starts the run in its Start()
        }

        /// <summary>Plays today's Daily Challenge: same seed and route for every player.</summary>
        public void PlayDaily(DailyChallenge c)
        {
            GameplaySession.Shift = null;
            GameplaySession.PlayMode = GameMode.FreeRide;
            GameplaySession.Challenge = c;
            Services.Pools.ReleaseAll();
            SetState(GameState.Loading);
            Services.Scenes.Load(SceneLoader.GameplayScene, p => Services.UI.ShowLoading(p));
        }

        /// <summary>Plays a career shift (its own route; ends at the terminal).</summary>
        public void PlayShift(CareerShift shift)
        {
            GameplaySession.Challenge = null;
            GameplaySession.PlayMode = GameMode.FreeRide;
            GameplaySession.Shift = shift;
            Time.timeScale = 1f;
            Services.Pools.ReleaseAll();
            SetState(GameState.Loading);
            Services.Scenes.Load(SceneLoader.GameplayScene, p => Services.UI.ShowLoading(p));
        }

        /// <summary>Plays a game mode (Rush Hour / Night Shift) on the selected route.</summary>
        public void PlayMode(GameMode mode)
        {
            GameplaySession.Challenge = null;
            GameplaySession.Shift = null;
            GameplaySession.PlayMode = mode;
            Time.timeScale = 1f;
            Services.Pools.ReleaseAll();
            SetState(GameState.Loading);
            Services.Scenes.Load(SceneLoader.GameplayScene, p => Services.UI.ShowLoading(p));
        }

        public void GoToMainMenu()
        {
            GameplaySession.Challenge = null;
            GameplaySession.Shift = null;
            GameplaySession.PlayMode = GameMode.FreeRide;
            Time.timeScale = 1f;
            Services.Pools.ReleaseAll();
            if (SceneManager.GetActiveScene().name == SceneLoader.MainMenuScene)
            {
                SetState(GameState.MainMenu);
                return;
            }
            SetState(GameState.Loading);
            Services.Scenes.Load(SceneLoader.MainMenuScene, p => Services.UI.ShowLoading(p), () => SetState(GameState.MainMenu));
        }

        public void Pause()
        {
            if (State == GameState.Playing || State == GameState.Starting) SetState(GameState.Paused);
        }

        public void Resume()
        {
            if (State == GameState.Paused) SetState(GameState.Playing);
        }

        public void Restart()
        {
            Time.timeScale = 1f;
            if (Session != null) Session.StartRun();
            else Play();
        }

        // ------------------------------------------------------------------ lifecycle
        private void OnApplicationPause(bool paused)
        {
            // Android: home button, incoming call, screen off -> pause the run
            if (paused) Pause();
        }

        private void OnApplicationFocus(bool focus)
        {
            if (!focus) Pause();
        }

        /// <summary>Android back button / Escape.</summary>
        public void HandleBack()
        {
            if (Services.UI != null && Services.UI.HandleBack()) return;
            switch (State)
            {
                case GameState.Playing:
                case GameState.Starting: Pause(); break;
                case GameState.Paused: Resume(); break;
                case GameState.GameOver: GoToMainMenu(); break;
                case GameState.MainMenu:
#if UNITY_ANDROID && !UNITY_EDITOR
                    Application.Quit();
#endif
                    break;
            }
        }
    }
}
