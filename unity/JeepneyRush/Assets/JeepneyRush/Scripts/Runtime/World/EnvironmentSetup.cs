using UnityEngine;
using UnityEngine.Rendering;

namespace JeepneyRush
{
    /// <summary>
    /// Manila daytime look: warm sun with soft shadows, tri-light ambient, sky and
    /// distance fog. Creates a camera and sun if the scene does not have them, so every
    /// scene works even if it was created empty.
    /// </summary>
    public static class EnvironmentSetup
    {
        public static readonly Color Fog = new Color(0.97f, 0.87f, 0.75f);

        public static Camera EnsureCamera()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.backgroundColor = new Color(0.62f, 0.83f, 0.97f);
            if (cam.GetComponent<CameraController>() == null) cam.gameObject.AddComponent<CameraController>();
            return cam;
        }

        public static Light EnsureSun()
        {
            Light sun = null;
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None)) if (l.type == LightType.Directional) { sun = l; break; }
            if (sun == null)
            {
                sun = new GameObject("Sun").AddComponent<Light>();
                sun.type = LightType.Directional;
            }
            sun.color = new Color(1f, 0.95f, 0.84f);
            sun.intensity = 1.6f;
            sun.transform.rotation = Quaternion.Euler(52f, -35f, 0f);
            sun.shadowStrength = 0.75f;
            sun.shadowBias = 0.04f;
            GraphicsQuality.ConfigureSun(sun);
            RenderSettings.sun = sun;
            return sun;
        }

        public static void Apply() => Apply(JeepneyRush.Logic.EnvironmentKind.Manila);

        /// <summary>Per-route sky, fog and ambient colours.</summary>
        public static void Apply(JeepneyRush.Logic.EnvironmentKind env)
        {
            EnsureSun();
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.78f, 0.88f, 1f);
            RenderSettings.ambientEquatorColor = new Color(0.86f, 0.8f, 0.72f);
            RenderSettings.ambientGroundColor = new Color(0.45f, 0.4f, 0.35f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            var fog = env == JeepneyRush.Logic.EnvironmentKind.Provincial ? new Color(0.78f, 0.9f, 0.95f)
                : env == JeepneyRush.Logic.EnvironmentKind.Beach ? new Color(0.72f, 0.9f, 0.98f) : Fog;
            RenderSettings.fogColor = fog;
            if (env != JeepneyRush.Logic.EnvironmentKind.Manila) RenderSettings.ambientEquatorColor = new Color(0.8f, 0.86f, 0.88f);
            RenderSettings.fogStartDistance = 70f;
            RenderSettings.fogEndDistance = 210f;
            var sky = Resources.Load<Material>("Materials/JR_Skybox");
            if (sky == null)
            {
                var sh = Shader.Find("Skybox/Procedural");
                if (sh != null)
                {
                    sky = new Material(sh);
                    if (sky.HasProperty("_SkyTint")) sky.SetColor("_SkyTint", new Color(0.45f, 0.65f, 0.95f));
                    if (sky.HasProperty("_GroundColor")) sky.SetColor("_GroundColor", Fog);
                    if (sky.HasProperty("_AtmosphereThickness")) sky.SetFloat("_AtmosphereThickness", 0.8f);
                }
            }
            if (sky != null) RenderSettings.skybox = sky;
        }
    }
}
