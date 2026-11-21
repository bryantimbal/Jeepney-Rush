using UnityEngine;

namespace JeepneyRush
{
    /// <summary>
    /// All particle effects built in code (no prefab dependencies): coin sparkle, crash
    /// sparks + smoke, landing dust, boarding confetti, diesel exhaust and speed lines.
    /// Each effect is one pooled ParticleSystem using Emit() bursts.
    /// </summary>
    public sealed class EffectsManager : MonoBehaviour
    {
        private ParticleSystem _sparks, _smoke, _sparkle, _confetti, _dust, _exhaust, _speedLines;
        private float _exhaustTimer;

        public void Build()
        {
            _sparks = Make("Sparks", 200, 0.5f, 0.12f, new Color(1f, 0.82f, 0.3f), 18f);
            _smoke = Make("Smoke", 100, 1.0f, 0.6f, new Color(0.45f, 0.45f, 0.45f, 0.8f), -1.5f, true);
            _sparkle = Make("CoinSparkle", 150, 0.35f, 0.12f, new Color(1f, 0.95f, 0.6f), 4f);
            _confetti = Make("Confetti", 300, 1.2f, 0.14f, Color.white, 9f);
            _dust = Make("Dust", 100, 0.6f, 0.45f, new Color(0.8f, 0.72f, 0.6f, 0.7f), -0.5f, true);
            _exhaust = Make("Exhaust", 120, 0.7f, 0.22f, new Color(0.6f, 0.6f, 0.6f, 0.6f), -1.5f, true);
            _speedLines = Make("SpeedLines", 80, 0.4f, 0.06f, new Color(1, 1, 1, 0.5f), 0f);
            var r = _speedLines.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.lengthScale = 6f;
        }

        private ParticleSystem Make(string name, int max, float life, float size, Color color, float gravity, bool grow = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true; // keeps simulating Emit() bursts; emission itself is disabled
            main.playOnAwake = false;
            main.maxParticles = max;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.6f, life);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size * 1.4f);
            main.startColor = color;
            main.gravityModifier = gravity / 9.81f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission;
            em.enabled = false;
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.3f;
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, grow ? AnimationCurve.Linear(0, 0.6f, 1, 2.2f) : AnimationCurve.Linear(0, 1, 1, 0));
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 0.6f), new GradientAlphaKey(0, 1) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = MaterialLibrary.Particle();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            ps.Play();
            return ps;
        }

        private static void Burst(ParticleSystem ps, Vector3 pos, int count, Vector3 velocity, float spread, Color? color = null)
        {
            if (ps == null) return;
            var ep = new ParticleSystem.EmitParams { applyShapeToPosition = true };
            for (int i = 0; i < count; i++)
            {
                ep.position = pos;
                ep.velocity = velocity + Random.insideUnitSphere * spread;
                if (color.HasValue) ep.startColor = color.Value;
                ps.Emit(ep, 1);
            }
        }

        private static readonly Color[] Flag = { new Color(0.81f, 0.07f, 0.15f), new Color(0.99f, 0.82f, 0.09f), new Color(0f, 0.22f, 0.66f), Color.white, new Color(0.02f, 0.84f, 0.63f) };

        public void Crash(Vector3 pos)
        {
            Burst(_sparks, pos, 26, new Vector3(0, 4, -3), 7f);
            Burst(_smoke, pos, 10, new Vector3(0, 1.5f, -4), 1.5f);
        }

        public void CoinSparkle(Vector3 pos)
        {
            Burst(_sparkle, pos, 10, Vector3.up * 2.5f, 4f, new Color(1f, 0.82f, 0.2f));
            Burst(_sparkle, pos, 5, Vector3.up * 4f, 2f, Color.white);
        }

        public void Confetti(Vector3 pos, int n = 30)
        {
            for (int i = 0; i < n; i++) Burst(_confetti, pos, 1, new Vector3(0, 5, -2), 5f, Flag[i % Flag.Length]);
        }

        /// <summary>Turbo flames from the tail pipe.</summary>
        public void Turbo(Vector3 playerPos) =>
            Burst(_sparks, playerPos + new Vector3(0.55f, 0.5f, -3.2f), 2, new Vector3(0, 1f, -12f), 1.5f, new Color(1f, 0.55f, 0.1f));

        /// <summary>COIN RUSH celebration: a golden fountain of sparkles.</summary>
        public void CoinRush(Vector3 pos)
        {
            for (int i = 0; i < 40; i++)
            {
                float a = i / 40f * Mathf.PI * 2f;
                Burst(_sparkle, pos, 1, new Vector3(Mathf.Cos(a) * 6f, 7f, Mathf.Sin(a) * 6f), 1.5f, new Color(1f, 0.85f, 0.2f));
            }
            Confetti(pos, 20);
        }

        public void Dust(Vector3 pos) => Burst(_dust, pos, 10, new Vector3(0, 0.5f, -4), 2f);

        /// <summary>Called every frame: diesel puffs from the tail pipe and speed lines at high speed.</summary>
        public void Tick(float dt, Vector3 playerPos, float worldSpeed, float speed01)
        {
            _exhaustTimer -= dt;
            if (_exhaustTimer <= 0)
            {
                _exhaustTimer = 0.12f;
                Burst(_exhaust, playerPos + new Vector3(0.55f, 0.45f, -3.2f), 1, new Vector3(0, 0.6f, -worldSpeed * 0.5f - 2f), 0.4f);
            }
            if (speed01 > 0.7f && Random.value < (speed01 - 0.65f) * dt * 18f)
            {
                float side = Random.value < 0.5f ? -1 : 1;
                var pos = new Vector3(side * Random.Range(3.5f, 7f), Random.Range(0.5f, 5f), Random.Range(15f, 35f));
                Burst(_speedLines, pos, 1, new Vector3(0, 0, -worldSpeed * 2.5f), 0f);
            }
        }

        public void Clear()
        {
            foreach (var ps in new[] { _sparks, _smoke, _sparkle, _confetti, _dust, _exhaust, _speedLines })
                if (ps != null) ps.Clear();
        }
    }
}
