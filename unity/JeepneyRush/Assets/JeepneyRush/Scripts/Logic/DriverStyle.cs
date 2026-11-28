using System.Collections.Generic;

namespace JeepneyRush.Logic
{
    public enum DriverPart { Shirt, Hat, Extra, Konduktor }

    /// <summary>A driver look item (bought once with coins, used on every jeepney) or the konduktor.</summary>
    public sealed class DriverItem
    {
        public string Id, Name, Color;
        public DriverPart Part;
        public int Price;
        public bool IsStock => Price == 0;
    }

    /// <summary>
    /// The driver's look (shirt, headwear, towel / shades) and the konduktor sidekick. Items are sold like
    /// talyer cosmetics (same server purchase, jr_cosmetics); what is worn is kept on the phone.
    /// The konduktor rides on the rear step: fare dodgers can't escape and fares are +<see cref="KonduktorFare"/>.
    /// </summary>
    public static class DriverStyle
    {
        public const string KonduktorId = "konduktor";
        public const float KonduktorFare = 0.05f;

        public static readonly DriverItem[] All =
        {
            new DriverItem { Id = "drv_sando", Part = DriverPart.Shirt, Name = "Puting Sando (stock)", Color = "#F2F2F2" },
            new DriverItem { Id = "drv_polo", Part = DriverPart.Shirt, Name = "Polo Shirt", Price = 300, Color = "#1D4E89" },
            new DriverItem { Id = "drv_jacket", Part = DriverPart.Shirt, Name = "Jacket (pang-gabi)", Price = 500, Color = "#2B2D42" },
            new DriverItem { Id = "drv_jersey", Part = DriverPart.Shirt, Name = "Basketball Jersey", Price = 600, Color = "#C8102E" },
            new DriverItem { Id = "drv_barong", Part = DriverPart.Shirt, Name = "Barong Tagalog", Price = 900, Color = "#F3E9D2" },
            new DriverItem { Id = "drv_nohat", Part = DriverPart.Hat, Name = "Walang sombrero (stock)" },
            new DriverItem { Id = "drv_cap", Part = DriverPart.Hat, Name = "Cap", Price = 200, Color = "#E63946" },
            new DriverItem { Id = "drv_capback", Part = DriverPart.Hat, Name = "Cap (pabaliktad)", Price = 250, Color = "#1F3FBF" },
            new DriverItem { Id = "drv_salakot", Part = DriverPart.Hat, Name = "Salakot", Price = 600, Color = "#C8A165" },
            new DriverItem { Id = "drv_none", Part = DriverPart.Extra, Name = "Wala (stock)" },
            new DriverItem { Id = "drv_towel", Part = DriverPart.Extra, Name = "Bimpo sa balikat", Price = 150, Color = "#FFFFFF" },
            new DriverItem { Id = "drv_shades", Part = DriverPart.Extra, Name = "Shades", Price = 300, Color = "#111111" },
            new DriverItem { Id = "drv_towelshades", Part = DriverPart.Extra, Name = "Bimpo + Shades", Price = 400, Color = "#FFFFFF" },
            new DriverItem { Id = KonduktorId, Part = DriverPart.Konduktor, Name = "Konduktor (sidekick)", Price = 1500, Color = "#FCD116" },
        };

        public static DriverItem Get(string id)
        {
            foreach (var i in All) if (i.Id == id) return i;
            return null;
        }

        public static IEnumerable<DriverItem> ForPart(DriverPart p) { foreach (var i in All) if (i.Part == p) yield return i; }

        public static DriverItem Stock(DriverPart p) { foreach (var i in All) if (i.Part == p && i.IsStock) return i; return null; }

        public static bool Owns(SaveData d, DriverItem i) => i != null && (i.IsStock || (d?.ownedCosmetics != null && d.ownedCosmetics.Contains(i.Id)));

        public static bool HasKonduktor(SaveData d) => Owns(d, Get(KonduktorId)) && (d == null || !d.konduktorOff);

        /// <summary>Worn item for a part (falls back to stock if not owned).</summary>
        public static DriverItem Worn(SaveData d, DriverPart p)
        {
            string id = p == DriverPart.Shirt ? d?.driverShirt : p == DriverPart.Hat ? d?.driverHat : d?.driverExtra;
            var i = Get(id);
            return i != null && i.Part == p && Owns(d, i) ? i : Stock(p);
        }

        /// <summary>Model key for the worn look (driver mesh cache).</summary>
        public static string LookKey(SaveData d) => $"driver_{Worn(d, DriverPart.Shirt).Id}_{Worn(d, DriverPart.Hat).Id}_{Worn(d, DriverPart.Extra).Id}";
    }
}
