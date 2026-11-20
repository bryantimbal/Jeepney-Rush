using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JeepneyRush
{
    /// <summary>Async scene loading with progress reporting (used by the splash and menus).</summary>
    public sealed class SceneLoader : MonoBehaviour
    {
        public const string BootstrapScene = "Bootstrap";
        public const string MainMenuScene = "MainMenu";
        public const string GameplayScene = "Gameplay";

        public bool IsLoading { get; private set; }

        public void Load(string scene, Action<float> progress = null, Action done = null)
        {
            if (IsLoading) return;
            StartCoroutine(LoadRoutine(scene, progress, done));
        }

        private IEnumerator LoadRoutine(string scene, Action<float> progress, Action done)
        {
            IsLoading = true;
            AsyncOperation op = null;
            try
            {
                op = SceneManager.LoadSceneAsync(scene);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SceneLoader] Cannot load '{scene}'. Is it in Build Settings? Run 'Jeepney Rush > Setup Project'. {e}");
            }
            if (op == null)
            {
                IsLoading = false;
                yield break;
            }
            while (!op.isDone)
            {
                progress?.Invoke(Mathf.Clamp01(op.progress / 0.9f));
                yield return null;
            }
            IsLoading = false;
            progress?.Invoke(1f);
            done?.Invoke();
        }
    }
}
