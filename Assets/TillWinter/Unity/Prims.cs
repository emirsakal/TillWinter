using UnityEngine;
using UnityEngine.Rendering;

namespace TillWinter.Unity
{
    /// <summary>Programmer-art helpers: flat materials, primitives without colliders, a cone mesh, gradient textures.</summary>
    public static class Prims
    {
        public static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        public static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private static Shader _lit, _unlit;

        public static Shader LitShader => _lit != null ? _lit : (_lit = Shader.Find("Universal Render Pipeline/Lit"));
        public static Shader UnlitShader => _unlit != null ? _unlit : (_unlit = Shader.Find("Universal Render Pipeline/Unlit"));

        public static Material Lit(Color color, float smoothness = 0.15f, bool emissive = false)
        {
            var m = new Material(LitShader);
            m.SetColor(BaseColorId, color);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", 0f);
            if (emissive)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor(EmissionColorId, Color.black);
            }
            return m;
        }

        public static Material Unlit(Color color)
        {
            var m = new Material(UnlitShader);
            m.SetColor(BaseColorId, color);
            return m;
        }

        /// <summary>Switches a URP Lit/Unlit/Particles material to alpha-blended transparent.</summary>
        public static Material MakeTransparent(Material m)
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_AlphaClip", 0f);
            m.DisableKeyword("_ALPHATEST_ON");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            return m;
        }

        /// <summary>Radial alpha gradient: fully opaque up to <paramref name="inner"/>, fading to 0 at <paramref name="outer"/> (0..1 of half-size).</summary>
        public static Texture2D RadialGradient(int size, float inner, float outer)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x + 0.5f - half) * (x + 0.5f - half) + (y + 0.5f - half) * (y + 0.5f - half)) / half;
                float a = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(inner, outer, d));
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        /// <summary>Transparent centre, opaque edges: frost creeping in from the screen border.</summary>
        public static Sprite EdgeFadeSprite(int size = 256, float inner = 0.45f)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Abs(x + 0.5f - half) / half, dy = Mathf.Abs(y + 0.5f - half) / half;
                float d = Mathf.Max(dx, dy);
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(inner, 1f, d));
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>A 1xN alpha ramp, opaque at one edge and clear at the other (top and bottom shades).</summary>
        public static Sprite VerticalFadeSprite(bool topOpaque, int height = 64)
        {
            var tex = new Texture2D(1, height, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < height; y++)
            {
                float a = topOpaque ? y / (height - 1f) : 1f - y / (height - 1f);
                tex.SetPixel(0, y, new Color(1f, 1f, 1f, a * a * (3f - 2f * a)));
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0f, 0f, 1f, height), new Vector2(0.5f, 0.5f));
        }

        /// <summary>Diagonal hatching (the frost span on the season bar), tiled by the Image.</summary>
        public static Sprite HatchSprite(int size = 32, int stripe = 6)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat };
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                bool on = ((x + y) % stripe) < stripe / 2;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, on ? 1f : 0.25f));
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        }

        public static Sprite CircleSprite(int size = 64)
        {
            var tex = RadialGradient(size, 0.9f, 1f);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        public static Sprite RoundedRectSprite(int size = 64, int radius = 18)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
                float cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                float a = Mathf.Clamp01(radius - d + 0.5f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply();
            int b = radius + 2;
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        }

        private static readonly System.Collections.Generic.Dictionary<PrimitiveType, Mesh> _meshes = new System.Collections.Generic.Dictionary<PrimitiveType, Mesh>();

        /// <summary>Shared mesh of a built-in primitive without keeping a primitive object around.</summary>
        public static Mesh BuiltinMesh(PrimitiveType type)
        {
            if (_meshes.TryGetValue(type, out var m) && m != null) return m;
            var go = GameObject.CreatePrimitive(type);
            m = go.GetComponent<MeshFilter>().sharedMesh;
            Object.Destroy(go);
            _meshes[type] = m;
            return m;
        }

        public static float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);
        public static float Damp(float current, float target, float speed, float dt) => Mathf.Lerp(current, target, 1f - Mathf.Exp(-speed * dt));
        public static Color Damp(Color current, Color target, float speed, float dt) => Color.Lerp(current, target, 1f - Mathf.Exp(-speed * dt));
        public static Vector3 Damp(Vector3 current, Vector3 target, float speed, float dt) => Vector3.Lerp(current, target, 1f - Mathf.Exp(-speed * dt));
    }
}
