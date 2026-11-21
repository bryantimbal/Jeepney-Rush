using System;
using System.Reflection;
using UnityEngine;

namespace JeepneyRush
{
    /// <summary>
    /// Player-selectable graphics level (Settings) and a clean-image setup:
    /// full-resolution rendering, MSAA anti-aliasing, no film grain / post effects and
    /// stable shadows. Low = no real-time shadows, 2x MSAA, 85% render scale.
    /// The URP asset is tuned through reflection so this compiles without the URP package.
    /// </summary>
    public static class GraphicsQuality
    {
        public static int Level { get; private set; } = 1;

        public static void Apply(int level)
        {
            Level = Mathf.Clamp(level, 0, 2);
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
            QualitySettings.shadowDistance = Level == 0 ? 0f : 45f;
            TuneUrp();
            foreach (var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (light.type == LightType.Directional) ConfigureSun(light);
            foreach (var cam in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) ConfigureCamera(cam);
        }

        private static void Set(object target, string prop, object value)
        {
            if (target == null) return;
            try
            {
                var p = target.GetType().GetProperty(prop, BindingFlags.Public | BindingFlags.Instance);
                if (p != null && p.CanWrite) p.SetValue(target, value);
            }
            catch (Exception e) { Debug.LogWarning($"[Graphics] could not set {prop}: {e.Message}"); }
        }

        /// <summary>
        /// The Unity 6 URP template's "Mobile" asset renders below native resolution; with no
        /// anti-aliasing that looks grainy and shimmers. Render at native res with MSAA.
        /// </summary>
        private static void TuneUrp()
        {
            var rp = QualitySettings.renderPipeline != null ? QualitySettings.renderPipeline : UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline;
            if (rp == null) return;
            Set(rp, "renderScale", Level == 0 ? 0.85f : 1f);
            Set(rp, "msaaSampleCount", Level == 0 ? 2 : 4);
            Set(rp, "supportsHDR", false);
            Set(rp, "shadowDistance", Level == 0 ? 0f : 45f);
            Set(rp, "shadowCascadeCount", 1);
            Set(rp, "supportsSoftShadows", Level == 2);
            Set(rp, "mainLightShadowmapResolution", Level == 2 ? 2048 : 1024);
            Set(rp, "shadowDepthBias", 1.2f);
            Set(rp, "shadowNormalBias", 1.2f);
        }

        /// <summary>No post-processing (removes film grain/vignette from template volumes), MSAA on.</summary>
        public static void ConfigureCamera(Camera cam)
        {
            if (cam == null) return;
            cam.allowMSAA = true;
            cam.allowHDR = false;
            var type = Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
            if (type == null) return;
            var data = cam.GetComponent(type);
            if (data == null) return;
            Set(data, "renderPostProcessing", false);
            Set(data, "dithering", false);
            Set(data, "stopNaN", false);
        }

        public static void ConfigureSun(Light sun)
        {
            sun.shadows = Level == 0 ? LightShadows.None : Level == 2 ? LightShadows.Soft : LightShadows.Hard;
            sun.shadowResolution = Level == 2 ? UnityEngine.Rendering.LightShadowResolution.High : UnityEngine.Rendering.LightShadowResolution.Medium;
            sun.shadowBias = 0.08f;
            sun.shadowNormalBias = 0.6f;
        }
    }
}
