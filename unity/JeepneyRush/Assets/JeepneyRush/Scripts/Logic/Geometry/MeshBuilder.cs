using System;
using System.Collections.Generic;

namespace JeepneyRush.Logic.Geometry
{
    /// <summary>
    /// Procedural low-poly modelling kit: boxes, cylinders, lathes, extrusions and
    /// textured quads, with a transform stack for sub-assemblies. Flat colours come
    /// from the shared <see cref="Palette"/>; "tex:" submeshes carry real UVs.
    /// </summary>
    public sealed class MeshBuilder
    {
        private struct Frame { public V3 Origin; public Rot Rot; public float Scale; }

        public readonly MeshData Data = new MeshData();
        private readonly Palette _palette;
        private readonly Stack<Frame> _frames = new Stack<Frame>();

        public MeshBuilder(Palette palette = null) { _palette = palette ?? Palette.Shared; }

        // ---------------------------------------------------------------- transforms
        public void Push(V3 origin, Rot rot, float scale = 1f) => _frames.Push(new Frame { Origin = origin, Rot = rot, Scale = scale });
        public void Pop() => _frames.Pop();

        private V3 TP(V3 p)
        {
            foreach (var f in _frames) p = f.Origin + f.Rot.Apply(p * f.Scale);
            return p;
        }

        private V3 TN(V3 n)
        {
            foreach (var f in _frames) n = f.Rot.Apply(n);
            return n.Normalized;
        }

        // ------------------------------------------------------------------ primitives
        private int Vert(V3 p, V3 n, V2 uv)
        {
            Data.Positions.Add(p);
            Data.Normals.Add(n);
            Data.Uvs.Add(uv);
            return Data.Positions.Count - 1;
        }

        /// <summary>Adds a triangle (already in world space), fixing winding so it faces <paramref name="outward"/>.</summary>
        private void Tri(List<int> idx, int a, int b, int c, V3 outward)
        {
            var pa = Data.Positions[a];
            var n = V3.Cross(Data.Positions[b] - pa, Data.Positions[c] - pa);
            if (V3.Dot(n, outward) < 0) { idx.Add(a); idx.Add(c); idx.Add(b); }
            else { idx.Add(a); idx.Add(b); idx.Add(c); }
        }

        /// <summary>Flat quad from four local-space corners given in perimeter order.</summary>
        public void Quad(string key, V3 a, V3 b, V3 c, V3 d, V3 normal, V2 uvA, V2 uvB, V2 uvC, V2 uvD)
        {
            var idx = Data.Submesh(key);
            var n = TN(normal);
            int ia = Vert(TP(a), n, uvA), ib = Vert(TP(b), n, uvB), ic = Vert(TP(c), n, uvC), id = Vert(TP(d), n, uvD);
            Tri(idx, ia, ib, ic, n);
            Tri(idx, ia, ic, id, n);
        }

        public void QuadColor(string key, string color, V3 a, V3 b, V3 c, V3 d, V3 normal)
        {
            var uv = _palette.Uv(color);
            Quad(key, a, b, c, d, normal, uv, uv, uv, uv);
        }

        /// <summary>Axis-aligned (in the local frame, then rotated) box.</summary>
        public void Box(string key, string color, float w, float h, float d, float x, float y, float z, Rot rot = default)
        {
            Push(new V3(x, y, z), rot);
            float hx = w / 2, hy = h / 2, hz = d / 2;
            V3 P(float sx, float sy, float sz) => new V3(sx * hx, sy * hy, sz * hz);
            QuadColor(key, color, P(1, -1, -1), P(1, 1, -1), P(1, 1, 1), P(1, -1, 1), new V3(1, 0, 0));
            QuadColor(key, color, P(-1, -1, 1), P(-1, 1, 1), P(-1, 1, -1), P(-1, -1, -1), new V3(-1, 0, 0));
            QuadColor(key, color, P(-1, 1, -1), P(-1, 1, 1), P(1, 1, 1), P(1, 1, -1), new V3(0, 1, 0));
            QuadColor(key, color, P(-1, -1, 1), P(-1, -1, -1), P(1, -1, -1), P(1, -1, 1), new V3(0, -1, 0));
            QuadColor(key, color, P(-1, -1, 1), P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1), new V3(0, 0, 1));
            QuadColor(key, color, P(1, -1, -1), P(-1, -1, -1), P(-1, 1, -1), P(1, 1, -1), new V3(0, 0, -1));
            Pop();
        }

        /// <summary>
        /// Surface of revolution around the local Y axis. Profile points are (radius, y)
        /// from bottom to top. Smooth normals for round things (tyres, lamps, hubcaps).
        /// </summary>
        public void Lathe(string key, string color, IList<V2> profile, int segments, float x, float y, float z, Rot rot = default, bool smooth = true, bool capBottom = false, bool capTop = false)
        {
            var idx = Data.Submesh(key);
            var uv = _palette.Uv(color);
            Push(new V3(x, y, z), rot);
            int rings = profile.Count;
            for (int s = 0; s < segments; s++)
            {
                float a0 = (float)(s * 2 * Math.PI / segments), a1 = (float)((s + 1) * 2 * Math.PI / segments);
                for (int r = 0; r < rings - 1; r++)
                {
                    var p0 = profile[r];
                    var p1 = profile[r + 1];
                    V3 At(V2 p, float a) => new V3((float)Math.Cos(a) * p.X, p.Y, (float)Math.Sin(a) * p.X);
                    var c00 = At(p0, a0); var c01 = At(p0, a1); var c10 = At(p1, a0); var c11 = At(p1, a1);
                    // profile tangent -> outward normal in the radial plane
                    float dy = p1.Y - p0.Y, dr = p1.X - p0.X;
                    V3 N(float a) => new V3((float)Math.Cos(a) * dy, -dr, (float)Math.Sin(a) * dy).Normalized;
                    float am = (a0 + a1) / 2;
                    if (smooth)
                    {
                        int i0 = Vert(TP(c00), TN(N(a0)), uv), i1 = Vert(TP(c01), TN(N(a1)), uv);
                        int i2 = Vert(TP(c11), TN(N(a1)), uv), i3 = Vert(TP(c10), TN(N(a0)), uv);
                        var o = TN(N(am));
                        if (p0.X > 1e-5f) Tri(idx, i0, i1, i2, o);
                        Tri(idx, i0, i2, i3, o);
                    }
                    else
                    {
                        var n = TN(N(am));
                        int i0 = Vert(TP(c00), n, uv), i1 = Vert(TP(c01), n, uv), i2 = Vert(TP(c11), n, uv), i3 = Vert(TP(c10), n, uv);
                        if (p0.X > 1e-5f) Tri(idx, i0, i1, i2, n);
                        Tri(idx, i0, i2, i3, n);
                    }
                }
            }
            if (capBottom) Cap(idx, uv, profile[0], segments, -1);
            if (capTop) Cap(idx, uv, profile[rings - 1], segments, 1);
            Pop();
        }

        private void Cap(List<int> idx, V2 uv, V2 ring, int segments, int dir)
        {
            var n = TN(new V3(0, dir, 0));
            int c = Vert(TP(new V3(0, ring.Y, 0)), n, uv);
            for (int s = 0; s < segments; s++)
            {
                float a0 = (float)(s * 2 * Math.PI / segments), a1 = (float)((s + 1) * 2 * Math.PI / segments);
                int i0 = Vert(TP(new V3((float)Math.Cos(a0) * ring.X, ring.Y, (float)Math.Sin(a0) * ring.X)), n, uv);
                int i1 = Vert(TP(new V3((float)Math.Cos(a1) * ring.X, ring.Y, (float)Math.Sin(a1) * ring.X)), n, uv);
                Tri(idx, c, i0, i1, n);
            }
        }

        /// <summary>Capped cylinder along the local Y axis.</summary>
        public void Cylinder(string key, string color, float rTop, float rBottom, float height, int segments, float x, float y, float z, Rot rot = default, bool smooth = false)
        {
            var prof = new[] { new V2(rBottom, -height / 2), new V2(rTop, height / 2) };
            Lathe(key, color, prof, segments, x, y, z, rot, smooth, capBottom: rBottom > 0, capTop: rTop > 0);
        }

        /// <summary>Cylinder lying along the X axis (wheels, axles, horizontal bars).</summary>
        public void CylinderX(string key, string color, float r, float length, int segments, float x, float y, float z, bool smooth = false) =>
            Cylinder(key, color, r, r, length, segments, x, y, z, new Rot(0, 0, (float)(Math.PI / 2)), smooth);

        /// <summary>Cylinder lying along the Z axis.</summary>
        public void CylinderZ(string key, string color, float r, float length, int segments, float x, float y, float z, bool smooth = false) =>
            Cylinder(key, color, r, r, length, segments, x, y, z, new Rot((float)(Math.PI / 2), 0, 0), smooth);

        /// <summary>Low-poly UV sphere.</summary>
        public void Sphere(string key, string color, float radius, float x, float y, float z, int segments = 8, float sx = 1, float sy = 1, float sz = 1)
        {
            var prof = new List<V2>();
            int rings = Math.Max(3, segments / 2);
            for (int i = 0; i <= rings; i++)
            {
                double a = -Math.PI / 2 + Math.PI * i / rings;
                prof.Add(new V2((float)Math.Cos(a) * radius, (float)Math.Sin(a) * radius));
            }
            Push(new V3(x, y, z), Rot.None);
            Push(V3.Zero, Rot.None, 1f);
            // non-uniform scale: bake into profile by stretching after generation
            int start = Data.Positions.Count;
            Lathe(key, color, prof, segments, 0, 0, 0, Rot.None, true);
            Pop();
            Pop();
            if (sx != 1 || sy != 1 || sz != 1)
            {
                var c = TP(new V3(x, y, z));
                for (int i = start; i < Data.Positions.Count; i++)
                {
                    var p = Data.Positions[i] - c;
                    Data.Positions[i] = c + new V3(p.X * sx, p.Y * sy, p.Z * sz);
                }
            }
        }

        /// <summary>
        /// Extrudes a 2D side profile (points are (z, y) in the local frame) between
        /// x0 and x1. Ideal for fenders, hoods and visors seen from the side.
        /// </summary>
        public void ExtrudeX(string key, string color, IList<V2> profile, float x0, float x1)
        {
            var idx = Data.Submesh(key);
            var uv = _palette.Uv(color);
            int n = profile.Count;
            // signed area to know the polygon orientation
            float area = 0;
            for (int i = 0; i < n; i++) { var p = profile[i]; var q = profile[(i + 1) % n]; area += p.X * q.Y - q.X * p.Y; }
            float orient = area >= 0 ? 1 : -1;
            // sides
            for (int i = 0; i < n; i++)
            {
                var p = profile[i];
                var q = profile[(i + 1) % n];
                // outward normal of edge p->q in (z,y) plane for a CCW polygon is (dy, -dz)
                float dz = q.X - p.X, dy = q.Y - p.Y;
                var nrm = new V3(0, -dz * orient, dy * orient).Normalized;
                QuadColor(key, color, new V3(x0, p.Y, p.X), new V3(x1, p.Y, p.X), new V3(x1, q.Y, q.X), new V3(x0, q.Y, q.X), nrm);
            }
            // caps
            var tris = Triangulate(profile);
            foreach (var (capX, dir) in new[] { (x0, -1f), (x1, 1f) })
            {
                var nn = TN(new V3(dir, 0, 0));
                for (int t = 0; t < tris.Count; t += 3)
                {
                    int a = Vert(TP(new V3(capX, profile[tris[t]].Y, profile[tris[t]].X)), nn, uv);
                    int b = Vert(TP(new V3(capX, profile[tris[t + 1]].Y, profile[tris[t + 1]].X)), nn, uv);
                    int c = Vert(TP(new V3(capX, profile[tris[t + 2]].Y, profile[tris[t + 2]].X)), nn, uv);
                    Tri(idx, a, b, c, nn);
                }
            }
        }

        /// <summary>Ear-clipping triangulation of a simple polygon. Returns index triples.</summary>
        public static List<int> Triangulate(IList<V2> poly)
        {
            var result = new List<int>();
            int n = poly.Count;
            if (n < 3) return result;
            var v = new List<int>();
            float area = 0;
            for (int i = 0; i < n; i++) { var p = poly[i]; var q = poly[(i + 1) % n]; area += p.X * q.Y - q.X * p.Y; }
            for (int i = 0; i < n; i++) v.Add(area > 0 ? i : n - 1 - i); // make CCW
            int guard = 0;
            while (v.Count > 3 && guard++ < 10000)
            {
                bool clipped = false;
                for (int i = 0; i < v.Count; i++)
                {
                    int a = v[(i + v.Count - 1) % v.Count], b = v[i], c = v[(i + 1) % v.Count];
                    var A = poly[a]; var B = poly[b]; var C = poly[c];
                    float cross = (B.X - A.X) * (C.Y - A.Y) - (B.Y - A.Y) * (C.X - A.X);
                    if (cross <= 1e-9f) continue; // reflex
                    bool inside = false;
                    foreach (int o in v)
                    {
                        if (o == a || o == b || o == c) continue;
                        if (PointInTri(poly[o], A, B, C)) { inside = true; break; }
                    }
                    if (inside) continue;
                    result.Add(a); result.Add(b); result.Add(c);
                    v.RemoveAt(i);
                    clipped = true;
                    break;
                }
                if (!clipped) break; // degenerate polygon: give up gracefully
            }
            if (v.Count == 3) { result.Add(v[0]); result.Add(v[1]); result.Add(v[2]); }
            return result;
        }

        private static bool PointInTri(V2 p, V2 a, V2 b, V2 c)
        {
            float d1 = (p.X - b.X) * (a.Y - b.Y) - (a.X - b.X) * (p.Y - b.Y);
            float d2 = (p.X - c.X) * (b.Y - c.Y) - (b.X - c.X) * (p.Y - c.Y);
            float d3 = (p.X - a.X) * (c.Y - a.Y) - (c.X - a.X) * (p.Y - a.Y);
            bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(neg && pos);
        }

        /// <summary>Arc of points (for fender profiles): centre (cz, cy), radius, angles in degrees.</summary>
        public static List<V2> Arc(float cz, float cy, float r, float fromDeg, float toDeg, int steps)
        {
            var l = new List<V2>();
            for (int i = 0; i <= steps; i++)
            {
                double a = (fromDeg + (toDeg - fromDeg) * i / steps) * Math.PI / 180;
                l.Add(new V2(cz + (float)Math.Cos(a) * r, cy + (float)Math.Sin(a) * r));
            }
            return l;
        }
    }
}
