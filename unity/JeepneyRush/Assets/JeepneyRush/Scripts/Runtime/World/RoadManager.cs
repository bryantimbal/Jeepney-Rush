using System.Collections.Generic;
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
        private float _scroll;
        private System.Random _rng = new System.Random(42);
        public int ActiveSegments => _active.Count;

        public void Build(int segmentVariants = ModelLibrary.ChunkVariants)
        {
            float centre = RoadLength / 2f - RoadNear; // spans z = -RoadNear .. RoadLength - RoadNear
            float roadW = SceneryBuilder.RoadHalfWidth * 2;
            _road = MaterialLibrary.Surface("road", new Vector2(1, RoadLength / RoadTexMetres));
            float swW = SceneryBuilder.SidewalkOuter - SceneryBuilder.RoadHalfWidth;
            _sidewalk = MaterialLibrary.Surface("sidewalk", new Vector2(swW / 2f, RoadLength / 2f));
            _curb = MaterialLibrary.Surface("curb", new Vector2(RoadLength / 4f, 1));

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
            var ground = Plane("Ground", MaterialLibrary.Lit(Texture2D.whiteTexture, new Color(0.42f, 0.42f, 0.44f), 0, 0.1f), new Vector3(0, -0.05f, centre), new Vector2(240, RoadLength));
            ground.GetComponent<MeshRenderer>().receiveShadows = false;

            for (int i = 0; i < segmentVariants; i++)
            {
                var model = ModelLibrary.Chunk(i);
                var go = ModelLibrary.Instantiate(model, "Segment" + i, transform, castShadows: false);
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
            var mesh = new Mesh { name = name };
            float hx = size.x / 2, hz = size.y / 2;
            mesh.SetVertices(new List<Vector3> { new Vector3(-hx, 0, -hz), new Vector3(hx, 0, -hz), new Vector3(hx, 0, hz), new Vector3(-hx, 0, hz) });
            mesh.SetNormals(new List<Vector3> { Vector3.up, Vector3.up, Vector3.up, Vector3.up });
            mesh.SetUVs(0, new List<Vector2> { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) });
            mesh.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
            mesh.RecalculateBounds();
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
            var mesh = new Mesh { name = name };
            mesh.SetVertices(new List<Vector3> { a, b, c, d });
            mesh.SetNormals(new List<Vector3> { normal, normal, normal, normal });
            mesh.SetUVs(0, new List<Vector2> { ua, ub, uc, ud });
            bool flip = Vector3.Dot(Vector3.Cross(b - a, c - a), normal) < 0;
            mesh.SetTriangles(flip ? new[] { 0, 2, 1, 0, 3, 2 } : new[] { 0, 1, 2, 0, 2, 3 }, 0);
            mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
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
            if (dz <= 0) return;
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
