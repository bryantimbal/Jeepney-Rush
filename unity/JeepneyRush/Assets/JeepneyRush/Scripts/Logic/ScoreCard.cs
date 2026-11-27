namespace JeepneyRush.Logic
{
    /// <summary>What a shareable score card says (the image is laid out by the UI).</summary>
    public sealed class ScoreCardInfo
    {
        public int Score;
        public float Distance;
        public EnvironmentKind Route;
        public string Jeepney;
        public GameMode Mode;
        /// <summary>Career shift title and stars (0..3), or null.</summary>
        public string Shift;
        public int Stars;
        public bool ShiftDone;
        /// <summary>Ghost Race: the rival and whether you won (null = no ghost).</summary>
        public string Ghost;
        public bool BeatGhost;
        /// <summary>Rank line from the server ("#3 this week"), or null.</summary>
        public string Rank;
        public bool NewBest;
    }

    public static class ScoreCard
    {
        /// <summary>Big line over the score.</summary>
        public static string Headline(ScoreCardInfo c)
        {
            if (c.Shift != null) return c.ShiftDone ? "BIYAHE COMPLETE!" : "HINDI NAKARATING...";
            if (c.Ghost != null) return c.BeatGhost ? "PANALO SA GHOST RACE!" : "MUNTIK NA SA GHOST RACE!";
            if (c.Mode == GameMode.RushHour) return "RUSH HOUR SURVIVOR!";
            if (c.Mode == GameMode.NightShift) return "NIGHT SHIFT TAPOS!";
            return c.NewBest ? "BAGONG RECORD!" : "HARI NG KALSADA?";
        }

        /// <summary>Small line under the score (route, shift, rival).</summary>
        public static string Detail(ScoreCardInfo c)
        {
            string route = Environments.Get(c.Route).Name;
            if (c.Shift != null) return $"{c.Shift} · {route}";
            if (c.Ghost != null) return $"vs {c.Ghost} · {route}";
            return $"{GameModes.Name(c.Mode)} · {route}";
        }

        public static string Challenge(ScoreCardInfo c) => c.Ghost != null && c.BeatGhost ? "SINO ANG SUSUNOD?" : "KAYA MO BA 'TO?";

        /// <summary>Text that goes with the image (Messenger / Facebook caption).</summary>
        public static string ShareText(ScoreCardInfo c, string appId)
        {
            string km = c.Distance >= 1000 ? $"{c.Distance / 1000f:0.0} km" : $"{c.Distance:0} m";
            string what = c.Shift != null ? $"{c.Shift} ({c.Stars}/3 stars)" : c.Ghost != null ? (c.BeatGhost ? $"tinalo ko si {c.Ghost} sa Ghost Race" : $"muntik ko nang talunin si {c.Ghost}") : GameModes.Name(c.Mode).ToLowerInvariant();
            return $"{c.Score:N0} points, {km} sa {Environments.Get(c.Route).Name} – {what} sa Jeepney Rush! {Challenge(c)} https://play.google.com/store/apps/details?id={appId}";
        }
    }
}
