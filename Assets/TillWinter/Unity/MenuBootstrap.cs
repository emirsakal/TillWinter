using System.Collections.Generic;
using System.IO;
using TillWinter.Core;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TillWinter.Unity
{
    /// <summary>
    /// The title scene (Menu.unity), built in code like the farm: one small floating plot of land where crops sprout,
    /// grow and ripen in a wave while the seasons turn Spring, Summer, Autumn, Winter (the field clears, snow falls)
    /// and round again — the game in one loop — under the title and Play / New game / Settings / Credits / Quit.
    /// Settings and Credits reuse the pause sheets; Play fades out and loads Farm.unity.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class MenuBootstrap : MonoBehaviour
    {
        [Tooltip("EN string table (Assets/TillWinter/Unity/Localization/en.json).")]
        public TextAsset StringTable;
        [Tooltip("TR string table (Assets/TillWinter/Unity/Localization/tr.json).")]
        public TextAsset StringTableTr;
        [Tooltip("Seconds per season in the title loop.")]
        public float SeasonSeconds = 5f;

        private const int Grid = 3;
        private const float IslandWidth = 6.4f;
        /// <summary>Where the island centre sits vertically on screen (title above, buttons below).</summary>
        private const float IslandScreenY = 0.6f;
        private const float ButtonWidth = 620f, ButtonHeight = 112f, ButtonGap = 22f, BottomMargin = 170f;
        private static readonly int[] Tiers = { 0, 1, 3, 2, 0, 5, 1, 5, 2 };

        private Camera _cam;
        private Light _sun;
        private VisualCatalog _catalog;
        private SeasonPalette _seasons;
        private Palette _palette;
        private VfxPlayer _fx;
        private PauseMenu _sheets;
        private HudTheme _theme;
        private readonly PaletteBinder[] _soil = new PaletteBinder[Grid * Grid];
        private readonly Transform[] _cropRoots = new Transform[Grid * Grid];
        private readonly GameObject[,] _stages = new GameObject[Grid * Grid, 3];
        private readonly int[] _shownStage = new int[Grid * Grid];
        private readonly float[] _wet = new float[Grid * Grid];
        private float _t;

        private CanvasGroup _group;
        private RectTransform _title;
        private Vector2 _titleBase;
        private readonly List<RectTransform> _buttons = new List<RectTransform>();
        private readonly List<CanvasGroup> _buttonGroups = new List<CanvasGroup>();
        private readonly List<Vector2> _buttonBase = new List<Vector2>();
        private GameObject _confirm;
        private TMP_Text _version;
        private bool _versionFinal;
        private Image _fade;
        private float _leaveT = -1f;

        private void Awake()
        {
            Time.timeScale = 1f;
            Application.targetFrameRate = AppLifecycle.YearFps;
            GameLanguage.Apply(GameLanguage.Resolve(SettingsStore.Current.Language, Application.systemLanguage), StringTable, StringTableTr);
            _theme = HudTheme.Load();
            _catalog = VisualCatalog.Load();
            _palette = Palette.Load();
            _palette.ApplyToMaterials(_catalog.SlotMaterials);
            _seasons = SeasonPalette.Load();

            BuildWorld();
            var audio = new GameObject("Audio").AddComponent<AudioManager>();
            audio.transform.SetParent(transform, false);
            audio.Init();
            _fx = new GameObject("Vfx").AddComponent<VfxPlayer>();
            _fx.transform.SetParent(transform, false);
            _fx.Init(VfxCatalog.Load());
            QualityTiers.Init(_sun, _cam);

            var canvas = GameBootstrap.BuildCanvas(transform);
            _sheets = canvas.gameObject.AddComponent<PauseMenu>();
            _sheets.Init(null, audio, canvas, null); // settings + credits sheets, no running farm
            _sheets.SetButtonVisible(false);
            BuildUi(canvas);
            StartCoroutine(BuildInfo.Load());
            ApplySeason(0f);
        }

        // ------------------------------------------------------------------ world

        private void BuildWorld()
        {
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            camGo.transform.SetParent(transform, false);
            _cam = camGo.AddComponent<Camera>();
            _cam.orthographic = true;
            _cam.nearClipPlane = 0.1f;
            _cam.farClipPlane = 100f;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.allowHDR = false;
            _cam.allowMSAA = false;
            camGo.AddComponent<AudioListener>();
            var data = _cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false;
            data.renderShadows = true;
            _cam.transform.rotation = Quaternion.Euler(50f, 0f, 0f); // the farm camera's 40 degrees from top-down
            FrameCamera();

            _sun = new GameObject("Sun").AddComponent<Light>();
            _sun.transform.SetParent(transform, false);
            _sun.type = LightType.Directional;
            _sun.shadows = LightShadows.Soft;
            _sun.shadowStrength = 0.5f;
            RenderSettings.sun = _sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.fog = false;

            var root = new GameObject("Island").transform;
            root.SetParent(transform, false);
            var block = new GameObject("Block");
            block.transform.SetParent(root, false);
            block.AddComponent<MeshFilter>().sharedMesh = DioramaView.BuildBlock(Grid, 0.8f, 0.9f, 0f);
            var mr = block.AddComponent<MeshRenderer>();
            mr.sharedMaterials = new[] { _catalog.SlotMaterial(PaletteSlot.Grass), _catalog.SlotMaterial(PaletteSlot.SoilBlock) };
            mr.shadowCastingMode = ShadowCastingMode.Off;

            for (int i = 0; i < Grid * Grid; i++)
            {
                int x = i % Grid, y = i / Grid;
                var plot = _catalog.Spawn(_catalog.Plot, root, "Plot");
                plot.transform.localPosition = new Vector3(x - 1f, 0f, y - 1f);
                _soil[i] = plot.GetComponent<PaletteBinder>();
                var anchor = plot.transform.Find("CropAnchor");
                if (anchor == null)
                {
                    anchor = new GameObject("CropAnchor").transform;
                    anchor.SetParent(plot.transform, false);
                    anchor.localPosition = new Vector3(0f, 0.16f, 0f);
                }
                _cropRoots[i] = anchor;
                var tier = _catalog.Crops[Tiers[i] % _catalog.Crops.Length];
                for (int s = 0; s < 3; s++)
                {
                    _stages[i, s] = _catalog.Spawn(tier?.Stage(s), anchor, "Stage" + s);
                    _stages[i, s].SetActive(false);
                }
                _shownStage[i] = -1;
            }
            if (_catalog.Trees != null && _catalog.Trees.Length > 0)
                Place(root, _catalog.Trees[_catalog.Trees.Length > 3 ? 3 : 0], new Vector3(-2.05f, 0f, 1.9f), 30f, 0.9f);
            Place(root, _catalog.Bush, new Vector3(2.0f, 0f, -1.95f), 20f, 0.8f);
        }

        private void Place(Transform parent, GameObject prefab, Vector3 pos, float yaw, float scale)
        {
            if (prefab == null) return;
            var go = _catalog.Spawn(prefab, parent, prefab.name);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = Vector3.one * scale;
        }

        private void FrameCamera()
        {
            float size = IslandWidth / (2f * Mathf.Max(0.2f, _cam.aspect));
            _cam.orthographicSize = size;
            float shift = (0.5f - IslandScreenY) * 2f * size;
            var lookAt = _cam.transform.up * shift;
            _cam.transform.position = lookAt - _cam.transform.forward * 30f;
        }

        // ------------------------------------------------------------------ ui

        private void BuildUi(RectTransform canvas)
        {
            var root = UiKit.Rect("Title", canvas);
            UiKit.Stretch(root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _group = root.gameObject.AddComponent<CanvasGroup>();
            var shade = UiKit.Panel(root, "BottomShade", _theme.MenuShade, false, false);
            shade.sprite = VerticalFade();
            shade.type = Image.Type.Simple;
            UiKit.Stretch(shade.rectTransform, Vector2.zero, new Vector2(1f, 0.46f), Vector2.zero, Vector2.zero);

            var safe = UiKit.Rect("Safe", root);
            SafeArea.Apply(safe);
            var title = UiKit.Label(safe, "Name", Strings.Get("menu.title"), UiType.Display, _theme.MenuTitle, TextAnchor.MiddleCenter, FontStyle.Bold);
            _title = title.rectTransform;
            UiKit.Box(_title, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -230f), new Vector2(1040f, 200f));
            UiKit.Outline(title, 0.24f);
            _titleBase = _title.anchoredPosition;
            var subtitle = UiKit.Label(safe, "Subtitle", Strings.Get("menu.subtitle"), UiType.Heading, _theme.MenuSubtitle, TextAnchor.MiddleCenter);
            UiKit.Box(subtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -410f), new Vector2(1000f, 70f));
            UiKit.Outline(subtitle, 0.16f);

            bool hasSave = File.Exists(SaveController.FilePath);
            var entries = new List<(string key, UnityAction action, bool primary)> { (hasSave ? "menu.continue" : "menu.play", StartGame, true) };
            if (hasSave) entries.Add(("menu.new_game", () => _confirm.SetActive(true), false));
            entries.Add(("menu.settings", () => _sheets.OpenSettingsFrom(null), false));
            entries.Add(("menu.credits", () => _sheets.OpenCreditsFrom(null), false));
            if (Application.platform != RuntimePlatform.IPhonePlayer) entries.Add(("menu.quit", Application.Quit, false)); // iOS apps never quit themselves
            for (int i = 0; i < entries.Count; i++)
            {
                var (key, action, primary) = entries[i];
                var b = UiKit.Button(safe, key, Strings.Get(key), primary ? UiType.Title : UiType.Heading, primary ? _theme.MenuPrimary : _theme.MenuSecondary, _theme.MenuButtonText, action);
                if (primary) UiKit.ButtonLabel(b).fontStyle = FontStyles.Bold;
                var rt = b.GetComponent<RectTransform>();
                float y = BottomMargin + (entries.Count - 1 - i) * (ButtonHeight + ButtonGap) + (primary ? ButtonGap : 0f);
                UiKit.Box(rt, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, y), new Vector2(primary ? ButtonWidth + 60f : ButtonWidth, primary ? ButtonHeight + 20f : ButtonHeight));
                _buttons.Add(rt);
                _buttonGroups.Add(b.gameObject.AddComponent<CanvasGroup>());
                _buttonBase.Add(rt.anchoredPosition);
            }
            _version = UiKit.Label(safe, "Version", "", UiType.Label, _theme.MenuSubtitle, TextAnchor.MiddleCenter);
            UiKit.Box(_version.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 80f), new Vector2(900f, 50f));
            RefreshVersion();
            BuildConfirm(root);

            _fade = UiKit.Panel(canvas, "Fade", _theme.BootFade, false, false);
            UiKit.Stretch(_fade.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _fade.transform.SetAsLastSibling();
        }

        private void BuildConfirm(RectTransform root)
        {
            var overlay = UiKit.Panel(root, "NewGameConfirm", _theme.SheetOverlay, false, true);
            UiKit.Stretch(overlay.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _confirm = overlay.gameObject;
            var page = UiKit.Panel(overlay.transform, "Page", _theme.SheetPaper, true, true);
            UiKit.Box(page.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 560f));
            var t = UiKit.Label(page.transform, "Title", Strings.Get("menu.new_game_title"), UiType.Title, _theme.SheetInk, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Box(t.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(820f, 90f));
            var body = UiKit.Label(page.transform, "Body", Strings.Get("menu.new_game_body"), UiType.Body, _theme.SheetMuted, TextAnchor.MiddleCenter);
            UiKit.Box(body.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(800f, 160f));
            var yes = UiKit.Button(page.transform, "Yes", Strings.Get("menu.new_game_yes"), UiType.Body, _theme.SheetDanger, _theme.SheetButtonText, NewGame);
            UiKit.Box(yes.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-200f, 50f), new Vector2(360f, 104f));
            var no = UiKit.Button(page.transform, "Cancel", Strings.Get("ui.cancel"), UiType.Body, _theme.SheetIdle, _theme.SheetButtonText, () => _confirm.SetActive(false));
            UiKit.Box(no.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(200f, 50f), new Vector2(360f, 104f));
            _confirm.AddComponent<SheetTransition>().Page = page.rectTransform;
            _confirm.SetActive(false);
        }

        /// <summary>A 1x64 alpha ramp, opaque at the bottom edge; tinted by the theme colour.</summary>
        private static Sprite VerticalFade()
        {
            var tex = new Texture2D(1, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 64; y++)
            {
                float a = 1f - y / 63f;
                tex.SetPixel(0, y, new Color(1f, 1f, 1f, a * a * (3f - 2f * a)));
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0f, 0f, 1f, 64f), new Vector2(0.5f, 0.5f));
        }

        private void RefreshVersion()
        {
            var info = BuildInfo.Current;
            _versionFinal = info != null;
            _version.text = info != null && info.Build > 0
                ? Strings.Format("settings.version", ("version", info.Version), ("build", info.Build))
                : Strings.Format("settings.version_editor", ("version", Application.version));
        }

        // ------------------------------------------------------------------ flow

        private void StartGame()
        {
            if (_leaveT >= 0f) return;
            _leaveT = 0f;
            Haptics.Play(HapticKind.Selection);
        }

        private void NewGame()
        {
            SaveController.DeleteFiles(SaveController.FilePath);
            _confirm.SetActive(false);
            StartGame();
        }

        // ------------------------------------------------------------------ loop

        private void Update()
        {
            float dt = Time.deltaTime;
            _t += dt;
            ApplySeason(dt);
            if (!_versionFinal && BuildInfo.Current != null) RefreshVersion();
            AnimateUi(Time.timeSinceLevelLoad);

            if (_leaveT >= 0f)
            {
                _leaveT += Time.unscaledDeltaTime;
                var c = _fade.color;
                c.a = Mathf.Clamp01(_leaveT / 0.35f);
                _fade.color = c;
                if (_leaveT >= 0.4f) { _leaveT = -2f; SceneManager.LoadScene(SceneNames.Farm); }
            }
            else if (_leaveT > -2f)
            {
                var c = _fade.color;
                c.a = Mathf.Max(0f, 1f - Time.timeSinceLevelLoad / 0.6f); // fade in, like the farm
                _fade.color = c;
                _fade.raycastTarget = false;
            }
        }

        private void LateUpdate() => FrameCamera(); // keeps the fit when the aspect changes

        /// <summary>Season light and sky, snow and petals, and the crop wave across the nine plots.</summary>
        private void ApplySeason(float dt)
        {
            float cycle = SeasonSeconds * 4f;
            float ct = _t % cycle;
            int si = Mathf.Min(3, (int)(ct / SeasonSeconds));
            float st = (ct - si * SeasonSeconds) / SeasonSeconds;
            var season = (Season)si;
            var next = (Season)((si + 1) % 4);
            float blend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((st - 0.7f) / 0.3f));
            var look = SeasonLook.Lerp(_seasons.For(season), _seasons.For(next), blend);

            _sun.color = look.Light;
            _sun.intensity = look.Intensity;
            _sun.transform.rotation = Quaternion.Euler(look.Angle);
            RenderSettings.ambientSkyColor = look.AmbientSky;
            RenderSettings.ambientEquatorColor = look.AmbientEquator;
            RenderSettings.ambientGroundColor = look.AmbientGround;
            _cam.backgroundColor = Color.Lerp(look.SkyBottom, look.SkyTop, 0.35f);
            PaletteBinder.SetSeason(look.SnowAmount, look.LeafTint, look.GrassTint);
            bool winter = season == Season.Winter;
            _fx.SetRate(VfxId.Snow, winter ? 45f : 0f);
            _fx.SetRate(VfxId.Petals, season == Season.Spring ? 4f : 0f);
            _fx.SetRate(VfxId.Leaves, season == Season.Autumn ? 7f : 0f);

            // Growing year: 0..1 across Spring to Autumn; each plot starts a little later along a diagonal wave.
            float year = winter ? 1f : (si + st) / 3f;
            for (int i = 0; i < Grid * Grid; i++)
            {
                int x = i % Grid, y = i / Grid;
                float local = winter ? 0f : Mathf.Clamp01((year - (x + (Grid - 1 - y)) * 0.06f) / 0.7f);
                int stage = local < 0.34f ? 0 : local < 0.68f ? 1 : 2;
                if (stage != _shownStage[i])
                {
                    for (int s = 0; s < 3; s++) _stages[i, s].SetActive(s == stage);
                    _shownStage[i] = stage;
                }
                float scale = winter ? 0f : local <= 0f ? 0f : 0.35f + 0.65f * Prims.EaseOutQuad(local);
                var root = _cropRoots[i];
                float current = Mathf.MoveTowards(root.localScale.y, scale, dt * 3f);
                float wobble = stage == 2 && !winter ? Mathf.Sin(_t * 5f + i) * 5f : 0f;
                root.localScale = new Vector3(Mathf.Lerp(0.7f, 1f, current), Mathf.Max(0.0001f, current), Mathf.Lerp(0.7f, 1f, current));
                root.localRotation = Quaternion.Euler(0f, 0f, wobble);
                _wet[i] = Mathf.MoveTowards(_wet[i], !winter && local > 0f ? 1f : 0f, dt * 2f);
                if (_soil[i] != null) _soil[i].Override(PaletteSlot.SoilDry, Color.Lerp(_palette.SoilDry, _palette.SoilWet, _wet[i]));
            }
        }

        /// <summary>Fade in, gentle title bob, buttons rising in one after another.</summary>
        private void AnimateUi(float t)
        {
            _group.alpha = Mathf.Clamp01(t / 0.5f);
            _title.anchoredPosition = _titleBase + new Vector2(0f, Mathf.Sin(Time.unscaledTime * 1.1f) * 8f);
            for (int i = 0; i < _buttons.Count; i++)
            {
                float k = Mathf.Clamp01((t - 0.3f - i * 0.07f) / 0.35f);
                float e = 1f - (1f - k) * (1f - k);
                _buttons[i].anchoredPosition = _buttonBase[i] - new Vector2(0f, 40f * (1f - e));
                _buttonGroups[i].alpha = e;
            }
        }
    }
}
