using System;
using System.Collections.Generic;
using System.Globalization;

namespace JeepneyRush.Logic.Geometry
{
    /// <summary>
    /// Colour atlas: every flat colour used by procedural meshes is a cell in one small
    /// texture, so all vertex-coloured geometry shares ONE material (one SRP batch).
    /// </summary>
    public sealed class Palette
    {
        public const int Size = 16; // 16x16 = 256 colours
        private readonly Dictionary<uint, int> _index = new Dictionary<uint, int>();
        public readonly List<(byte r, byte g, byte b)> Colors = new List<(byte, byte, byte)>();
        public bool Dirty { get; set; }

        public static readonly Palette Shared = new Palette();

        public static (byte r, byte g, byte b) Parse(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return (255, 0, 255);
            var h = hex.TrimStart('#');
            if (h.Length == 3) h = $"{h[0]}{h[0]}{h[1]}{h[1]}{h[2]}{h[2]}";
            if (h.Length != 6 || !uint.TryParse(h, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v)) return (255, 0, 255);
            return ((byte)(v >> 16), (byte)(v >> 8), (byte)v);
        }

        public int IndexOf(string hex)
        {
            var (r, g, b) = Parse(hex);
            uint key = ((uint)r << 16) | ((uint)g << 8) | b;
            if (_index.TryGetValue(key, out int i)) return i;
            if (Colors.Count >= Size * Size) return 0; // full: fall back to first colour
            i = Colors.Count;
            Colors.Add((r, g, b));
            _index[key] = i;
            Dirty = true;
            return i;
        }

        /// <summary>UV at the centre of the colour's cell (v=0 is the bottom row, Unity convention).</summary>
        public V2 Uv(string hex)
        {
            int i = IndexOf(hex);
            return new V2(((i % Size) + 0.5f) / Size, ((i / Size) + 0.5f) / Size);
        }

        /// <summary>Hex colour multiplied by k (k&lt;1 darkens, k&gt;1 lightens toward white).</summary>
        public static string Shade(string hex, float k)
        {
            var (r, g, b) = Parse(hex);
            byte S(byte c) => (byte)Math.Max(0, Math.Min(255, k <= 1 ? c * k : c + (255 - c) * (k - 1)));
            return $"#{S(r):X2}{S(g):X2}{S(b):X2}";
        }
    }
}
