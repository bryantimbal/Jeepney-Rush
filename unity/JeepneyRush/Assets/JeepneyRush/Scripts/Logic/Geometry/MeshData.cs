using System;
using System.Collections.Generic;

namespace JeepneyRush.Logic.Geometry
{
    /// <summary>Minimal engine-free vector so mesh generation is unit-testable and previewable outside Unity.</summary>
    public struct V3
    {
        public float X, Y, Z;
        public V3(float x, float y, float z) { X = x; Y = y; Z = z; }
        public static V3 operator +(V3 a, V3 b) => new V3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static V3 operator -(V3 a, V3 b) => new V3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static V3 operator *(V3 a, float k) => new V3(a.X * k, a.Y * k, a.Z * k);
        public static V3 operator -(V3 a) => new V3(-a.X, -a.Y, -a.Z);
        public static V3 Cross(V3 a, V3 b) => new V3(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
        public static float Dot(V3 a, V3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        public float Length => (float)Math.Sqrt(X * X + Y * Y + Z * Z);
        public V3 Normalized { get { float l = Length; return l > 1e-8f ? this * (1f / l) : new V3(0, 1, 0); } }
        public static readonly V3 Zero = new V3(0, 0, 0);
        public static readonly V3 Up = new V3(0, 1, 0);
        public override string ToString() => $"({X:0.###},{Y:0.###},{Z:0.###})";
    }

    public struct V2
    {
        public float X, Y;
        public V2(float x, float y) { X = x; Y = y; }
    }

    /// <summary>Rotation as Euler angles (radians), applied Z, then X, then Y (Unity order).</summary>
    public struct Rot
    {
        public float X, Y, Z;
        public Rot(float x, float y, float z) { X = x; Y = y; Z = z; }
        public static readonly Rot None = new Rot(0, 0, 0);
        public bool IsNone => X == 0 && Y == 0 && Z == 0;

        public V3 Apply(V3 v)
        {
            if (IsNone) return v;
            float c, s;
            // Z
            c = (float)Math.Cos(Z); s = (float)Math.Sin(Z);
            v = new V3(v.X * c - v.Y * s, v.X * s + v.Y * c, v.Z);
            // X
            c = (float)Math.Cos(X); s = (float)Math.Sin(X);
            v = new V3(v.X, v.Y * c - v.Z * s, v.Y * s + v.Z * c);
            // Y
            c = (float)Math.Cos(Y); s = (float)Math.Sin(Y);
            v = new V3(v.X * c + v.Z * s, v.Y, -v.X * s + v.Z * c);
            return v;
        }
    }

    /// <summary>
    /// Vertex/index buffers grouped into submeshes by material key
    /// ("paint", "chrome", "glass", "glow", or "tex:&lt;textureName&gt;").
    /// Triangles follow Unity's convention: Cross(b-a, c-a) points out of the front face.
    /// </summary>
    public sealed class MeshData
    {
        public readonly List<V3> Positions = new List<V3>();
        public readonly List<V3> Normals = new List<V3>();
        public readonly List<V2> Uvs = new List<V2>();
        public readonly List<string> SubmeshKeys = new List<string>();
        public readonly List<List<int>> SubmeshIndices = new List<List<int>>();

        public List<int> Submesh(string key)
        {
            int i = SubmeshKeys.IndexOf(key);
            if (i >= 0) return SubmeshIndices[i];
            SubmeshKeys.Add(key);
            var l = new List<int>();
            SubmeshIndices.Add(l);
            return l;
        }

        public int TriangleCount
        {
            get { int n = 0; foreach (var s in SubmeshIndices) n += s.Count / 3; return n; }
        }

        public (V3 min, V3 max) Bounds()
        {
            var min = new V3(float.MaxValue, float.MaxValue, float.MaxValue);
            var max = new V3(float.MinValue, float.MinValue, float.MinValue);
            foreach (var p in Positions)
            {
                min = new V3(Math.Min(min.X, p.X), Math.Min(min.Y, p.Y), Math.Min(min.Z, p.Z));
                max = new V3(Math.Max(max.X, p.X), Math.Max(max.Y, p.Y), Math.Max(max.Z, p.Z));
            }
            return (min, max);
        }
    }
}

namespace JeepneyRush.Logic.Geometry
{
    public static class MeshOps
    {
        /// <summary>Appends <paramref name="src"/> translated by <paramref name="offset"/> into <paramref name="dst"/> (matching submesh keys).</summary>
        public static void Append(MeshData dst, MeshData src, V3 offset)
        {
            int baseIndex = dst.Positions.Count;
            foreach (var p in src.Positions) dst.Positions.Add(p + offset);
            dst.Normals.AddRange(src.Normals);
            dst.Uvs.AddRange(src.Uvs);
            for (int s = 0; s < src.SubmeshKeys.Count; s++)
            {
                var idx = dst.Submesh(src.SubmeshKeys[s]);
                foreach (int i in src.SubmeshIndices[s]) idx.Add(i + baseIndex);
            }
        }
    }
}
