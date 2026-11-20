namespace JeepneyRush.Logic
{
    /// <summary>Paint scheme + identity of a jeepney. Colours are hex strings so this stays engine-free.</summary>
    public sealed class JeepneyLivery
    {
        public string Id;
        public string DisplayName;
        public string Body;      // main painted panels
        public string Hood;
        public string Accent1;   // stripes / sun rays
        public string Accent2;
        public string Roof;
        public string Trim;      // pillars / visor
        public string RoofSign;  // text on the roof sign (pre-rendered texture name)
        public string Route;
        public bool ChromeLowerBody; // stainless lower body like the classic Sarao style

        public string SideTexture => $"jeepney_side_{Id}";
        public string RearTexture => $"jeepney_rear_{Id}";
        public string SignTexture => $"jeepney_sign_{Id}";
    }

    /// <summary>Phase 1 ships two free liveries (from the art references); the Phase 2 garage adds the rest.</summary>
    public static class JeepneyCatalog
    {
        public const string DefaultId = "pilipinas";

        public static readonly JeepneyLivery[] All =
        {
            new JeepneyLivery
            {
                Id = "pilipinas", DisplayName = "Pilipinas Classic",
                Body = "#C8102E", Hood = "#1F3FBF", Accent1 = "#FCD116", Accent2 = "#FFFFFF", Roof = "#1F3FBF", Trim = "#FCD116",
                RoofSign = "PILIPINAS", Route = "QUIAPO - CUBAO", ChromeLowerBody = true,
            },
            new JeepneyLivery
            {
                Id = "sanjuan", DisplayName = "San Juan Express",
                Body = "#8E2BD9", Hood = "#8E2BD9", Accent1 = "#FF8A1F", Accent2 = "#FFD23F", Roof = "#2FA4E7", Trim = "#25C98B",
                RoofSign = "SAN JUAN - SAN FERNANDO", Route = "SAN JUAN - SAN FERNANDO", ChromeLowerBody = false,
            },
        };

        public static JeepneyLivery Get(string id)
        {
            foreach (var j in All) if (j.Id == id) return j;
            return All[0];
        }

        public static bool Exists(string id)
        {
            foreach (var j in All) if (j.Id == id) return true;
            return false;
        }
    }
}
