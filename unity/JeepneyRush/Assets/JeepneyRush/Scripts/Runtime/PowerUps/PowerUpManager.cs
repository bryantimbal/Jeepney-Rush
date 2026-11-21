using System.Collections.Generic;
using JeepneyRush.Logic;
using JeepneyRush.Logic.Geometry;
using UnityEngine;

namespace JeepneyRush
{
    /// <summary>Pooled floating power-up pickup with a trigger collider.</summary>
    public sealed class PowerUpPickup : MonoBehaviour
    {
        public PowerUpKind Kind;
        public bool Taken;
        public Transform Visual;
        public float Age;

        public static GameObject Create(PowerUpKind kind)
        {
            var root = new GameObject("PowerUp_" + kind);
            var vis = ModelLibrary.Instantiate(ModelLibrary.PowerUp(kind), "Visual", root.transform, castShadows: false);
            vis.transform.localPosition = new Vector3(0, 1.3f, 0);
            var col = root.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 0.9f;
            col.center = new Vector3(0, 1.3f, 0);
            var rb = root.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            var p = root.AddComponent<PowerUpPickup>();
            p.Kind = kind;
            p.Visual = vis.transform;
            return root;
        }
    }

    /// <summary>
    /// Power-ups: spawns pickups for the SpawnDirector, tracks active timers
    /// (<see cref="PowerUpState"/>) and drives their visual effects (shield bubble).
    /// The session applies the gameplay effects (magnet, turbo, double coins, …).
    /// </summary>
    public sealed class PowerUpManager : MonoBehaviour
    {
        private readonly List<PowerUpPickup> _pickups = new List<PowerUpPickup>();
        private BalanceConfig _cfg;
        private Transform _bubble;
        private float _time;
        public PowerUpState State { get; private set; }
        public float DurationScale = 1f;

        public void Init(BalanceConfig cfg, Transform playerRoot)
        {
            _cfg = cfg;
            State = new PowerUpState(cfg);
            foreach (PowerUpKind k in System.Enum.GetValues(typeof(PowerUpKind)))
            {
                var kind = k;
                string key = "pu:" + k;
                if (!Services.Pools.Has(key)) Services.Pools.Register(key, () => PowerUpPickup.Create(kind));
            }
            if (_bubble == null)
            {
                _bubble = ModelLibrary.Instantiate(ModelLibrary.Bubble(), "ShieldBubble", playerRoot, castShadows: false).transform;
                _bubble.localPosition = new Vector3(0, 1.3f, -0.1f);
                _bubble.localScale = new Vector3(1.75f, 1.75f, 3.8f);
            }
            _bubble.gameObject.SetActive(false);
        }

        public void ResetRun(BalanceConfig cfg, float durationScale)
        {
            Clear();
            _cfg = cfg;
            State = new PowerUpState(cfg);
            DurationScale = durationScale;
        }

        public void Clear()
        {
            foreach (var p in _pickups) Services.Pools.Release(p.gameObject);
            _pickups.Clear();
        }

        public void Spawn(PowerUpKind kind, int lane, float unityZ)
        {
            var p = Services.Pools.Get("pu:" + kind, transform).GetComponent<PowerUpPickup>();
            p.Taken = false;
            p.Age = Random.value * 5f;
            p.Visual.localScale = Vector3.one;
            p.transform.localPosition = new Vector3(_cfg.LaneX(lane), 0, unityZ);
            _pickups.Add(p);
        }

        public void Tick(float dt, float speed)
        {
            _time += dt;
            State.Tick(dt);
            for (int i = _pickups.Count - 1; i >= 0; i--)
            {
                var p = _pickups[i];
                p.Age += dt;
                var pos = p.transform.localPosition;
                pos.z -= speed * dt;
                p.transform.localPosition = pos;
                p.Visual.localRotation = Quaternion.Euler(0, p.Age * 120f, 0);
                p.Visual.localPosition = new Vector3(0, 1.3f + Mathf.Sin(p.Age * 3f) * 0.15f, 0);
                if (p.Taken)
                {
                    p.Visual.localScale *= 1f - dt * 7f;
                    if (p.Visual.localScale.x < 0.08f) Remove(i);
                    continue;
                }
                if (pos.z < -_cfg.despawnBehind) Remove(i);
            }
            bool shield = State.IsActive(PowerUpKind.Shield);
            if (_bubble.gameObject.activeSelf != shield) _bubble.gameObject.SetActive(shield);
            if (shield)
            {
                float pulse = 1f + Mathf.Sin(_time * 6f) * 0.03f;
                _bubble.localScale = new Vector3(1.75f * pulse, 1.75f * pulse, 3.8f * pulse);
                // blink during the last two seconds so the player knows it is ending
                if (State.Remaining(PowerUpKind.Shield) < 2f) _bubble.gameObject.SetActive(Mathf.FloorToInt(_time * 8) % 2 == 0);
            }
        }

        private void Remove(int i)
        {
            Services.Pools.Release(_pickups[i].gameObject);
            _pickups.RemoveAt(i);
        }

        /// <summary>Activates the pickup's power-up. Returns false if it was already taken.</summary>
        public bool Collect(PowerUpPickup p)
        {
            if (p.Taken) return false;
            p.Taken = true;
            State.Activate(p.Kind, DurationScale);
            return true;
        }
    }
}
