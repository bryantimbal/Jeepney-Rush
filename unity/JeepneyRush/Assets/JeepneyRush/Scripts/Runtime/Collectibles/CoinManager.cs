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
        private const string PoolKey = "coin";
        private readonly List<Coin> _coins = new List<Coin>();
        private BalanceConfig _cfg;
        private float _spin;

        public void Init(BalanceConfig cfg)
        {
            _cfg = cfg;
            if (!Services.Pools.Has(PoolKey)) Services.Pools.Register(PoolKey, Coin.Create, 60);
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
                    case CoinFormation.Arc: y = 1.0f + Mathf.Sin(t * Mathf.PI) * (_cfg.jumpHeight + 0.6f); break;
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

        public void Clear()
        {
            foreach (var c in _coins) Services.Pools.Release(c.gameObject);
            _coins.Clear();
        }

        public int ActiveCount => _coins.Count;

        public void Tick(float dt, float speed)
        {
            _spin += dt * 220f;
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
                else p.y = c.BaseY + Mathf.Sin(_spin * 0.02f + p.z * 0.3f) * 0.08f;
                c.transform.localPosition = p;
                c.Visual.localRotation = Quaternion.Euler(0, _spin + p.z * 8f, 0);
                if (p.z < -_cfg.despawnBehind) Remove(i);
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
