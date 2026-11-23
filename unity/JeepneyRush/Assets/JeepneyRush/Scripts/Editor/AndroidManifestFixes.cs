using System.IO;
using UnityEditor.Android;
using UnityEngine;

namespace JeepneyRush.EditorTools
{
    /// <summary>
    /// Adjusts the Android manifest Unity generates, after every Android build export.
    /// Locking the game to portrait makes Unity declare android.hardware.screen.portrait as a
    /// REQUIRED feature, which Google Play flags because it hides the app from tablets,
    /// Chromebooks, Play Games on PC and Android XR. The game still runs in portrait – the
    /// feature is just marked optional so those devices can install it.
    /// </summary>
    public sealed class AndroidManifestFixes : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 100;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            // `path` is the unityLibrary module; check the launcher module too
            foreach (var manifest in new[]
            {
                Path.Combine(path, "src", "main", "AndroidManifest.xml"),
                Path.Combine(path, "..", "launcher", "src", "main", "AndroidManifest.xml"),
            })
            {
                if (!File.Exists(manifest)) continue;
                string xml = File.ReadAllText(manifest);
                string fixedXml = JeepneyRush.Logic.Release.ManifestPatch.MakeFeaturesOptional(xml);
                if (fixedXml != xml)
                {
                    File.WriteAllText(manifest, fixedXml);
                    Debug.Log("[Jeepney Rush] Marked hardware features optional in " + manifest);
                }
            }
        }
    }
}
