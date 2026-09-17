using System;
using System.IO;
using TillWinter.Unity;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace TillWinter.EditorTools.Build
{
    /// <summary>
    /// App icon from the game's own assets: the floating island with a bed of ripe crops, dusted with snow, under a
    /// frosted top edge, no text.
    /// Built in the IconRenderer scene (Assets/Art/Icon/IconRenderer.unity), rendered at 2048 and downsampled to 1024.
    /// The crop is matted from a black and a white render (alpha = 1 - (white - black)), so the Android adaptive
    /// foreground is truly transparent without relying on the pipeline's alpha output.
    /// Outputs: Icon.png (opaque composite, iOS/legacy), IconForeground.png + IconBackground.png (Android adaptive
    /// layers), SplashLogo.png (sprite); all store sizes are exported to Builds/Icons.
    /// </summary>
    public static class IconRenderer
    {
        public const string Dir = "Assets/Art/Icon/";
        public const string ScenePath = Dir + "IconRenderer.unity";
        public const string IconPath = Dir + "Icon.png";
        public const string ForegroundPath = Dir + "IconForeground.png";
        public const string BackgroundPath = Dir + "IconBackground.png";
        public const string SplashPath = Dir + "SplashLogo.png";
        private const string Resources = "Assets/TillWinter/Unity/Resources/";
        private const int Size = 1024;
        private const int Super = 2;
        /// <summary>Android adaptive icons show only the central ~66 % of the foreground: frame it looser.</summary>
        private const float AdaptiveOrtho = 2.9f;
        /// <summary>Opaque composite (iOS / legacy): the whole block stays inside iOS's rounded mask with a margin.</summary>
        private const float IconOrtho = 2.2f;
        /// <summary>Look-at height: centres the subject (block base to stem) in the frame, which the adaptive safe zone needs.</summary>
        private const float LookAtY = -0.35f;

        private static readonly int[] AndroidLegacy = { 48, 72, 96, 144, 192 };
        private static readonly int[] AndroidAdaptive = { 108, 162, 216, 324, 432 };
        private static readonly int[] Ios = { 20, 29, 40, 58, 60, 76, 80, 87, 120, 152, 167, 180, 1024 };

        [MenuItem("Till Winter/Render app icon")]
        public static void RenderMenu()
        {
            Render();
            Apply();
            ExportSizes();
        }

        public static void RenderBatch()
        {
            int code = 0;
            try { RenderMenu(); }
            catch (Exception e) { Debug.LogError("[Build] icon failed: " + e); code = 1; }
            EditorApplication.Exit(code);
        }

        public static void EnsureIcons(bool force)
        {
            if (force || !File.Exists(IconPath) || !File.Exists(ForegroundPath) || !File.Exists(BackgroundPath) || !File.Exists(SplashPath)) Render();
            Apply();
            ExportSizes();
        }

        // ------------------------------------------------------------------ render

        public static void Render()
        {
            var visuals = AssetDatabase.LoadAssetAtPath<VisualCatalog>(Resources + "VisualCatalog.asset");
            var palette = AssetDatabase.LoadAssetAtPath<Palette>(Resources + "Palette.asset");
            var seasons = AssetDatabase.LoadAssetAtPath<SeasonPalette>(Resources + "SeasonPalette.asset");
            if (visuals == null || palette == null || seasons == null) throw new Exception("VisualCatalog/Palette/SeasonPalette missing: run art-setup.bat first");
            Directory.CreateDirectory(Dir);
            palette.ApplyToMaterials(visuals.SlotMaterials);

            string previous = EditorSceneManager.GetActiveScene().path;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cam = BuildScene(visuals, seasons.Spring);
            EditorSceneManager.SaveScene(scene, ScenePath);

            int big = Size * Super;
            cam.orthographicSize = IconOrtho;
            var tight = Downsample(Matte(RenderRaw(cam, Color.black, big), RenderRaw(cam, Color.white, big)), big, Super);
            cam.orthographicSize = AdaptiveOrtho;
            var loose = Downsample(Matte(RenderRaw(cam, Color.black, big), RenderRaw(cam, Color.white, big)), big, Super);
            var background = Background(palette, seasons.Spring, Size);
            var icon = Composite(background, tight);

            Save(IconPath, icon, false, false);
            Save(ForegroundPath, loose, true, false);
            Save(BackgroundPath, background, false, false);
            Save(SplashPath, tight, true, true);
            if (!string.IsNullOrEmpty(previous) && File.Exists(previous)) EditorSceneManager.OpenScene(previous);
            Debug.Log("[Build] icon rendered from " + ScenePath);
        }

        private static Camera BuildScene(VisualCatalog v, SeasonLook look)
        {
            RenderSettings.fog = false;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = look.AmbientSky;
            RenderSettings.ambientEquatorColor = look.AmbientEquator;
            RenderSettings.ambientGroundColor = look.AmbientGround;
            var sh = new SphericalHarmonicsL2();
            sh.AddAmbientLight(look.AmbientEquator);
            RenderSettings.ambientProbe = sh;
            Shader.SetGlobalColor("_TW_SeasonTint", Color.white);

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = look.Light;
            sun.intensity = 1.25f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.45f;
            sun.transform.rotation = Quaternion.Euler(50f, -40f, 0f);

            // The farm itself, small: a floating island with a 2x2 bed of ripe crops, a pine and a rock hanging below,
            // lightly dusted with the coming winter.
            var root = new GameObject("IconSubject").transform;
            var island = new GameObject("Island");
            island.transform.SetParent(root, false);
            island.AddComponent<MeshFilter>().sharedMesh = DioramaView.BuildBlock(2, 0.42f, 1.0f, 0f);
            island.AddComponent<MeshRenderer>().sharedMaterials = new[] { v.SlotMaterial(PaletteSlot.Grass), v.SlotMaterial(PaletteSlot.SoilBlock) };
            Place(v.HangingRock, root, new Vector3(0.05f, -0.75f, 0.05f), 0f, 1.6f);
            int[] tiers = { 3, 0, 4, 1 };
            for (int i = 0; i < 4; i++)
            {
                var pos = new Vector3(i % 2 == 0 ? -0.5f : 0.5f, 0f, i < 2 ? -0.45f : 0.45f);
                var plot = Place(v.Plot, root, pos, 0f, 1f);
                if (plot == null) continue;
                foreach (var r in plot.GetComponentsInChildren<Renderer>(true))
                    if (r.sharedMaterial == v.SlotMaterial(PaletteSlot.SoilDry)) r.sharedMaterial = v.SlotMaterial(PaletteSlot.SoilWet);
                var cracks = plot.transform.Find("Cracks");
                if (cracks != null) cracks.gameObject.SetActive(false);
                var tier = v.Crops != null && tiers[i] < v.Crops.Length ? v.Crops[tiers[i]] : null;
                var anchor = plot.transform.Find("CropAnchor");
                Place(tier?.Ripe, anchor != null ? anchor : plot.transform, Vector3.zero, 15f * i, 1f);
            }
            if (v.Trees != null && v.Trees.Length > 2) Place(v.Trees[2], root, new Vector3(1.12f, 0f, 1.05f), 0f, 0.85f);
            Shader.SetGlobalFloat("_TW_Snow", 0.12f);
            Shader.SetGlobalFloat("_TW_Calm", 1f);

            var cam = new GameObject("IconCamera").AddComponent<Camera>();
            cam.orthographic = true;
            cam.transform.rotation = Quaternion.Euler(30f, -38f, 0f);
            cam.transform.position = new Vector3(0f, LookAtY, 0f) - cam.transform.forward * 12f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 40f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.allowHDR = false;
            cam.allowMSAA = false;
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false;
            data.antialiasing = AntialiasingMode.None;
            data.renderShadows = true;
            return cam;
        }

        private static GameObject Place(GameObject prefab, Transform parent, Vector3 pos, float yaw, float scale)
        {
            if (prefab == null) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = Vector3.one * scale;
            return go;
        }

        private static Color[] RenderRaw(Camera cam, Color clear, int size)
        {
            cam.backgroundColor = clear;
            var desc = new RenderTextureDescriptor(size, size, RenderTextureFormat.ARGB32, 24) { sRGB = true, msaaSamples = 1 };
            var rt = RenderTexture.GetTemporary(desc);
            cam.targetTexture = rt;
            var request = new RenderPipeline.StandardRequest { destination = rt };
            if (RenderPipeline.SupportsRenderRequest(cam, request)) RenderPipeline.SubmitRenderRequest(cam, request);
            else cam.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false, false);
            tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            tex.Apply();
            RenderTexture.active = previous;
            cam.targetTexture = null;
            RenderTexture.ReleaseTemporary(rt);
            var px = tex.GetPixels();
            Object.DestroyImmediate(tex);
            return px;
        }

        private static Color[] Matte(Color[] black, Color[] white)
        {
            var o = new Color[black.Length];
            for (int i = 0; i < o.Length; i++)
            {
                var b = black[i];
                var w = white[i];
                float a = Mathf.Clamp01(1f - ((w.r - b.r) + (w.g - b.g) + (w.b - b.b)) / 3f);
                o[i] = a > 0.004f ? new Color(Mathf.Clamp01(b.r / a), Mathf.Clamp01(b.g / a), Mathf.Clamp01(b.b / a), a) : Color.clear;
            }
            return o;
        }

        private static Color[] Downsample(Color[] src, int srcSize, int factor)
        {
            int size = srcSize / factor;
            var o = new Color[size * size];
            float n = factor * factor;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float r = 0, g = 0, b = 0, a = 0;
                for (int dy = 0; dy < factor; dy++)
                for (int dx = 0; dx < factor; dx++)
                {
                    var c = src[(y * factor + dy) * srcSize + x * factor + dx];
                    r += c.r * c.a; g += c.g * c.a; b += c.b * c.a; a += c.a; // premultiplied, so edges do not darken
                }
                o[y * size + x] = a > 0f ? new Color(r / a, g / a, b / a, a / n) : Color.clear;
            }
            return o;
        }

        /// <summary>Spring sky gradient with a frosted band along the top edge and a few flakes under it.</summary>
        private static Color[] Background(Palette p, SeasonLook look, int size)
        {
            var o = new Color[size * size];
            var frost = p.Snow;
            var rng = new System.Random(11);
            var flakes = new (float x, float y, float r)[24];
            for (int i = 0; i < flakes.Length; i++)
                flakes[i] = ((float)rng.NextDouble() * size, size * (0.62f + 0.2f * (float)rng.NextDouble()), 3f + 6f * (float)rng.NextDouble());
            for (int y = 0; y < size; y++)
            {
                float v = y / (size - 1f);
                for (int x = 0; x < size; x++)
                {
                    var c = Color.Lerp(look.SkyBottom, look.SkyTop, Mathf.SmoothStep(0f, 1f, v));
                    float edge = 0.86f + 0.018f * Mathf.Sin(x * 0.031f) + 0.012f * Mathf.Sin(x * 0.083f + 1.3f);
                    float t = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(edge - 0.015f, edge + 0.03f, v));
                    c = Color.Lerp(c, frost, t * 0.95f);
                    foreach (var f in flakes)
                    {
                        float d = Mathf.Sqrt((x - f.x) * (x - f.x) + (y - f.y) * (y - f.y));
                        if (d < f.r + 1f) c = Color.Lerp(c, frost, Mathf.Clamp01(f.r + 1f - d) * 0.9f);
                    }
                    c.a = 1f;
                    o[y * size + x] = c;
                }
            }
            return o;
        }

        private static Color[] Composite(Color[] bg, Color[] fg)
        {
            var o = new Color[bg.Length];
            for (int i = 0; i < o.Length; i++)
            {
                var c = Color.Lerp(bg[i], fg[i], fg[i].a);
                c.a = 1f;
                o[i] = c;
            }
            return o;
        }

        private static void Save(string path, Color[] px, bool alpha, bool sprite)
        {
            int size = Mathf.RoundToInt(Mathf.Sqrt(px.Length));
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false, false);
            tex.SetPixels(px);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = sprite ? TextureImporterType.Sprite : TextureImporterType.Default;
            if (sprite) imp.spriteImportMode = SpriteImportMode.Single;
            imp.sRGBTexture = true;
            imp.alphaSource = alpha ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            imp.alphaIsTransparency = alpha;
            imp.mipmapEnabled = false;
            imp.npotScale = TextureImporterNPOTScale.None;
            imp.maxTextureSize = Size;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.SaveAndReimport();
        }

        // ------------------------------------------------------------------ player settings

        public static void Apply()
        {
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            var fg = AssetDatabase.LoadAssetAtPath<Texture2D>(ForegroundPath);
            var bg = AssetDatabase.LoadAssetAtPath<Texture2D>(BackgroundPath);
            if (icon == null || fg == null || bg == null) throw new Exception("icon textures missing: run IconRenderer.Render");
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any); // iOS and every size Unity derives
#if UNITY_ANDROID
            SetAndroid(UnityEditor.Android.AndroidPlatformIconKind.Adaptive, bg, fg);
            SetAndroid(UnityEditor.Android.AndroidPlatformIconKind.Round, icon);
            SetAndroid(UnityEditor.Android.AndroidPlatformIconKind.Legacy, icon);
#endif
            var logo = AssetDatabase.LoadAssetAtPath<Sprite>(SplashPath);
            if (logo != null)
            {
                PlayerSettings.SplashScreen.logos = new[] { PlayerSettings.SplashScreenLogo.Create(2f, logo) };
                PlayerSettings.SplashScreen.drawMode = PlayerSettings.SplashScreen.DrawMode.UnityLogoBelow;
            }
        }

#if UNITY_ANDROID
        private static void SetAndroid(PlatformIconKind kind, params Texture2D[] layers)
        {
            var icons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
            foreach (var i in icons) i.SetTextures(layers);
            PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, kind, icons);
        }
#endif

        /// <summary>Every store size as PNG under Builds/Icons (gitignored): legacy + adaptive layers for Android, the iOS set.</summary>
        public static void ExportSizes()
        {
            var icon = Load(IconPath);
            var fg = Load(ForegroundPath);
            var bg = Load(BackgroundPath);
            string root = Path.Combine("Builds", "Icons");
            Directory.CreateDirectory(Path.Combine(root, "android"));
            Directory.CreateDirectory(Path.Combine(root, "ios"));
            foreach (int s in AndroidLegacy) Write(Path.Combine(root, "android", "ic_launcher_" + s + ".png"), Resize(icon, s));
            foreach (int s in AndroidAdaptive)
            {
                Write(Path.Combine(root, "android", "ic_launcher_foreground_" + s + ".png"), Resize(fg, s));
                Write(Path.Combine(root, "android", "ic_launcher_background_" + s + ".png"), Resize(bg, s));
            }
            foreach (int s in Ios) Write(Path.Combine(root, "ios", "Icon-" + s + ".png"), Resize(icon, s));
            Debug.Log("[Build] icon sizes exported to " + root);
        }

        private static Texture2D Load(string path)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            tex.LoadImage(File.ReadAllBytes(path));
            return tex;
        }

        /// <summary>Area-average downscale (sharp and alias-free for icon sizes).</summary>
        private static Texture2D Resize(Texture2D src, int size)
        {
            int n = src.width;
            var s = src.GetPixels();
            var o = new Color[size * size];
            float scale = n / (float)size;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int x0 = Mathf.FloorToInt(x * scale), x1 = Mathf.Max(x0 + 1, Mathf.FloorToInt((x + 1) * scale));
                int y0 = Mathf.FloorToInt(y * scale), y1 = Mathf.Max(y0 + 1, Mathf.FloorToInt((y + 1) * scale));
                float r = 0, g = 0, b = 0, a = 0;
                int count = 0;
                for (int yy = y0; yy < y1 && yy < n; yy++)
                for (int xx = x0; xx < x1 && xx < n; xx++)
                {
                    var c = s[yy * n + xx];
                    r += c.r * c.a; g += c.g * c.a; b += c.b * c.a; a += c.a;
                    count++;
                }
                o[y * size + x] = a > 0f ? new Color(r / a, g / a, b / a, a / count) : Color.clear;
            }
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false, false);
            tex.SetPixels(o);
            tex.Apply();
            return tex;
        }

        private static void Write(string path, Texture2D tex)
        {
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }
    }
}
