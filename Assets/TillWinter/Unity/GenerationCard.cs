using System;
using TillWinter.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TillWinter.Unity
{
    /// <summary>Retire transition: full-screen "Generation N" card with a flavour line and the seeds counting up. Tap after 0.5 s to skip.</summary>
    public sealed class GenerationCard : MonoBehaviour
    {
        private GameController _game;
        private AudioManager _audio;
        private GameObject _panel;
        private CanvasGroup _group;
        private TMP_Text _title, _flavour, _seeds, _tap, _sealNumber;
        private RectTransform _seal;
        private float _t;
        private int _seedsTarget;
        private bool _open;
        private Action _onDone;
        private float _lastTick;
        private int _shownSeeds;

        public bool IsOpen => _open;

        public void Init(GameController game, AudioManager audio, RectTransform canvas)
        {
            _game = game;
            _audio = audio;
            var theme = TreeTheme.Load("HeritageTheme");
            _theme = theme;
            var bg = UiKit.Panel(canvas, "GenerationCard", theme.Overlay, false, true);
            _panel = bg.gameObject;
            _group = _panel.AddComponent<CanvasGroup>();
            var btn = _panel.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => { if (_t > 0.5f) Finish(); });
            // A seal behind the title: the generation's number, stamped.
            var sealRing = UiKit.CircleImage(bg.transform, "Seal", new Color(theme.Gold.r, theme.Gold.g, theme.Gold.b, 0.18f), new Vector2(0f, 250f), 300f);
            _seal = sealRing.rectTransform;
            UiKit.CircleImage(_seal, "SealInner", theme.Overlay, Vector2.zero, 250f);
            _sealNumber = UiKit.Label(_seal, "Number", "", UiType.Display, new Color(theme.Gold.r, theme.Gold.g, theme.Gold.b, 0.55f), TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Stretch(_sealNumber.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _title = UiKit.Label(bg.transform, "Title", "", UiType.Hero, theme.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Box(_title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 160f), new Vector2(1000f, 110f));
            _flavour = UiKit.Label(bg.transform, "Flavour", "", UiType.Body, theme.Ink, TextAnchor.MiddleCenter, FontStyle.Italic);
            UiKit.Box(_flavour.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(900f, 90f));
            UiKit.CircleImage(bg.transform, "Seed", theme.Seed, new Vector2(-410f, -60f), 56f);
            _seeds = UiKit.Label(bg.transform, "Seeds", "", UiType.Big, theme.Seed, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiKit.Box(_seeds.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(40f, -60f), new Vector2(760f, 80f));
            _seeds.enableWordWrapping = false;
            // What the generation that just ended did: the album page it wrote, on the card that marks its passing.
            _summary = UiKit.Label(bg.transform, "Summary", "", UiType.Label, theme.InkMuted, TextAnchor.MiddleCenter);
            UiKit.Box(_summary.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -170f), new Vector2(900f, 50f));
            _summary.enableWordWrapping = false;
            _summary.enableAutoSizing = true;
            _summary.fontSizeMin = 22f;
            _stars = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                _stars[i] = NodeIcons.Image(bg.transform, "star", theme.Gold);
                UiKit.Box(_stars[i].rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2((i - 1) * 54f, -240f), Vector2.one * 42f);
            }
            _tap = UiKit.Label(bg.transform, "Tap", Strings.Get("gen.tap_to_continue"), UiType.Label, theme.InkMuted, TextAnchor.MiddleCenter);
            UiKit.Box(_tap.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 120f), new Vector2(600f, 40f));
            // Cinematic bars slide in: passing on the farm is the biggest moment in the game.
            var barColor = new Color(theme.PaperVignette.r, theme.PaperVignette.g, theme.PaperVignette.b, 1f);
            _barTop = UiKit.Panel(bg.transform, "BarTop", barColor, false, false).rectTransform;
            UiKit.Stretch(_barTop, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -BarHeight), Vector2.zero);
            _barBottom = UiKit.Panel(bg.transform, "BarBottom", barColor, false, false).rectTransform;
            UiKit.Stretch(_barBottom, Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, BarHeight));
            _tap.transform.SetAsLastSibling();
            _panel.SetActive(false);
        }

        private TreeTheme _theme;
        private TMP_Text _summary;
        private Image[] _stars;
        private const float BarHeight = 150f;
        private RectTransform _barTop, _barBottom;

        public void Show(RetireEvent e, Action onDone)
        {
            _onDone = onDone;
            _seedsTarget = e.SeedsEarned;
            _t = 0f;
            _lastTick = 0f;
            _title.text = Strings.Format("gen.title", ("gen", e.Generation));
            _sealNumber.text = e.Generation.ToString();
            string key = "gen.flavour." + e.Generation;
            string flavour = Strings.Get(key);
            _flavour.text = flavour == key ? Strings.Get("gen.flavour.default") : flavour;
            var album = _game.State.Album;
            var page = album.Count > 0 ? album[album.Count - 1] : null;
            _summary.text = page == null ? "" : Strings.Format("album.line",
                ("years", page.Years), ("coins", NumberFormat.Short(page.Coins)),
                ("seeds", page.Seeds), ("harvests", NumberFormat.Whole(page.Harvests)));
            for (int i = 0; i < _stars.Length; i++)
            {
                bool earned = page != null && i < page.BestGrade;
                var c = earned ? _theme.Gold : new Color(_theme.InkMuted.r, _theme.InkMuted.g, _theme.InkMuted.b, 0.3f);
                _stars[i].color = c;
            }
            _shownSeeds = 0;
            SplitSeedsTemplate();
            WriteSeeds(0);
            _panel.transform.SetAsLastSibling();
            _panel.SetActive(true);
            _group.alpha = 0f;
            _open = true;
            _game.InputBlocked = true;
        }

        /// <summary>Tap-to-skip; only after the 0.5 s guard (same rule as the panel button).</summary>
        public void Skip() { if (_t > 0.5f) Finish(); }

        private void Finish()
        {
            if (!_open) return;
            _open = false;
            _panel.SetActive(false);
            _onDone?.Invoke();
        }

        // The count-up rewrites the seed line every few frames: into a char buffer, not a new string each time.
        private readonly char[] _seedChars = new char[128];
        private readonly char[] _numChars = new char[32];
        private string _seedHead = "", _seedTail = "";

        private void SplitSeedsTemplate()
        {
            string t = Strings.Get("gen.seeds");
            int at = t.IndexOf("{seeds}", System.StringComparison.Ordinal);
            _seedHead = at < 0 ? t : t.Substring(0, at);
            _seedTail = at < 0 ? "" : t.Substring(at + "{seeds}".Length);
        }

        private void WriteSeeds(int seeds)
        {
            int n = 0;
            for (int i = 0; i < _seedHead.Length && n < _seedChars.Length; i++) _seedChars[n++] = _seedHead[i];
            int digits = NumberFormat.Short(seeds, _numChars);
            for (int i = 0; i < digits && n < _seedChars.Length; i++) _seedChars[n++] = _numChars[i];
            for (int i = 0; i < _seedTail.Length && n < _seedChars.Length; i++) _seedChars[n++] = _seedTail[i];
            _seeds.SetText(_seedChars, 0, n);
        }

        private void Update()
        {
            if (!_open) return;
            float dt = Time.unscaledDeltaTime;
            _t += dt;
            _group.alpha = Mathf.Clamp01(_t / 0.4f);
            float count = Mathf.Clamp01((_t - 0.6f) / 1.4f);
            int shown = Mathf.RoundToInt(Prims.EaseOutQuad(count) * _seedsTarget);
            if (shown != _shownSeeds) { _shownSeeds = shown; WriteSeeds(shown); }
            if (count < 1f && _t - _lastTick > 0.08f) { _lastTick = _t; _audio.Play(SfxId.CoinArrive, 0.6f); }
            _tap.alpha = _t > 0.5f ? 0.5f + 0.5f * Mathf.Abs(Mathf.Sin(_t * 2f)) : 0f;
            float bars = UiMotion.EaseOut(Mathf.Clamp01(_t / 0.6f));
            _barTop.anchoredPosition = new Vector2(0f, BarHeight * (1f - bars));
            _barBottom.anchoredPosition = new Vector2(0f, -BarHeight * (1f - bars));
            _seal.localScale = Vector3.one * Mathf.Lerp(0.85f, 1f, Prims.EaseOutQuad(Mathf.Clamp01(_t / 0.6f)));
            _seal.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-6f, 0f, Prims.EaseOutQuad(Mathf.Clamp01(_t / 0.8f))));
            if (_t > 6f) Finish();
        }
    }
}
