using UnityEngine;

namespace JeepneyRush
{
    /// <summary>
    /// Player-selectable graphics level (Settings). 0 = Low (no real-time shadows, reduced
    /// resolution), 1 = Medium (default, shadows), 2 = High (sharper shadows, native resolution).
    /// </summary>
    public static class GraphicsQuality
    {
        public static int Level { get; private set; } = 1;

        public static void Apply(int level)
        {
            Level = Mathf.Clamp(level, 0, 2);
            // project Quality levels may differ per template; clamp to what exists
            int count = QualitySettings.names.Length;
            if (count > 0) QualitySettings.SetQualityLevel(Mathf.Clamp(Mathf.RoundToInt(Level / 2f * (count - 1)), 0, count - 1), true);
            QualitySettings.shadowDistance = Level == 0 ? 0f : Level == 1 ? 40f : 60f;
            // render at a lower resolution on Low to help weaker GPUs
            float scale = Level == 0 ? 0.7f : Level == 1 ? 0.85f : 1f;
            var full = Screen.currentResolution;
            if (Application.isMobilePlatform && full.width > 0)
                Screen.SetResolution(Mathf.RoundToInt(full.width * scale), Mathf.RoundToInt(full.height * scale), true);
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (light.type == LightType.Directional) ConfigureSun(light);
        }

        public static void ConfigureSun(Light sun)
        {
            sun.shadows = Level == 0 ? LightShadows.None : LightShadows.Soft;
            sun.shadowResolution = Level == 2 ? UnityEngine.Rendering.LightShadowResolution.High : UnityEngine.Rendering.LightShadowResolution.Medium;
        }
    }
}
