using JeepneyRush.Logic;
using UnityEngine;

namespace JeepneyRush
{
    /// <summary>
    /// A pooled traffic vehicle or obstacle. Its trigger collider is sized from the
    /// HazardCatalog numbers minus the same forgiveness margin the fairness tests use.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class Hazard : MonoBehaviour
    {
        public HazardKind Kind { get; private set; }
        public HazardDef Def { get; private set; }
        public string PoolKey { get; private set; }
        public int Lane;
        /// <summary>Second lane while changing lanes, else -1.</summary>
        public int TargetLane = -1;
        public float LaneT;
        public float SignalTime;
        public float Speed;
        public bool Solid;
        public bool Hit;
        /// <summary>Taho vendor / pedicab that stepped onto the sidewalk after a honk (no longer blocks).</summary>
        public bool SteppedAside;
        /// <summary>Tricycle pulling out from the sidewalk: visual sideways offset still to cover (m).</summary>
        public float PullOut;
        /// <summary>The HUD already announced this road event.</summary>
        public bool Warned;
        public float Age;
        public Vector3 KnockVelocity;
        public float KnockSpin;
        private Transform _body;

        /// <summary>Distance ahead in "logic" convention (negative = ahead). Unity +Z is ahead.</summary>
        public float LogicZ => -transform.localPosition.z;

        public static GameObject Create(HazardKind kind, int variant, string poolKey)
        {
            var model = ModelLibrary.Hazard(kind, variant);
            var root = new GameObject(poolKey);
            var body = ModelLibrary.Instantiate(model, "Body", root.transform, castShadows: !HazardCatalog.IsSoft(kind));
            var def = HazardCatalog.Get(kind);
            var col = root.AddComponent<BoxCollider>();
            col.isTrigger = true;
            float f = CollisionMath.Forgive;
            col.size = new Vector3(Mathf.Max(0.1f, (def.HalfWidth - f) * 2), def.Height, Mathf.Max(0.1f, (def.HalfLength - f) * 2));
            col.center = new Vector3(0, def.Height / 2f, 0);
            var rb = root.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            var h = root.AddComponent<Hazard>();
            h.Kind = kind;
            h.Def = def;
            h.PoolKey = poolKey;
            h._body = body.transform;
            return root;
        }

        public void ResetState(int lane, float speed, bool solid)
        {
            Lane = lane;
            TargetLane = -1;
            LaneT = 0;
            SignalTime = 0;
            Speed = speed;
            Solid = solid;
            Hit = false;
            SteppedAside = false;
            PullOut = 0;
            Warned = false;
            Age = Random.value * 10f;
            KnockVelocity = Vector3.zero;
            KnockSpin = 0;
            transform.localRotation = Quaternion.identity;
            _body.localPosition = Vector3.zero;
            _body.localRotation = Quaternion.identity;
            GetComponent<BoxCollider>().enabled = true;
        }

        public Transform Body => _body;
    }
}
