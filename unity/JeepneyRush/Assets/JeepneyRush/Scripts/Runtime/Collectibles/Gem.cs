using UnityEngine;

namespace JeepneyRush
{
    /// <summary>Pooled road gem (premium currency) with a trigger collider.</summary>
    [RequireComponent(typeof(SphereCollider))]
    public sealed class Gem : MonoBehaviour
    {
        public bool Collected;
        public Transform Visual;
        /// <summary>A parol token (Christmas event) rather than a gem.</summary>
        public bool IsParol;
        /// <summary>A sticker-album postcard rather than a gem.</summary>
        public bool IsPostcard;

        public static GameObject Create() => Make(false);
        public static GameObject CreateParol() => Make(true);
        public static GameObject CreatePostcard()
        {
            var root = Make(false, true);
            return root;
        }

        private static GameObject Make(bool parol, bool postcard = false)
        {
            var root = new GameObject(postcard ? "Postcard" : parol ? "Parol" : "Gem");
            var vis = postcard
                ? ModelLibrary.Instantiate(ModelLibrary.Get("postcard", Logic.Geometry.PropBuilder.Postcard), "Visual", root.transform, castShadows: false)
                : parol
                ? ModelLibrary.Instantiate(ModelLibrary.Get("parol_token", Logic.Geometry.PropBuilder.ParolToken), "Visual", root.transform, castShadows: false)
                : ModelLibrary.Instantiate(ModelLibrary.Get("gem", Logic.Geometry.PropBuilder.Gem), "Visual", root.transform, castShadows: false);
            var col = root.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 0.9f;
            var rb = root.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            var g = root.AddComponent<Gem>();
            g.Visual = vis.transform;
            g.IsParol = parol;
            g.IsPostcard = postcard;
            return root;
        }
    }
}
