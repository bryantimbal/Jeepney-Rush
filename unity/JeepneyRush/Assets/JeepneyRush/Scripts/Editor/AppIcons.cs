using System.Linq;
using UnityEditor;
using UnityEngine;

namespace JeepneyRush.EditorTools
{
    /// <summary>
    /// Puts the Jeepney Rush icon in every Android icon slot. Unity's default icon only covers
    /// old launchers: Android 8+ uses ADAPTIVE icons (background + foreground layers) and Unity
    /// falls back to its own logo when those are empty – which is why the phone showed the
    /// Unity icon. Runs from Setup, before every release build, and from the menu.
    /// </summary>
    public static class AppIcons
    {
        public const string Dir = "Assets/JeepneyRush/Art/Icon/";
        public const string Full = Dir + "icon-512.png", Background = Dir + "icon_adaptive_bg.png", Foreground = Dir + "icon_adaptive_fg.png";

        [MenuItem("Jeepney Rush/Setup/App Icons (Android)", priority = 30)]
        public static void ApplyMenu()
        {
            string problem = Apply();
            if (problem != null) EditorUtility.DisplayDialog("App icons", problem, "OK");
            else Debug.Log("[Jeepney Rush] App icons set for every Android icon type (adaptive, round, legacy).");
        }

        /// <summary>Returns null on success, otherwise what is missing.</summary>
        public static string Apply()
        {
            var full = AssetDatabase.LoadAssetAtPath<Texture2D>(Full);
            var bg = AssetDatabase.LoadAssetAtPath<Texture2D>(Background);
            var fg = AssetDatabase.LoadAssetAtPath<Texture2D>(Foreground);
            if (full == null || bg == null || fg == null)
                return "Icon images not found in " + Dir + " – copy the whole Assets/JeepneyRush folder again.";

            // default icon (used by every platform without its own icons)
#if UNITY_2021_2_OR_NEWER
            PlayerSettings.SetIcons(UnityEditor.Build.NamedBuildTarget.Unknown, new[] { full }, IconKind.Any);
#else
            PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Unknown, new[] { full });
#endif
            // Android: adaptive (2 layers), round and legacy, every density
#pragma warning disable 618
            var kinds = PlayerSettings.GetSupportedIconKindsForPlatform(BuildTargetGroup.Android);
            foreach (var kind in kinds)
            {
                var icons = PlayerSettings.GetPlatformIcons(BuildTargetGroup.Android, kind);
                bool adaptive = kind.ToString().IndexOf("Adaptive", System.StringComparison.OrdinalIgnoreCase) >= 0;
                foreach (var icon in icons)
                {
                    if (adaptive && icon.maxLayerCount >= 2) icon.SetTextures(bg, fg); // layer 0 = background, 1 = foreground
                    else icon.SetTextures(Enumerable.Repeat(full, Mathf.Max(1, icon.minLayerCount)).ToArray());
                }
                PlayerSettings.SetPlatformIcons(BuildTargetGroup.Android, kind, icons);
            }
#pragma warning restore 618
            AssetDatabase.SaveAssets();
            return null;
        }
    }
}
