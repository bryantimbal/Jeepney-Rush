using System;
using System.Text;

namespace JeepneyRush.Logic
{
    /// <summary>Versioned save model (serialised with Unity's JsonUtility: public fields only).</summary>
    [Serializable]
    public class SaveData
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        public int coins;
        public int bestScore;
        public float bestDistance;
        public string selectedJeepney = JeepneyCatalog.DefaultId;

        // settings
        public bool music = true;
        public bool sfx = true;
        public bool vibration = true;
        /// <summary>0.5 (needs long swipes) .. 1.5 (very sensitive).</summary>
        public float swipeSensitivity = 1f;
        public int graphicsQuality = 1; // 0 low, 1 medium, 2 high
        public bool tutorialDone;

        // lifetime stats
        public int runs;
        public float totalDistance;
        public int totalCoins;
        public int totalPassengers;
        public int crashes;
    }

    /// <summary>On-disk wrapper: the body plus an integrity checksum.</summary>
    [Serializable]
    public class SaveEnvelope
    {
        public int version;
        public string checksum;
        public string body;
    }

    public static class SaveValidation
    {
        /// <summary>FNV-1a over UTF-8. Detects truncated/corrupted files – not an anti-cheat measure.</summary>
        public static string Checksum(string s)
        {
            uint h = 0x811C9DC5;
            foreach (byte b in Encoding.UTF8.GetBytes(s ?? string.Empty))
            {
                h ^= b;
                h = unchecked(h * 0x01000193);
            }
            return h.ToString("x8");
        }

        private static int NonNeg(int v, int max = 1_000_000_000) => v < 0 ? 0 : v > max ? max : v;
        private static float NonNeg(float v, float max = 1e9f) => float.IsNaN(v) || float.IsInfinity(v) || v < 0 ? 0 : v > max ? max : v;

        /// <summary>Clamp every field into a valid range and migrate older versions. Returns the same instance.</summary>
        public static SaveData Sanitize(SaveData d)
        {
            if (d == null) return new SaveData();
            // future migrations: if (d.version < 2) { ... }
            d.version = SaveData.CurrentVersion;
            d.coins = NonNeg(d.coins);
            d.bestScore = NonNeg(d.bestScore);
            d.bestDistance = NonNeg(d.bestDistance);
            if (!JeepneyCatalog.Exists(d.selectedJeepney)) d.selectedJeepney = JeepneyCatalog.DefaultId;
            if (float.IsNaN(d.swipeSensitivity) || d.swipeSensitivity < 0.5f || d.swipeSensitivity > 1.5f) d.swipeSensitivity = 1f;
            if (d.graphicsQuality < 0 || d.graphicsQuality > 2) d.graphicsQuality = 1;
            d.runs = NonNeg(d.runs);
            d.totalDistance = NonNeg(d.totalDistance);
            d.totalCoins = NonNeg(d.totalCoins);
            d.totalPassengers = NonNeg(d.totalPassengers);
            d.crashes = NonNeg(d.crashes);
            return d;
        }

        public static bool Verify(SaveEnvelope env) => env != null && !string.IsNullOrEmpty(env.body) && env.checksum == Checksum(env.body);
    }

    /// <summary>Result of a finished run, applied to the save in one place.</summary>
    public struct RunSummary
    {
        public int Score;
        public float Distance;
        public int Coins;
        public int Passengers;
        public bool Crashed;
    }

    public static class Progress
    {
        /// <summary>Applies a run. Returns (newBestScore, newBestDistance).</summary>
        public static (bool score, bool distance) Apply(SaveData d, RunSummary r)
        {
            int score = Math.Max(0, r.Score);
            bool bestScore = score > d.bestScore;
            bool bestDistance = r.Distance > d.bestDistance;
            if (bestScore) d.bestScore = score;
            if (bestDistance) d.bestDistance = r.Distance;
            d.coins += Math.Max(0, r.Coins);
            d.runs++;
            d.totalDistance += Math.Max(0, r.Distance);
            d.totalCoins += Math.Max(0, r.Coins);
            d.totalPassengers += Math.Max(0, r.Passengers);
            if (r.Crashed) d.crashes++;
            SaveValidation.Sanitize(d);
            return (bestScore, bestDistance);
        }
    }
}
