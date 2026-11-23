using System.Collections.Generic;
using JeepneyRush.Logic;
using JeepneyRush.Logic.Geometry;
using UnityEngine;

namespace JeepneyRush
{
    /// <summary>
    /// Makes the world feel alive (purely visual – nothing here touches gameplay): people walking along
    /// the sidewalks (umbrellas up when it rains, fewer at night), stray dogs and – in the provinces –
    /// chickens on the roadside, and flocks of birds flapping across the sky by day (pigeons in the city,
    /// gulls by the sea, white egrets over the fields, a hawk in the mountains).
    /// </summary>
    public sealed class LifeManager : MonoBehaviour
    {
        private sealed class Walker { public Transform T, Umbrella; public float X, ShownX, Z, Dir, Speed, Phase; public bool Umbrellas, Visible = true; public bool Animal; }
        private sealed class Bird { public Transform T, WingL, WingR; public Vector3 Offset; public float Phase, Rate; }
        private sealed class Flock { public Transform T; public Vector3 Vel; public readonly List<Bird> Birds = new List<Bird>(); }

        private readonly List<Walker> _walkers = new List<Walker>();
        private readonly List<Flock> _flocks = new List<Flock>();
        private Transform _ground, _sky;
        private EnvironmentKind _env;
        private float _time;
        /// <summary>Walkers and animals – bent along the road curve by the RoadBender.</summary>
        public Transform GroundRoot => _ground;
        /// <summary>Intersections, so walkers squeeze past the tricycle terminals along the curb.</summary>
        public IntersectionManager Crossings;

        private static readonly string[] UmbrellaColors = { "#E63946", "#1D3557", "#2A9D8F", "#F4A261", "#8338EC", "#111111" };

        public void Init(EnvironmentKind env)
        {
            _env = env;
            foreach (Transform c in transform) Destroy(c.gameObject);
            _walkers.Clear();
            _flocks.Clear();
            _ground = new GameObject("Walkers").transform; _ground.SetParent(transform, false);
            _sky = new GameObject("Birds").transform; _sky.SetParent(transform, false);

            int people = env == EnvironmentKind.Fiesta ? 18 : env == EnvironmentKind.Manila ? 14 : env == EnvironmentKind.Edsa ? 12 : env == EnvironmentKind.Cebu ? 12
                : env == EnvironmentKind.Baguio ? 9 : env == EnvironmentKind.Beach ? 7 : env == EnvironmentKind.Provincial ? 6 : 4;
            for (int i = 0; i < people; i++) AddWalker(false, i);
            int dogs = env == EnvironmentKind.Edsa ? 0 : env == EnvironmentKind.Baguio ? 1 : 2;
            for (int i = 0; i < dogs; i++) AddWalker(true, i);
            bool farm = env == EnvironmentKind.Provincial || env == EnvironmentKind.Mountain || env == EnvironmentKind.Beach;
            if (farm) for (int i = 0; i < 3; i++) AddChicken(i);

            // birds: what flies depends on the route
            string color = env == EnvironmentKind.Beach || env == EnvironmentKind.Cebu ? "#F2F2F2"      // gulls
                : env == EnvironmentKind.Provincial ? "#FAFAFA"                                           // egrets (tagak)
                : env == EnvironmentKind.Mountain ? "#4A3B2E"                                             // hawk
                : "#5E636E";                                                                              // pigeons / maya
            int flocks = env == EnvironmentKind.Edsa ? 1 : env == EnvironmentKind.Mountain ? 2 : env == EnvironmentKind.Beach || env == EnvironmentKind.Provincial ? 3 : 2;
            int perFlock = env == EnvironmentKind.Mountain ? 1 : env == EnvironmentKind.Provincial ? 6 : 7;
            float size = env == EnvironmentKind.Mountain ? 2.2f : env == EnvironmentKind.Provincial ? 1.4f : env == EnvironmentKind.Beach || env == EnvironmentKind.Cebu ? 1.3f : 1f;
            for (int f = 0; f < flocks; f++) AddFlock(color, size, perFlock, f);
        }

        private void AddWalker(bool dog, int i)
        {
            var w = new Walker { Animal = dog };
            if (dog)
            {
                string c = new[] { "#B08455", "#3B2F2A", "#D9C3A0", "#7A5A3E" }[i % 4];
                w.T = ModelLibrary.Instantiate(ModelLibrary.Get("dog_" + c, () => LifeBuilder.Dog(c)), "Dog" + i, _ground, castShadows: false).transform;
                w.Speed = Random.Range(0.6f, 1.2f);
            }
            else
            {
                w.T = ModelLibrary.Instantiate(ModelLibrary.Person(i % PropBuilder.PersonVariants), "Walker" + i, _ground, castShadows: false).transform;
                string c = UmbrellaColors[i % UmbrellaColors.Length];
                w.Umbrella = ModelLibrary.Instantiate(ModelLibrary.Get("umbrella_" + c, () => LifeBuilder.Umbrella(c)), "Umbrella", w.T, castShadows: false).transform;
                w.Umbrella.localPosition = new Vector3(0.18f, 1.15f, 0.05f);
                w.Umbrella.gameObject.SetActive(false);
                w.Umbrellas = Random.value < 0.85f;
                w.Speed = Random.Range(1.0f, 1.6f);
            }
            Respawn(w, Random.Range(-10f, 140f));
            _walkers.Add(w);
        }

        private void AddChicken(int i)
        {
            string c = new[] { "#8B3A1E", "#F2E6D0", "#3B2F2A" }[i % 3];
            var w = new Walker { Animal = true, Speed = Random.Range(0.2f, 0.6f) };
            w.T = ModelLibrary.Instantiate(ModelLibrary.Get("chicken_" + c, () => LifeBuilder.Chicken(c)), "Chicken" + i, _ground, castShadows: false).transform;
            Respawn(w, Random.Range(0f, 140f));
            _walkers.Add(w);
        }

        private float SidewalkX(bool animal)
        {
            float side = Random.value < 0.5f ? -1f : 1f;
            float r = SceneryBuilder.RoadHalfWidth;
            bool city = _env == EnvironmentKind.Manila || _env == EnvironmentKind.Edsa || _env == EnvironmentKind.Cebu;
            if (_env == EnvironmentKind.Edsa && side < 0) side = 1f; // the left side of EDSA is the busway and the MRT
            // stay in front of the vendors at the building line
            float far = city ? SceneryBuilder.SidewalkOuter - 1.6f : r + 1.8f;
            return side * Random.Range(r + (animal ? 0.7f : 0.9f), animal ? r + 1.8f : far);
        }

        private void Respawn(Walker w, float z)
        {
            w.X = w.ShownX = SidewalkX(w.Animal);
            w.Z = z;
            w.Dir = Random.value < 0.5f ? 1f : -1f;
            w.Phase = Random.Range(0f, 10f);
            w.T.localRotation = Quaternion.Euler(0, w.Dir > 0 ? 0f : 180f, 0);
        }

        private void AddFlock(string color, float size, int count, int index)
        {
            var f = new Flock { T = new GameObject("Flock" + index).transform };
            f.T.SetParent(_sky, false);
            for (int i = 0; i < count; i++)
            {
                var bird = new Bird { T = new GameObject("Bird").transform };
                bird.T.SetParent(f.T, false);
                ModelLibrary.Instantiate(ModelLibrary.Get($"bird_body_{color}_{size}", () => LifeBuilder.BirdBody(color, size)), "Body", bird.T, castShadows: false);
                var wingModel = ModelLibrary.Get($"bird_wing_{color}_{size}", () => LifeBuilder.BirdWing(color, size));
                bird.WingR = ModelLibrary.Instantiate(wingModel, "WingR", bird.T, castShadows: false).transform;
                bird.WingL = ModelLibrary.Instantiate(wingModel, "WingL", bird.T, castShadows: false).transform;
                bird.WingL.localScale = new Vector3(-1, 1, 1);
                // loose V formation
                int row = (i + 1) / 2;
                float sideOff = i == 0 ? 0 : (i % 2 == 0 ? 1 : -1) * row * 1.3f * size;
                bird.Offset = new Vector3(sideOff + Random.Range(-0.3f, 0.3f), Random.Range(-0.4f, 0.4f), -row * 1.1f * size);
                bird.Phase = Random.Range(0f, 6.28f);
                bird.Rate = Random.Range(7f, 9.5f) / Mathf.Sqrt(size);
                f.Birds.Add(bird);
            }
            ResetFlock(f, true);
            _flocks.Add(f);
        }

        private void ResetFlock(Flock f, bool anywhere)
        {
            float side = Random.value < 0.5f ? -1f : 1f;
            float z = anywhere ? Random.Range(30f, 120f) : Random.Range(90f, 150f);
            f.T.localPosition = new Vector3(side * Random.Range(10f, 45f), Random.Range(14f, 30f), z);
            // fly across the road, a bit forward or backward
            f.Vel = new Vector3(-side * Random.Range(3f, 7f), Random.Range(-0.2f, 0.2f), Random.Range(4f, 12f));
            f.T.localRotation = Quaternion.LookRotation(f.Vel.normalized);
        }

        /// <summary>
        /// Horn: dogs and chickens between <paramref name="zFrom"/> and <paramref name="zTo"/> metres
        /// ahead scurry off. Returns how many were scared and the position of one (for a bubble).
        /// </summary>
        public int Scare(float zFrom, float zTo, out Vector3 where)
        {
            int n = 0; where = Vector3.zero;
            foreach (var w in _walkers)
            {
                if (!w.Animal || w.T == null || !w.T.gameObject.activeSelf || w.Z < zFrom || w.Z > zTo) continue;
                w.Speed = Random.Range(3.5f, 5f);
                w.Dir = 1f;
                w.X += Mathf.Sign(w.X) * 0.8f;
                if (n++ == 0) where = w.T.position;
            }
            return n;
        }

        /// <summary>Is (x, z) next to a corner tricycle terminal (with a little run-up)?</summary>
        private bool TerminalAt(float x, float z)
        {
            if (Crossings == null || !Environments.Tricycles(_env)) return false;
            foreach (var v in Crossings.Items)
            {
                float lz = z - v.transform.localPosition.z;
                foreach (var t in PropBuilder.TodaZones)
                    if (t.Side * x > 0 && lz > t.Z0 - 2f && lz < t.Z0 + t.Length + 1.5f) return true;
            }
            return false;
        }

        /// <param name="speed">Jeepney speed (m/s): the world slides back toward the jeepney.</param>
        public void Tick(float dt, float speed, float night, float wet)
        {
            _time += dt;
            // ---- walkers, dogs, chickens
            for (int i = 0; i < _walkers.Count; i++)
            {
                var w = _walkers[i];
                if (w.T == null) continue;
                w.Z += (w.Dir * w.Speed - speed) * dt;
                if (w.Z < -30f) Respawn(w, Random.Range(90f, 150f));
                else if (w.Z > 170f) Respawn(w, Random.Range(-20f, 10f));
                // fewer people out late at night; animals wander regardless
                bool show = w.Animal || (i % 3 != 0) || night < 0.6f;
                if (w.T.gameObject.activeSelf != show) w.T.gameObject.SetActive(show);
                if (!show) continue;
                float step = _time * w.Speed * (w.Animal ? 7f : 5.5f) + w.Phase;
                float bob = Mathf.Abs(Mathf.Sin(step)) * (w.Animal ? 0.03f : 0.05f);
                float tx = TerminalAt(w.X, w.Z) ? Mathf.Sign(w.X) * VendorBuilder.CurbWalkX : w.X;
                w.ShownX = Mathf.MoveTowards(w.ShownX, tx, 2.5f * dt);
                w.T.localPosition = new Vector3(w.ShownX, SceneryBuilder.SidewalkHeight + bob, w.Z);
                w.T.localRotation = Quaternion.Euler(0, w.Dir > 0 ? 0f : 180f, Mathf.Sin(step) * (w.Animal ? 4f : 2.5f));
                if (w.Umbrella != null)
                {
                    bool up = w.Umbrellas && wet > 0.2f;
                    if (w.Umbrella.gameObject.activeSelf != up) w.Umbrella.gameObject.SetActive(up);
                }
            }
            // ---- birds by day (they shelter at night and in heavy rain)
            bool birds = night < 0.55f && wet < 0.6f;
            if (_sky.gameObject.activeSelf != birds) _sky.gameObject.SetActive(birds);
            if (!birds) return;
            foreach (var f in _flocks)
            {
                var p = f.T.localPosition + (f.Vel - new Vector3(0, 0, speed)) * dt;
                f.T.localPosition = p;
                if (p.z < -20f || p.z > 190f || Mathf.Abs(p.x) > 70f) ResetFlock(f, false);
                foreach (var b in f.Birds)
                {
                    // flap, with short glides
                    float cycle = Mathf.Sin(_time * 0.6f + b.Phase);
                    float flap = cycle > 0.7f ? 0.15f : Mathf.Sin(_time * b.Rate + b.Phase);
                    float a = 10f + flap * 38f;
                    b.WingR.localRotation = Quaternion.Euler(0, 0, a);
                    b.WingL.localRotation = Quaternion.Euler(0, 0, -a);
                    b.T.localPosition = b.Offset + new Vector3(0, Mathf.Sin(_time * 1.3f + b.Phase) * 0.25f, 0);
                }
            }
        }
    }
}
