using System;
using System.Collections.Generic;
using JeepneyRush.Logic;

namespace JeepneyRush
{
    /// <summary>Sticker album for the game and UI: the server when online (cached), else the save file.</summary>
    public static class AlbumProgress
    {
        private static bool Online => Services.Online != null && Services.Online.Configured;
        private static readonly HashSet<int> Cached = new HashSet<int>();
        private static readonly HashSet<int> CachedSets = new HashSet<int>();

        public static void Invalidate() { }

        public static bool Owns(int card) => Online ? Cached.Contains(card) : Services.Save.Data.albumCards.Contains(card);

        public static void AddLocal(int card)
        {
            var d = Services.Save.Data;
            if (d.albumCards.Contains(card)) d.coins += Album.DuplicateCoins; else d.albumCards.Add(card);
            Services.Save.Save();
        }

        public static void Status(Action<AlbumInfo, string> done)
        {
            if (Online)
            {
                if (!Services.Online.SignedIn) { done(null, "Sign in under ACCOUNT first."); return; }
                Services.Online.AlbumStatus((i, e) =>
                {
                    if (i != null) { Cached.Clear(); foreach (var c in i.cards) Cached.Add(c); CachedSets.Clear(); foreach (var s in i.sets) CachedSets.Add(s); }
                    done(i, e);
                });
                return;
            }
            var d = Services.Save.Data;
            done(new AlbumInfo { cards = d.albumCards.ToArray(), sets = d.albumSets.ToArray() }, null);
        }

        /// <param name="set">route 0..7, or 8 = the whole album</param>
        public static void ClaimSet(int set, Action<bool, string, int> done)
        {
            if (Online) { Services.Online.AlbumClaimSet(set, done); return; }
            var d = Services.Save.Data;
            if (d.albumSets.Contains(set)) { done(false, "Na-claim na.", 0); return; }
            bool full = set == Album.Routes ? d.albumCards.Count >= Album.Count : Album.SetComplete(d.albumCards.Contains, set);
            if (!full) { done(false, "Hindi pa kumpleto.", 0); return; }
            d.albumSets.Add(set);
            if (set == Album.Routes) d.gems += Album.AlbumGems; else { d.coins += Album.SetCoins; d.gems += Album.SetGems; }
            Services.Save.Save();
            done(true, null, 0);
        }
    }
}
