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
    /// One-off UI setup (ui-setup.bat / menu): imports TMP Essential Resources, builds the Nunito SDF
    /// font asset (Latin + Turkish) into Assets/Fonts/Resources, and creates the TreeTheme asset.
    /// Safe to re-run: existing assets are kept.
    /// </summary>
    public static class UiSetup
    {
        private const string FontSource = "Assets/Fonts/Nunito-Variable.ttf";
        private const string FontAssetPath = "Assets/Fonts/Resources/NunitoSDF.asset";
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
            CreateTheme();
            CreateExtraAssets();
            WireBootstrap();
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
            asset.name = "NunitoSDF";
            bool ok = asset.TryAddCharacters(Characters, out string missing);
            Debug.Log("[UiSetup] glyphs added: " + ok + (string.IsNullOrEmpty(missing) ? "" : " missing: " + missing));
            asset.atlasPopulationMode = AtlasPopulationMode.Static;
            AssetDatabase.CreateAsset(asset, FontAssetPath);
            asset.material.name = "NunitoSDF Material";
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            for (int i = 0; i < asset.atlasTextures.Length; i++)
            {
                asset.atlasTextures[i].name = "NunitoSDF Atlas " + i;
                AssetDatabase.AddObjectToAsset(asset.atlasTextures[i], asset);
            }
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(FontAssetPath, ImportAssetOptions.ForceUpdate);
            Debug.Log("[UiSetup] font asset created: " + FontAssetPath + " (" + asset.characterTable.Count + " characters)");
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
                tex.name = "NunitoSDF Atlas " + i;
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
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<HudTheme>(), dir + "HudTheme.asset");
                Debug.Log("[UiSetup] HudTheme created");
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
            }
            else TreeTheme.ApplyAlmanacPage(theme);
            theme.StyleVersion = TreeTheme.CurrentStyle;
            EditorUtility.SetDirty(theme);
            Debug.Log("[UiSetup] " + theme.name + " restyled to style " + TreeTheme.CurrentStyle);
        }
    }
}
