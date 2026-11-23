using System;
using System.Collections.Generic;

namespace JeepneyRush.Logic
{
    /// <summary>Small seeded PRNG (mulberry32) so runs and tests are reproducible.</summary>
    public sealed class GameRandom
    {
        private uint _s;

        public GameRandom(int seed) { _s = unchecked((uint)seed); }

        /// <summary>An independent stream derived from this one without advancing it (new features keep old sequences).</summary>
        public GameRandom Fork(uint salt) => new GameRandom(unchecked((int)(_s ^ salt * 0x9E3779B9u)));

        public double Next()
        {
            unchecked
            {
                _s += 0x6D2B79F5;
                uint t = _s;
                t = (uint)((t ^ (t >> 15)) * (t | 1));
                t ^= t + (uint)((t ^ (t >> 7)) * (t | 61));
                return ((t ^ (t >> 14)) & 0xFFFFFFFFu) / 4294967296.0;
            }
        }

        public float Range(float min, float max) => (float)(min + (max - min) * Next());

        /// <summary>Inclusive integer range.</summary>
        public int Int(int min, int maxInclusive) => Math.Min(maxInclusive, min + (int)Math.Floor(Next() * (maxInclusive - min + 1)));

        public bool Chance(double p) => Next() < p;

        public T Pick<T>(IReadOnlyList<T> items) => items[Math.Min(items.Count - 1, (int)(Next() * items.Count))];

        public T Weighted<T>(IReadOnlyList<(T item, float weight)> items)
        {
            float total = 0;
            foreach (var i in items) total += Math.Max(0, i.weight);
            double r = Next() * total;
            foreach (var i in items)
            {
                r -= Math.Max(0, i.weight);
                if (r <= 0) return i.item;
            }
            return items[items.Count - 1].item;
        }
    }
}
