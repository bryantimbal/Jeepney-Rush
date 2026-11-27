using System;

namespace JeepneyRush.Logic
{
    /// <summary>Result of create / join / leave.</summary>
    [Serializable]
    public class BarangayOp
    {
        public bool ok;
        public string reason;
        public string code;
        public string name;
        public string until;
    }

    [Serializable]
    public class BarangayMember
    {
        public string display_name;
        public int level;
        public long score;
        public bool is_me;
    }

    /// <summary>My barangay (found = false when I'm not in one).</summary>
    [Serializable]
    public class BarangayMine
    {
        public bool found;
        public string name;
        public string city;
        public string code;
        public long score;
        public int rank;
        /// <summary>When I may switch barangay again (empty = now).</summary>
        public string can_switch_at;
        public BarangayMember[] members = new BarangayMember[0];
    }

    [Serializable]
    public class BarangayRow
    {
        public int rank;
        public string name;
        public string city;
        public long score;
        public int members;
        public bool is_mine;
    }

    [Serializable]
    public class BarangayChampion
    {
        public string name;
        public string city;
        public long score;
    }

    [Serializable]
    public class BarangayBoard
    {
        public BarangayRow[] rows = new BarangayRow[0];
        public BarangayRow me;
        public BarangayChampion champion;
    }

    /// <summary>Barangay vs Barangay rules shared by the UI (the server enforces them).</summary>
    public static class Barangays
    {
        public const int TopMembers = 10, MaxMembers = 200, NameMax = 32;
        public const int SwitchCooldownDays = 3;

        public static string ReasonText(string reason)
        {
            switch (reason)
            {
                case "invalid_name": return "Barangay name: 3–32 letters, numbers, spaces (walang mura).";
                case "invalid_city": return "City / municipality: 3–32 letters.";
                case "cooldown": return $"Puwede ka lang lumipat ng barangay isang beses kada {SwitchCooldownDays} araw.";
                case "unknown_code": return "Walang barangay na may ganyang code.";
                case "full": return $"Puno na ang barangay na 'yan ({MaxMembers} members).";
                case "limit": return "Masyadong maraming barangay na ang ginawa mo ngayon. Bukas ulit!";
                case null: return null;
                default: return "Hindi nagawa: " + reason;
            }
        }

        /// <summary>Normalises a code the player typed ("brgy-ab12" → "BRGYAB12"), null if it can't be one.</summary>
        public static string NormalizeCode(string code)
        {
            if (string.IsNullOrEmpty(code)) return null;
            var sb = new System.Text.StringBuilder();
            foreach (char ch in code) if (char.IsLetterOrDigit(ch)) sb.Append(char.ToUpperInvariant(ch));
            string c = sb.ToString();
            if (c.Length == 4) c = "BRGY" + c; // just the four characters after BRGY
            return c.Length == 8 && c.StartsWith("BRGY") ? c : null;
        }

        /// <summary>Text to invite neighbours (Messenger / Facebook).</summary>
        public static string InviteText(string name, string code, string appId) =>
            $"Sali ka sa barangay namin sa Jeepney Rush: {name}! Code: {code}. Talunin natin ang ibang barangay ngayong linggo! https://play.google.com/store/apps/details?id={appId}";
    }
}
