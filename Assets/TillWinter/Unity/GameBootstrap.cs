using TillWinter.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace TillWinter.Unity
{
    /// <summary>
    /// Composition root. The scene contains only this component; everything else (camera, light,
    /// field, UI, audio) is built in code so the demo has no asset dependencies.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class GameBootstrap : MonoBehaviour
    {
        [Tooltip("Optional. Leave empty to use FarmConfig defaults.")]
        public FarmConfigAsset ConfigAsset;
        public int Seed = 12345;
        [Tooltip("Plots the ring is pushed toward the top of the screen so the finger does not cover it.")]
        public float RingOffsetPlots = 0.8f;

        private void Awake()
        {
            Application.targetFrameRate = 60;
            var config = ConfigAsset != null ? ConfigAsset.Config : new FarmConfig();

            var root = new GameObject("TillWinter");
            var game = root.AddComponent<GameController>();
            game.Init(config, Seed);
            game.RingOffsetPlots = RingOffsetPlots;
            game.Pointer = root.AddComponent<PointerInput>();

            var camRig = new GameObject("CameraRig").AddComponent<CameraRig>();
            camRig.transform.SetParent(root.transform, false);
            camRig.Setup();
            game.Cam = camRig.Cam;

            var audio = new GameObject("Audio").AddComponent<AudioManager>();
            audio.transform.SetParent(root.transform, false);
            audio.Init();

            var fx = new GameObject("Fx").AddComponent<FxManager>();
            fx.transform.SetParent(root.transform, false);
            fx.Init();

            var season = new GameObject("Season").AddComponent<SeasonPresenter>();
            season.transform.SetParent(root.transform, false);
            season.Init(game);

            var field = new GameObject("Field").AddComponent<FieldView>();
            field.transform.SetParent(root.transform, false);
            field.Init(game, camRig, fx, audio);

            var ring = new GameObject("Ring").AddComponent<RingView>();
            ring.transform.SetParent(root.transform, false);
            ring.Init(game);

            var apprentice = new GameObject("Apprentice").AddComponent<ApprenticeView>();
            apprentice.transform.SetParent(root.transform, false);
            apprentice.Init(game);

            var crows = new GameObject("Crows").AddComponent<CrowsView>();
            crows.transform.SetParent(root.transform, false);
            crows.Init(game, fx, audio);

            var canvas = BuildCanvas(root.transform);
            var hud = canvas.gameObject.AddComponent<HudView>();
            hud.Init(game, audio, canvas);
            var shop = canvas.gameObject.AddComponent<WinterShopView>();
            shop.Init(game, audio, canvas);
            var debug = canvas.gameObject.AddComponent<DebugPanel>();
            debug.Init(game, audio, canvas);
        }

        private static RectTransform BuildCanvas(Transform parent)
        {
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            es.transform.SetParent(parent, false);

            var go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(parent, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 2340f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0f; // match width so 1080x1920 / 1080x2400 keep the same horizontal layout
            return go.GetComponent<RectTransform>();
        }
    }
}
