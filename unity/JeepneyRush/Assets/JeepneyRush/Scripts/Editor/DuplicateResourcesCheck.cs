using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace JeepneyRush.EditorTools
{
    /// <summary>
    /// Warns when another "Resources" folder in the project holds a texture/font with the same
    /// name as one of the game's own (Assets/JeepneyRush/Resources). Unity's Resources.Load then
    /// picks either copy, so an old file left over from an earlier update can show stale art
    /// (e.g. a stop sign with the wrong name). Runs once after scripts reload, and from the menu.
    /// </summary>
    [InitializeOnLoad]
    public static class DuplicateResourcesCheck
    {
        private const string Ours = "Assets/JeepneyRush/Resources/";

        static DuplicateResourcesCheck() => EditorApplication.delayCall += () => Check(false);

        [MenuItem("Jeepney Rush/Check for Old Duplicate Files")]
        public static void CheckFromMenu() => Check(true);

        private static void Check(bool verbose)
        {
            var ours = new HashSet<string>();
            var others = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D t:Font t:Material"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                int r = path.IndexOf("/Resources/");
                if (r < 0) continue;
                string rel = path.Substring(r + "/Resources/".Length);
                rel = Path.ChangeExtension(rel, null);
                if (path.StartsWith(Ours)) ours.Add(rel); else others.Add(path);
            }
            var dupes = new List<string>();
            foreach (var p in others)
            {
                int r = p.IndexOf("/Resources/");
                string rel = Path.ChangeExtension(p.Substring(r + "/Resources/".Length), null);
                if (ours.Contains(rel)) dupes.Add(p);
            }
            if (dupes.Count > 0)
                Debug.LogError("[Jeepney Rush] Old copies of game files found outside Assets/JeepneyRush. Unity may load these instead of the current ones " +
                               "(wrong stop names, old art). Delete them:\n  " + string.Join("\n  ", dupes));
            else if (verbose) Debug.Log("[Jeepney Rush] No duplicate game files found.");
        }
    }
}
