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
            "ÂâÎîÛû·–—→▶×…•°≤≥ ";

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
                Debug.Log("[UiSetup] font asset already present");
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

        /// <summary>Assigns en.json to GameBootstrap.StringTable in Farm.unity (edits the scene through the editor, never by hand).</summary>
        private static void WireBootstrap()
        {
            const string scenePath = "Assets/TillWinter/Scenes/Farm.unity";
            var table = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/TillWinter/Unity/Localization/en.json");
            if (table == null) throw new FileNotFoundException("en.json not imported");
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);
            var boot = UnityEngine.Object.FindFirstObjectByType<GameBootstrap>();
            if (boot == null) throw new InvalidOperationException("Bootstrap not found in " + scenePath);
            if (boot.StringTable == table) { Debug.Log("[UiSetup] string table already wired"); return; }
            boot.StringTable = table;
            EditorUtility.SetDirty(boot);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            Debug.Log("[UiSetup] string table wired into Farm.unity");
        }

        private static void CreateTheme()
        {
            if (File.Exists(ThemePath))
            {
                Debug.Log("[UiSetup] theme already present");
                return;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(ThemePath));
            var theme = ScriptableObject.CreateInstance<TreeTheme>();
            AssetDatabase.CreateAsset(theme, ThemePath);
            Debug.Log("[UiSetup] theme created: " + ThemePath);
        }
    }
}
