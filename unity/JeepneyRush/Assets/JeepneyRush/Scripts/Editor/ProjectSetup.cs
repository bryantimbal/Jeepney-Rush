using System;
using System.IO;
using System.Linq;
using JeepneyRush.Logic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace JeepneyRush.EditorTools
{
    /// <summary>
    /// One-click project configuration: Jeepney Rush ▸ Setup Project.
    /// Idempotent – safe to run again after pulling changes.
    /// </summary>
    public static class ProjectSetup
    {
        private const string Root = "Assets/JeepneyRush";
        private const string Res = Root + "/Resources";
        private const string Scenes = Root + "/Scenes";
        private const string Settings = Root + "/Settings";

        [MenuItem("Jeepney Rush/Setup Project", priority = 0)]
        public static void SetupAll()
        {
            try
            {
                EditorUtility.DisplayProgressBar("Jeepney Rush", "Player settings", 0.1f);
                ConfigurePlayer();
                EditorUtility.DisplayProgressBar("Jeepney Rush", "Render pipeline (URP)", 0.2f);
                EnsureUrp();
                EditorUtility.DisplayProgressBar("Jeepney Rush", "Textures", 0.35f);
                ConfigureTextures();
                EditorUtility.DisplayProgressBar("Jeepney Rush", "Materials", 0.5f);
                CreateMaterials();
                EditorUtility.DisplayProgressBar("Jeepney Rush", "ScriptableObjects", 0.6f);
                CreateConfigAssets();
                EditorUtility.DisplayProgressBar("Jeepney Rush", "Scenes", 0.75f);
                CreateScenes();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("[Jeepney Rush] Setup complete. Open Assets/JeepneyRush/Scenes/Bootstrap.unity and press Play.");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path)!.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        // ------------------------------------------------------------------ player
        [MenuItem("Jeepney Rush/Setup/Player Settings (Android, portrait)")]
        public static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "Jeepney Rush";
            PlayerSettings.productName = "Jeepney Rush";
            if (string.IsNullOrEmpty(PlayerSettings.bundleVersion) || PlayerSettings.bundleVersion == "0.1") PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;
#if UNITY_2021_2_OR_NEWER
            var android = UnityEditor.Build.NamedBuildTarget.Android;
            PlayerSettings.SetApplicationIdentifier(android, "ph.jeepneyrush.game"); // change before publishing
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetManagedStrippingLevel(android, ManagedStrippingLevel.Low);
#else
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "ph.jeepneyrush.game");
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.Android, ManagedStrippingLevel.Low);
#endif
            PlayerSettings.Android.renderOutsideSafeArea = true;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.runInBackground = false;
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan, GraphicsDeviceType.OpenGLES3 });
            if (PlayerSettings.Android.bundleVersionCode < 1) PlayerSettings.Android.bundleVersionCode = 1;
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Art/Icon/icon-512.png");
            if (icon != null)
            {
#if UNITY_2021_2_OR_NEWER
                PlayerSettings.SetIcons(UnityEditor.Build.NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
#else
                PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Unknown, new[] { icon });
#endif
            }
        }

        // ------------------------------------------------------------------ URP
        /// <summary>
        /// Creates and assigns a mobile-tuned URP asset if the project has none (e.g. when the
        /// folder is opened directly rather than from the Universal 3D template). Uses
        /// reflection so this script compiles even before the URP package is installed.
        /// </summary>
        [MenuItem("Jeepney Rush/Setup/Render Pipeline (URP)")]
        public static void EnsureUrp()
        {
            if (GraphicsSettings.defaultRenderPipeline != null) return;
            var assetType = Type.GetType("UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset, Unity.RenderPipelines.Universal.Runtime");
            var dataType = Type.GetType("UnityEngine.Rendering.Universal.UniversalRendererData, Unity.RenderPipelines.Universal.Runtime");
            if (assetType == null || dataType == null)
            {
                Debug.LogError("[Jeepney Rush] URP package not found. Install 'Universal RP' from the Package Manager (or create the project from the Universal 3D template), then run Setup again.");
                return;
            }
            EnsureFolder(Settings);
            var data = ScriptableObject.CreateInstance(dataType);
            AssetDatabase.CreateAsset(data, Settings + "/JR_URP_Renderer.asset");
            var create = assetType.GetMethod("Create", new[] { dataType.BaseType });
            var asset = (RenderPipelineAsset)create!.Invoke(null, new object[] { data });
            void Set(string prop, object value)
            {
                var p = assetType.GetProperty(prop);
                if (p != null && p.CanWrite) p.SetValue(asset, value);
            }
            Set("shadowDistance", 60f);
            Set("msaaSampleCount", 2);
            Set("supportsHDR", false);
            Set("renderScale", 1f);
            AssetDatabase.CreateAsset(asset, Settings + "/JR_URP_Mobile.asset");
            GraphicsSettings.defaultRenderPipeline = asset;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = asset;
            }
            QualitySettings.SetQualityLevel(current, false);
            Debug.Log("[Jeepney Rush] Created and assigned URP asset at " + Settings);
        }

        // ------------------------------------------------------------------ textures
        [MenuItem("Jeepney Rush/Setup/Texture Import Settings")]
        public static void ConfigureTextures()
        {
            var tiled = new[] { "road", "sidewalk", "curb", "facade", "grass", "sand", "road_dirt", "dirt", "curb_dirt" };
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Res + "/Textures" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(path) is TextureImporter imp)) continue;
                string name = Path.GetFileNameWithoutExtension(path);
                bool ui = name.StartsWith("ui_") || name == "logo";
                imp.textureType = TextureImporterType.Default;
                imp.sRGBTexture = true;
                imp.alphaIsTransparency = true;
                imp.mipmapEnabled = !ui;
                imp.wrapMode = tiled.Contains(name) ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                imp.filterMode = FilterMode.Bilinear;
                imp.anisoLevel = name == "road" ? 4 : 1;
                imp.maxTextureSize = name == "ui_splash_bg" ? 1024 : 2048;
                imp.textureCompression = ui ? TextureImporterCompression.CompressedHQ : TextureImporterCompression.Compressed;
                imp.SaveAndReimport();
            }
        }

        // ------------------------------------------------------------------ materials
        private static Material SaveMaterial(string name, Shader shader, Action<Material> configure)
        {
            if (shader == null) { Debug.LogWarning($"[Jeepney Rush] Shader for {name} not found."); return null; }
            string path = $"{Res}/Materials/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = shader;
            configure?.Invoke(m);
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Shader Find(params string[] names)
        {
            foreach (var n in names) { var s = Shader.Find(n); if (s != null) return s; }
            return null;
        }

        [MenuItem("Jeepney Rush/Setup/Materials")]
        public static void CreateMaterials()
        {
            EnsureFolder(Res + "/Materials");
            SaveMaterial("JR_Lit", Find("Universal Render Pipeline/Lit", "Standard"), m => m.enableInstancing = true);
            SaveMaterial("JR_Unlit", Find("Universal Render Pipeline/Unlit", "Unlit/Texture"), null);
            SaveMaterial("JR_UnlitTransparent", Find("Universal Render Pipeline/Unlit", "Unlit/Transparent"), m =>
            {
                if (!m.HasProperty("_Surface")) return;
                m.SetFloat("_Surface", 1);
                m.SetFloat("_Blend", 0);
                m.SetOverrideTag("RenderType", "Transparent");
                m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 0);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)RenderQueue.Transparent;
            });
            SaveMaterial("JR_Particle", Find("Universal Render Pipeline/Particles/Unlit", "Particles/Standard Unlit", "Sprites/Default"), m =>
            {
                if (m.HasProperty("_Surface"))
                {
                    m.SetFloat("_Surface", 1);
                    m.SetOverrideTag("RenderType", "Transparent");
                    m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                    m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                    m.SetInt("_ZWrite", 0);
                    m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    m.renderQueue = (int)RenderQueue.Transparent;
                }
            });
            SaveMaterial("JR_Skybox", Find("Skybox/Procedural"), m =>
            {
                if (m.HasProperty("_SkyTint")) m.SetColor("_SkyTint", new Color(0.45f, 0.65f, 0.95f));
                if (m.HasProperty("_GroundColor")) m.SetColor("_GroundColor", EnvironmentSetup.Fog);
                if (m.HasProperty("_AtmosphereThickness")) m.SetFloat("_AtmosphereThickness", 0.8f);
                if (m.HasProperty("_Exposure")) m.SetFloat("_Exposure", 1.2f);
            });
        }

        // ------------------------------------------------------------------ ScriptableObjects
        [MenuItem("Jeepney Rush/Setup/Config Assets (GameBalance, Jeepneys, OnlineConfig)")]
        public static void CreateConfigAssets()
        {
            EnsureFolder(Res);
            string balancePath = Res + "/GameBalance.asset";
            if (AssetDatabase.LoadAssetAtPath<GameBalance>(balancePath) == null)
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<GameBalance>(), balancePath);
            // Phase 3: Supabase settings (empty = offline build). Never overwritten once you fill it in.
            string onlinePath = Res + "/OnlineConfig.asset";
            if (AssetDatabase.LoadAssetAtPath<OnlineConfig>(onlinePath) == null)
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<OnlineConfig>(), onlinePath);
            EnsureFolder(Res + "/Jeepneys");
            var defaults = JeepneySpec.LoadAll(); // runtime defaults when none exist
            foreach (var l in JeepneyCatalog.All)
            {
                string p = $"{Res}/Jeepneys/{l.Id}.asset";
                if (AssetDatabase.LoadAssetAtPath<JeepneySpec>(p) != null) continue;
                var spec = ScriptableObject.CreateInstance<JeepneySpec>();
                var d = defaults.FirstOrDefault(x => x.liveryId == l.Id);
                spec.liveryId = l.Id;
                spec.displayName = l.DisplayName;
                spec.description = d != null ? d.description : l.DisplayName;
                spec.handling = Mathf.Clamp(3 + Mathf.RoundToInt(l.HandlingBonus * 100), 1, 5);
                spec.passengerCapacity = 5 + l.CapacityBonus;
                spec.health = 3 + l.HealthBonus;
                AssetDatabase.CreateAsset(spec, p);
            }
        }

        // ------------------------------------------------------------------ scenes
        [MenuItem("Jeepney Rush/Setup/Create Scenes")]
        public static void CreateScenes()
        {
            EnsureFolder(Scenes);
            // Bootstrap: persistent managers are created by Bootstrap (RuntimeInitializeOnLoad), the
            // scene hosts the splash/loading sequence.
            NewScene("Bootstrap", () =>
            {
                new GameObject("Bootstrap").AddComponent<Bootstrap>();
                var cam = new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.11f, 0.06f, 0.2f);
                cam.gameObject.AddComponent<AudioListener>();
            });
            NewScene("MainMenu", () =>
            {
                AddCameraAndSun();
                new GameObject("MainMenu").AddComponent<MainMenuController>();
            });
            NewScene("Gameplay", () =>
            {
                AddCameraAndSun();
                var root = new GameObject("Gameplay");
                root.AddComponent<GameplaySession>();
                // the managers listed in the design document, visible in the hierarchy
                void Child<T>(string n) where T : Component { var go = new GameObject(n); go.transform.SetParent(root.transform, false); go.AddComponent<T>(); }
                Child<DifficultyManager>("DifficultyManager");
                Child<ScoreManager>("ScoreManager");
                Child<RoadManager>("RoadManager");
                Child<TrafficManager>("TrafficManager");
                Child<ObstacleManager>("ObstacleManager");
                Child<CoinManager>("CoinManager");
                Child<PassengerManager>("PassengerManager");
                Child<EffectsManager>("Effects");
                var player = new GameObject("PlayerJeepney");
                player.transform.SetParent(root.transform, false);
                player.AddComponent<Rigidbody>().isKinematic = true;
                player.AddComponent<BoxCollider>();
                player.AddComponent<CollisionHandler>();
                player.AddComponent<PlayerController>();
                player.AddComponent<PlayerInputHandler>();
            });
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene($"{Scenes}/Bootstrap.unity", true),
                new EditorBuildSettingsScene($"{Scenes}/MainMenu.unity", true),
                new EditorBuildSettingsScene($"{Scenes}/Gameplay.unity", true),
            };
            EditorSceneManager.OpenScene($"{Scenes}/Bootstrap.unity");
        }

        private static void AddCameraAndSun()
        {
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<CameraController>();
            camGo.transform.position = new Vector3(0, 6.8f, -10.5f);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(52f, -35f, 0f);
            sun.shadows = LightShadows.Soft;
        }

        private static void NewScene(string name, Action populate)
        {
            string path = $"{Scenes}/{name}.unity";
            if (File.Exists(path))
            {
                Debug.Log($"[Jeepney Rush] Scene {name} already exists – kept as is (delete it to regenerate).");
                return;
            }
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            populate();
            EditorSceneManager.SaveScene(scene, path);
        }
    }
}
