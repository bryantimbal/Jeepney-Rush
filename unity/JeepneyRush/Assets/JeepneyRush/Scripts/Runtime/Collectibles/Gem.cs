using UnityEngine;

namespace JeepneyRush
{
    /// <summary>Pooled road gem (premium currency) with a trigger collider.</summary>
    [RequireComponent(typeof(SphereCollider))]
    public sealed class Gem : MonoBehaviour
    {
        public bool Collected;
        public Transform Visual;

        public static GameObject Create()
        {
            var root = new GameObject("Gem");
            var vis = ModelLibrary.Instantiate(ModelLibrary.Get("gem", Logic.Geometry.PropBuilder.Gem), "Visual", root.transform, castShadows: false);
            var col = root.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 0.9f;
            var rb = root.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            var g = root.AddComponent<Gem>();
            g.Visual = vis.transform;
            return root;
        }
    }
}
