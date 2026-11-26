namespace JeepneyRush.Logic
{
    public enum UpdateNeed { None, Optional, Required }

    /// <summary>The newest release and the oldest one still allowed (from Google Play or the server).</summary>
    [System.Serializable]
    public class AppVersionInfo
    {
        public int latest_code;
        public int min_code;
        public string latest_name = "";
        public string message = "";
    }

    /// <summary>
    /// When to ask the player to update. A version below the minimum must update (old clients
    /// that the server no longer supports); a newer optional release is offered once per version
    /// (MAMAYA NA remembers it), and again when an even newer one comes out.
    /// </summary>
    public static class AppUpdates
    {
        public static UpdateNeed Decide(int currentCode, int latestCode, int minCode, int dismissedCode)
        {
            if (currentCode <= 0) return UpdateNeed.None;            // unknown version (editor without settings): never nag
            if (minCode > currentCode) return UpdateNeed.Required;
            if (latestCode > currentCode && latestCode > dismissedCode) return UpdateNeed.Optional;
            return UpdateNeed.None;
        }

        public static string Title(UpdateNeed n) => n == UpdateNeed.Required ? "KAILANGAN NG UPDATE" : "MAY BAGONG VERSION!";

        public static string Text(UpdateNeed n, AppVersionInfo v)
        {
            string what = string.IsNullOrEmpty(v?.message) ? "" : "\n" + v.message;
            string name = string.IsNullOrEmpty(v?.latest_name) ? "" : " " + v.latest_name;
            return n == UpdateNeed.Required
                ? $"This version of Jeepney Rush is too old to keep playing online. Please update{name} on Google Play.{what}"
                : $"Jeepney Rush{name} is out on Google Play.{what}";
        }
    }
}
