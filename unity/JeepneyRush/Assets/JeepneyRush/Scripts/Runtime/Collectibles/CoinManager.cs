using System.Collections.Generic;
using JeepneyRush.Logic;
using UnityEngine;

namespace JeepneyRush
{
    /// <summary>
    /// Spawns coin formations (line trails, zigzags across two lanes, arcs over jump
    /// walls), spins and scrolls them, and handles collection.
    /// </summary>
    public sealed class CoinManager : MonoBehaviour
    {
        private const string PoolKey = "coin", GemPool = "gem";
        private readonly List<Coin> _coins = new List<Coin>();
        private readonly List<Gem> _gems = new List<Gem>();
        private BalanceConfig _cfg;
        private float _spin;

        public void Init(BalanceConfig cfg)
        {
            _cfg = cfg;
            if (!Services.Pools.Has(PoolKey)) Services.Pools.Register(PoolKey, Coin.Create, 60);
            if (!Services.Pools.Has(GemPool)) Services.Pools.Register(GemPool, Gem.Create, 2);
        }

        public void SpawnFormation(CoinFormation f, int lane, int lane2, float unityZ, int count, float spacing)
        {
            for (int i = 0; i < count; i++)
            {
                float t = count > 1 ? i / (float)(count - 1) : 0;
                float x = _cfg.LaneX(lane);
                float y = 1.0f;
                switch (f)
                {
                    case CoinFormation.Arc: y = 1.0f + Mathf.Sin(t * Mathf.PI) * 1.2f; break; // legacy (no longer generated)
                    case CoinFormation.Zigzag:
                        // first third in lane, middle third crossing, last third in lane2
                        float k = Mathf.Clamp01((t - 0.33f) / 0.34f);
                        x = Mathf.Lerp(_cfg.LaneX(lane), _cfg.LaneX(lane2), k * k * (3 - 2 * k));
                        break;
                }
                var c = Services.Pools.Get(PoolKey, transform).GetComponent<Coin>();
                c.Collected = false;
                c.BaseY = y;
                c.transform.localPosition = new Vector3(x, y, unityZ + i * spacing);
                c.Visual.localScale = Vector3.one;
                _coins.Add(c);
            }
        }

        /// <summary>A gem floating a little higher than coins, so it stands out.</summary>
        public void SpawnGem(int lane, float unityZ)
        {
            var g = Services.Pools.Get(GemPool, transform).GetComponent<Gem>();
            g.Collected = false;
            g.transform.localPosition = new Vector3(_cfg.LaneX(lane), 1.5f, unityZ);
            g.Visual.localScale = Vector3.one;
            _gems.Add(g);
        }

        public void Clear()
        {
            foreach (var c in _coins) Services.Pools.Release(c.gameObject);
            _coins.Clear();
            foreach (var g in _gems) Services.Pools.Release(g.gameObject);
            _gems.Clear();
        }

        /// <summary>Called by CollisionHandler. True the first time a gem is touched.</summary>
        public bool Collect(Gem g)
        {
            if (g.Collected) return false;
            g.Collected = true;
            return true;
        }

        public int ActiveCount => _coins.Count;

        /// <summary>Coin Magnet power-up: coins within this radius fly to the jeepney (0 = off).</summary>
        public float MagnetRadius;
        public Vector3 MagnetTarget;

        public void Tick(float dt, float speed)
        {
            _spin += dt * 220f;
            TickGems(dt, speed);
            for (int i = _coins.Count - 1; i >= 0; i--)
            {
                var c = _coins[i];
                var p = c.transform.localPosition;
                p.z -= speed * dt;
                if (c.Collected)
                {
                    // fly up and shrink
                    p.y += dt * 6f;
                    c.Visual.localScale *= 1f - dt * 6f;
                    if (c.Visual.localScale.x < 0.1f) { Remove(i); continue; }
                }
                else if (MagnetRadius > 0 && p.z < MagnetRadius && p.z > -2f && Mathf.Abs(p.x - MagnetTarget.x) < MagnetRadius)
                {
                    // pulled toward the jeepney; the trigger collects it on contact
                    p = Vector3.MoveTowards(p, MagnetTarget + new Vector3(0, 1.2f, 0), dt * 30f);
                }
                else p.y = c.BaseY + Mathf.Sin(_spin * 0.02f + p.z * 0.3f) * 0.08f;
                c.transform.localPosition = p;
                c.Visual.localRotation = Quaternion.Euler(0, _spin + p.z * 8f, 0);
                if (p.z < -_cfg.despawnBehind) Remove(i);
            }
        }

        private void TickGems(float dt, float speed)
        {
            for (int i = _gems.Count - 1; i >= 0; i--)
            {
                var g = _gems[i];
                var p = g.transform.localPosition;
                p.z -= speed * dt;
                if (g.Collected)
                {
                    p.y += dt * 7f;
                    g.Visual.localScale *= 1f - dt * 5f;
                    if (g.Visual.localScale.x < 0.1f) { Services.Pools.Release(g.gameObject); _gems.RemoveAt(i); continue; }
                }
                else if (MagnetRadius > 0 && p.z < MagnetRadius && p.z > -2f && Mathf.Abs(p.x - MagnetTarget.x) < MagnetRadius)
                    p = Vector3.MoveTowards(p, MagnetTarget + new Vector3(0, 1.2f, 0), dt * 30f);
                else p.y = 1.5f + Mathf.Sin(_spin * 0.03f + i) * 0.15f;
                g.transform.localPosition = p;
                g.Visual.localRotation = Quaternion.Euler(0, _spin * 0.8f, 0);
                if (p.z < -_cfg.despawnBehind) { Services.Pools.Release(g.gameObject); _gems.RemoveAt(i); }
            }
        }

        private void Remove(int i)
        {
            Services.Pools.Release(_coins[i].gameObject);
            _coins.RemoveAt(i);
        }

        /// <summary>Called by CollisionHandler. Returns the coin value, or 0 if already taken.</summary>
        public int Collect(Coin c)
        {
            if (c.Collected) return 0;
            c.Collected = true;
            return _cfg.coinValue;
        }
    }
}
