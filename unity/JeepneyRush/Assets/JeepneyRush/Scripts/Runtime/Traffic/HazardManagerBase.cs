using System.Collections.Generic;
using JeepneyRush.Logic;
using UnityEngine;

namespace JeepneyRush
{
    /// <summary>Shared pooled-hazard behaviour for TrafficManager and ObstacleManager.</summary>
    public abstract class HazardManagerBase : MonoBehaviour
    {
        protected readonly List<Hazard> Items = new List<Hazard>();
        protected BalanceConfig Cfg;
        protected GameRandom Rng;
        public IReadOnlyList<Hazard> Active => Items;

        public virtual void Init(BalanceConfig cfg, GameRandom rng)
        {
            Cfg = cfg;
            Rng = rng;
        }

        public void SetRandom(GameRandom rng) => Rng = rng;

        public Hazard Spawn(HazardKind kind, int lane, float unityZ, float speed, bool solid)
        {
            int variant = Rng.Int(0, ModelLibrary.VariantCount(kind) - 1);
            string key = $"hz:{kind}:{variant}";
            var pools = Services.Pools;
            if (!pools.Has(key)) pools.Register(key, () => Hazard.Create(kind, variant, key));
            var h = pools.Get(key, transform).GetComponent<Hazard>();
            h.ResetState(lane, speed, solid);
            float x = Cfg.LaneX(lane);
            if (kind == HazardKind.ParkedSedan || kind == HazardKind.ParkedJeepney) x += lane == 0 ? -0.4f : 0.4f; // hug the curb
            h.transform.localPosition = new Vector3(x, 0, unityZ);
            if (kind == HazardKind.Pothole) h.Body.localRotation = Quaternion.Euler(0, Rng.Range(0, 180), 0);
            Items.Add(h);
            return h;
        }

        public void Clear()
        {
            foreach (var h in Items) Services.Pools.Release(h.gameObject);
            Items.Clear();
        }

        public void CollectBlockers(List<PredictBlocker> output)
        {
            foreach (var h in Items)
            {
                if (h.Hit) continue;
                int lane = h.TargetLane >= 0 ? h.Lane : h.Lane;
                output.Add(new PredictBlocker(lane, h.LogicZ, h.Def.HalfLength, h.Speed, h.Def.Jumpable && !h.Solid, h.TargetLane));
            }
        }

        public virtual void Tick(float dt, float playerSpeed)
        {
            for (int i = Items.Count - 1; i >= 0; i--)
            {
                var h = Items[i];
                h.Age += dt;
                var p = h.transform.localPosition;
                p.z -= (playerSpeed - h.Speed) * dt;
                if (h.KnockVelocity != Vector3.zero || h.KnockSpin != 0)
                {
                    h.KnockVelocity.y -= 25f * dt;
                    var b = h.Body.localPosition + h.KnockVelocity * dt;
                    if (b.y < 0)
                    {
                        b.y = 0;
                        h.KnockVelocity = new Vector3(h.KnockVelocity.x * 0.85f, Mathf.Abs(h.KnockVelocity.y) > 2 ? -h.KnockVelocity.y * 0.3f : 0, h.KnockVelocity.z * 0.85f);
                    }
                    h.Body.localPosition = b;
                    h.Body.Rotate(0, h.KnockSpin * dt * Mathf.Rad2Deg, 0, Space.Self);
                }
                Animate(h, dt, ref p);
                h.transform.localPosition = p;
                if (p.z < -Cfg.despawnBehind - h.Def.HalfLength || p.z > Cfg.spawnAhead + 150f)
                {
                    Services.Pools.Release(h.gameObject);
                    Items.RemoveAt(i);
                }
            }
        }

        protected virtual void Animate(Hazard h, float dt, ref Vector3 pos) { }

        /// <summary>Knock a hit hazard aside so the jeepney never gets stuck inside it.</summary>
        public void Knock(Hazard h, float fromX)
        {
            h.Hit = true;
            h.GetComponent<BoxCollider>().enabled = false;
            float away = Mathf.Sign(h.transform.localPosition.x - fromX);
            if (away == 0) away = 1;
            if (h.Def.Jumpable) { h.KnockVelocity = new Vector3(away * Rng.Range(3, 7), Rng.Range(6, 10), Rng.Range(8, 16)); h.KnockSpin = Rng.Range(-10, 10); }
            else { h.KnockVelocity = new Vector3(away * 3f, 1.5f, 8f); h.KnockSpin = away * 1.2f; }
        }
    }
}
