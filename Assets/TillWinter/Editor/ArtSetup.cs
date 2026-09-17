using System;
using System.Collections.Generic;
using System.IO;
using TillWinter.Unity;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.U2D;
using Object = UnityEngine.Object;

namespace TillWinter.EditorTools
{
    /// <summary>
    /// Art pass setup (art-setup.bat / menu). Builds every visual asset from code so nothing is hand-edited:
    /// import settings, shared TW_Toon materials, prefabs (Kenney FBX + primitives), the node icon atlas,
    /// Palette / SeasonPalette / VisualCatalog, FarmDecor prefab links, URP quality settings and the decal feature.
    /// Re-running rebuilds prefabs and materials; Palette/SeasonPalette values are kept if the assets exist.
    /// </summary>
    public static class ArtSetup
    {
        private const string Kenney = "Assets/Art/Kenney/";
        private const string MaterialsDir = "Assets/Art/Materials/";
        private const string PrefabsDir = "Assets/Art/Prefabs/";
        private const string MeshesDir = "Assets/Art/Meshes/";
        private const string TexturesDir = "Assets/Art/Textures/";
        private const string ResourcesDir = "Assets/TillWinter/Unity/Resources/";
        private const string IconsDir = Kenney + "game-icons/Icons";
        /// <summary>URP decal projectors did not render onto the field with the orthographic camera in this setup; the ring stays a
        /// textured disc (same soft-edge + inner-glow texture). Flip to try decals again: the feature and material are built when true.</summary>
        private const bool UseDecals = false;

        private static Shader _toon, _sky;
        private static Material _flat, _foodMap, _charMap, _golden, _soilBlock, _skyMat, _ringDecal;
        private static Material[] _slots;
        private static Mesh _cone;
        private static Palette _palette;
        private static readonly List<string> _log = new List<string>();

        public static void Run()
        {
            int code = 0;
            try { RunAll(); }
            catch (Exception e) { Debug.LogError("[ArtSetup] failed: " + e); code = 1; }
            EditorApplication.Exit(code);
        }

        [MenuItem("Till Winter/Art setup (materials, prefabs, catalog)")]
        public static void RunAll()
        {
            foreach (var d in new[] { MaterialsDir, PrefabsDir, MeshesDir, TexturesDir, ResourcesDir })
                Directory.CreateDirectory(d);
            ConfigureImports();
            _toon = Shader.Find("TillWinter/TW_Toon");
            _sky = Shader.Find("TillWinter/TW_Sky");
            if (_toon == null || _sky == null) throw new Exception("TW_Toon / TW_Sky shaders not found (compile error?)");
            // Atlas packing and URP asset edits reload the asset database; do them before holding any managed references.
            BuildIconAtlas();
            bool decal = ConfigureUrp();
            _palette = LoadOrCreate<Palette>(ResourcesDir + "Palette.asset");
            LoadOrCreate<SeasonPalette>(ResourcesDir + "SeasonPalette.asset");
            CreateMaterials();
            _cone = SaveMesh(ConeMesh(), "Cone");
            var catalog = LoadOrCreate<VisualCatalog>(ResourcesDir + "VisualCatalog.asset");
            BuildPrefabs(catalog);
            catalog.UseDecalRing = decal && _ringDecal != null;
            LinkDecor(catalog);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            File.WriteAllLines("TestResults/art-setup-bindings.txt", _log);
            Debug.Log("[ArtSetup] done");
        }

        // ------------------------------------------------------------------ imports

        private static void ConfigureImports()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { Kenney }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var imp = AssetImporter.GetAtPath(path) as ModelImporter;
                if (imp == null) continue;
                bool changed = imp.isReadable || imp.meshCompression != ModelImporterMeshCompression.Medium || imp.importAnimation != path.Contains("mini-characters") ||
                               imp.materialImportMode != ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                imp.isReadable = false;
                imp.meshCompression = ModelImporterMeshCompression.Medium;
                imp.importAnimation = path.Contains("mini-characters");
                imp.importBlendShapes = false;
                imp.importCameras = false;
                imp.importLights = false;
                imp.generateSecondaryUV = false;
                // Embedded materials are read once at prefab-build time to pick palette slots; prefabs reference the shared TW_Toon materials only.
                imp.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                imp.materialLocation = ModelImporterMaterialLocation.InPrefab;
                if (changed) imp.SaveAndReimport();
            }
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { IconsDir }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var imp = AssetImporter.GetAtPath(path) as TextureImporter;
                if (imp == null || imp.textureType == TextureImporterType.Sprite) continue;
                imp.textureType = TextureImporterType.Sprite;
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.mipmapEnabled = false;
                imp.alphaIsTransparency = true;
                imp.SaveAndReimport();
            }
            foreach (var guid in AssetDatabase.FindAssets("colormap t:Texture2D", new[] { Kenney }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var imp = AssetImporter.GetAtPath(path) as TextureImporter;
                if (imp == null || (imp.filterMode == FilterMode.Point && !imp.mipmapEnabled)) continue;
                imp.filterMode = FilterMode.Point; // colormaps are flat colour cells; no bleeding
                imp.mipmapEnabled = false;
                imp.SaveAndReimport();
            }
        }

        // ------------------------------------------------------------------ materials

        private static void CreateMaterials()
        {
            _flat = Material("TW_Flat", _toon, m => { });
            _foodMap = Material("TW_FoodColormap", _toon, m => m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Kenney + "food-kit/Models/colormap.png")));
            _charMap = Material("TW_CharacterColormap", _toon, m => m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Kenney + "mini-characters/Models/colormap.png")));
            _golden = Material("TW_Golden", _toon, m => { m.SetColor("_BaseColor", _palette.Golden); m.SetColor("_EmissionColor", _palette.GoldenGlow); m.SetFloat("_Wind", 0.06f); });
            _skyMat = Material("TW_Sky", _sky, m => { });
            _ringDecal = CreateRingDecal();
            // One material per palette slot: identical meshes on the same slot GPU-instance; colours come from the Palette at boot.
            var slots = (PaletteSlot[])Enum.GetValues(typeof(PaletteSlot));
            _slots = new Material[slots.Length];
            foreach (var slot in slots)
                _slots[(int)slot] = Material("TW_" + slot, _toon, m =>
                {
                    m.SetColor("_BaseColor", slot == PaletteSlot.White ? Color.white : _palette.Get(slot));
                    m.SetFloat("_Weathered", Palette.IsWeathered(slot) ? 1f : 0f);
                    m.SetFloat("_SeasonTint", Palette.IsSeasonTinted(slot) ? 1f : 0f);
                    m.SetFloat("_Wind", Sways(slot) ? 0.06f : 0f);
                });
            _soilBlock = _slots[(int)PaletteSlot.SoilBlock];
            foreach (var m in new[] { _flat, _foodMap, _charMap, _golden }) m.enableInstancing = true;
            foreach (var m in _slots) m.enableInstancing = true;
            _foodMap.SetFloat("_Weathered", 0f);
            _charMap.SetFloat("_Weathered", 0f);
        }

        private static Material Material(string name, Shader shader, Action<Material> setup)
        {
            string path = MaterialsDir + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = shader;
            setup(m);
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Material CreateRingDecal()
        {
            // Soft-edged ring with a subtle inner glow, baked into one texture.
            const int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x + 0.5f - half) * (x + 0.5f - half) + (y + 0.5f - half) * (y + 0.5f - half)) / half;
                float edge = 1f - Mathf.SmoothStep(0.9f, 1f, d);
                float rim = Mathf.SmoothStep(0.55f, 0.82f, d) * edge;
                float glow = (1f - Mathf.SmoothStep(0f, 0.7f, d)) * 0.28f;
                float a = Mathf.Clamp01(rim + glow);
                var c = Color.Lerp(new Color(1f, 0.96f, 0.75f), new Color(1f, 0.98f, 0.9f), rim);
                px[y * size + x] = new Color32((byte)(c.r * 255), (byte)(c.g * 255), (byte)(c.b * 255), (byte)(a * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            string texPath = TexturesDir + "RingDecal.png";
            File.WriteAllBytes(texPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(texPath);
            var ti = AssetImporter.GetAtPath(texPath) as TextureImporter;
            if (ti != null && (ti.mipmapEnabled || ti.wrapMode != TextureWrapMode.Clamp))
            {
                ti.mipmapEnabled = false;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.alphaIsTransparency = true;
                ti.SaveAndReimport();
            }
            var texAsset = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            var decalShader = Shader.Find("Shader Graphs/Decal");
            if (decalShader == null) { Debug.LogWarning("[ArtSetup] URP decal shader not found; ring falls back to the disc"); return null; }
            var m = Material("TW_RingDecal", decalShader, mat =>
            {
                if (mat.HasProperty("Base_Map")) mat.SetTexture("Base_Map", texAsset);
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", texAsset);
                if (mat.HasProperty("Base_Color")) mat.SetColor("Base_Color", Color.white);
                if (mat.HasProperty("_AffectAlbedo")) mat.SetFloat("_AffectAlbedo", 1f);
                if (mat.HasProperty("_AffectNormal")) mat.SetFloat("_AffectNormal", 0f);
                if (mat.HasProperty("_AffectMetallicSpecular")) mat.SetFloat("_AffectMetallicSpecular", 0f);
                if (mat.HasProperty("_AffectSmoothness")) mat.SetFloat("_AffectSmoothness", 0f);
                if (mat.HasProperty("_AffectEmission")) mat.SetFloat("_AffectEmission", 0f);
                mat.EnableKeyword("_MATERIAL_AFFECTS_ALBEDO");
                mat.DisableKeyword("_MATERIAL_AFFECTS_NORMAL");
            });
            return m;
        }

        /// <summary>Soft dark disc used as a fake contact shadow (URP shadows are off on the Low tier).</summary>
        private static Material CreateBlobShadowMaterial()
        {
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x + 0.5f - half) * (x + 0.5f - half) + (y + 0.5f - half) * (y + 0.5f - half)) / half;
                float a = 1f - Mathf.SmoothStep(0.2f, 1f, d);
                px[y * size + x] = new Color32(0, 0, 0, (byte)(a * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            string texPath = TexturesDir + "BlobShadow.png";
            File.WriteAllBytes(texPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(texPath);
            var ti = AssetImporter.GetAtPath(texPath) as TextureImporter;
            if (ti != null && (ti.mipmapEnabled || !ti.alphaIsTransparency))
            {
                ti.mipmapEnabled = false;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.alphaIsTransparency = true;
                ti.SaveAndReimport();
            }
            var texAsset = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            var shadowShader = Shader.Find("TillWinter/TW_Shadow");
            if (shadowShader == null) throw new Exception("TillWinter/TW_Shadow shader not found (compile error?)");
            return Material("TW_BlobShadow", shadowShader, m =>
            {
                m.SetTexture("_BaseMap", texAsset);
                m.SetColor("_BaseColor", new Color(0f, 0f, 0f, 0.32f));
            });
        }

        private static GameObject BuildBlobShadow()
        {
            var root = new GameObject("BlobShadow");
            var quad = Primitive(PrimitiveType.Quad, root.transform, "Disc", new Vector3(0f, 0.02f, 0f), Vector3.one, CreateBlobShadowMaterial());
            quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var r = quad.GetComponent<Renderer>();
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return root;
        }

        /// <summary>A small pond with a stone rim: the island had a well but no water.</summary>
        /// <summary>A stout post with a cap, for the fence ends.</summary>
        private static GameObject BuildFencePost()
        {
            var (root, b) = Root("FencePost");
            Prim(PrimitiveType.Cube, root.transform, "Post", new Vector3(0f, 0.3f, 0f), new Vector3(0.12f, 0.6f, 0.12f), b, PaletteSlot.Wood);
            Prim(PrimitiveType.Cube, root.transform, "Cap", new Vector3(0f, 0.62f, 0f), new Vector3(0.17f, 0.05f, 0.17f), b, PaletteSlot.WoodLight);
            return root;
        }

        /// <summary>A pole and a cloth on a pivot ("Cloth") the flag view waves.</summary>
        private static GameObject BuildFlag()
        {
            var (root, b) = Root("Flag");
            Prim(PrimitiveType.Cylinder, root.transform, "Pole", new Vector3(0f, 0.8f, 0f), new Vector3(0.04f, 0.8f, 0.04f), b, PaletteSlot.WoodLight);
            Prim(PrimitiveType.Sphere, root.transform, "Knob", new Vector3(0f, 1.62f, 0f), Vector3.one * 0.07f, b, PaletteSlot.Golden);
            var pivot = new GameObject("Cloth").transform;
            pivot.SetParent(root.transform, false);
            pivot.localPosition = new Vector3(0f, 1.42f, 0f);
            var cloth = Prim(PrimitiveType.Cube, pivot, "Face", new Vector3(0.24f, 0f, 0f), new Vector3(0.46f, 0.28f, 0.02f), b, PaletteSlot.Roof, null, null, false);
            cloth.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.On;
            Prim(PrimitiveType.Cube, pivot, "Stripe", new Vector3(0.24f, 0f, 0f), new Vector3(0.47f, 0.06f, 0.025f), b, PaletteSlot.Wall, null, null, false);
            return root;
        }

        /// <summary>A hen: round body, tail, head with comb and beak; "Head" and "Body" move in the view.</summary>
        private static GameObject BuildChicken()
        {
            var (root, b) = Root("Chicken");
            var body = new GameObject("Body").transform;
            body.SetParent(root.transform, false);
            Prim(PrimitiveType.Sphere, body, "Belly", new Vector3(0f, 0.12f, 0f), new Vector3(0.16f, 0.14f, 0.2f), b, PaletteSlot.Wall);
            var tail = Prim(PrimitiveType.Cube, body, "Tail", new Vector3(0f, 0.19f, -0.09f), new Vector3(0.06f, 0.1f, 0.05f), b, PaletteSlot.Wall);
            tail.transform.localRotation = Quaternion.Euler(-30f, 0f, 0f);
            var head = new GameObject("Head").transform;
            head.SetParent(body, false);
            head.localPosition = new Vector3(0f, 0.2f, 0.08f);
            Prim(PrimitiveType.Sphere, head, "Skull", Vector3.zero, Vector3.one * 0.09f, b, PaletteSlot.Wall);
            Prim(PrimitiveType.Cube, head, "Comb", new Vector3(0f, 0.05f, 0f), new Vector3(0.015f, 0.04f, 0.05f), b, PaletteSlot.Roof);
            Prim(PrimitiveType.Cube, head, "Beak", new Vector3(0f, -0.005f, 0.05f), new Vector3(0.025f, 0.02f, 0.035f), b, PaletteSlot.Beak);
            Prim(PrimitiveType.Sphere, head, "EyeL", new Vector3(-0.03f, 0.012f, 0.03f), Vector3.one * 0.016f, b, PaletteSlot.Eye);
            Prim(PrimitiveType.Sphere, head, "EyeR", new Vector3(0.03f, 0.012f, 0.03f), Vector3.one * 0.016f, b, PaletteSlot.Eye);
            for (int i = 0; i < 2; i++)
                Prim(PrimitiveType.Cube, root.transform, "Leg" + i, new Vector3(i == 0 ? -0.035f : 0.035f, 0.03f, 0f), new Vector3(0.015f, 0.06f, 0.015f), b, PaletteSlot.Beak);
            return root;
        }

        /// <summary>A curled, sleeping cat with a tail the view swishes ("Tail").</summary>
        private static GameObject BuildCat()
        {
            var (root, b) = Root("Cat");
            Prim(PrimitiveType.Sphere, root.transform, "Body", new Vector3(0f, 0.07f, 0f), new Vector3(0.24f, 0.13f, 0.2f), b, PaletteSlot.WoodLight);
            Prim(PrimitiveType.Sphere, root.transform, "Head", new Vector3(0.1f, 0.1f, 0.05f), Vector3.one * 0.11f, b, PaletteSlot.WoodLight);
            var earL = ConeObj(root.transform, "EarL", new Vector3(0.08f, 0.14f, 0.03f), new Vector3(0.025f, 0.045f, 0.025f), b, PaletteSlot.WoodLight);
            earL.transform.localRotation = Quaternion.Euler(0f, 0f, 15f);
            var earR = ConeObj(root.transform, "EarR", new Vector3(0.13f, 0.14f, 0.08f), new Vector3(0.025f, 0.045f, 0.025f), b, PaletteSlot.WoodLight);
            earR.transform.localRotation = Quaternion.Euler(0f, 0f, -15f);
            var tail = new GameObject("Tail").transform;
            tail.SetParent(root.transform, false);
            tail.localPosition = new Vector3(-0.1f, 0.04f, 0f);
            var t = Prim(PrimitiveType.Capsule, tail, "Swish", new Vector3(-0.02f, 0f, 0.07f), new Vector3(0.04f, 0.08f, 0.04f), b, PaletteSlot.WoodLight);
            t.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            return root;
        }

        /// <summary>One unit of irrigation channel along z: wooden sides and water between (stretched by the diorama).</summary>
        private static GameObject BuildChannel()
        {
            var (root, b) = Root("Channel");
            Prim(PrimitiveType.Cube, root.transform, "Bed", new Vector3(0f, 0.02f, 0f), new Vector3(0.26f, 0.04f, 1f), b, PaletteSlot.Wood);
            var water = Prim(PrimitiveType.Cube, root.transform, "Water", new Vector3(0f, 0.045f, 0f), new Vector3(0.16f, 0.02f, 1f), b, PaletteSlot.Water, null, null, false);
            water.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            return root;
        }

        /// <summary>A sunflower: stem, two leaves and a big head facing the camera.</summary>
        private static GameObject BuildSunflower()
        {
            var (root, b) = Root("Sunflower");
            Prim(PrimitiveType.Cylinder, root.transform, "Stem", new Vector3(0f, 0.3f, 0f), new Vector3(0.03f, 0.3f, 0.03f), b, PaletteSlot.Leaf);
            var leaf = Prim(PrimitiveType.Cube, root.transform, "Leaf", new Vector3(0.06f, 0.25f, 0f), new Vector3(0.12f, 0.015f, 0.06f), b, PaletteSlot.Leaf);
            leaf.transform.localRotation = Quaternion.Euler(0f, 0f, 25f);
            var head = new GameObject("Head").transform;
            head.SetParent(root.transform, false);
            head.localPosition = new Vector3(0f, 0.62f, 0f);
            head.localRotation = Quaternion.Euler(-60f, 0f, 0f);
            Prim(PrimitiveType.Cylinder, head, "Petals", Vector3.zero, new Vector3(0.22f, 0.012f, 0.22f), b, PaletteSlot.Crop5);
            Prim(PrimitiveType.Cylinder, head, "Seeds", new Vector3(0f, 0.012f, 0f), new Vector3(0.11f, 0.012f, 0.11f), b, PaletteSlot.Wood);
            return root;
        }

        private static GameObject BuildSteppingStone()
        {
            var (root, b) = Root("SteppingStone");
            var s = Prim(PrimitiveType.Cylinder, root.transform, "Stone", new Vector3(0f, 0.01f, 0f), new Vector3(0.24f, 0.012f, 0.19f), b, PaletteSlot.Stone);
            s.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            return root;
        }

        private static GameObject BuildPond()
        {
            var (root, b) = Root("Pond");
            var water = Prim(PrimitiveType.Cylinder, root.transform, "Water", new Vector3(0f, 0.04f, 0f), new Vector3(1.05f, 0.04f, 0.8f), b, PaletteSlot.Water, null, null, false);
            water.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            var seed = new System.Random(11);
            for (int i = 0; i < 8; i++)
            {
                float a = i / 8f * Mathf.PI * 2f;
                var stone = Prim(PrimitiveType.Cube, root.transform, "Stone" + i, new Vector3(Mathf.Cos(a) * 0.56f, 0.04f, Mathf.Sin(a) * 0.44f), new Vector3(0.17f, 0.09f, 0.15f), b, PaletteSlot.Stone);
                stone.transform.localRotation = Quaternion.Euler(0f, (float)(seed.NextDouble() * 90.0), 0f);
            }
            return root;
        }

        /// <summary>Bee: striped body and two wings (WingL/WingR) the critter view flaps.</summary>
        private static GameObject BuildBee()
        {
            var (root, b) = Root("Bee");
            Prim(PrimitiveType.Sphere, root.transform, "Body", Vector3.zero, new Vector3(0.07f, 0.06f, 0.09f), b, PaletteSlot.Crop5, null, null, false);
            Prim(PrimitiveType.Cube, root.transform, "Stripe", new Vector3(0f, 0f, -0.01f), new Vector3(0.072f, 0.062f, 0.018f), b, PaletteSlot.Eye, null, null, false);
            Prim(PrimitiveType.Sphere, root.transform, "Head", new Vector3(0f, 0.005f, 0.05f), Vector3.one * 0.04f, b, PaletteSlot.Eye, null, null, false);
            var l = Prim(PrimitiveType.Cube, root.transform, "WingL", new Vector3(-0.035f, 0.035f, 0f), new Vector3(0.05f, 0.004f, 0.035f), b, PaletteSlot.Cloud, null, null, false);
            var r = Prim(PrimitiveType.Cube, root.transform, "WingR", new Vector3(0.035f, 0.035f, 0f), new Vector3(0.05f, 0.004f, 0.035f), b, PaletteSlot.Cloud, null, null, false);
            foreach (var go in new[] { l, r }) go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            return root;
        }

        /// <summary>Pond frog: a squat body, two eye bumps and a throat the view puffs ("Throat").</summary>
        private static GameObject BuildFrog()
        {
            var (root, b) = Root("Frog");
            Prim(PrimitiveType.Sphere, root.transform, "Body", new Vector3(0f, 0.06f, 0f), new Vector3(0.17f, 0.11f, 0.19f), b, PaletteSlot.LeafDark);
            Prim(PrimitiveType.Sphere, root.transform, "Throat", new Vector3(0f, 0.05f, 0.08f), new Vector3(0.09f, 0.06f, 0.06f), b, PaletteSlot.Sprout);
            for (int i = 0; i < 2; i++)
            {
                float x = i == 0 ? -0.045f : 0.045f;
                Prim(PrimitiveType.Sphere, root.transform, "Bump" + i, new Vector3(x, 0.115f, 0.05f), Vector3.one * 0.05f, b, PaletteSlot.LeafDark);
                Prim(PrimitiveType.Sphere, root.transform, "Eye" + i, new Vector3(x, 0.125f, 0.065f), Vector3.one * 0.028f, b, PaletteSlot.Eye);
                Prim(PrimitiveType.Sphere, root.transform, "Foot" + i, new Vector3(x * 2f, 0.02f, -0.05f), new Vector3(0.06f, 0.03f, 0.08f), b, PaletteSlot.Leaf);
            }
            return root;
        }

        /// <summary>Butterfly: a body and two wings the view flaps (children named WingL/WingR, like the crow).</summary>
        private static GameObject BuildButterfly()
        {
            var (root, b) = Root("Butterfly");
            Prim(PrimitiveType.Cube, root.transform, "Body", new Vector3(0f, 0f, 0f), new Vector3(0.025f, 0.025f, 0.1f), b, PaletteSlot.Wood, null, null, false);
            var l = Prim(PrimitiveType.Cube, root.transform, "WingL", new Vector3(-0.06f, 0.01f, 0f), new Vector3(0.11f, 0.008f, 0.085f), b, PaletteSlot.Flower, null, null, false);
            l.transform.localRotation = Quaternion.Euler(0f, 0f, 18f);
            var r = Prim(PrimitiveType.Cube, root.transform, "WingR", new Vector3(0.06f, 0.01f, 0f), new Vector3(0.11f, 0.008f, 0.085f), b, PaletteSlot.Flower, null, null, false);
            r.transform.localRotation = Quaternion.Euler(0f, 0f, -18f);
            foreach (var rend in root.GetComponentsInChildren<Renderer>()) rend.shadowCastingMode = ShadowCastingMode.Off;
            return root;
        }

        private static Mesh SaveMesh(Mesh mesh, string name)
        {
            string path = MeshesDir + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) return existing;
            var copy = Object.Instantiate(mesh);
            copy.name = name;
            AssetDatabase.CreateAsset(copy, path);
            return copy;
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a != null) return a;
            a = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(a, path);
            Debug.Log("[ArtSetup] created " + path);
            return a;
        }

        // ------------------------------------------------------------------ prefabs

        private static void BuildPrefabs(VisualCatalog c)
        {
            c.Golden = _golden;
            c.SlotMaterials = _slots;
            c.SoilBlock = _soilBlock;
            c.Sky = _skyMat;
            c.RingDecal = _ringDecal;
            c.RingTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturesDir + "RingDecal.png");

            c.Plot = Save("Plot", BuildPlot());
            c.Crops = new CropTierVisual[6];
            // One plant on a whole plot read as a speck from the play camera: every stage is planted as a small bed on
            // the two soil ridges (four plants, two for the bushy crops, one pumpkin on its own trailing vine).
            c.Crops[0] = Tier("Carrot", Sprouts(), Clump(Kit("nature-kit", "crops_leafsStageB", 0.36f), Bed4, 3), Clump(Kit("nature-kit", "crop_carrot", 0.56f), Bed4, 4));
            c.Crops[1] = Tier("Tomato", Sprouts(), Clump(Kit("nature-kit", "plant_bush", 0f, 0.3f), Bed2, 5), Clump(TomatoPlant(), Bed2, 6)); // the mid stage stays smaller than the ripe plant
            c.Crops[2] = Tier("Corn", Clump(Kit("nature-kit", "crops_cornStageA", 0.24f), Bed4, 7), Clump(Kit("nature-kit", "crops_cornStageB", 0.6f), Bed4, 8), Clump(Kit("nature-kit", "crops_cornStageD", 1.1f), Bed4, 9));
            c.Crops[3] = Tier("Pumpkin", Sprouts(), Clump(Kit("nature-kit", "plant_bushLarge", 0f, 0.34f), Bed2, 10), PumpkinPatch());
            c.Crops[4] = Tier("Grapes", Sprouts(), GrapeVine(false), GrapeVine(true));
            c.Crops[5] = Tier("Wheat", Clump(Kit("nature-kit", "crops_wheatStageA", 0.22f), Bed4, 11), Clump(Kit("nature-kit", "crops_wheatStageA", 0.52f), Bed4, 12), Clump(Kit("nature-kit", "crops_wheatStageB", 0.82f), Bed4, 13));
            c.RingDots = Save("RingDots", BuildRingDots());

            var chars = new[] { "character-male-a", "character-female-a", "character-male-b", "character-female-b", "character-male-c", "character-female-c" };
            c.Apprentices = new GameObject[6];
            for (int i = 0; i < 6; i++) c.Apprentices[i] = Save("Apprentice" + i, Apprentice(chars[i], i));
            c.Tractor = Save("Tractor", BuildTractor());
            c.Crow = Save("Crow", BuildCrow());
            c.Cloud = Save("Cloud", BuildCloud());
            c.Greenhouse = Save("Greenhouse", BuildGreenhouse());

            c.Houses = new GameObject[3];
            for (int i = 0; i < 3; i++) c.Houses[i] = Save("House" + i, BuildHouse(i));
            c.Well = Save("Well", BuildWell());
            c.Windmill = Save("Windmill", BuildWindmill());
            c.Fence = Save("Fence", Kit("nature-kit", "fence_simple", 0f, 1f));
            c.FenceGate = Save("FenceGate", Kit("nature-kit", "fence_gate", 0f, 1f));
            c.Trees = new[]
            {
                Save("TreeDefault", Kit("nature-kit", "tree_default", 1.35f)),
                Save("TreeOak", Kit("nature-kit", "tree_oak", 1.3f)),
                Save("TreePine", Kit("nature-kit", "tree_pineRoundA", 1.5f)),
                Save("TreeSmall", Kit("nature-kit", "tree_small", 1.0f)),
            };
            c.Bush = Save("Bush", Kit("nature-kit", "plant_bushLarge", 0.5f));
            c.Rock = Save("Rock", Kit("nature-kit", "rock_smallA", 0f, 0.5f));
            c.PathTile = Save("PathTile", BuildPathTile());
            c.Signpost = Save("Signpost", BuildSignpost());
            c.Flowerbed = Save("Flowerbed", BuildFlowerbed());
            c.Barrel = Save("Barrel", BuildBarrels());
            c.LogStack = Save("LogStack", Kit("nature-kit", "log_stack", 0.4f));
            c.Mushroom = Save("Mushroom", Kit("nature-kit", "mushroom_red", 0.28f));
            c.Stump = Save("Stump", Kit("nature-kit", "stump_round", 0.3f));
            c.TreeAutumn = Save("TreeAutumn", Kit("nature-kit", "tree_default_fall", 1.35f));
            c.Pond = Save("Pond", BuildPond());
            c.Kennel = Save("Kennel", BuildKennel());
            c.FencePost = Save("FencePost", BuildFencePost());
            c.Flag = Save("Flag", BuildFlag());
            c.Chicken = Save("Chicken", BuildChicken());
            c.Cat = Save("Cat", BuildCat());
            c.Channel = Save("Channel", BuildChannel());
            c.Sunflower = Save("Sunflower", BuildSunflower());
            c.SteppingStone = Save("SteppingStone", BuildSteppingStone());
            c.Dog = Save("Dog", BuildDog());
            c.GrassTuft = Save("GrassTuft", BuildGrassTuft());
            c.Pebbles = Save("Pebbles", BuildPebbles());
            c.Flowers = new[]
            {
                Save("FlowerRed", Kit("nature-kit", "flower_redA", 0.2f)),
                Save("FlowerYellow", Kit("nature-kit", "flower_yellowA", 0.2f)),
                Save("FlowerPurple", Kit("nature-kit", "flower_purpleA", 0.2f)),
            };
            c.HangingRock = Save("HangingRock", BuildHangingRock());
            c.Butterfly = Save("Butterfly", BuildButterfly());
            c.Bee = Save("Bee", BuildBee());
            c.Frog = Save("Frog", BuildFrog());
            c.BlobShadow = Save("BlobShadow", BuildBlobShadow());
        }

        /// <summary>Plant spots on a plot: on the two soil ridges (z = +-RidgeZ), inside the soil tile.</summary>
        private const float RidgeZ = 0.19f;
        private static readonly Vector3[] Bed4 = { new Vector3(-0.22f, 0f, -RidgeZ), new Vector3(0.2f, 0f, -RidgeZ), new Vector3(-0.2f, 0f, RidgeZ), new Vector3(0.22f, 0f, RidgeZ) };
        private static readonly Vector3[] Bed2 = { new Vector3(-0.2f, 0f, -0.12f), new Vector3(0.2f, 0f, 0.12f) };

        private static GameObject Sprouts() => Clump(Kit("nature-kit", "crops_leafsStageA", 0.18f), Bed4, 2);

        /// <summary>
        /// Plants one single-plant prefab at every spot, each with its own turn and a little size variation. All copies
        /// stay under the one root binder, so the plot view still tints and lights the whole bed at once.
        /// </summary>
        private static GameObject Clump(GameObject single, Vector3[] spots, int seed)
        {
            var binder = single.GetComponent<PaletteBinder>();
            var plant = new GameObject("Plant").transform;
            plant.SetParent(single.transform, false);
            for (int i = single.transform.childCount - 1; i >= 0; i--)
            {
                var child = single.transform.GetChild(i);
                if (child != plant) child.SetParent(plant, false);
            }
            var originals = plant.GetComponentsInChildren<Renderer>(true);
            var bindings = new List<PaletteBinder.Binding>(binder.Bindings);
            var rnd = new System.Random(seed);
            for (int i = 0; i < spots.Length; i++)
            {
                var t = i == 0 ? plant : Object.Instantiate(plant.gameObject, single.transform).transform;
                t.name = "Plant" + i;
                t.localPosition = spots[i] + new Vector3((float)(rnd.NextDouble() - 0.5) * 0.04f, 0f, (float)(rnd.NextDouble() - 0.5) * 0.04f);
                t.localRotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f);
                t.localScale = Vector3.one * (0.9f + (float)rnd.NextDouble() * 0.2f);
                if (i == 0) continue;
                var copies = t.GetComponentsInChildren<Renderer>(true);
                foreach (var bd in bindings)
                {
                    int k = System.Array.IndexOf(originals, bd.Renderer);
                    if (k >= 0) binder.Add(copies[k], bd.Slot, bd.Tint, bd.Weathered, bd.MaterialIndex);
                }
            }
            return single;
        }

        /// <summary>The pumpkin sits on its own vine: one big fruit with trailing leaves across the plot.</summary>
        private static GameObject PumpkinPatch()
        {
            var root = Kit("nature-kit", "crop_pumpkin", 0.5f);
            root.name = "PumpkinPatch";
            var b = root.GetComponent<PaletteBinder>();
            root.transform.Find("Model").localPosition = new Vector3(0.08f, 0f, 0.04f);
            var rnd = new System.Random(21);
            var vine = Prim(PrimitiveType.Cylinder, root.transform, "Vine", new Vector3(-0.17f, 0.02f, -0.08f), new Vector3(0.025f, 0.2f, 0.025f), b, PaletteSlot.LeafDark);
            vine.transform.localRotation = Quaternion.Euler(0f, 30f, 90f);
            for (int i = 0; i < 5; i++)
            {
                float a = -2.4f + i * 0.9f;
                var leaf = Prim(PrimitiveType.Cube, root.transform, "Leaf" + i,
                    new Vector3(-0.1f + Mathf.Cos(a) * 0.24f, 0.03f + i * 0.004f, Mathf.Sin(a) * 0.2f),
                    new Vector3(0.17f, 0.018f, 0.15f), b, i % 2 == 0 ? PaletteSlot.Leaf : PaletteSlot.LeafDark);
                leaf.transform.localRotation = Quaternion.Euler((float)(rnd.NextDouble() - 0.5) * 16f, (float)rnd.NextDouble() * 90f, (float)(rnd.NextDouble() - 0.5) * 16f);
            }
            return root;
        }

        /// <summary>Beads around the player's ring (unit radius), turned by RingView.</summary>
        private static GameObject BuildRingDots()
        {
            var (root, b) = Root("RingDots");
            for (int i = 0; i < 10; i++)
            {
                float a = i * Mathf.PI * 2f / 10f;
                var d = Prim(PrimitiveType.Sphere, root.transform, "Dot" + i, new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)), Vector3.one * (i % 2 == 0 ? 0.075f : 0.05f), b, PaletteSlot.Cloud, null, null, false);
                var r = d.GetComponent<Renderer>();
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            return root;
        }

        private static CropTierVisual Tier(string name, GameObject sprout, GameObject growing, GameObject ripe) => new CropTierVisual
        {
            Sprout = Save(name + "Sprout", sprout),
            Growing = Save(name + "Growing", growing),
            Ripe = Save(name + "Ripe", ripe),
        };

        private static GameObject Save(string name, GameObject go)
        {
            go.name = name;
            string path = PrefabsDir + name + ".prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        /// <summary>Kenney FBX -> root with the model fitted to <paramref name="height"/> (or <paramref name="width"/>), materials swapped to TW_Toon, palette bindings from the embedded material names/colours.</summary>
        private static GameObject Kit(string kit, string model, float height, float width = 0f)
        {
            string path = Kenney + kit + "/Models/" + model + ".fbx";
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null) throw new Exception("model missing: " + path);
            var root = new GameObject(model);
            var inst = Object.Instantiate(asset);
            inst.name = "Model";
            inst.transform.SetParent(root.transform, false);
            if (kit != "mini-characters") foreach (var a in inst.GetComponentsInChildren<Animator>()) Object.DestroyImmediate(a);
            foreach (var col in inst.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(col);
            var binder = root.AddComponent<PaletteBinder>();
            Material shared = kit == "food-kit" ? _foodMap : kit == "mini-characters" ? _charMap : _flat;
            foreach (var r in inst.GetComponentsInChildren<Renderer>())
            {
                var mats = r.sharedMaterials;
                var replaced = new Material[mats.Length];
                for (int i = 0; i < mats.Length; i++)
                {
                    PaletteSlot slot = shared == _flat ? SlotFor(mats[i], model) : PaletteSlot.White;
                    replaced[i] = shared == _flat ? _slots[(int)slot] : shared;
                    binder.Add(r, slot, null, true, i);
                    _log.Add(model + " / " + (mats[i] != null ? mats[i].name : "null") + " -> " + slot);
                }
                r.sharedMaterials = replaced;
                r.shadowCastingMode = ShadowCastingMode.On;
                r.receiveShadows = true;
            }
            Fit(inst.transform, height, width);
            return root;
        }

        /// <summary>Embedded Kenney material -> palette slot: name hints first, nearest palette colour otherwise.</summary>
        /// <summary>Explicit model/material -> slot pairs where the kit's material name would mislead the heuristics.</summary>
        private static readonly Dictionary<string, PaletteSlot> SlotOverrides = new Dictionary<string, PaletteSlot>
        {
            { "crop_carrot/leafsFall", PaletteSlot.Crop0 }, { "crop_carrot/grass", PaletteSlot.Sprout },
            { "crop_pumpkin/leafsFall", PaletteSlot.Crop3 }, { "crop_pumpkin/grass", PaletteSlot.Sprout },
            { "crops_cornStageA/grass", PaletteSlot.Sprout }, { "crops_cornStageB/grass", PaletteSlot.Sprout },
            { "crops_cornStageD/corn", PaletteSlot.Crop2 }, { "crops_cornStageD/grass", PaletteSlot.Sprout },
            { "crops_leafsStageA/grass", PaletteSlot.Sprout }, { "crops_leafsStageB/grass", PaletteSlot.Sprout },
            { "crops_wheatStageA/grass", PaletteSlot.Sprout }, { "crops_wheatStageB/_defaultMat", PaletteSlot.Crop5 }, { "crops_wheatStageB/woodInner", PaletteSlot.WoodLight },
            { "flower_purpleA/colorPurple", PaletteSlot.Crop4 }, { "flower_yellowA/colorYellow", PaletteSlot.Crop2 },
            { "mushroom_red/_defaultMat", PaletteSlot.Wall },
            { "rock_largeA/dirt", PaletteSlot.Stone },
            { "fence_simple/wood", PaletteSlot.WoodLight }, { "fence_gate/wood", PaletteSlot.WoodLight },
            { "log_stack/woodInner", PaletteSlot.WoodLight }, { "stump_round/woodInner", PaletteSlot.WoodLight },
            { "plant_bushLarge/grass", PaletteSlot.Leaf },
        };

        private static PaletteSlot SlotFor(Material m, string model)
        {
            string n = (m != null ? m.name : "").ToLowerInvariant();
            if (SlotOverrides.TryGetValue(model + "/" + (m != null ? m.name : ""), out var forced)) return forced;
            if (n.Contains("leaf") || n.Contains("foliage") || n.Contains("bush")) return n.Contains("dark") ? PaletteSlot.LeafDark : PaletteSlot.Leaf;
            if (n.Contains("wood") || n.Contains("bark") || n.Contains("trunk") || n.Contains("log")) return n.Contains("light") ? PaletteSlot.WoodLight : PaletteSlot.Wood;
            if (n.Contains("stone") || n.Contains("rock")) return PaletteSlot.Stone;
            if (n.Contains("dirt") || n.Contains("soil") || n.Contains("ground")) return PaletteSlot.SoilDry;
            if (n.Contains("grass")) return PaletteSlot.Grass;
            if (n.Contains("path") || n.Contains("sand")) return PaletteSlot.Path;
            if (n.Contains("snow") || n.Contains("white")) return PaletteSlot.Snow;
            Color c = m != null ? (m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : m.color) : Color.magenta;
            PaletteSlot[] candidates =
            {
                PaletteSlot.Leaf, PaletteSlot.LeafDark, PaletteSlot.Sprout, PaletteSlot.Wood, PaletteSlot.WoodLight, PaletteSlot.Stone,
                PaletteSlot.Crop0, PaletteSlot.Crop1, PaletteSlot.Crop2, PaletteSlot.Crop3, PaletteSlot.Crop4, PaletteSlot.Crop5,
                PaletteSlot.Flower, PaletteSlot.Roof, PaletteSlot.Wall, PaletteSlot.SoilDry, PaletteSlot.Path, PaletteSlot.Snow, PaletteSlot.Metal,
            };
            PaletteSlot best = PaletteSlot.Leaf;
            float bestD = float.MaxValue;
            foreach (var s in candidates)
            {
                var p = _palette.Get(s);
                float d = (p.r - c.r) * (p.r - c.r) + (p.g - c.g) * (p.g - c.g) + (p.b - c.b) * (p.b - c.b);
                if (d < bestD) { bestD = d; best = s; }
            }
            return best;
        }

        /// <summary>Scales the model so its bounds height (or width) hits the target, feet at y=0, centred on x/z.</summary>
        private static void Fit(Transform model, float height, float width)
        {
            model.localScale = Vector3.one;
            model.localPosition = Vector3.zero;
            var renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            float scale = height > 0f ? height / Mathf.Max(0.001f, b.size.y) : width / Mathf.Max(0.001f, Mathf.Max(b.size.x, b.size.z));
            model.localScale = Vector3.one * scale;
            model.localPosition = -new Vector3(b.center.x, b.min.y, b.center.z) * scale;
        }

        private static GameObject Prim(PrimitiveType type, Transform parent, string name, Vector3 pos, Vector3 scale, PaletteBinder binder, PaletteSlot slot, Material mat = null, Color? tint = null, bool weathered = true)
        {
            var go = Primitive(type, parent, name, pos, scale, mat ?? _slots[(int)slot]);
            binder.Add(go.GetComponent<Renderer>(), slot, tint, weathered);
            return go;
        }

        private static GameObject ConeObj(Transform parent, string name, Vector3 pos, Vector3 scale, PaletteBinder binder, PaletteSlot slot)
        {
            var go = MeshObject(_cone, parent, name, pos, scale, _slots[(int)slot]);
            binder.Add(go.GetComponent<Renderer>(), slot);
            return go;
        }

        private static (GameObject root, PaletteBinder binder) Root(string name)
        {
            var go = new GameObject(name);
            return (go, go.AddComponent<PaletteBinder>());
        }

        /// <summary>A tuft of grass: thin blades leaning out from one root. Scattered along the island's rim.</summary>
        private static GameObject BuildGrassTuft()
        {
            var (root, b) = Root("GrassTuft");
            var rnd = new System.Random(5);
            for (int i = 0; i < 6; i++)
            {
                float a = (i * 60f + (float)rnd.NextDouble() * 25f) * Mathf.Deg2Rad;
                float h = 0.12f + (float)rnd.NextDouble() * 0.08f;
                var blade = ConeObj(root.transform, "Blade" + i, new Vector3(Mathf.Cos(a) * 0.03f, 0f, Mathf.Sin(a) * 0.03f),
                    new Vector3(0.022f, h, 0.022f), b, i % 2 == 0 ? PaletteSlot.Leaf : PaletteSlot.LeafDark);
                blade.transform.localRotation = Quaternion.Euler(Mathf.Sin(a) * 22f, 0f, -Mathf.Cos(a) * 22f);
            }
            return root;
        }

        /// <summary>Two flat stones.</summary>
        private static GameObject BuildPebbles()
        {
            var (root, b) = Root("Pebbles");
            Prim(PrimitiveType.Sphere, root.transform, "A", new Vector3(0f, 0.015f, 0f), new Vector3(0.12f, 0.05f, 0.09f), b, PaletteSlot.Stone);
            Prim(PrimitiveType.Sphere, root.transform, "B", new Vector3(0.09f, 0.01f, 0.05f), new Vector3(0.07f, 0.035f, 0.06f), b, PaletteSlot.Stone);
            return root;
        }

        /// <summary>
        /// Rock and a root hanging from the underside of the island: it floats, so the underside should look like torn
        /// earth rather than a clean cut. Hangs below its root transform.
        /// </summary>
        private static GameObject BuildHangingRock()
        {
            var (root, b) = Root("HangingRock");
            ConeObj(root.transform, "Rock", Vector3.zero, new Vector3(0.42f, 0.7f, 0.42f), b, PaletteSlot.SoilBlock)
                .transform.localRotation = Quaternion.Euler(180f, 20f, 0f);
            ConeObj(root.transform, "Tip", new Vector3(0.16f, 0f, 0.05f), new Vector3(0.18f, 0.45f, 0.18f), b, PaletteSlot.Stone)
                .transform.localRotation = Quaternion.Euler(180f, 0f, 0f);
            Prim(PrimitiveType.Cylinder, root.transform, "Root", new Vector3(-0.2f, -0.24f, 0.02f), new Vector3(0.022f, 0.24f, 0.022f), b, PaletteSlot.Wood)
                .transform.localRotation = Quaternion.Euler(0f, 0f, 12f);
            return root;
        }

        /// <summary>A little gabled kennel for beside the house. No kit has one, so it is primitives like the well.</summary>
        private static GameObject BuildKennel()
        {
            var (root, b) = Root("Kennel");
            // Small, dark wood and a green roof: with the house's red roof and its size it read as a second house.
            Prim(PrimitiveType.Cube, root.transform, "Body", new Vector3(0f, 0.17f, 0f), new Vector3(0.46f, 0.34f, 0.52f), b, PaletteSlot.Wood);
            Prim(PrimitiveType.Cube, root.transform, "Doorway", new Vector3(0f, 0.13f, -0.265f), new Vector3(0.22f, 0.26f, 0.04f), b, PaletteSlot.Crow); // the Eye slot is the white of an eye
            var roofL = Prim(PrimitiveType.Cube, root.transform, "RoofL", new Vector3(-0.13f, 0.42f, 0f), new Vector3(0.35f, 0.06f, 0.6f), b, PaletteSlot.LeafDark);
            roofL.transform.localRotation = Quaternion.Euler(0f, 0f, 38f);
            var roofR = Prim(PrimitiveType.Cube, root.transform, "RoofR", new Vector3(0.13f, 0.42f, 0f), new Vector3(0.35f, 0.06f, 0.6f), b, PaletteSlot.LeafDark);
            roofR.transform.localRotation = Quaternion.Euler(0f, 0f, -38f);
            return root;
        }

        /// <summary>
        /// The farm dog, with the heart and speech bubble it shows when patted built in and hidden. They ride in the
        /// prefab rather than becoming a VfxId, because one cosmetic flourish does not earn a pooled particle system.
        /// </summary>
        private static GameObject BuildDog()
        {
            var (root, b) = Root("Dog");
            Prim(PrimitiveType.Capsule, root.transform, "Body", new Vector3(0f, 0.2f, 0f), new Vector3(0.22f, 0.2f, 0.22f), b, PaletteSlot.WoodLight)
                .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Prim(PrimitiveType.Sphere, root.transform, "Head", new Vector3(0f, 0.34f, 0.22f), Vector3.one * 0.23f, b, PaletteSlot.WoodLight);
            Prim(PrimitiveType.Sphere, root.transform, "Snout", new Vector3(0f, 0.3f, 0.34f), Vector3.one * 0.12f, b, PaletteSlot.Wall);
            Prim(PrimitiveType.Sphere, root.transform, "Nose", new Vector3(0f, 0.31f, 0.4f), Vector3.one * 0.06f, b, PaletteSlot.Eye);
            Prim(PrimitiveType.Sphere, root.transform, "EyeL", new Vector3(-0.07f, 0.38f, 0.32f), Vector3.one * 0.05f, b, PaletteSlot.Eye);
            Prim(PrimitiveType.Sphere, root.transform, "EyeR", new Vector3(0.07f, 0.38f, 0.32f), Vector3.one * 0.05f, b, PaletteSlot.Eye);
            Prim(PrimitiveType.Cube, root.transform, "EarL", new Vector3(-0.11f, 0.44f, 0.2f), new Vector3(0.07f, 0.11f, 0.05f), b, PaletteSlot.Wood);
            Prim(PrimitiveType.Cube, root.transform, "EarR", new Vector3(0.11f, 0.44f, 0.2f), new Vector3(0.07f, 0.11f, 0.05f), b, PaletteSlot.Wood);
            for (int i = 0; i < 4; i++)
                Prim(PrimitiveType.Cube, root.transform, "Leg" + i, new Vector3(i % 2 == 0 ? -0.09f : 0.09f, 0.06f, i < 2 ? 0.12f : -0.12f),
                    new Vector3(0.07f, 0.12f, 0.07f), b, PaletteSlot.WoodLight);

            // The tail is its own child so the view can wag it.
            var tail = new GameObject("Tail");
            tail.transform.SetParent(root.transform, false);
            tail.transform.localPosition = new Vector3(0f, 0.3f, -0.2f);
            Prim(PrimitiveType.Cube, tail.transform, "Wag", new Vector3(0f, 0.07f, -0.03f), new Vector3(0.05f, 0.17f, 0.05f), b, PaletteSlot.Wood)
                .transform.localRotation = Quaternion.Euler(-35f, 0f, 0f);

            // Hidden until patted.
            var bubble = new GameObject("Bubble");
            bubble.transform.SetParent(root.transform, false);
            bubble.transform.localPosition = new Vector3(0f, 0.72f, 0.1f);
            Prim(PrimitiveType.Sphere, bubble.transform, "Puff", Vector3.zero, new Vector3(0.36f, 0.26f, 0.2f), b, PaletteSlot.Cloud);
            Prim(PrimitiveType.Sphere, bubble.transform, "Tail", new Vector3(-0.1f, -0.15f, 0f), Vector3.one * 0.08f, b, PaletteSlot.Cloud);
            Prim(PrimitiveType.Sphere, bubble.transform, "HeartL", new Vector3(-0.05f, 0.04f, -0.12f), Vector3.one * 0.11f, b, PaletteSlot.Crop1);
            Prim(PrimitiveType.Sphere, bubble.transform, "HeartR", new Vector3(0.05f, 0.04f, -0.12f), Vector3.one * 0.11f, b, PaletteSlot.Crop1);
            Prim(PrimitiveType.Cube, bubble.transform, "HeartTip", new Vector3(0f, -0.05f, -0.12f), new Vector3(0.11f, 0.11f, 0.02f), b, PaletteSlot.Crop1)
                .transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            bubble.SetActive(false);
            return root;
        }

        /// <summary>Slots that lean in the breeze: foliage, flowers and every crop (a crop mesh mixes these slots).</summary>
        private static bool Sways(PaletteSlot slot) =>
            slot == PaletteSlot.Leaf || slot == PaletteSlot.LeafDark || slot == PaletteSlot.Sprout || slot == PaletteSlot.Flower ||
            (slot >= PaletteSlot.Crop0 && slot <= PaletteSlot.Golden);

        private static GameObject BuildPlot()
        {
            var (root, b) = Root("Plot");
            // The camera looks down, so a gap in z is foreshortened to nothing: the tile is shorter in z than in x
            // to leave a visible gap on all four sides of a plot.
            var soil = Prim(PrimitiveType.Cube, root.transform, "Soil", new Vector3(0f, 0.08f, 0f), new Vector3(0.94f, 0.16f, 0.8f), b, PaletteSlot.SoilDry);
            soil.GetComponent<Renderer>().receiveShadows = true;
            soil.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            // Two raised rows with a furrow between and around them: the flat box read as a tile, not a bed.
            // Same slot as the soil (Dry/Wet follows), a touch lighter so the rows catch the sun.
            for (int i = 0; i < 2; i++)
            {
                float z = i == 0 ? -RidgeZ : RidgeZ;
                var ridge = Prim(PrimitiveType.Cube, root.transform, "Ridge" + i, new Vector3(0f, 0.18f, z), new Vector3(0.86f, 0.04f, 0.2f), b, PaletteSlot.SoilDry, null, new Color(1.07f, 1.05f, 1.03f));
                ridge.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.On;
                ridge.GetComponent<Renderer>().receiveShadows = true;
                var crest = Prim(PrimitiveType.Cube, root.transform, "Crest" + i, new Vector3(0f, 0.205f, z), new Vector3(0.8f, 0.012f, 0.12f), b, PaletteSlot.SoilDry, null, new Color(1.13f, 1.1f, 1.06f));
                crest.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
            var cracks = new GameObject("Cracks").transform;
            cracks.SetParent(root.transform, false);
            var seed = new System.Random(7);
            for (int i = 0; i < 3; i++)
            {
                float x = (float)(seed.NextDouble() * 0.6 - 0.3), z = (float)(seed.NextDouble() * 0.6 - 0.3);
                float len = 0.18f + (float)seed.NextDouble() * 0.2f;
                var crack = Prim(PrimitiveType.Cube, cracks, "Crack" + i, new Vector3(x, 0.212f, z), new Vector3(len, 0.006f, 0.025f), b, PaletteSlot.Wood, null, null, false);
                crack.transform.localRotation = Quaternion.Euler(0f, (float)seed.NextDouble() * 180f, 0f);
                crack.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
            // Wet sheen: a few tiny droplets (toggled by the view).
            var drops = new GameObject("Droplets").transform;
            drops.SetParent(root.transform, false);
            for (int i = 0; i < 3; i++)
            {
                var d = Prim(PrimitiveType.Sphere, drops, "Drop" + i, new Vector3(-0.25f + i * 0.25f, 0.2f, (i - 1) * 0.2f), Vector3.one * 0.045f, b, PaletteSlot.Water, null, null, false);
                d.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
            drops.gameObject.SetActive(false);
            var anchor = new GameObject("CropAnchor").transform;
            anchor.SetParent(root.transform, false);
            anchor.localPosition = new Vector3(0f, 0.2f, 0f);
            // "Ready": a small gem that bobs over a ripe bed (shown, coloured and moved by PlotView).
            var mark = new GameObject("RipeMark").transform;
            mark.SetParent(root.transform, false);
            mark.localPosition = new Vector3(0f, 0.9f, 0f);
            var up = ConeObj(mark, "Top", Vector3.zero, new Vector3(0.075f, 0.07f, 0.075f), b, PaletteSlot.Crop0);
            var down = ConeObj(mark, "Bottom", Vector3.zero, new Vector3(0.075f, 0.1f, 0.075f), b, PaletteSlot.Crop0);
            down.transform.localRotation = Quaternion.Euler(180f, 0f, 0f);
            foreach (var r in mark.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
            mark.gameObject.SetActive(false);
            return root;
        }

        private static GameObject TomatoPlant()
        {
            var root = Kit("food-kit", "tomato", 0.3f);
            root.name = "TomatoPlant";
            var b = root.GetComponent<PaletteBinder>();
            var model = root.transform.Find("Model");
            model.localPosition += new Vector3(0f, 0.28f, 0f);
            Prim(PrimitiveType.Cylinder, root.transform, "Stem", new Vector3(0f, 0.16f, 0f), new Vector3(0.05f, 0.16f, 0.05f), b, PaletteSlot.LeafDark);
            var leaf = Prim(PrimitiveType.Cube, root.transform, "Leaf", new Vector3(-0.12f, 0.2f, 0.04f), new Vector3(0.22f, 0.02f, 0.1f), b, PaletteSlot.Leaf);
            leaf.transform.localRotation = Quaternion.Euler(0f, 20f, 30f);
            var leaf2 = Prim(PrimitiveType.Cube, root.transform, "Leaf2", new Vector3(0.12f, 0.1f, -0.04f), new Vector3(0.2f, 0.02f, 0.1f), b, PaletteSlot.Leaf);
            leaf2.transform.localRotation = Quaternion.Euler(0f, -30f, -25f);
            // Tomatoes grow tied to a stake.
            Prim(PrimitiveType.Cylinder, root.transform, "Stake", new Vector3(0.06f, 0.26f, -0.05f), new Vector3(0.025f, 0.26f, 0.025f), b, PaletteSlot.WoodLight);
            Prim(PrimitiveType.Cylinder, root.transform, "Tie", new Vector3(0.06f, 0.3f, -0.05f), new Vector3(0.04f, 0.008f, 0.04f), b, PaletteSlot.Wall);
            return root;
        }

        private static GameObject GrapeVine(bool ripe)
        {
            var (root, b) = Root(ripe ? "GrapesRipe" : "GrapesGrowing");
            Prim(PrimitiveType.Cylinder, root.transform, "PostA", new Vector3(-0.34f, 0.3f, 0f), new Vector3(0.05f, 0.3f, 0.05f), b, PaletteSlot.WoodLight);
            Prim(PrimitiveType.Cylinder, root.transform, "PostB", new Vector3(0.34f, 0.3f, 0f), new Vector3(0.05f, 0.3f, 0.05f), b, PaletteSlot.WoodLight);
            Prim(PrimitiveType.Cube, root.transform, "Bar", new Vector3(0f, 0.55f, 0f), new Vector3(0.76f, 0.03f, 0.03f), b, PaletteSlot.WoodLight);
            var leafA = Prim(PrimitiveType.Cube, root.transform, "LeafA", new Vector3(-0.1f, 0.5f, 0.03f), new Vector3(0.2f, 0.02f, 0.14f), b, PaletteSlot.Leaf);
            leafA.transform.localRotation = Quaternion.Euler(10f, 20f, 15f);
            var leafB = Prim(PrimitiveType.Cube, root.transform, "LeafB", new Vector3(0.12f, 0.42f, -0.03f), new Vector3(0.18f, 0.02f, 0.12f), b, PaletteSlot.Leaf);
            leafB.transform.localRotation = Quaternion.Euler(-10f, -30f, -15f);
            if (ripe)
            {
                var bunch = Kit("food-kit", "grapes", 0.26f);
                bunch.name = "Bunch";
                bunch.transform.SetParent(root.transform, false);
                bunch.transform.localPosition = new Vector3(-0.12f, 0.22f, 0.04f);
                var bunch2 = Kit("food-kit", "grapes", 0.22f);
                bunch2.name = "Bunch2";
                bunch2.transform.SetParent(root.transform, false);
                bunch2.transform.localPosition = new Vector3(0.13f, 0.26f, 0.04f);
                // Kit roots carry their own binder; fold the fruit into the vine's so the plot view lights it.
                foreach (var extra in new[] { bunch, bunch2 })
                {
                    var eb = extra.GetComponent<PaletteBinder>();
                    foreach (var bd in eb.Bindings) b.Add(bd.Renderer, bd.Slot, bd.Tint, bd.Weathered, bd.MaterialIndex);
                    Object.DestroyImmediate(eb);
                }
            }
            return root;
        }

        private static GameObject Apprentice(string model, int index)
        {
            var root = Kit("mini-characters", model, 0.52f); // 0.72 hid the whole plot the helper stood on
            root.name = "Apprentice" + index;
            var b = root.GetComponent<PaletteBinder>();
            AddIdleAnimator(root.transform.Find("Model").gameObject, Kenney + "mini-characters/Models/" + model + ".fbx");
            var slot = (PaletteSlot)((int)PaletteSlot.Cloth0 + index);
            var hat = new GameObject("Hat").transform;
            hat.SetParent(root.transform, false);
            hat.localPosition = new Vector3(0f, 0.505f, 0f);
            Prim(PrimitiveType.Cylinder, hat, "Brim", Vector3.zero, new Vector3(0.26f, 0.011f, 0.26f), b, slot);
            Prim(PrimitiveType.Cylinder, hat, "Crown", new Vector3(0f, 0.036f, 0f), new Vector3(0.123f, 0.036f, 0.123f), b, slot);
            // "Picking this": a thought puff with the crop's colour in it, shown while harvesting. Its own binder, so the
            // view can tint the crop dot without touching the character.
            var (bubble, bb) = Root("Bubble");
            bubble.transform.SetParent(root.transform, false);
            bubble.transform.localPosition = new Vector3(0.12f, 0.8f, 0f);
            Prim(PrimitiveType.Sphere, bubble.transform, "Puff", Vector3.zero, new Vector3(0.2f, 0.16f, 0.12f), bb, PaletteSlot.Cloud, null, null, false);
            Prim(PrimitiveType.Sphere, bubble.transform, "Tail", new Vector3(-0.07f, -0.1f, 0f), Vector3.one * 0.04f, bb, PaletteSlot.Cloud, null, null, false);
            Prim(PrimitiveType.Sphere, bubble.transform, "Dot", new Vector3(0f, 0f, -0.05f), Vector3.one * 0.08f, bb, PaletteSlot.Golden, null, null, false);
            foreach (var r in bubble.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
            bubble.SetActive(false);
            return root;
        }

        /// <summary>Kenney's rigged characters rest in a T-pose; play the kit's idle clip so they stand naturally (walk = bob, S6 scope).</summary>
        private static void AddIdleAnimator(GameObject model, string fbxPath)
        {
            AnimationClip idle = null;
            var names = new List<string>();
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
                if (o is AnimationClip clip && !clip.name.StartsWith("__"))
                {
                    names.Add(clip.name);
                    if (idle == null || clip.name.ToLowerInvariant().Contains("idle")) idle = clip;
                }
            if (idle == null) { Debug.LogWarning("[ArtSetup] no animation clips in " + fbxPath); return; }
            _log.Add(Path.GetFileName(fbxPath) + " clips: " + string.Join(", ", names) + " -> " + idle.name);
            string ctrlPath = PrefabsDir + "ApprenticeIdle.controller";
            var ctrl = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(ctrlPath);
            if (ctrl == null) ctrl = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPathWithClip(ctrlPath, idle);
            var animator = model.GetComponent<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = ctrl;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullCompletely;
        }

        private static GameObject BuildTractor()
        {
            var (root, b) = Root("Tractor");
            Prim(PrimitiveType.Cube, root.transform, "Hull", new Vector3(0f, 0.28f, 0f), new Vector3(0.5f, 0.22f, 0.34f), b, PaletteSlot.Roof);
            Prim(PrimitiveType.Cube, root.transform, "Cab", new Vector3(-0.1f, 0.48f, 0f), new Vector3(0.22f, 0.2f, 0.28f), b, PaletteSlot.Roof);
            Prim(PrimitiveType.Cube, root.transform, "Window", new Vector3(-0.1f, 0.5f, 0f), new Vector3(0.23f, 0.12f, 0.29f), b, PaletteSlot.Glass, null, null, false);
            Prim(PrimitiveType.Cylinder, root.transform, "Chimney", new Vector3(0.18f, 0.48f, 0.08f), new Vector3(0.05f, 0.12f, 0.05f), b, PaletteSlot.Metal);
            foreach (var (x, z) in new[] { (-0.17f, -0.17f), (-0.17f, 0.17f), (0.17f, -0.17f), (0.17f, 0.17f) })
            {
                var w = Prim(PrimitiveType.Cylinder, root.transform, "Wheel", new Vector3(x, 0.12f, z), new Vector3(0.24f, 0.04f, 0.24f), b, PaletteSlot.Crow, null, null, false);
                w.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
            return root;
        }

        private static GameObject BuildCrow()
        {
            var (root, b) = Root("Crow");
            Prim(PrimitiveType.Sphere, root.transform, "Torso", new Vector3(0f, 0.16f, 0f), new Vector3(0.26f, 0.22f, 0.3f), b, PaletteSlot.Crow, null, null, false);
            Prim(PrimitiveType.Sphere, root.transform, "Head", new Vector3(0f, 0.3f, 0.1f), new Vector3(0.15f, 0.15f, 0.15f), b, PaletteSlot.Crow, null, null, false);
            var beak = ConeObj(root.transform, "Beak", new Vector3(0f, 0.3f, 0.16f), new Vector3(0.035f, 0.12f, 0.035f), b, PaletteSlot.Beak);
            beak.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Prim(PrimitiveType.Sphere, root.transform, "EyeL", new Vector3(-0.05f, 0.33f, 0.14f), Vector3.one * 0.035f, b, PaletteSlot.Eye, null, null, false);
            Prim(PrimitiveType.Sphere, root.transform, "EyeR", new Vector3(0.05f, 0.33f, 0.14f), Vector3.one * 0.035f, b, PaletteSlot.Eye, null, null, false);
            Prim(PrimitiveType.Cube, root.transform, "WingL", new Vector3(-0.14f, 0.2f, 0f), new Vector3(0.16f, 0.03f, 0.2f), b, PaletteSlot.Crow, null, null, false);
            Prim(PrimitiveType.Cube, root.transform, "WingR", new Vector3(0.14f, 0.2f, 0f), new Vector3(0.16f, 0.03f, 0.2f), b, PaletteSlot.Crow, null, null, false);
            return root;
        }

        private static GameObject BuildCloud()
        {
            var (root, b) = Root("Cloud");
            foreach (var (n, p, s) in new[] { ("A", new Vector3(0f, 0f, 0f), new Vector3(1.1f, 0.5f, 0.7f)), ("B", new Vector3(-0.4f, 0.1f, 0.05f), new Vector3(0.7f, 0.45f, 0.55f)), ("C", new Vector3(0.4f, 0.12f, -0.05f), new Vector3(0.75f, 0.5f, 0.6f)) })
            {
                var go = Prim(PrimitiveType.Sphere, root.transform, n, p, s, b, PaletteSlot.Cloud, null, null, false);
                go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
            return root;
        }

        private static GameObject BuildGreenhouse()
        {
            var (root, b) = Root("Greenhouse");
            Prim(PrimitiveType.Cube, root.transform, "Base", new Vector3(0f, 0.02f, 0f), new Vector3(0.95f, 0.04f, 0.75f), b, PaletteSlot.Metal);
            Prim(PrimitiveType.Cube, root.transform, "Glass", new Vector3(0f, 0.35f, 0f), new Vector3(0.9f, 0.62f, 0.7f), b, PaletteSlot.Glass, null, new Color(0.9f, 0.95f, 1f));
            var roof = Prim(PrimitiveType.Cube, root.transform, "RoofA", new Vector3(-0.22f, 0.76f, 0f), new Vector3(0.5f, 0.03f, 0.74f), b, PaletteSlot.Glass);
            roof.transform.localRotation = Quaternion.Euler(0f, 0f, 30f);
            var roofB = Prim(PrimitiveType.Cube, root.transform, "RoofB", new Vector3(0.22f, 0.76f, 0f), new Vector3(0.5f, 0.03f, 0.74f), b, PaletteSlot.Glass);
            roofB.transform.localRotation = Quaternion.Euler(0f, 0f, -30f);
            Prim(PrimitiveType.Cube, root.transform, "Ridge", new Vector3(0f, 0.9f, 0f), new Vector3(0.06f, 0.05f, 0.78f), b, PaletteSlot.Metal);
            for (int i = 0; i < 3; i++)
                Prim(PrimitiveType.Cube, root.transform, "Frame" + i, new Vector3(-0.3f + i * 0.3f, 0.35f, 0f), new Vector3(0.03f, 0.64f, 0.72f), b, PaletteSlot.Metal);
            return root;
        }

        private static GameObject BuildHouse(int size)
        {
            var (root, b) = Root("House" + size);
            float w = 1.0f + size * 0.35f, d = 0.8f + size * 0.2f, h = 0.6f + size * 0.15f;
            Prim(PrimitiveType.Cube, root.transform, "Wall", new Vector3(0f, h * 0.5f, 0f), new Vector3(w, h, d), b, PaletteSlot.Wall);
            Prim(PrimitiveType.Cube, root.transform, "Base", new Vector3(0f, 0.04f, 0f), new Vector3(w + 0.08f, 0.08f, d + 0.08f), b, PaletteSlot.Stone);
            var roofL = Prim(PrimitiveType.Cube, root.transform, "RoofL", new Vector3(-w * 0.25f, h + 0.22f, 0f), new Vector3(w * 0.62f, 0.06f, d + 0.2f), b, PaletteSlot.Roof);
            roofL.transform.localRotation = Quaternion.Euler(0f, 0f, 35f);
            var roofR = Prim(PrimitiveType.Cube, root.transform, "RoofR", new Vector3(w * 0.25f, h + 0.22f, 0f), new Vector3(w * 0.62f, 0.06f, d + 0.2f), b, PaletteSlot.Roof);
            roofR.transform.localRotation = Quaternion.Euler(0f, 0f, -35f);
            Prim(PrimitiveType.Cube, root.transform, "Gable", new Vector3(0f, h + 0.18f, 0f), new Vector3(w * 0.5f, 0.36f, d - 0.02f), b, PaletteSlot.Wall);
            Prim(PrimitiveType.Cube, root.transform, "Door", new Vector3(0f, 0.2f, -d * 0.5f - 0.005f), new Vector3(0.2f, 0.4f, 0.03f), b, PaletteSlot.Wood);
            // The windows have their own binder so the diorama can light them on cold evenings without the whole house glowing.
            var (windows, wb) = Root("Windows");
            windows.transform.SetParent(root.transform, false);
            for (int i = 0; i <= size; i++)
                Prim(PrimitiveType.Cube, windows.transform, "Window" + i, new Vector3(-w * 0.3f + i * (w * 0.6f / Mathf.Max(1, size)), h * 0.6f, -d * 0.5f - 0.005f), new Vector3(0.16f, 0.16f, 0.02f), wb, PaletteSlot.Glass, null, null, false);
            if (size >= 1) Prim(PrimitiveType.Cube, root.transform, "Chimney", new Vector3(w * 0.3f, h + 0.45f, d * 0.15f), new Vector3(0.14f, 0.35f, 0.14f), b, PaletteSlot.Stone);
            if (size >= 2) Prim(PrimitiveType.Cube, root.transform, "Porch", new Vector3(0f, 0.03f, -d * 0.5f - 0.2f), new Vector3(w * 0.7f, 0.06f, 0.4f), b, PaletteSlot.WoodLight);
            return root;
        }

        private static GameObject BuildWell()
        {
            var (root, b) = Root("Well");
            Prim(PrimitiveType.Cylinder, root.transform, "Ring", new Vector3(0f, 0.15f, 0f), new Vector3(0.5f, 0.15f, 0.5f), b, PaletteSlot.Stone);
            Prim(PrimitiveType.Cylinder, root.transform, "Water", new Vector3(0f, 0.22f, 0f), new Vector3(0.4f, 0.04f, 0.4f), b, PaletteSlot.Water, null, null, false);
            Prim(PrimitiveType.Cube, root.transform, "PostA", new Vector3(-0.2f, 0.5f, 0f), new Vector3(0.05f, 0.4f, 0.05f), b, PaletteSlot.Wood);
            Prim(PrimitiveType.Cube, root.transform, "PostB", new Vector3(0.2f, 0.5f, 0f), new Vector3(0.05f, 0.4f, 0.05f), b, PaletteSlot.Wood);
            Prim(PrimitiveType.Cylinder, root.transform, "Axle", new Vector3(0f, 0.72f, 0f), new Vector3(0.04f, 0.22f, 0.04f), b, PaletteSlot.WoodLight).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            ConeObj(root.transform, "Roof", new Vector3(0f, 0.85f, 0f), new Vector3(0.4f, 0.25f, 0.4f), b, PaletteSlot.Roof);
            return root;
        }

        private static GameObject BuildWindmill()
        {
            var (root, b) = Root("Windmill");
            ConeObj(root.transform, "Tower", Vector3.zero, new Vector3(0.38f, 1.5f, 0.38f), b, PaletteSlot.Wall);
            Prim(PrimitiveType.Cube, root.transform, "Door", new Vector3(0f, 0.15f, -0.3f), new Vector3(0.14f, 0.3f, 0.03f), b, PaletteSlot.Wood);
            ConeObj(root.transform, "Cap", new Vector3(0f, 1.3f, 0f), new Vector3(0.22f, 0.3f, 0.22f), b, PaletteSlot.Roof);
            var hub = new GameObject("Hub").transform;
            hub.SetParent(root.transform, false);
            hub.localPosition = new Vector3(0f, 1.25f, -0.26f);
            for (int i = 0; i < 4; i++)
            {
                var blade = Prim(PrimitiveType.Cube, hub, "Blade" + i, Vector3.zero, new Vector3(0.1f, 0.75f, 0.02f), b, PaletteSlot.WoodLight);
                blade.transform.localRotation = Quaternion.Euler(0f, 0f, i * 90f);
                blade.transform.localPosition = blade.transform.localRotation * new Vector3(0f, 0.38f, 0f);
            }
            Prim(PrimitiveType.Sphere, hub, "HubBall", Vector3.zero, Vector3.one * 0.08f, b, PaletteSlot.Wood);
            hub.gameObject.AddComponent<Spinner>();
            return root;
        }

        private static GameObject BuildPathTile()
        {
            var (root, b) = Root("PathTile");
            var slab = Prim(PrimitiveType.Cube, root.transform, "Slab", new Vector3(0f, 0.015f, 0f), new Vector3(0.7f, 0.03f, 1.0f), b, PaletteSlot.Path);
            slab.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            var seed = new System.Random(3);
            for (int i = 0; i < 3; i++)
            {
                var stone = Prim(PrimitiveType.Cube, root.transform, "Stone" + i, new Vector3((float)(seed.NextDouble() * 0.4 - 0.2), 0.035f, -0.35f + i * 0.35f), new Vector3(0.16f, 0.02f, 0.12f), b, PaletteSlot.Stone, null, null, false);
                stone.transform.localRotation = Quaternion.Euler(0f, (float)(seed.NextDouble() * 60 - 30), 0f);
                stone.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
            return root;
        }

        private static GameObject BuildSignpost()
        {
            var (root, b) = Root("Signpost");
            Prim(PrimitiveType.Cube, root.transform, "Post", new Vector3(0f, 0.35f, 0f), new Vector3(0.05f, 0.7f, 0.05f), b, PaletteSlot.Wood);
            Prim(PrimitiveType.Cube, root.transform, "Sign", new Vector3(0.12f, 0.6f, 0f), new Vector3(0.35f, 0.14f, 0.03f), b, PaletteSlot.WoodLight);
            Prim(PrimitiveType.Cube, root.transform, "Sign2", new Vector3(-0.1f, 0.42f, 0f), new Vector3(0.28f, 0.12f, 0.03f), b, PaletteSlot.WoodLight);
            return root;
        }

        private static GameObject BuildFlowerbed()
        {
            var (root, b) = Root("Flowerbed");
            Prim(PrimitiveType.Cube, root.transform, "Bed", new Vector3(0f, 0.04f, 0f), new Vector3(0.7f, 0.08f, 0.4f), b, PaletteSlot.Wood);
            var soil = Prim(PrimitiveType.Cube, root.transform, "Soil", new Vector3(0f, 0.06f, 0f), new Vector3(0.62f, 0.06f, 0.32f), b, PaletteSlot.SoilWet);
            soil.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            string[] flowers = { "flower_redA", "flower_yellowA", "flower_purpleA", "flower_redA" };
            for (int i = 0; i < 4; i++)
            {
                var f = Kit("nature-kit", flowers[i], 0.22f);
                f.transform.SetParent(root.transform, false);
                f.transform.localPosition = new Vector3(-0.22f + i * 0.15f, 0.08f, i % 2 == 0 ? -0.08f : 0.08f);
            }
            return root;
        }

        private static GameObject BuildBarrels()
        {
            var root = new GameObject("Barrels");
            for (int i = 0; i < 2; i++)
            {
                var barrel = Kit("food-kit", "barrel", 0.38f);
                barrel.transform.SetParent(root.transform, false);
                barrel.transform.localPosition = new Vector3(-0.16f + i * 0.32f, 0f, i * 0.1f);
                barrel.transform.localRotation = Quaternion.Euler(0f, i * 40f, 0f);
            }
            return root;
        }


        // ------------------------------------------------------------------ primitive helpers (editor-only; runtime never builds primitives)

        private static GameObject Primitive(PrimitiveType type, Transform parent, string name, Vector3 localPos, Vector3 localScale, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            var col = go.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.On;
            r.receiveShadows = true;
            return go;
        }

        private static GameObject MeshObject(Mesh mesh, Transform parent, string name, Vector3 localPos, Vector3 localScale, Material material)
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

        /// <summary>Unit cone: base radius 1 at y=0, apex at y=1, flat-shaded.</summary>
        private static Mesh ConeMesh()
        {
            const int segments = 16;
            var verts = new Vector3[segments * 6];
            var normals = new Vector3[verts.Length];
            var tris = new int[segments * 6];
            for (int i = 0; i < segments; i++)
            {
                float a0 = i / (float)segments * Mathf.PI * 2f, a1 = (i + 1) / (float)segments * Mathf.PI * 2f;
                var p0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
                var p1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                var apex = new Vector3(0f, 1f, 0f);
                int v = i * 3;
                verts[v] = p0; verts[v + 1] = apex; verts[v + 2] = p1;
                var n = Vector3.Cross(apex - p0, p1 - p0).normalized;
                normals[v] = normals[v + 1] = normals[v + 2] = n;
                tris[i * 6] = v; tris[i * 6 + 1] = v + 1; tris[i * 6 + 2] = v + 2;
                int c = segments * 3 + i * 3;
                verts[c] = p0; verts[c + 1] = Vector3.zero; verts[c + 2] = p1;
                normals[c] = normals[c + 1] = normals[c + 2] = Vector3.down;
                tris[i * 6 + 3] = c; tris[i * 6 + 4] = c + 2; tris[i * 6 + 5] = c + 1;
            }
            var mesh = new Mesh { name = "Cone", vertices = verts, normals = normals, triangles = tris };
            mesh.RecalculateBounds();
            return mesh;
        }

        // ------------------------------------------------------------------ icons

        private static void BuildIconAtlas()
        {
            string path = ResourcesDir + "NodeIcons.spriteatlas";
            EditorSettings.spritePackerMode = SpritePackerMode.AlwaysOnAtlas;
            var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(path);
            if (atlas == null)
            {
                atlas = new SpriteAtlas();
                AssetDatabase.CreateAsset(atlas, path);
            }
            var existing = atlas.GetPackables();
            if (existing != null && existing.Length > 0) atlas.Remove(existing);
            atlas.Add(new Object[] { AssetDatabase.LoadAssetAtPath<DefaultAsset>(IconsDir) });
            var packing = atlas.GetPackingSettings();
            packing.enableRotation = false;
            packing.enableTightPacking = false;
            packing.padding = 4;
            atlas.SetPackingSettings(packing);
            var tex = atlas.GetTextureSettings();
            tex.generateMipMaps = false;
            tex.filterMode = FilterMode.Bilinear;
            atlas.SetTextureSettings(tex);
            atlas.SetIncludeInBuild(true);
            EditorUtility.SetDirty(atlas);
            AssetDatabase.SaveAssets();
            SpriteAtlasUtility.PackAtlases(new[] { atlas }, EditorUserBuildSettings.activeBuildTarget);
            Debug.Log("[ArtSetup] icon atlas packed with " + atlas.spriteCount + " sprites");
        }

        // ------------------------------------------------------------------ decor links

        private static void LinkDecor(VisualCatalog c)
        {
            var set = AssetDatabase.LoadAssetAtPath<FarmDecorSet>(ResourcesDir + "FarmDecor.asset");
            if (set == null)
            {
                set = FarmDecorSet.Defaults();
                AssetDatabase.CreateAsset(set, ResourcesDir + "FarmDecor.asset");
            }
            else
            {
                set.Items = FarmDecorSet.Defaults().Items; // table is code; the asset carries the prefab links
            }
            int tree = 0;
            foreach (var item in set.Items)
                item.Prefab = c.Decor(item.Kind, item.Kind == DecorKind.Tree ? tree++ : 0);
            EditorUtility.SetDirty(set);
        }

        // ------------------------------------------------------------------ URP

        private static bool ConfigureUrp()
        {
            foreach (var rp in new[] { "Assets/Settings/Mobile_RPAsset.asset", "Assets/Settings/PC_RPAsset.asset" })
            {
                var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(rp);
                if (asset == null) { Debug.LogWarning("[ArtSetup] missing " + rp); continue; }
                var so = new SerializedObject(asset);
                Set(so, "m_MainLightShadowmapResolution", 1024);
                Set(so, "m_ShadowCascadeCount", 1);
                Set(so, "m_ShadowDistance", 45f);
                Set(so, "m_MSAA", 2);
                Set(so, "m_SupportsHDR", false);
                Set(so, "m_SoftShadowsSupported", true);
                Set(so, "m_UseSRPBatcher", false);
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(asset);
            }
            bool decal = UseDecals;
            foreach (var path in new[] { "Assets/Settings/Mobile_Renderer.asset", "Assets/Settings/PC_Renderer.asset" })
                if (UseDecals) decal &= EnsureDecalFeature(path);
                else RemoveDecalFeature(path);
            AssetDatabase.SaveAssets();
            return decal;
        }

        private static void Set(SerializedObject so, string prop, object value)
        {
            var p = so.FindProperty(prop);
            if (p == null) { Debug.LogWarning("[ArtSetup] URP property not found: " + prop); return; }
            switch (value)
            {
                case int i: p.intValue = i; break;
                case float f: p.floatValue = f; break;
                case bool b: p.boolValue = b; break;
            }
        }

        /// <summary>Screen-space decals: no DepthNormals prepass, so the whole scene is not drawn twice (mobile budget).</summary>
        private static void SetScreenSpace(DecalRendererFeature feature)
        {
            var so = new SerializedObject(feature);
            var technique = so.FindProperty("m_Settings.technique");
            if (technique != null) technique.enumValueIndex = 2; // DecalTechniqueOption.ScreenSpace (internal enum: Automatic=0, DBuffer=1, ScreenSpace=2)
            var dist = so.FindProperty("m_Settings.maxDrawDistance");
            if (dist != null) dist.floatValue = 200f;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(feature);
        }

        private static void RemoveDecalFeature(string path)
        {
            var data = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(path);
            if (data == null) return;
            var so = new SerializedObject(data);
            var features = so.FindProperty("m_RendererFeatures");
            var map = so.FindProperty("m_RendererFeatureMap");
            if (features == null || map == null) return;
            bool removed = false;
            for (int i = features.arraySize - 1; i >= 0; i--)
            {
                var obj = features.GetArrayElementAtIndex(i).objectReferenceValue;
                if (!(obj is DecalRendererFeature)) continue;
                features.DeleteArrayElementAtIndex(i);
                if (i < map.arraySize) map.DeleteArrayElementAtIndex(i);
                AssetDatabase.RemoveObjectFromAsset(obj);
                Object.DestroyImmediate(obj, true);
                removed = true;
            }
            if (!removed) return;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            Debug.Log("[ArtSetup] decal renderer feature removed from " + path);
        }

        private static bool EnsureDecalFeature(string path)
        {
            var data = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(path);
            if (data == null) { Debug.LogWarning("[ArtSetup] renderer data missing: " + path); return false; }
            foreach (var f in data.rendererFeatures) if (f is DecalRendererFeature existing) { SetScreenSpace(existing); return true; }
            var feature = ScriptableObject.CreateInstance<DecalRendererFeature>();
            feature.name = "Decals";
            SetScreenSpace(feature);
            AssetDatabase.AddObjectToAsset(feature, data);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);
            var so = new SerializedObject(data);
            var features = so.FindProperty("m_RendererFeatures");
            var map = so.FindProperty("m_RendererFeatureMap");
            if (features == null || map == null) { Debug.LogWarning("[ArtSetup] renderer feature properties not found"); return false; }
            features.arraySize++;
            features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
            map.arraySize++;
            map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            Debug.Log("[ArtSetup] decal renderer feature added to " + path);
            return true;
        }
    }
}
