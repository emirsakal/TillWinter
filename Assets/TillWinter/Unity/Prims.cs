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
        private static Mesh _cone, _coneDown;

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

        public static GameObject Primitive(PrimitiveType type, Transform parent, string name, Vector3 localPos, Vector3 localScale, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.On;
            r.receiveShadows = true;
            return go;
        }

        public static GameObject MeshObject(Mesh mesh, Transform parent, string name, Vector3 localPos, Vector3 localScale, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            return go;
        }

        /// <summary>Unit cone: base radius 1 at y=0, apex at y=1 (or flipped: base at y=1, apex at y=0).</summary>
        public static Mesh Cone(bool apexDown = false)
        {
            if (!apexDown && _cone != null) return _cone;
            if (apexDown && _coneDown != null) return _coneDown;
            const int segments = 16;
            var verts = new Vector3[segments * 3 + segments * 3];
            var normals = new Vector3[verts.Length];
            var tris = new int[segments * 6];
            float apexY = apexDown ? 0f : 1f;
            float baseY = apexDown ? 1f : 0f;
            for (int i = 0; i < segments; i++)
            {
                float a0 = i / (float)segments * Mathf.PI * 2f;
                float a1 = (i + 1) / (float)segments * Mathf.PI * 2f;
                var p0 = new Vector3(Mathf.Cos(a0), baseY, Mathf.Sin(a0));
                var p1 = new Vector3(Mathf.Cos(a1), baseY, Mathf.Sin(a1));
                var apex = new Vector3(0f, apexY, 0f);
                int v = i * 3;
                // side
                verts[v] = p0; verts[v + 1] = apex; verts[v + 2] = p1;
                var n = Vector3.Cross(apex - p0, p1 - p0).normalized;
                if (apexDown) n = -n;
                normals[v] = normals[v + 1] = normals[v + 2] = n;
                tris[i * 6 + 0] = v; tris[i * 6 + 1] = apexDown ? v + 2 : v + 1; tris[i * 6 + 2] = apexDown ? v + 1 : v + 2;
                // cap
                int c = segments * 3 + i * 3;
                var centre = new Vector3(0f, baseY, 0f);
                verts[c] = p0; verts[c + 1] = centre; verts[c + 2] = p1;
                var cn = apexDown ? Vector3.up : Vector3.down;
                normals[c] = normals[c + 1] = normals[c + 2] = cn;
                tris[i * 6 + 3] = c; tris[i * 6 + 4] = apexDown ? c + 1 : c + 2; tris[i * 6 + 5] = apexDown ? c + 2 : c + 1;
            }
            var mesh = new Mesh { name = apexDown ? "ConeDown" : "Cone", vertices = verts, normals = normals, triangles = tris };
            mesh.RecalculateBounds();
            if (apexDown) _coneDown = mesh; else _cone = mesh;
            return mesh;
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
