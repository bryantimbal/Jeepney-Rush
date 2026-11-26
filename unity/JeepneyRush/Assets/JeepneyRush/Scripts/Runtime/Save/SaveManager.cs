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

        /// <summary>Applies a finished run (bests, coins, XP, missions, achievements) and saves.</summary>
        public Progress.RunRewards RecordRun(RunSummary run)
        {
            // missions follow Philippine time (UTC+8), the same clock the server uses
            var result = Progress.Apply(Data, run, PhilippineTime.DayKey, PhilippineTime.WeekKey);
            Save();
            return result;
        }

        /// <summary>Rolls daily/weekly missions over when the date changed.</summary>
        public void RefreshMissions()
        {
            Missions.Refresh(Data, PhilippineTime.DayKey, PhilippineTime.WeekKey);
        }

        /// <summary>Writes a copy of the current save next to it (jeepneyrush_save.&lt;tag&gt;.bak) – recovery insurance.</summary>
        public void Backup(string tag)
        {
            try
            {
                string body = JsonUtility.ToJson(Data);
                var env = new SaveEnvelope { version = SaveData.CurrentVersion, body = body, checksum = SaveValidation.Checksum(body) };
                File.WriteAllText(Path.Combine(Application.persistentDataPath, $"jeepneyrush_save.{tag}.bak"), JsonUtility.ToJson(env));
                Debug.Log($"[Save] backup written: jeepneyrush_save.{tag}.bak");
            }
            catch (Exception e) { Debug.LogWarning("[Save] backup failed: " + e.Message); }
        }

        public void ResetProgress()
        {
            var keep = Data;
            Data = new SaveData
            {
                music = keep.music, sfx = keep.sfx, vibration = keep.vibration,
                swipeSensitivity = keep.swipeSensitivity, graphicsQuality = keep.graphicsQuality, tutorialDone = keep.tutorialDone,
                screenshotButton = keep.screenshotButton, storeFrame = keep.storeFrame,
                steerMode = keep.steerMode, steerModeChosen = keep.steerModeChosen, laneAssist = keep.laneAssist, cameraEffects = keep.cameraEffects, driverView = keep.driverView,
                maxSteer = keep.maxSteer, steerReturn = keep.steerReturn, tiltCentre = keep.tiltCentre, tiltCalibrated = keep.tiltCalibrated,
                tiltDeadZone = keep.tiltDeadZone, tiltSmoothing = keep.tiltSmoothing, wheelSize = keep.wheelSize, wheelOpacity = keep.wheelOpacity,
                wheelMaxAngle = keep.wheelMaxAngle, buttonScale = keep.buttonScale, buttonOpacity = keep.buttonOpacity, leftHanded = keep.leftHanded,
                steerTutorials = keep.steerTutorials, soundTrip = keep.soundTrip,
                orientation = keep.orientation, tiltCentreLand = keep.tiltCentreLand, tiltCalibratedLand = keep.tiltCalibratedLand,
                tiltRoll = keep.tiltRoll, tiltRollLand = keep.tiltRollLand, tiltRollCal = keep.tiltRollCal, tiltRollLandCal = keep.tiltRollLandCal,
                tiltAxesKnown = keep.tiltAxesKnown, tiltAxesRotated = keep.tiltAxesRotated,
                tiltNeutral = keep.tiltNeutral, tiltNeutralLand = keep.tiltNeutralLand, tiltNeutralCal = keep.tiltNeutralCal, tiltNeutralLandCal = keep.tiltNeutralLandCal,
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
