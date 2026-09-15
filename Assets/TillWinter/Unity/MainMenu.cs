using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TillWinter.Unity
{
    /// <summary>
    /// Title screen shown at every launch (and from the pause menu): the farm idles behind it (no sim tick, particles
    /// keep drifting), the game's name on top, Play/Continue, New game, Settings, Statistics, Credits and Quit below.
    /// Sheets reuse <see cref="PauseMenu"/>. Play hides the menu and raises <see cref="Played"/>.
    /// </summary>
    public sealed class MainMenu : MonoBehaviour
    {
        private const float ButtonWidth = 640f, ButtonHeight = 118f, ButtonGap = 24f, BottomMargin = 200f;

        public static MainMenu Instance { get; private set; }
        public bool IsOpen => _root != null && _root.activeSelf;
        public event Action Played;
        /// <summary>Smoke test: the menu screenshot was taken, Play may be pressed.</summary>
        public bool SmokeShotTaken { get; set; }

        private GameController _game;
        private PauseMenu _pause;
        private SaveController _save;
        private HudTheme _theme;
        private GameObject _root, _confirm;
        private CanvasGroup _group;
        private RectTransform _title, _subtitle;
        private Vector2 _titleBase, _subtitleBase;
        private TMP_Text _version;
        private bool _versionFinal;
        private readonly List<RectTransform> _buttons = new List<RectTransform>();
        private readonly List<CanvasGroup> _buttonGroups = new List<CanvasGroup>();
        private readonly List<Vector2> _buttonBase = new List<Vector2>();
        private float _shownAt;

        public void Init(GameController game, RectTransform canvas, PauseMenu pause, SaveController save, bool hasProgress)
        {
            Instance = this;
            _game = game;
            _pause = pause;
            _save = save;
            _theme = HudTheme.Load();

            var root = UiKit.Rect("MainMenu", canvas);
            UiKit.Stretch(root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _root = root.gameObject;
            _group = _root.AddComponent<CanvasGroup>();

            // Shades top and bottom so the title and buttons read over the farm; the middle stays clear.
            var clear = _theme.MenuShade;
            clear.a = 0f;
            var blocker = UiKit.Panel(root, "Blocker", clear, false, true); // taps never reach the field
            UiKit.Stretch(blocker.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Shade(root, "TopShade", new Vector2(0f, 0.6f), Vector2.one, true);
            Shade(root, "BottomShade", Vector2.zero, new Vector2(1f, 0.52f), false);

            var safe = UiKit.Rect("Safe", root);
            SafeArea.Apply(safe);

            var title = UiKit.Label(safe, "Title", Strings.Get("menu.title"), 150, _theme.MenuTitle, TextAnchor.MiddleCenter, FontStyle.Bold);
            _title = title.rectTransform;
            UiKit.Box(_title, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -250f), new Vector2(1040f, 200f));
            UiKit.Outline(title, 0.22f);
            _titleBase = _title.anchoredPosition;
            var subtitle = UiKit.Label(safe, "Subtitle", Strings.Get("menu.subtitle"), 44, _theme.MenuSubtitle, TextAnchor.MiddleCenter);
            _subtitle = subtitle.rectTransform;
            UiKit.Box(_subtitle, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -440f), new Vector2(1000f, 70f));
            UiKit.Outline(subtitle, 0.14f);
            _subtitleBase = _subtitle.anchoredPosition;

            var entries = new List<(string key, UnityAction action, bool primary)>
            {
                (hasProgress ? "menu.continue" : "menu.play", Play, true),
            };
            if (hasProgress) entries.Add(("menu.new_game", () => _confirm.SetActive(true), false));
            entries.Add(("menu.settings", () => _pause.OpenSettingsFrom(null), false));
            entries.Add(("menu.stats", () => _pause.ShowStats(() => { _pause.CloseSheets(); _game.SetPaused(false); }), false));
            entries.Add(("menu.credits", () => _pause.OpenCreditsFrom(null), false));
            if (Application.platform != RuntimePlatform.IPhonePlayer) entries.Add(("menu.quit", Application.Quit, false)); // iOS apps never quit themselves

            for (int i = 0; i < entries.Count; i++)
            {
                var (key, action, primary) = entries[i];
                var b = UiKit.Button(safe, key, Strings.Get(key), primary ? 48 : 38, primary ? _theme.MenuPrimary : _theme.MenuSecondary, _theme.MenuButtonText, action);
                var rt = b.GetComponent<RectTransform>();
                // Stacked from the bottom; the primary button is larger and sits a gap above the rest.
                float h = primary ? ButtonHeight + 22f : ButtonHeight;
                float y = BottomMargin + (entries.Count - 1 - i) * (ButtonHeight + ButtonGap) + (primary ? ButtonGap : 0f);
                UiKit.Box(rt, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, y), new Vector2(primary ? ButtonWidth + 60f : ButtonWidth, h));
                if (primary) UiKit.ButtonLabel(b).fontStyle = FontStyles.Bold;
                _buttons.Add(rt);
                _buttonGroups.Add(b.gameObject.AddComponent<CanvasGroup>());
                _buttonBase.Add(rt.anchoredPosition);
            }

            _version = UiKit.Label(safe, "Version", "", 26, _theme.MenuSubtitle, TextAnchor.MiddleCenter);
            UiKit.Box(_version.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 90f), new Vector2(900f, 50f));

            BuildConfirm(root);
            Show();
        }

        private void BuildConfirm(RectTransform root)
        {
            var overlay = UiKit.Panel(root, "NewGameConfirm", _theme.SheetOverlay, false, true);
            UiKit.Stretch(overlay.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _confirm = overlay.gameObject;
            var page = UiKit.Panel(overlay.transform, "Page", _theme.SheetPaper, true, true);
            UiKit.Box(page.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 560f));
            var t = UiKit.Label(page.transform, "Title", Strings.Get("menu.new_game_title"), 52, _theme.SheetInk, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Box(t.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(820f, 90f));
            var body = UiKit.Label(page.transform, "Body", Strings.Get("menu.new_game_body"), 34, _theme.SheetMuted, TextAnchor.MiddleCenter);
            UiKit.Box(body.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(800f, 160f));
            var yes = UiKit.Button(page.transform, "Yes", Strings.Get("menu.new_game_yes"), 36, _theme.SheetDanger, _theme.SheetButtonText, NewGame);
            UiKit.Box(yes.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-200f, 50f), new Vector2(360f, 104f));
            var no = UiKit.Button(page.transform, "Cancel", Strings.Get("ui.cancel"), 36, _theme.SheetIdle, _theme.SheetButtonText, () => _confirm.SetActive(false));
            UiKit.Box(no.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(200f, 50f), new Vector2(360f, 104f));
            _confirm.SetActive(false);
        }

        private void Shade(RectTransform parent, string name, Vector2 min, Vector2 max, bool topOpaque)
        {
            var img = UiKit.Panel(parent, name, _theme.MenuShade, false, false);
            img.sprite = VerticalFade(topOpaque);
            img.type = Image.Type.Simple;
            UiKit.Stretch(img.rectTransform, min, max, Vector2.zero, Vector2.zero);
        }

        /// <summary>A 1×64 alpha ramp (opaque at the screen edge, clear toward the middle), tinted by the theme colour.</summary>
        private static Sprite VerticalFade(bool topOpaque)
        {
            var tex = new Texture2D(1, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 64; y++)
            {
                float a = topOpaque ? y / 63f : 1f - y / 63f;
                a = a * a * (3f - 2f * a);
                tex.SetPixel(0, y, new Color(1f, 1f, 1f, a));
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0f, 0f, 1f, 64f), new Vector2(0.5f, 0.5f));
        }

        // ------------------------------------------------------------------ flow

        public void Show()
        {
            _root.SetActive(true);
            _root.transform.SetAsLastSibling();
            _confirm.SetActive(false);
            _game.MenuOpen = true;
            if (HudView.Instance != null) HudView.Instance.Hidden = true;
            _pause.SetButtonVisible(false);
            _shownAt = Time.unscaledTime;
            _versionFinal = false;
            RefreshVersion();
            Animate(0f);
        }

        public void Play()
        {
            if (!IsOpen) return;
            _root.SetActive(false);
            _game.MenuOpen = false;
            if (HudView.Instance != null) HudView.Instance.Hidden = false;
            _pause.SetButtonVisible(true);
            Haptics.Play(HapticKind.Selection);
            Played?.Invoke();
        }

        private void NewGame()
        {
            _save.Detach();
            _save.DeleteSave();
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private void RefreshVersion()
        {
            var info = BuildInfo.Current;
            _versionFinal = info != null;
            _version.text = info != null && info.Build > 0
                ? Strings.Format("settings.version", ("version", info.Version), ("build", info.Build))
                : Strings.Format("settings.version_editor", ("version", Application.version));
        }

        private void Update()
        {
            if (!IsOpen) return;
            if (!_versionFinal && BuildInfo.Current != null) RefreshVersion();
            Animate(Time.unscaledTime - _shownAt);
        }

        /// <summary>Fade in, title bob, buttons rise in one after another.</summary>
        private void Animate(float t)
        {
            _group.alpha = Mathf.Clamp01(t / 0.5f);
            float bob = Mathf.Sin(Time.unscaledTime * 1.1f) * 8f;
            _title.anchoredPosition = _titleBase + new Vector2(0f, bob);
            _subtitle.anchoredPosition = _subtitleBase + new Vector2(0f, bob * 0.5f);
            for (int i = 0; i < _buttons.Count; i++)
            {
                float k = Mathf.Clamp01((t - 0.25f - i * 0.07f) / 0.35f);
                float e = 1f - (1f - k) * (1f - k);
                _buttons[i].anchoredPosition = _buttonBase[i] - new Vector2(0f, 40f * (1f - e));
                _buttonGroups[i].alpha = e;
            }
        }
    }
}
