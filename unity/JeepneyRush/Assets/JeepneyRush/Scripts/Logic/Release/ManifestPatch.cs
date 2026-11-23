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
