using System;
using System.IO;
using JeepneyRush.Logic;
using UnityEngine;

namespace JeepneyRush
{
    /// <summary>
    /// Versioned JSON save in persistentDataPath with checksum, atomic writes and an
    /// automatic backup that is used if the main file is missing or corrupted.
    /// Local data is convenience storage only – never trusted for purchases.
    /// </summary>
    public sealed class SaveManager : MonoBehaviour
    {
        private const string FileName = "jeepneyrush_save.json";
        private const string BackupName = "jeepneyrush_save.bak";

        public SaveData Data { get; private set; } = new SaveData();
        public string LoadReport { get; private set; } = "new";

        private string MainPath => Path.Combine(Application.persistentDataPath, FileName);
        private string BackupPath => Path.Combine(Application.persistentDataPath, BackupName);

        public void Load()
        {
            var main = TryRead(MainPath);
            if (main != null) { Data = main; LoadReport = "ok"; return; }
            var bak = TryRead(BackupPath);
            if (bak != null)
            {
                Data = bak;
                LoadReport = "restored-from-backup";
                Debug.LogWarning("[Save] Main save missing or corrupted – restored from backup.");
                Save();
                return;
            }
            LoadReport = File.Exists(MainPath) ? "reset-corrupted" : "new";
            if (LoadReport != "new") Debug.LogWarning("[Save] Save file corrupted and no backup – starting fresh.");
            Data = new SaveData();
        }

        private static SaveData TryRead(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                var env = JsonUtility.FromJson<SaveEnvelope>(File.ReadAllText(path));
                if (!SaveValidation.Verify(env)) return null;
                var data = JsonUtility.FromJson<SaveData>(env.body);
                return SaveValidation.Sanitize(data);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Save] Could not read {path}: {e.Message}");
                return null;
            }
        }

        public bool Save()
        {
            try
            {
                SaveValidation.Sanitize(Data);
                string body = JsonUtility.ToJson(Data);
                var env = new SaveEnvelope { version = SaveData.CurrentVersion, body = body, checksum = SaveValidation.Checksum(body) };
                string json = JsonUtility.ToJson(env);
                Directory.CreateDirectory(Application.persistentDataPath);
                string tmp = MainPath + ".tmp";
                File.WriteAllText(tmp, json);
                if (File.Exists(MainPath)) File.Copy(MainPath, BackupPath, true);
                if (File.Exists(MainPath)) File.Delete(MainPath);
                File.Move(tmp, MainPath);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Save] Failed to write save: {e.Message}");
                return false;
            }
        }

        public (bool score, bool distance) RecordRun(RunSummary run)
        {
            var result = Progress.Apply(Data, run);
            Save();
            return result;
        }

        public void ResetProgress()
        {
            var keep = Data;
            Data = new SaveData
            {
                music = keep.music, sfx = keep.sfx, vibration = keep.vibration,
                swipeSensitivity = keep.swipeSensitivity, graphicsQuality = keep.graphicsQuality, tutorialDone = keep.tutorialDone,
            };
            Save();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) Save();
        }

        private void OnApplicationQuit() => Save();
    }
}
