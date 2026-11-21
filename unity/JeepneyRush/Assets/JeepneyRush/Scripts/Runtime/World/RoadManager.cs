using System.Collections.Generic;
using JeepneyRush.Logic;
using JeepneyRush.Logic.Geometry;
using UnityEngine;

namespace JeepneyRush
{
    /// <summary>
    /// Endless road: one long road mesh whose textures scroll, plus a ring of
    /// recycled scenery segments spawned ahead and recycled behind the camera.
    /// The player stays at z = 0; the world moves toward -Z.
    /// </summary>
    public sealed class RoadManager : MonoBehaviour
    {
        private const float RoadLength = 320f;
        private const float RoadNear = 30f; // metres behind the player still covered
        private const float RoadTexMetres = 12f;

        private readonly List<RoadSegment> _active = new List<RoadSegment>();
        private readonly List<RoadSegment> _free = new List<RoadSegment>();
        private Material _road, _sidewalk, _curb;
        private const float RowMetres = 5f;
        private struct Strip { public Mesh Mesh; public Vector3[] Base; public Vector3[] Work; public Vector3 Origin; }
        private readonly List<Strip> _strips = new List<Strip>();
        private bool _bent;
        private float _roadDry = 0.45f;
        private float _scroll;
        private System.Random _rng = new System.Random(42);
        public int ActiveSegments => _active.Count;

        public EnvironmentKind Environment { get; private set; }
        /// <summary>Parent of the scenery segments (bent rigidly by <see cref="RoadBender"/>).</summary>
        public Transform SegmentRoot { get; private set; }

        /// <summary>Destroys the current road and builds the one for <paramref name="env"/> (route change in the menu).</summary>
        public void Rebuild(EnvironmentKind env)
        {
            for (int i = transform.childCount - 1; i >= 0; i--) Destroy(transform.GetChild(i).gameObject);
            _active.Clear();
            _strips.Clear();
            _free.Clear();
            Build(env);
        }

        public void Build(EnvironmentKind env = EnvironmentKind.Manila, int segmentVariants = ModelLibrary.ChunkVariants)
        {
            Environment = env;
            float centre = RoadLength / 2f - RoadNear; // spans z = -RoadNear .. RoadLength - RoadNear
            float roadW = SceneryBuilder.RoadHalfWidth * 2;
            bool paved = Environments.Paved(env);
            _road = MaterialLibrary.Surface(paved ? "road" : "road_dirt", new Vector2(1, RoadLength / RoadTexMetres));
            if (_road.HasProperty("_Smoothness")) _roadDry = _road.GetFloat("_Smoothness");
            float swW = SceneryBuilder.SidewalkOuter - SceneryBuilder.RoadHalfWidth;
            string ground = env == EnvironmentKind.Provincial || env == EnvironmentKind.Baguio ? "grass" : env == EnvironmentKind.Beach ? "sand" : env == EnvironmentKind.Mountain ? "dirt" : "sidewalk";
            _sidewalk = MaterialLibrary.Surface(ground, new Vector2(swW / 2f, RoadLength / 2f));
            _curb = MaterialLibrary.Surface(paved ? "curb" : "curb_dirt", new Vector2(RoadLength / 4f, 1));

            Plane("Road", _road, new Vector3(0, 0, centre), new Vector2(roadW, RoadLength));
            foreach (float s in new[] { -1f, 1f })
            {
                Plane("Sidewalk", _sidewalk, new Vector3(s * (SceneryBuilder.RoadHalfWidth + swW / 2f), SceneryBuilder.SidewalkHeight, centre), new Vector2(swW, RoadLength));
                // vertical curb face toward the road (texture runs along Z)
                float x = s * SceneryBuilder.RoadHalfWidth, h = SceneryBuilder.SidewalkHeight, hz = RoadLength / 2f;
                var nrm = new Vector3(-s, 0, 0);
                Quad("Curb", _curb, new Vector3(x, 0, centre - hz), new Vector3(x, h, centre - hz), new Vector3(x, h, centre + hz), new Vector3(x, 0, centre + hz), nrm,
                    new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0));
            }
            var groundColor = env == EnvironmentKind.Provincial ? new Color(0.42f, 0.6f, 0.3f) : env == EnvironmentKind.Beach ? new Color(0.91f, 0.85f, 0.65f)
                : env == EnvironmentKind.Mountain ? new Color(0.3f, 0.42f, 0.26f) : env == EnvironmentKind.Baguio ? new Color(0.28f, 0.42f, 0.3f)
                : env == EnvironmentKind.Cebu ? new Color(0.45f, 0.44f, 0.42f) : new Color(0.42f, 0.42f, 0.44f);
            var groundGo = Plane("Ground", MaterialLibrary.Lit(Texture2D.whiteTexture, groundColor, 0, 0.1f), new Vector3(0, -0.05f, centre), new Vector2(240, RoadLength));
            groundGo.GetComponent<MeshRenderer>().receiveShadows = false;

            SegmentRoot = new GameObject("Segments").transform;
            SegmentRoot.SetParent(transform, false);
            for (int i = 0; i < segmentVariants; i++)
            {
                var model = ModelLibrary.Chunk(env, i);
                var go = ModelLibrary.Instantiate(model, "Segment" + i, SegmentRoot, castShadows: false);
                var seg = go.AddComponent<RoadSegment>();
                seg.Variant = i;
                go.SetActive(false);
                _free.Add(seg);
            }
            Layout();
        }

        /// <summary>Flat quad lying on XZ (local +Y up), size x by z.</summary>
        private GameObject Plane(string name, Material mat, Vector3 pos, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = pos;
            float hx = size.x / 2, hz = size.y / 2;
            // subdivided along Z so hills and bends can deform it (see Bend)
            var mesh = Grid(name, new Vector3(-hx, 0, -hz), new Vector3(hx, 0, -hz), size.y, Vector3.up, false, pos);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        /// <summary>Arbitrary quad (world-local coordinates) with triangles wound to face <paramref name="normal"/>.</summary>
        private GameObject Quad(string name, Material mat, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            // a/b: the cross-section at the near end, d: far end of a (texture u runs along Z)
            var mesh = Grid(name, a, b, d.z - a.z, normal, true, Vector3.zero);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        /// <summary>
        /// Two-column grid from cross-section a→b (at the near end) extruded <paramref name="length"/>
        /// metres along +Z in <see cref="RowMetres"/> rows. UV: across = u, along = v
        /// (or swapped when <paramref name="uAlongZ"/>). Registered for bending.
        /// </summary>
        private Mesh Grid(string name, Vector3 a, Vector3 b, float length, Vector3 normal, bool uAlongZ, Vector3 origin)
        {
            int rows = Mathf.Max(1, Mathf.CeilToInt(length / RowMetres));
            var verts = new Vector3[(rows + 1) * 2];
            var nrm = new Vector3[verts.Length];
            var uv = new Vector2[verts.Length];
            for (int r = 0; r <= rows; r++)
            {
                float t = r / (float)rows, dz = length * t;
                verts[r * 2] = a + new Vector3(0, 0, dz);
                verts[r * 2 + 1] = b + new Vector3(0, 0, dz);
                nrm[r * 2] = nrm[r * 2 + 1] = normal;
                uv[r * 2] = uAlongZ ? new Vector2(t, 0) : new Vector2(0, t);
                uv[r * 2 + 1] = uAlongZ ? new Vector2(t, 1) : new Vector2(1, t);
            }
            var tris = new List<int>(rows * 6);
            bool flip = Vector3.Dot(Vector3.Cross(b - a, verts[2] - a), normal) < 0;
            for (int r = 0; r < rows; r++)
            {
                int i0 = r * 2, i1 = i0 + 1, i2 = i0 + 3, i3 = i0 + 2; // a, b, b', a'
                if (flip) tris.AddRange(new[] { i0, i2, i1, i0, i3, i2 });
                else tris.AddRange(new[] { i0, i1, i2, i0, i2, i3 });
            }
            var mesh = new Mesh { name = name };
            mesh.MarkDynamic();
            mesh.vertices = verts;
            mesh.normals = nrm;
            mesh.uv = uv;
            mesh.SetTriangles(tris, 0);
            // generous bounds: bending moves vertices far sideways / up on curves and hills
            mesh.bounds = new Bounds(new Vector3((a.x + b.x) / 2, 0, a.z + length / 2), new Vector3(Mathf.Abs(b.x - a.x) + 300f, 120f, length + 20f));
            _strips.Add(new Strip { Mesh = mesh, Base = verts, Work = (Vector3[])verts.Clone(), Origin = origin });
            return mesh;
        }

        /// <summary>Deforms the road, sidewalk, curb and ground strips to the current <see cref="RoadCurve"/>.</summary>
        public void Bend()
        {
            if (!RoadCurve.Enabled)
            {
                if (!_bent) return;
                foreach (var st in _strips) st.Mesh.vertices = st.Base;
                _bent = false;
                return;
            }
            _bent = true;
            foreach (var st in _strips)
            {
                for (int i = 0; i < st.Base.Length; i++)
                {
                    var v = st.Base[i];
                    st.Work[i] = v + RoadCurve.Offset(st.Origin.z + v.z);
                }
                st.Mesh.vertices = st.Work;
            }
        }

        /// <summary>Rain makes the asphalt darker and glossy (reflections of the sky).</summary>
        public void SetWetness(float wet)
        {
            if (_road == null) return;
            float smooth = Mathf.Lerp(_roadDry, 0.88f, wet);
            if (_road.HasProperty("_Smoothness")) _road.SetFloat("_Smoothness", smooth);
            if (_road.HasProperty("_Glossiness")) _road.SetFloat("_Glossiness", smooth);
            var c = Color.Lerp(Color.white, new Color(0.62f, 0.66f, 0.72f), wet);
            if (_road.HasProperty("_BaseColor")) _road.SetColor("_BaseColor", c);
            if (_road.HasProperty("_Color")) _road.SetColor("_Color", c);
        }

        /// <summary>Places segments from just behind the camera to the horizon (5–7 active).</summary>
        public void Layout()
        {
            foreach (var s in _active) { s.gameObject.SetActive(false); _free.Add(s); }
            _active.Clear();
            float z = -RoadNear + SceneryBuilder.ChunkLength / 2f;
            // 7 active segments cover the camera to beyond the fog end; one spare for recycling
            while (z - SceneryBuilder.ChunkLength / 2f < 230f && _free.Count > 0)
            {
                Place(z);
                z += SceneryBuilder.ChunkLength;
            }
        }

        private void Place(float z)
        {
            var seg = _free[_rng.Next(_free.Count)];
            _free.Remove(seg);
            seg.transform.localPosition = new Vector3(0, 0, z);
            seg.gameObject.SetActive(true);
            _active.Add(seg);
        }

        public void Scroll(float dz)
        {
            if (dz < 0) { ScrollBack(-dz); return; }
            if (dz == 0) return;
            _scroll += dz;
            Offset(_road, 0, dz / RoadTexMetres);
            Offset(_sidewalk, 0, dz / 2f);
            Offset(_curb, dz / 4f, 0);
            float farthest = float.MinValue;
            foreach (var s in _active) { s.MoveBy(dz); farthest = Mathf.Max(farthest, s.transform.localPosition.z); }
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var s = _active[i];
                if (s.FrontZ < -RoadNear)
                {
                    _active.RemoveAt(i);
                    s.gameObject.SetActive(false);
                    _free.Add(s);
                    farthest += SceneryBuilder.ChunkLength;
                    Place(farthest);
                }
            }
        }

        /// <summary>Reversing: the street moves forward again; recycle the farthest segment to behind.</summary>
        private void ScrollBack(float dz)
        {
            _scroll -= dz;
            Offset(_road, 0, -dz / RoadTexMetres);
            Offset(_sidewalk, 0, -dz / 2f);
            Offset(_curb, -dz / 4f, 0);
            float nearest = float.MaxValue;
            RoadSegment far = null;
            foreach (var s in _active)
            {
                s.MoveBy(-dz);
                float z = s.transform.localPosition.z;
                nearest = Mathf.Min(nearest, z);
                if (far == null || z > far.transform.localPosition.z) far = s;
            }
            if (far != null && nearest - SceneryBuilder.ChunkLength / 2f > -RoadNear)
            {
                var p = far.transform.localPosition;
                p.z = nearest - SceneryBuilder.ChunkLength;
                far.transform.localPosition = p;
            }
        }

        private static void Offset(Material m, float du, float dv)
        {
            if (m == null) return;
            var o = m.mainTextureOffset;
            o.x = Mathf.Repeat(o.x + du, 1f);
            o.y = Mathf.Repeat(o.y + dv, 1f);
            m.mainTextureOffset = o;
        }
    }
}
