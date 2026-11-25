using System.Text.RegularExpressions;

namespace JeepneyRush.Logic.Release
{
    /// <summary>Edits to the Android manifest Unity generates (applied by the editor after export).</summary>
    public static class ManifestPatch
    {
        /// <summary>Hardware features the game works without: they must never be "required".</summary>
        public static readonly string[] OptionalFeatures =
        {
            "android.hardware.screen.portrait",
            "android.hardware.screen.landscape",
            "android.hardware.touchscreen",
            "android.hardware.touchscreen.multitouch",
            "android.hardware.touchscreen.multitouch.distinct",
        };

        /// <summary>Permissions the game needs that Unity can't detect on its own (vibration goes through JNI).</summary>
        public static readonly string[] RequiredPermissions = { "android.permission.VIBRATE" };

        /// <summary>Adds the uses-permission entries that are missing (idempotent).</summary>
        public static string EnsurePermissions(string xml)
        {
            foreach (var perm in RequiredPermissions)
            {
                if (Regex.IsMatch(xml, "<uses-permission\\b[^>]*android:name\\s*=\\s*\"" + Regex.Escape(perm) + "\"")) continue;
                var open = Regex.Match(xml, "<manifest\\b[^>]*>", RegexOptions.Singleline);
                if (!open.Success) continue;
                int at = open.Index + open.Length;
                xml = xml.Substring(0, at) + "\n  <uses-permission android:name=\"" + perm + "\" />" + xml.Substring(at);
            }
            return xml;
        }

        /// <summary>Sets android:required="false" on those uses-feature entries (adds it when missing).</summary>
        public static string MakeFeaturesOptional(string xml)
        {
            foreach (var feature in OptionalFeatures)
            {
                var rx = new Regex("<uses-feature\\b[^>]*android:name\\s*=\\s*\"" + Regex.Escape(feature) + "\"[^>]*?/?>", RegexOptions.Singleline);
                xml = rx.Replace(xml, m =>
                {
                    string tag = m.Value;
                    if (Regex.IsMatch(tag, "android:required\\s*=\\s*\"")) return Regex.Replace(tag, "android:required\\s*=\\s*\"[^\"]*\"", "android:required=\"false\"");
                    return tag.EndsWith("/>") ? tag.Substring(0, tag.Length - 2).TrimEnd() + " android:required=\"false\" />"
                                              : tag.Substring(0, tag.Length - 1).TrimEnd() + " android:required=\"false\">";
                });
            }
            return xml;
        }
    }
}
