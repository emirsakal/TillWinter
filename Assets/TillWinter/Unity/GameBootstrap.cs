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
        [Tooltip("EN string table (Assets/TillWinter/Unity/Localization/en.json).")]
        public TextAsset StringTable;
        [Tooltip("TR string table (Assets/TillWinter/Unity/Localization/tr.json).")]
        public TextAsset StringTableTr;

        private void Awake()
        {
            AppLifecycle.MarkBootStart();
            Application.targetFrameRate = AppLifecycle.YearFps; // 30 while the Winter screen is open (AppLifecycle)
            Screen.sleepTimeout = SleepTimeout.SystemSetting;
#if !TW_DEBUG && !UNITY_EDITOR
            Debug.unityLogger.logEnabled = false; // release builds log nothing
#endif
            Time.timeScale = 1f; // a reload from the pause menu (language, reset) starts unpaused
            GameLanguage.Apply(GameLanguage.Resolve(SettingsStore.Current.Language, Application.systemLanguage), StringTable, StringTableTr);
            var config = ConfigAsset != null ? ConfigAsset.Config : new FarmConfig();

            var root = new GameObject("TillWinter");
            var game = root.AddComponent<GameController>();
            var saveGo = new GameObject("Save");
            saveGo.transform.SetParent(root.transform, false);
            var save = saveGo.AddComponent<SaveController>();
            var loaded = SaveController.Load(save.Path);
            FarmSim sim = loaded != null ? FarmSim.FromSave(loaded, config) : null;
            OfflineReport offline = default;
            if (sim != null)
            {
                game.InitFrom(sim);
                long now = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                double elapsed = System.Math.Max(0, now - loaded.SavedAtUnixSeconds); // clock went backwards -> 0
                offline = sim.SimulateOffline(elapsed);
                Debug.Log("[TillWinter] Loaded save (gen " + sim.State.Generation.Generation + ", year " + sim.State.Year + ", " + sim.State.Phase + "); offline " + offline.SecondsSimulated + " s, +" + offline.CoinsEarned + " coins");
            }
            else
            {
                if (loaded != null) Debug.LogWarning("[TillWinter] Save could not be restored (schema " + loaded.SchemaVersion + "); starting fresh");
                game.Init(config, Seed);
            }
            save.Attach(game);
            game.RingOffsetPlots = RingOffsetPlots;
            game.Pointer = root.AddComponent<PointerInput>();

            var camRig = new GameObject("CameraRig").AddComponent<CameraRig>();
            camRig.transform.SetParent(root.transform, false);
            camRig.Setup();
            game.Cam = camRig.Cam;

            var audio = new GameObject("Audio").AddComponent<AudioManager>();
            audio.transform.SetParent(root.transform, false);
            audio.Init();

            var fx = new GameObject("Vfx").AddComponent<VfxPlayer>();
            fx.transform.SetParent(root.transform, false);
            fx.Init(VfxCatalog.Load()); // pools pre-warmed here; nothing instantiates during play
            var _ = SettingsStore.Current; // settings.json (haptics, volumes) read once at boot

            // Everything visual comes from the catalogue (Resources/VisualCatalog, built by art-setup.bat).
            var catalog = VisualCatalog.Load();
            Palette.Load().ApplyToMaterials(catalog.SlotMaterials);

            var season = new GameObject("Season").AddComponent<SeasonPresenter>();
            season.transform.SetParent(root.transform, false);
            season.Init(game, camRig.Cam, catalog, fx);
            QualityTiers.Init(season.Sun, camRig.Cam);

            var diorama = new GameObject("Diorama").AddComponent<DioramaView>();
            diorama.transform.SetParent(root.transform, false);
            diorama.Init(game, catalog);

            var field = new GameObject("Field").AddComponent<FieldView>();
            field.transform.SetParent(root.transform, false);
            field.Init(game, camRig, fx, audio, catalog);

            var ring = new GameObject("Ring").AddComponent<RingView>();
            ring.transform.SetParent(root.transform, false);
            ring.Init(game, catalog);

            var apprentices = new GameObject("Apprentices").AddComponent<ApprenticesView>();
            apprentices.transform.SetParent(root.transform, false);
            apprentices.Init(game, catalog);

            var cloud = new GameObject("Cloud").AddComponent<CloudView>();
            cloud.transform.SetParent(root.transform, false);
            cloud.Init(game, fx, audio, catalog);
            var tractor = new GameObject("Tractor").AddComponent<TractorView>();
            tractor.transform.SetParent(root.transform, false);
            tractor.Init(game, catalog, fx, audio);
            var greenhouse = new GameObject("Greenhouse").AddComponent<GreenhouseView>();
            greenhouse.transform.SetParent(root.transform, false);
            greenhouse.Init(game, fx, catalog);

            var crows = new GameObject("Crows").AddComponent<CrowsView>();
            crows.transform.SetParent(root.transform, false);
            crows.Init(game, fx, audio, catalog);

            var canvas = BuildCanvas(root.transform);
            var hud = canvas.gameObject.AddComponent<HudView>();
            hud.Init(game, audio, canvas);
            var shop = canvas.gameObject.AddComponent<WinterScreen>();
            shop.Init(game, audio, canvas);
            var away = canvas.gameObject.AddComponent<AwayCard>();
            away.Init(game, audio, canvas, hud);
            var onboarding = canvas.gameObject.AddComponent<OnboardingView>();
            onboarding.Init(game, canvas);
            var pause = canvas.gameObject.AddComponent<PauseMenu>();
            pause.Init(game, audio, canvas, save);
            var ending = canvas.gameObject.AddComponent<EndingView>();
            ending.Init(game, canvas, pause);
#if TW_DEBUG || UNITY_EDITOR
            var debug = canvas.gameObject.AddComponent<DebugPanel>();
            debug.Init(game, audio, canvas, save, away);
            pause.DeveloperToggle = debug.Toggle;
#endif
            var menu = canvas.gameObject.AddComponent<MainMenu>(); // every launch opens on the title screen
            menu.Init(game, canvas, pause, save, loaded != null);
            canvas.gameObject.AddComponent<BootFade>().Init(canvas); // no loading text: the diorama fades in
            var lifecycle = root.AddComponent<AppLifecycle>();
            lifecycle.Init(game, audio, away, shop);
            AppLifecycle.MarkBootEnd();
            bool firstPlay = true;
            menu.Played += () =>
            {
                if (!firstPlay) return; // back from the pause menu: nothing to hand over again
                firstPlay = false;
                if (game.State.Phase != Phase.Year) shop.Open();
                if (offline.CoinsEarned > 0) away.Show(offline);
            };
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
