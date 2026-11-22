using System.IO;
using System.Linq;
using UnityEditor;

namespace JeepneyRush.EditorTools
{
    /// <summary>
    /// Import rules for everything in Resources/Textures, applied automatically whenever a
    /// texture is (re)imported – so copying a new version of the textures over the project
    /// never brings back uncompressed images.
    ///   Android: ASTC 4x4 for text and UI (stays crisp), ASTC 6x6 for everything else
    ///   (about 4x smaller than 4x4 / 9x smaller than RGBA32), max 1024 px except the splash.
    /// </summary>
    public sealed class TextureRules : AssetPostprocessor
    {
        public const string Folder = "Assets/JeepneyRush/Resources/Textures/";
        private static readonly string[] Tiled = { "road", "sidewalk", "curb", "facade", "grass", "sand", "road_dirt", "dirt", "curb_dirt" };

        private void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').StartsWith(Folder)) return;
            Apply((TextureImporter)assetImporter, Path.GetFileNameWithoutExtension(assetPath));
        }

        /// <summary>Text, logos and UI need sharp edges; photos/patterns compress harder.</summary>
        public static bool IsSharp(string name) =>
            name.StartsWith("ui_") || name.StartsWith("stop_") || name.EndsWith("_sign") || name.EndsWith("_label") || name.EndsWith("_fascia")
            || name == "logo" || name == "ninjatech_logo" || name == "shop_signs" || name.StartsWith("jeepney_sign") || name.StartsWith("dash_");

        public static void Apply(TextureImporter imp, string name)
        {
            bool ui = name.StartsWith("ui_") || name == "logo" || name == "ninjatech_logo";
            imp.textureType = TextureImporterType.Default;
            imp.sRGBTexture = true;
            imp.alphaIsTransparency = true;
            imp.isReadable = false;
            imp.mipmapEnabled = !ui;
            imp.wrapMode = Tiled.Contains(name) ? UnityEngine.TextureWrapMode.Repeat : UnityEngine.TextureWrapMode.Clamp;
            imp.filterMode = UnityEngine.FilterMode.Bilinear;
            imp.anisoLevel = name == "road" ? 4 : 1;
            imp.maxTextureSize = name == "ui_splash_bg" ? 1024 : 2048;
            imp.textureCompression = ui ? TextureImporterCompression.CompressedHQ : TextureImporterCompression.Compressed;

            var android = imp.GetPlatformTextureSettings("Android");
            android.overridden = true;
            android.maxTextureSize = 1024;
            android.format = IsSharp(name) ? TextureImporterFormat.ASTC_4x4 : TextureImporterFormat.ASTC_6x6;
            android.compressionQuality = 50;
            imp.SetPlatformTextureSettings(android);
        }
    }
}
