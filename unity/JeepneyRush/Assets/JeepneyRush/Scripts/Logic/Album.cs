using System;
using System.Collections.Generic;

namespace JeepneyRush.Logic
{
    /// <summary>
    /// Sticker album: landmark postcards, <see cref="PerRoute"/> per route. At most one postcard appears on
    /// the road per run (after <see cref="MinDistance"/> m, <see cref="Chance"/> of runs) and shows a landmark
    /// of the route you're driving; a duplicate pays <see cref="DuplicateCoins"/>. A full route set pays
    /// <see cref="SetCoins"/> + <see cref="SetGems"/>, the whole album <see cref="AlbumGems"/> gems.
    /// The server (jr_album_*) checks the card belongs to the run's route.
    /// </summary>
    public static class Album
    {
        public const int PerRoute = 4, DuplicateCoins = 20, SetCoins = 300, SetGems = 10, AlbumGems = 100;
        public const float MinDistance = 800f, Chance = 0.5f;

        private static readonly string[][] Names =
        {
            new[] { "Quiapo Church", "Intramuros", "Rizal Park", "Binondo Arch" },             // Manila
            new[] { "Palayan", "Bahay Kubo", "Simbahan ng Bayan", "Kalabaw sa Bukid" },         // Provincial
            new[] { "White Beach", "Parola", "Bangka", "Paglubog ng Araw" },                    // Beach
            new[] { "Rice Terraces", "Echo Valley", "Sumaguing Cave", "Pine Forest" },         // Mountain Province
            new[] { "MRT sa EDSA", "EDSA Shrine", "Guadalupe Bridge", "Ortigas Skyline" },     // EDSA
            new[] { "Magellan's Cross", "Basilica del Santo Niño", "Fort San Pedro", "IT Park" }, // Cebu
            new[] { "Burnham Park", "Session Road", "Mines View", "Strawberry Farm" },          // Baguio
            new[] { "Banderitas", "Parade Float", "Peryahan", "Banda sa Plaza" },               // Fiesta Town
        };

        public static int Routes => Names.Length;
        public static int Count => Routes * PerRoute;
        public static int RouteOf(int card) => card / PerRoute;
        public static string Name(int card) => card >= 0 && card < Count ? Names[card / PerRoute][card % PerRoute] : "";
        public static int Card(EnvironmentKind env, int i) => (int)env * PerRoute + i;

        /// <summary>The postcard a pickup gives: one you don't have yet if possible.</summary>
        public static int Pick(EnvironmentKind env, Func<int, bool> owned, GameRandom rng)
        {
            var missing = new List<int>();
            for (int i = 0; i < PerRoute; i++) if (!owned(Card(env, i))) missing.Add(Card(env, i));
            if (missing.Count > 0) return missing[rng.Int(0, missing.Count - 1)];
            return Card(env, rng.Int(0, PerRoute - 1));
        }

        /// <summary>Is the card valid for a run on this route of this length?</summary>
        public static bool Valid(int card, int env, float distance) => card >= 0 && card < Count && RouteOf(card) == env && distance >= MinDistance;

        public static bool SetComplete(Func<int, bool> owned, int route)
        {
            for (int i = 0; i < PerRoute; i++) if (!owned(route * PerRoute + i)) return false;
            return true;
        }
    }
}
