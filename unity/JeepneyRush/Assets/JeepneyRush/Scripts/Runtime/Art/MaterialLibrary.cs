using System.Collections.Generic;
using JeepneyRush.Logic.Geometry;
using UnityEngine;

namespace JeepneyRush
{
    /// <summary>
    /// Creates and caches every material. Base materials come from Resources/Materials
    /// (created by Jeepney Rush ▸ Setup Project with URP shaders so they are included in
    /// builds); if they are missing (e.g. first play in the editor) the shaders are found
    /// by name, falling back to built-in shaders.
    /// </summary>
    public static class MaterialLibrary
    {
        private static readonly Dictionary<string, Material> Cache = new Dictionary<string, Material>();
        private static Texture2D _palette;

        public static Texture2D PaletteTexture
        {
            get
            {
                if (_palette == null)
                {
                    _palette = new Texture2D(Palette.Size, Palette.Size, TextureFormat.RGBA32, false, false)
                    {
                        name = "JR_Palette", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp,
                    };
                    Palette.Shared.Dirty = true;
                }
                if (Palette.Shared.Dirty) RefreshPalette();
                return _palette;
            }
        }

        /// <summary>Uploads newly registered palette colours to the GPU.</summary>
        public static void RefreshPalette()
        {
            if (_palette == null) { var _ = PaletteTexture; return; }
            var px = new Color32[Palette.Size * Palette.Size];
            var cols = Palette.Shared.Colors;
            for (int i = 0; i < px.Length; i++)
                px[i] = i < cols.Count ? new Color32(cols[i].r, cols[i].g, cols[i].b, 255) : new Color32(255, 0, 255, 255);
            _palette.SetPixels32(px);
            _palette.Apply(false, false);
            Palette.Shared.Dirty = false;
        }

        private static Material Base(string resource, params string[] shaderNames)
        {
            var m = Resources.Load<Material>("Materials/" + resource);
            if (m != null) return new Material(m);
            foreach (var n in shaderNames)
            {
                var sh = Shader.Find(n);
                if (sh != null) return new Material(sh);
            }
            Debug.LogWarning($"[Materials] No shader found for {resource}; run Jeepney Rush ▸ Setup Project.");
            return new Material(Shader.Find("Hidden/InternalErrorShader"));
        }

        public static Material Lit(Texture tex, Color tint, float metallic, float smoothness)
        {
            var m = Base("JR_Lit", "Universal Render Pipeline/Lit", "Standard");
            SetTex(m, tex);
            SetColor(m, tint);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
            m.enableInstancing = true;
            return m;
        }

        public static Material Unlit(Texture tex, Color tint, bool transparent = false)
        {
            var m = transparent
                ? Base("JR_UnlitTransparent", "Universal Render Pipeline/Unlit", "Unlit/Transparent")
                : Base("JR_Unlit", "Universal Render Pipeline/Unlit", "Unlit/Texture");
            SetTex(m, tex);
            SetColor(m, tint);
            if (transparent && m.HasProperty("_Surface") && Resources.Load<Material>("Materials/JR_UnlitTransparent") == null)
            {
                // runtime fallback setup of a URP transparent surface
                m.SetFloat("_Surface", 1);
                m.SetFloat("_Blend", 0);
                m.SetOverrideTag("RenderType", "Transparent");
                m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 0);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
            return m;
        }

        private static void SetTex(Material m, Texture t)
        {
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", t);
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", t);
        }

        private static void SetColor(Material m, Color c)
        {
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        }

        public static Texture2D LoadTexture(string name)
        {
            var t = Resources.Load<Texture2D>("Textures/" + name);
            if (t == null)
            {
                Debug.LogWarning($"[Materials] Missing texture Resources/Textures/{name}.png");
                t = Texture2D.whiteTexture;
            }
            return t;
        }

        /// <summary>Material for a MeshData submesh key.</summary>
        public static Material ForKey(string key)
        {
            if (Cache.TryGetValue(key, out var m) && m != null) return m;
            var pal = PaletteTexture;
            switch (key)
            {
                case "paint": m = Lit(pal, Color.white, 0.1f, 0.55f); break;
                case "chrome": m = Lit(pal, Color.white, 1f, 0.9f); break;
                case "glass": m = Lit(pal, Color.white, 0.5f, 0.95f); break;
                case "gold": m = Lit(pal, Color.white, 1f, 0.8f); break;
                case "glow": m = Unlit(pal, Color.white); break;
                case "bubble": m = Unlit(pal, new Color(1f, 1f, 1f, 0.28f), true); break;
                case "water": m = Lit(pal, Color.white, 0.35f, 0.97f); break;
                // night lighting: invisible by day, fade in after sunset (see SetNight)
                case "nightglow": m = Unlit(pal, new Color(1f, 1f, 1f, _night), true); break;      // lit windows, lamp glow
                case "lightpool": m = Unlit(LoadTexture("light_pool"), new Color(1f, 0.86f, 0.6f, _night * 0.55f), true); break; // pools of street light
                case "beam": m = Unlit(LoadTexture("light_beam"), new Color(1f, 0.95f, 0.8f, _night * 0.6f), true); break;     // headlight beam
                default:
                    if (key.StartsWith("utex:")) // self-lit texture (billboards, LED signs): bright day and night
                    {
                        m = Unlit(LoadTexture(key.Substring(5)), Color.white);
                    }
                    else if (key.StartsWith("tex:"))
                    {
                        var parts = key.Substring(4).Split('|');
                        var tex = LoadTexture(parts[0]);
                        var tint = Color.white;
                        if (parts.Length > 1) ColorUtility.TryParseHtmlString(parts[1], out tint);
                        m = parts[0] == "para_zone" ? Unlit(tex, Color.white, true) : Lit(tex, tint, 0.15f, 0.45f);
                    }
                    else m = Lit(pal, Color.magenta, 0, 0);
                    break;
            }
            m.name = "JR_" + key;
            Cache[key] = m;
            return m;
        }

        private static float _night;

        /// <summary>
        /// 0 = day, 1 = night: fades in lit windows, street-light pools and headlight beams.
        /// Cheap on any phone (a few transparent quads, no real-time lights).
        /// </summary>
        public static void SetNight(float night)
        {
            night = Mathf.Clamp01(night);
            if (Mathf.Abs(night - _night) < 0.004f && night > 0.001f && night < 0.999f) return;
            _night = night;
            SetAlpha("nightglow", new Color(1f, 1f, 1f, night));
            SetAlpha("lightpool", new Color(1f, 0.86f, 0.6f, night * 0.55f));
            SetAlpha("beam", new Color(1f, 0.95f, 0.8f, night * 0.6f));
        }

        public static float Night => _night;

        private static void SetAlpha(string key, Color c)
        {
            if (Cache.TryGetValue(key, out var m) && m != null) SetColor(m, c);
        }

        /// <summary>Material for scrolling ground surfaces (road, sidewalk, curb).</summary>
        public static Material Surface(string texture, Vector2 tiling)
        {
            var key = "surface:" + texture;
            if (Cache.TryGetValue(key, out var m) && m != null) return m;
            var tex = LoadTexture(texture);
            if (tex != null && tex != Texture2D.whiteTexture) tex.wrapMode = TextureWrapMode.Repeat; // road surfaces scroll – must tile even without import settings
            m = Lit(tex, Color.white, 0f, 0.25f);
            m.mainTextureScale = tiling;
            m.name = "JR_" + key;
            Cache[key] = m;
            return m;
        }

        public static Material Particle()
        {
            if (Cache.TryGetValue("particle", out var m) && m != null) return m;
            m = Base("JR_Particle", "Universal Render Pipeline/Particles/Unlit", "Particles/Standard Unlit", "Sprites/Default");
            var dot = LoadTexture("particle_dot");
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", dot);
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", dot);
            m.name = "JR_particle";
            Cache["particle"] = m;
            return m;
        }
    }
}
