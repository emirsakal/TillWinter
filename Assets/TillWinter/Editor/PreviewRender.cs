using System;
using System.IO;
using TillWinter.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace TillWinter.EditorTools
{
    /// <summary>
    /// Dev preview: every crop tier's three growth stages (columns: sprout, growing, ripe; rows: tiers) on wet soil
    /// tiles, seen from the game camera's angle, written to a PNG. Checks stage art without playing.
    /// Batch: Unity -batchmode -executeMethod TillWinter.EditorTools.PreviewRender.CropsBatch -out &lt;png&gt;
    /// </summary>
    public static class PreviewRender
    {
        private const string Resources = "Assets/TillWinter/Unity/Resources/";
        private const float Spacing = 1.2f;

        public static void CropsBatch()
        {
            int code = 0;
            try
            {
                string outPath = "crop-stages.png";
                bool autumn = false;
                var args = Environment.GetCommandLineArgs();
                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i] == "-out" && i + 1 < args.Length) outPath = args[i + 1];
                    if (args[i] == "-autumn") autumn = true;
                }
                RenderCrops(outPath, autumn);
            }
            catch (Exception e) { Debug.LogError("[Preview] failed: " + e); code = 1; }
            EditorApplication.Exit(code);
        }

        [MenuItem("Till Winter/Preview crop stages")]
        public static void CropsMenu() => RenderCrops("crop-stages.png", false);

        /// <param name="autumn">Render with the Autumn look and leaf tint (crops must not change colour with the season).</param>
        public static void RenderCrops(string outPath, bool autumn)
        {
            var visuals = AssetDatabase.LoadAssetAtPath<VisualCatalog>(Resources + "VisualCatalog.asset");
            var palette = AssetDatabase.LoadAssetAtPath<Palette>(Resources + "Palette.asset");
            var seasons = AssetDatabase.LoadAssetAtPath<SeasonPalette>(Resources + "SeasonPalette.asset");
            if (visuals == null || palette == null || seasons == null) throw new Exception("run art-setup.bat first");
            palette.ApplyToMaterials(visuals.SlotMaterials);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var look = autumn ? seasons.Autumn : seasons.Spring;
            RenderSettings.fog = false;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = look.AmbientSky;
            RenderSettings.ambientEquatorColor = look.AmbientEquator;
            RenderSettings.ambientGroundColor = look.AmbientGround;
            Shader.SetGlobalFloat("_TW_Snow", 0f);
            Shader.SetGlobalColor("_TW_SeasonTint", autumn ? look.LeafTint : Color.white);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = look.Light;
            sun.intensity = 1.2f;
            sun.transform.rotation = Quaternion.Euler(look.Angle);

            int tiers = visuals.Crops.Length;
            for (int t = 0; t < tiers; t++)
            {
                for (int s = 0; s < 3; s++)
                {
                    var pos = new Vector3(s * Spacing, 0f, -t * Spacing);
                    var tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Object.DestroyImmediate(tile.GetComponent<Collider>());
                    tile.transform.position = pos + new Vector3(0f, 0.08f, 0f);
                    tile.transform.localScale = new Vector3(0.96f, 0.16f, 0.96f);
                    tile.GetComponent<Renderer>().sharedMaterial = visuals.SlotMaterial(PaletteSlot.SoilWet);
                    var prefab = visuals.Crops[t]?.Stage(s);
                    if (prefab == null) continue;
                    var crop = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    crop.transform.position = pos + new Vector3(0f, 0.16f, 0f);
                }
            }

            const int w = 720, h = 1440;
            var cam = new GameObject("PreviewCamera").AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = tiers * Spacing * 0.5f + 0.4f;
            cam.transform.rotation = Quaternion.Euler(50f, 0f, 0f); // game camera: 40 degrees from top-down
            var centre = new Vector3(Spacing, 0.2f, -(tiers - 1) * Spacing * 0.5f);
            cam.transform.position = centre - cam.transform.forward * 20f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 60f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = look.SkyBottom;
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false;
            data.renderShadows = false;

            var rt = RenderTexture.GetTemporary(new RenderTextureDescriptor(w, h, RenderTextureFormat.ARGB32, 24) { sRGB = true });
            cam.targetTexture = rt;
            var request = new RenderPipeline.StandardRequest { destination = rt };
            if (RenderPipeline.SupportsRenderRequest(cam, request)) RenderPipeline.SubmitRenderRequest(cam, request);
            else cam.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = previous;
            cam.targetTexture = null;
            RenderTexture.ReleaseTemporary(rt);
            File.WriteAllBytes(outPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            Debug.Log("[Preview] crop stages written: " + outPath);
        }
    }
}
