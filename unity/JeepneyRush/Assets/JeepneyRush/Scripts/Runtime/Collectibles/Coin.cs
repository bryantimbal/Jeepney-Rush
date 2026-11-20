using UnityEngine;

namespace JeepneyRush
{
    /// <summary>Pooled spinning coin with a trigger collider.</summary>
    [RequireComponent(typeof(SphereCollider))]
    public sealed class Coin : MonoBehaviour
    {
        public float BaseY;
        public bool Collected;
        public Transform Visual;

        public static GameObject Create()
        {
            var root = new GameObject("Coin");
            var vis = ModelLibrary.Instantiate(ModelLibrary.Coin(), "Visual", root.transform, castShadows: false);
            var col = root.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 0.7f;
            var rb = root.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            var c = root.AddComponent<Coin>();
            c.Visual = vis.transform;
            return root;
        }
    }
}
