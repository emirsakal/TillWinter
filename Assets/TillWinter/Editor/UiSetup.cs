using System;
using System.IO;
using System.Reflection;
using TillWinter.Unity;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace TillWinter.EditorTools
{
    /// <summary>
    /// One-off UI setup (ui-setup.bat / menu): imports TMP Essential Resources, builds the Figtree SDF
    /// font asset (Latin + Turkish) into Assets/Fonts/Resources, and creates the TreeTheme asset.
    /// Safe to re-run: existing assets are kept.
    /// </summary>
    public static class UiSetup
    {
        // Figtree: warmer and more characterful than Nunito, and it carries the whole Turkish alphabet. That last part
        // is not optional — Fredoka was tried first and has no Ğ ğ İ Ş ş, which would have set half the Turkish UI in
        // the fallback font mid-word. Check a candidate's cmap before swapping. SIL OFL 1.1, licence kept beside it.
        // UiKit.Font loads this by name from Resources, so the two spellings must stay in step.
        private const string FontName = "FigtreeSDF";
        private const string FontSource = "Assets/Fonts/Figtree-SemiBold.ttf";
        private const string FontAssetPath = "Assets/Fonts/Resources/" + FontName + ".asset";
        // Rammetto One: the rounded display face for titles and big numbers (SIL OFL 1.1). It has the Turkish letters
        // but no ≤ ≥, which no title uses; Figtree is its fallback for anything else.
        private const string DisplayName = "RammettoSDF";
        private const string DisplaySource = "Assets/Fonts/RammettoOne-Regular.ttf";
        private const string DisplayAssetPath = "Assets/Fonts/Resources/" + DisplayName + ".asset";
        private const string ThemePath = "Assets/TillWinter/Unity/Resources/TreeTheme.asset";
        private const string Characters =
            " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~" +
            "ÇçĞğİıÖöŞşÜü" + // ÇçĞğİıÖöŞşÜü
            "ÂâÎîÛû·–—»«×…•°≤≥ ";

        public static void Run()
        {
            int code = 0;
            try
            {
                RunAll();
            }
            catch (Exception e)
            {
                Debug.LogError("[UiSetup] failed: " + e);
                code = 1;
            }
            EditorApplication.Exit(code);
        }

        [MenuItem("Till Winter/UI setup (TMP + font + theme)")]
        public static void RunAll()
        {
            ImportTmpEssentials();
            CreateFontAsset();
            CreateDisplayFontAsset();
            CreateTheme();
            CreateExtraAssets();
            WireBootstrap();
            CreateMenuScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[UiSetup] done");
        }

        private static void ImportTmpEssentials()
        {
            if (Directory.Exists("Assets/TextMesh Pro/Resources"))
            {
                Debug.Log("[UiSetup] TMP essentials already present");
                return;
            }
            var info = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.unity.ugui/package.json");
            string package = info != null ? Path.Combine(info.resolvedPath, "Package Resources", "TMP Essential Resources.unitypackage") : null;
            Debug.Log("[UiSetup] essentials package: " + package + " exists=" + (package != null && File.Exists(package)));
            if (package != null && File.Exists(package))
            {
                AssetDatabase.ImportPackage(package, false);
            }
            else
            {
                var importer = Type.GetType("TMPro.TMP_PackageResourceImporter, Unity.TextMeshPro.Editor");
                var method = importer?.GetMethod("ImportResources", BindingFlags.Public | BindingFlags.Static);
                method?.Invoke(null, new object[] { true, false, false });
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("[UiSetup] TMP essentials imported: " + Directory.Exists("Assets/TextMesh Pro/Resources"));
        }

        private static void CreateFontAsset()
        {
            if (File.Exists(FontAssetPath))
            {
                TopUpFontAsset();
                return;
            }
            var font = AssetDatabase.LoadAssetAtPath<Font>(FontSource);
            if (font == null) throw new FileNotFoundException("font not imported: " + FontSource);
            Directory.CreateDirectory(Path.GetDirectoryName(FontAssetPath));
            var asset = TMP_FontAsset.CreateFontAsset(font, 72, 8, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            if (asset == null) throw new InvalidOperationException("TMP_FontAsset.CreateFontAsset returned null");
            asset.name = FontName;
            bool ok = asset.TryAddCharacters(Characters, out string missing);
            Debug.Log("[UiSetup] glyphs added: " + ok + (string.IsNullOrEmpty(missing) ? "" : " missing: " + missing));
            asset.atlasPopulationMode = AtlasPopulationMode.Static;
            AssetDatabase.CreateAsset(asset, FontAssetPath);
            asset.material.name = FontName + " Material";
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            for (int i = 0; i < asset.atlasTextures.Length; i++)
            {
                asset.atlasTextures[i].name = FontName + " Atlas " + i;
                AssetDatabase.AddObjectToAsset(asset.atlasTextures[i], asset);
            }
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(FontAssetPath, ImportAssetOptions.ForceUpdate);
            Debug.Log("[UiSetup] font asset created: " + FontAssetPath + " (" + asset.characterTable.Count + " characters)");
        }

        private static string DisplayCharacters => Characters.Replace("≤", "").Replace("≥", "");

        private static void CreateDisplayFontAsset()
        {
            var body = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(DisplayAssetPath);
            if (existing != null)
            {
                if (body != null && !existing.fallbackFontAssetTable.Contains(body))
                {
                    existing.fallbackFontAssetTable.Add(body);
                    EditorUtility.SetDirty(existing);
                }
                Debug.Log("[UiSetup] display font asset already present");
                return;
            }
            var font = AssetDatabase.LoadAssetAtPath<Font>(DisplaySource);
            if (font == null) throw new FileNotFoundException("font not imported: " + DisplaySource);
            var asset = TMP_FontAsset.CreateFontAsset(font, 72, 8, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            if (asset == null) throw new InvalidOperationException("TMP_FontAsset.CreateFontAsset returned null (display)");
            asset.name = DisplayName;
            bool ok = asset.TryAddCharacters(DisplayCharacters, out string missing);
            Debug.Log("[UiSetup] display glyphs added: " + ok + (string.IsNullOrEmpty(missing) ? "" : " missing: " + missing));
            asset.atlasPopulationMode = AtlasPopulationMode.Static;
            if (body != null) asset.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset> { body };
            AssetDatabase.CreateAsset(asset, DisplayAssetPath);
            asset.material.name = DisplayName + " Material";
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            for (int i = 0; i < asset.atlasTextures.Length; i++)
            {
                asset.atlasTextures[i].name = DisplayName + " Atlas " + i;
                AssetDatabase.AddObjectToAsset(asset.atlasTextures[i], asset);
            }
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(DisplayAssetPath, ImportAssetOptions.ForceUpdate);
            Debug.Log("[UiSetup] display font asset created: " + DisplayAssetPath + " (" + asset.characterTable.Count + " characters)");
        }

        private const string MenuScenePath = "Assets/TillWinter/Scenes/Menu.unity";

        /// <summary>The title scene: one MenuBootstrap with both string tables; build order Menu, Farm (edited through the editor, never by hand).</summary>
        private static void CreateMenuScene()
        {
            var en = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/TillWinter/Unity/Localization/en.json");
            var tr = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/TillWinter/Unity/Localization/tr.json");
            var scene = File.Exists(MenuScenePath)
                ? UnityEditor.SceneManagement.EditorSceneManager.OpenScene(MenuScenePath)
                : UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);
            var boot = UnityEngine.Object.FindFirstObjectByType<MenuBootstrap>();
            if (boot == null) boot = new GameObject("MenuBootstrap").AddComponent<MenuBootstrap>();
            boot.StringTable = en;
            boot.StringTableTr = tr;
            EditorUtility.SetDirty(boot);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, MenuScenePath);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MenuScenePath, true),
                new EditorBuildSettingsScene("Assets/TillWinter/Scenes/Farm.unity", true),
            };
            Debug.Log("[UiSetup] title scene ready; build order Menu, Farm");
        }

        /// <summary>Adds any Characters glyph the existing atlas lacks, in place (keeps the asset and its GUID).</summary>
        private static void TopUpFontAsset()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (asset == null) throw new FileNotFoundException("font asset unreadable: " + FontAssetPath);
            if (asset.HasCharacters(Characters, out uint[] _, false, false)) { Debug.Log("[UiSetup] font asset already present, all glyphs"); return; }
            asset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            bool ok = asset.TryAddCharacters(Characters, out string missing);
            asset.atlasPopulationMode = AtlasPopulationMode.Static;
            for (int i = 0; i < asset.atlasTextures.Length; i++)
            {
                var tex = asset.atlasTextures[i];
                if (tex == null || AssetDatabase.Contains(tex)) continue;
                tex.name = FontName + " Atlas " + i;
                AssetDatabase.AddObjectToAsset(tex, asset);
            }
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            Debug.Log("[UiSetup] font asset topped up: " + ok + (string.IsNullOrEmpty(missing) ? "" : " missing: " + missing) + " (" + asset.characterTable.Count + " characters)");
        }

        /// <summary>Assigns en.json to GameBootstrap.StringTable in Farm.unity (edits the scene through the editor, never by hand).</summary>
        private static void WireBootstrap()
        {
            const string scenePath = "Assets/TillWinter/Scenes/Farm.unity";
            var table = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/TillWinter/Unity/Localization/en.json");
            if (table == null) throw new FileNotFoundException("en.json not imported");
            var tableTr = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/TillWinter/Unity/Localization/tr.json");
            if (tableTr == null) throw new FileNotFoundException("tr.json not imported");
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);
            var boot = UnityEngine.Object.FindFirstObjectByType<GameBootstrap>();
            if (boot == null) throw new InvalidOperationException("Bootstrap not found in " + scenePath);
            if (boot.StringTable == table && boot.StringTableTr == tableTr) { Debug.Log("[UiSetup] string tables already wired"); return; }
            boot.StringTable = table;
            boot.StringTableTr = tableTr;
            EditorUtility.SetDirty(boot);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            Debug.Log("[UiSetup] string table wired into Farm.unity");
        }

        private static void CreateExtraAssets()
        {
            const string dir = "Assets/TillWinter/Unity/Resources/";
            if (!File.Exists(dir + "HeritageTheme.asset"))
            {
                var t = ScriptableObject.CreateInstance<TreeTheme>();
                TreeTheme.ApplyHeritageDefaults(t);
                AssetDatabase.CreateAsset(t, dir + "HeritageTheme.asset");
                Debug.Log("[UiSetup] HeritageTheme created");
            }
            if (!File.Exists(dir + "HudTheme.asset"))
            {
                var hud = ScriptableObject.CreateInstance<HudTheme>();
                HudTheme.ApplyPalette(hud);
                AssetDatabase.CreateAsset(hud, dir + "HudTheme.asset");
                Debug.Log("[UiSetup] HudTheme created");
            }
            else
            {
                var hud = AssetDatabase.LoadAssetAtPath<HudTheme>(dir + "HudTheme.asset");
                if (hud != null && hud.StyleVersion < HudTheme.CurrentStyle)
                {
                    HudTheme.ApplyPalette(hud);
                    EditorUtility.SetDirty(hud);
                    Debug.Log("[UiSetup] HudTheme restyled to style " + HudTheme.CurrentStyle);
                }
            }
            if (!File.Exists(dir + "FarmDecor.asset"))
            {
                AssetDatabase.CreateAsset(FarmDecorSet.Defaults(), dir + "FarmDecor.asset");
                Debug.Log("[UiSetup] FarmDecor created");
            }
        }

        private static void CreateTheme()
        {
            if (File.Exists(ThemePath))
            {
                Restyle(AssetDatabase.LoadAssetAtPath<TreeTheme>(ThemePath), false);
                Restyle(AssetDatabase.LoadAssetAtPath<TreeTheme>("Assets/TillWinter/Unity/Resources/HeritageTheme.asset"), true);
                return;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(ThemePath));
            var theme = ScriptableObject.CreateInstance<TreeTheme>();
            theme.StyleVersion = TreeTheme.CurrentStyle;
            AssetDatabase.CreateAsset(theme, ThemePath);
            Debug.Log("[UiSetup] theme created: " + ThemePath);
        }

        /// <summary>Brings an existing theme asset up to <see cref="TreeTheme.CurrentStyle"/> in place (S9: dark Almanac page, opaque overlays).</summary>
        private static void Restyle(TreeTheme theme, bool heritage)
        {
            if (theme == null || theme.StyleVersion >= TreeTheme.CurrentStyle) { Debug.Log("[UiSetup] theme already present"); return; }
            if (heritage)
            {
                var o = theme.Overlay;
                theme.Overlay = new Color(o.r, o.g, o.b, 1f);
                var d = ScriptableObject.CreateInstance<TreeTheme>();
                TreeTheme.ApplyHeritageDefaults(d);
                theme.InkMuted = d.InkMuted; // S9: readable on the dark page
                theme.EdgeDim = d.EdgeDim;
                theme.ZoomMin = d.ZoomMin; // S9: the radial tree opens fully framed
                theme.InitialZoom = d.InitialZoom; // S9: the star needs more room than the old lanes
                UnityEngine.Object.DestroyImmediate(d);
            }
            else TreeTheme.ApplyAlmanacPage(theme);
            // Style 7: a locked node keeps more of its branch colour, so an unexplored tree reads as five branches
            // rather than one grey constellation.
            if (theme.StyleVersion < 7)
            {
                var defaults = ScriptableObject.CreateInstance<TreeTheme>();
                theme.LockedSaturation = defaults.LockedSaturation;
                UnityEngine.Object.DestroyImmediate(defaults);
            }
            theme.StyleVersion = TreeTheme.CurrentStyle;
            EditorUtility.SetDirty(theme);
            Debug.Log("[UiSetup] " + theme.name + " restyled to style " + TreeTheme.CurrentStyle);
        }
    }
}
