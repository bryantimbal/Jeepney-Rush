using System;
using System.Collections.Generic;

namespace JeepneyRush.Logic
{
    /// <summary>What a cosmetic item changes.</summary>
    public enum CustomSlot { Paint, Windshield, RearSticker, Sign, Horn, Mudflap, Ornament, Lights }

    public enum HornKind { Classic, TootToot, Musical, AirHorn, Cucaracha }
    public enum OrnamentKind { Horses, FiveHorses, GoldHorses, Eagle, Carabao, None }
    public enum LightsKind { None, FogBar, SideMarkers, Underglow }

    /// <summary>
    /// One cosmetic item. Cosmetics never change stats (or a jeepney's stat class): they are
    /// bought once with coins and can be used on every jeepney. A few are earned, not bought
    /// (<see cref="RankNeeded"/>: a career license).
    /// </summary>
    public sealed class CosmeticItem
    {
        public string Id;
        public CustomSlot Slot;
        public string Name;
        /// <summary>Coins; 0 = free / stock.</summary>
        public int Price;
        /// <summary>Earned with this career license (Career.Licenses index), not bought. -1 = bought.</summary>
        public int RankNeeded = -1;

        // ---- what it does
        /// <summary>Paint: body, hood, roof, trim (pillars / visor), roof stripe.</summary>
        public string Body, Hood, Roof, Trim, Stripe;
        /// <summary>Windshield banner / rear sticker / sign / printed mudflap texture.</summary>
        public string Texture;
        public string MudflapColor;
        public HornKind Horn;
        public OrnamentKind Ornament;
        public LightsKind Lights;
        /// <summary>Light colour (fog bar, markers, underglow).</summary>
        public string LightColor;

        public bool IsStock => Price == 0 && RankNeeded < 0;
        public bool Earned => RankNeeded >= 0;
    }

    /// <summary>The setup of one jeepney (item ids per slot; empty = stock).</summary>
    [Serializable]
    public class JeepneyLook
    {
        public string jeepney;
        public string paint = "", windshield = "", rear = "", sign = "", horn = "", mudflap = "", ornament = "", lights = "";

        public string Get(CustomSlot s)
        {
            switch (s)
            {
                case CustomSlot.Paint: return paint;
                case CustomSlot.Windshield: return windshield;
                case CustomSlot.RearSticker: return rear;
                case CustomSlot.Sign: return sign;
                case CustomSlot.Horn: return horn;
                case CustomSlot.Mudflap: return mudflap;
                case CustomSlot.Ornament: return ornament;
                default: return lights;
            }
        }

        public void Set(CustomSlot s, string id)
        {
            id = id ?? "";
            switch (s)
            {
                case CustomSlot.Paint: paint = id; break;
                case CustomSlot.Windshield: windshield = id; break;
                case CustomSlot.RearSticker: rear = id; break;
                case CustomSlot.Sign: sign = id; break;
                case CustomSlot.Horn: horn = id; break;
                case CustomSlot.Mudflap: mudflap = id; break;
                case CustomSlot.Ornament: ornament = id; break;
                default: lights = id; break;
            }
        }

        /// <summary>Key of the model this look builds (the horn doesn't change the model).</summary>
        public string ModelKey => $"{paint}|{windshield}|{rear}|{sign}|{mudflap}|{ornament}|{lights}";
        public bool IsStockModel => ModelKey == "||||||";
    }

    public static class Customization
    {
        public static readonly CustomSlot[] Slots = (CustomSlot[])Enum.GetValues(typeof(CustomSlot));

        public static string SlotName(CustomSlot s)
        {
            switch (s)
            {
                case CustomSlot.Paint: return "PINTURA";
                case CustomSlot.Windshield: return "WINDSHIELD";
                case CustomSlot.RearSticker: return "STICKER";
                case CustomSlot.Sign: return "KARATULA";
                case CustomSlot.Horn: return "BUSINA";
                case CustomSlot.Mudflap: return "MUDFLAP";
                case CustomSlot.Ornament: return "PALAMUTI";
                default: return "ILAW";
            }
        }

        private static CosmeticItem Paint(string id, string name, int price, string body, string hood, string roof, string trim, string stripe, int rank = -1) =>
            new CosmeticItem { Id = id, Slot = CustomSlot.Paint, Name = name, Price = price, RankNeeded = rank, Body = body, Hood = hood, Roof = roof, Trim = trim, Stripe = stripe };
        private static CosmeticItem Tex(string id, CustomSlot slot, string name, int price, string tex, int rank = -1) =>
            new CosmeticItem { Id = id, Slot = slot, Name = name, Price = price, RankNeeded = rank, Texture = tex };

        /// <summary>Everything in the talyer. Stock items (price 0) are what each jeepney comes with.</summary>
        public static readonly CosmeticItem[] All =
        {
            // ---- paint (the painted art panels on the sides keep their motif)
            Paint("paint_stock", "Orihinal (stock)", 0, null, null, null, null, null),
            Paint("paint_pulaputi", "Pula't Puti", 400, "#C8102E", "#F4F4F0", "#F4F4F0", "#C8102E", "#FCD116"),
            Paint("paint_asul", "Asul Dagat", 400, "#1D4E89", "#2A9D8F", "#E9F5F2", "#0B2545", "#FCD116"),
            Paint("paint_berde", "Berde Bukid", 400, "#2D6A4F", "#95D5B2", "#F1FAEE", "#1B4332", "#FFB703"),
            Paint("paint_dilaw", "Dilaw Araw", 450, "#FFB703", "#FB8500", "#FFF3C4", "#6B4226", "#C8102E"),
            Paint("paint_itim", "Itim at Chrome", 600, "#16161A", "#2B2D42", "#D9DEE6", "#0B0B0D", "#E6EBF2"),
            Paint("paint_rosas", "Rosas Pista", 450, "#E5568E", "#FFB3C6", "#FFF0F5", "#7B2D52", "#3BCEAC"),
            Paint("paint_ginto", "Gintong Hari", 0, "#C9A227", "#F4D35E", "#FFF8E1", "#7A5C00", "#C8102E", 4),
            // ---- windshield banner (top of the windshield)
            Tex("ws_none", CustomSlot.Windshield, "Wala (stock)", 0, null),
            Tex("ws_godbless", CustomSlot.Windshield, "GOD BLESS OUR TRIP", 150, "cos_ws_godbless"),
            Tex("ws_sakayna", CustomSlot.Windshield, "SAKAY NA!", 150, "cos_ws_sakayna"),
            Tex("ws_bahalana", CustomSlot.Windshield, "BAHALA NA", 150, "cos_ws_bahalana"),
            Tex("ws_hari", CustomSlot.Windshield, "HARI NG KALSADA", 0, "cos_ws_hari", 4),
            // ---- rear sticker (left rear panel)
            Tex("rear_none", CustomSlot.RearSticker, "Wala (stock)", 0, null),
            Tex("rear_bayad", CustomSlot.RearSticker, "BAYAD MUNA BAGO BABA", 150, "cos_rear_bayad"),
            Tex("rear_howsmy", CustomSlot.RearSticker, "HOW'S MY DRIVING?", 150, "cos_rear_howsmy"),
            Tex("rear_santonino", CustomSlot.RearSticker, "Santo Niño", 200, "cos_rear_santonino"),
            Tex("rear_araw", CustomSlot.RearSticker, "Araw at Tatlong Bituin", 200, "cos_rear_araw"),
            Tex("rear_lisensyado", CustomSlot.RearSticker, "LISENSYADO!", 0, "cos_rear_lisensyado", 1),
            // ---- roof sign board (your own route)
            Tex("sign_stock", CustomSlot.Sign, "Orihinal (stock)", 0, null),
            Tex("sign_quiapo", CustomSlot.Sign, "QUIAPO – CUBAO", 200, "cos_sign_quiapo"),
            Tex("sign_baclaran", CustomSlot.Sign, "BACLARAN – DIVISORIA", 200, "cos_sign_baclaran"),
            Tex("sign_monumento", CustomSlot.Sign, "MONUMENTO – LAWTON", 200, "cos_sign_monumento"),
            Tex("sign_cebu", CustomSlot.Sign, "COLON – IT PARK", 200, "cos_sign_cebu"),
            Tex("sign_baguio", CustomSlot.Sign, "BAGUIO – LA TRINIDAD", 200, "cos_sign_baguio"),
            Tex("sign_especial", CustomSlot.Sign, "SPECIAL TRIP", 250, "cos_sign_especial"),
            // ---- horn
            new CosmeticItem { Id = "horn_classic", Slot = CustomSlot.Horn, Name = "Klasik (stock)", Horn = HornKind.Classic },
            new CosmeticItem { Id = "horn_toot", Slot = CustomSlot.Horn, Name = "Toot-Toot-Toot", Price = 400, Horn = HornKind.TootToot },
            new CosmeticItem { Id = "horn_musical", Slot = CustomSlot.Horn, Name = "Tunog-Pista (musical)", Price = 600, Horn = HornKind.Musical },
            new CosmeticItem { Id = "horn_air", Slot = CustomSlot.Horn, Name = "Air Horn (patok)", Price = 600, Horn = HornKind.AirHorn },
            new CosmeticItem { Id = "horn_cucaracha", Slot = CustomSlot.Horn, Name = "La Cucaracha", Price = 800, Horn = HornKind.Cucaracha },
            // ---- mudflaps
            new CosmeticItem { Id = "mud_stock", Slot = CustomSlot.Mudflap, Name = "Orihinal (stock)" },
            new CosmeticItem { Id = "mud_red", Slot = CustomSlot.Mudflap, Name = "Pula", Price = 150, MudflapColor = "#B3001B" },
            new CosmeticItem { Id = "mud_chrome", Slot = CustomSlot.Mudflap, Name = "Chrome", Price = 250, MudflapColor = "#E6EBF2" },
            new CosmeticItem { Id = "mud_mahal", Slot = CustomSlot.Mudflap, Name = "MAHAL KITA (printed)", Price = 250, MudflapColor = "#141414", Texture = "cos_mud_mahal" },
            // ---- hood ornament (classic jeepneys)
            new CosmeticItem { Id = "orn_stock", Slot = CustomSlot.Ornament, Name = "Tatlong Kabayo (stock)", Ornament = OrnamentKind.Horses },
            new CosmeticItem { Id = "orn_five", Slot = CustomSlot.Ornament, Name = "Limang Kabayo", Price = 600, Ornament = OrnamentKind.FiveHorses },
            new CosmeticItem { Id = "orn_eagle", Slot = CustomSlot.Ornament, Name = "Agila", Price = 800, Ornament = OrnamentKind.Eagle },
            new CosmeticItem { Id = "orn_carabao", Slot = CustomSlot.Ornament, Name = "Kalabaw", Price = 800, Ornament = OrnamentKind.Carabao },
            new CosmeticItem { Id = "orn_none", Slot = CustomSlot.Ornament, Name = "Malinis (walang palamuti)", Price = 100, Ornament = OrnamentKind.None },
            new CosmeticItem { Id = "orn_gold", Slot = CustomSlot.Ornament, Name = "Gintong Kabayo", RankNeeded = 3, Ornament = OrnamentKind.GoldHorses },
            // ---- lights
            new CosmeticItem { Id = "lights_stock", Slot = CustomSlot.Lights, Name = "Orihinal (stock)", Lights = LightsKind.None },
            new CosmeticItem { Id = "lights_fog", Slot = CustomSlot.Lights, Name = "Fog Lights", Price = 700, Lights = LightsKind.FogBar, LightColor = "#FFF4C2" },
            new CosmeticItem { Id = "lights_markers", Slot = CustomSlot.Lights, Name = "Side Marker Lights", Price = 600, Lights = LightsKind.SideMarkers, LightColor = "#FFB000" },
            new CosmeticItem { Id = "lights_neon_blue", Slot = CustomSlot.Lights, Name = "Neon Underglow (asul)", Price = 1200, Lights = LightsKind.Underglow, LightColor = "#2EC4FF" },
            new CosmeticItem { Id = "lights_neon_pink", Slot = CustomSlot.Lights, Name = "Neon Underglow (pink)", Price = 1200, Lights = LightsKind.Underglow, LightColor = "#FF3FA4" },
        };

        public static CosmeticItem Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var c in All) if (c.Id == id) return c;
            return null;
        }

        public static IEnumerable<CosmeticItem> ForSlot(CustomSlot s)
        {
            foreach (var c in All) if (c.Slot == s) yield return c;
        }

        /// <summary>Stock / free items are always yours; earned ones need the license; the rest must be bought.</summary>
        public static bool Owns(SaveData d, CosmeticItem c)
        {
            if (c == null) return false;
            if (c.IsStock) return true;
            if (c.Earned) return d != null && d.careerRank >= c.RankNeeded;
            return d != null && d.ownedCosmetics != null && d.ownedCosmetics.Contains(c.Id);
        }

        /// <summary>Buys an item with coins (offline / local rules; the server applies the same). Returns false if not possible.</summary>
        public static bool Buy(SaveData d, CosmeticItem c)
        {
            if (d == null || c == null || c.IsStock || c.Earned || Owns(d, c) || d.coins < c.Price) return false;
            d.coins -= c.Price;
            d.ownedCosmetics ??= new List<string>();
            d.ownedCosmetics.Add(c.Id);
            return true;
        }

        /// <summary>The setup of a jeepney (a new, all-stock one if it has none yet).</summary>
        public static JeepneyLook LookFor(SaveData d, string jeepney)
        {
            if (d?.looks != null) foreach (var l in d.looks) if (l != null && l.jeepney == jeepney) return l;
            return new JeepneyLook { jeepney = jeepney };
        }

        /// <summary>Equips an owned item on a jeepney (stock ids clear the slot). Returns false if not owned.</summary>
        public static bool Equip(SaveData d, string jeepney, CosmeticItem c)
        {
            if (d == null || c == null || !Owns(d, c)) return false;
            d.looks ??= new List<JeepneyLook>();
            var look = d.looks.Find(l => l != null && l.jeepney == jeepney);
            if (look == null) { look = new JeepneyLook { jeepney = jeepney }; d.looks.Add(look); }
            look.Set(c.Slot, c.IsStock ? "" : c.Id);
            return true;
        }

        /// <summary>The item equipped in a slot (the stock one if none, or if it isn't owned any more).</summary>
        public static CosmeticItem Equipped(SaveData d, JeepneyLook look, CustomSlot s)
        {
            var c = Get(look?.Get(s));
            if (c != null && c.Slot == s && Owns(d, c)) return c;
            foreach (var x in All) if (x.Slot == s && x.IsStock) return x;
            return null;
        }

        /// <summary>The look with only items the player owns (an unowned / unknown id falls back to stock).</summary>
        public static JeepneyLook Effective(SaveData d, string jeepney)
        {
            var look = LookFor(d, jeepney);
            var e = new JeepneyLook { jeepney = jeepney };
            foreach (var s in Slots)
            {
                var c = Equipped(d, look, s);
                e.Set(s, c == null || c.IsStock ? "" : c.Id);
            }
            return e;
        }

        /// <summary>Copy of the livery with the look's paint / sign / mudflap applied (for the model builder).</summary>
        public static JeepneyLivery Apply(JeepneyLivery L, JeepneyLook look)
        {
            var c = L.Clone();
            var paint = Get(look?.paint);
            if (paint != null && paint.Body != null)
            {
                c.Body = paint.Body; c.Hood = paint.Hood; c.Roof = paint.Roof; c.Trim = paint.Trim; c.Accent2 = paint.Stripe;
            }
            var sign = Get(look?.sign);
            if (sign?.Texture != null) c.SignTextureOverride = sign.Texture;
            var mud = Get(look?.mudflap);
            if (mud?.MudflapColor != null) c.Mudflap = mud.MudflapColor;
            c.MudflapTexture = mud?.Texture;
            c.WindshieldTexture = Get(look?.windshield)?.Texture;
            c.RearStickerTexture = Get(look?.rear)?.Texture;
            var orn = Get(look?.ornament);
            if (orn != null) c.Ornament = orn.Ornament;
            var lights = Get(look?.lights);
            if (lights != null) { c.ExtraLights = lights.Lights; if (lights.LightColor != null) c.ExtraLightColor = lights.LightColor; }
            return c;
        }

        public static Audio.Sfx HornSound(HornKind k) =>
            k == HornKind.TootToot ? Audio.Sfx.HornToot : k == HornKind.Musical ? Audio.Sfx.HornMusical : k == HornKind.AirHorn ? Audio.Sfx.HornAir : k == HornKind.Cucaracha ? Audio.Sfx.HornCucaracha : Audio.Sfx.Horn;

        public static HornKind HornOf(SaveData d, string jeepney)
        {
            var c = Equipped(d, LookFor(d, jeepney), CustomSlot.Horn);
            return c != null ? c.Horn : HornKind.Classic;
        }
    }
}
