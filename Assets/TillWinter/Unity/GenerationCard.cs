using System;
using TillWinter.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TillWinter.Unity
{
    /// <summary>
    /// The album page a generation writes when the farm is handed on (v3.4): its photograph, a few lines in the
    /// margin, the seeds it earned counting up, and the heirs on offer with the challenge switch. Continue opens
    /// the Heritage field. Reopened from the Heritage screen's heir link to change the choice.
    /// </summary>
    public sealed class GenerationCard : MonoBehaviour
    {
        private GameController _game;
        private AudioManager _audio;
        private TreeTheme _theme;
        private GameObject _panel;
        private CanvasGroup _group;
        private TMP_Text _title, _years, _flavour, _lines, _seeds, _heirTitle, _challenge;
        private Image[] _stars;
        private Image[] _cells;
        private readonly Button[] _heirs = new Button[3];
        private Button _challengeButton;
        private float _t;
        private int _seedsTarget;
        private bool _open, _counting;
        private Action _onDone;
        private float _lastTick;
        private int _shownSeeds;
        private int _heirKey = -1;
        private const int PhotoCells = 6;

        public bool IsOpen => _open;

        public void Init(GameController game, AudioManager audio, RectTransform canvas)
        {
            _game = game;
            _audio = audio;
            var theme = TreeTheme.Load("HeritageTheme");
            _theme = theme;
            var bg = UiKit.Panel(canvas, "GenerationCard", theme.Card, false, true);
            _panel = bg.gameObject;
            _group = _panel.AddComponent<CanvasGroup>();
            var page = UiKit.Rect("Page", bg.transform);
            UiKit.Stretch(page, Vector2.zero, Vector2.one, new Vector2(44f, 40f), new Vector2(-44f, -60f));

            // The generation, and how long it farmed.
            _title = UiKit.Label(page, "Title", "", UiType.Title, theme.Ink, TextAnchor.MiddleLeft);
            UiKit.Stretch(_title.rectTransform, new Vector2(0f, 1f), new Vector2(0.6f, 1f), new Vector2(0f, -110f), new Vector2(0f, -30f));
            _years = UiKit.Label(page, "Years", "", UiType.Body, theme.InkMuted, TextAnchor.MiddleRight, FontStyle.Bold);
            UiKit.Stretch(_years.rectTransform, new Vector2(0.6f, 1f), new Vector2(1f, 1f), new Vector2(0f, -110f), new Vector2(0f, -30f));

            // The photograph: a small field in a white frame, slightly askew, glued to the page.
            var frame = UiKit.Panel(page, "Photo", Color.white, true, false);
            var frameRt = frame.rectTransform;
            UiKit.Box(frameRt, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(210f, -390f), new Vector2(400f, 470f));
            frameRt.localRotation = Quaternion.Euler(0f, 0f, 2.5f);
            var shadow = frame.gameObject.AddComponent<UnityEngine.UI.Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.18f);
            shadow.effectDistance = new Vector2(0f, -10f);
            var photo = UiKit.Panel(frame.transform, "Field", theme.FieldSoil, true, false);
            UiKit.Box(photo.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(352f, 352f));
            _cells = new Image[PhotoCells * PhotoCells];
            float cell = 352f / PhotoCells;
            for (int r = 0; r < PhotoCells; r++)
                for (int c = 0; c < PhotoCells; c++)
                {
                    var bed = UiKit.Panel(photo.transform, "Bed", theme.BedGrown, true, false);
                    UiKit.Box(bed.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(c * cell + 6f, -(r * cell + 6f)), Vector2.one * (cell - 12f));
                    _cells[r * PhotoCells + c] = bed;
                }
            _flavour = UiKit.Label(frame.transform, "Caption", "", UiType.Caption, theme.InkMuted, TextAnchor.MiddleCenter, FontStyle.Italic);
            UiKit.Stretch(_flavour.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(16f, 14f), new Vector2(-16f, 84f));

            // The lines in the margin: what the generation did.
            _lines = UiKit.Label(page, "Lines", "", UiType.Body, theme.Ink, TextAnchor.UpperLeft, FontStyle.Italic);
            UiKit.Stretch(_lines.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(450f, -640f), new Vector2(0f, -170f));
            _lines.lineSpacing = 18f;
            _stars = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                _stars[i] = NodeIcons.Image(page, "star", theme.Gold);
                UiKit.Box(_stars[i].rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(454f + i * 52f, -680f), Vector2.one * 44f);
            }

            // The seeds, counting up.
            var seedDot = UiKit.CircleImage(page, "Seed", theme.Seed, Vector2.zero, 40f);
            UiKit.Box(seedDot.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(20f, -800f), Vector2.one * 40f);
            _seeds = UiKit.Label(page, "Seeds", "", UiType.Big, theme.Seed, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiKit.Stretch(_seeds.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(84f, -850f), new Vector2(0f, -750f));
            _seeds.enableWordWrapping = false;
            _seeds.enableAutoSizing = true;
            _seeds.fontSizeMin = 30f;
            _seeds.fontSizeMax = _seeds.fontSize;

            // A rule, then who takes the farm.
            var rule = UiKit.Panel(page, "Rule", new Color(theme.Ink.r, theme.Ink.g, theme.Ink.b, 0.14f), false, false);
            UiKit.Stretch(rule.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -902f), new Vector2(0f, -900f));
            _heirTitle = UiKit.Label(page, "HeirTitle", Strings.Get("heir.title"), UiType.Body, theme.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiKit.Stretch(_heirTitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -980f), new Vector2(0f, -920f));
            for (int i = 0; i < _heirs.Length; i++)
            {
                int index = i;
                _heirs[i] = UiKit.Button(page, "Heir" + i, "", UiType.Label, theme.Paper, theme.Ink, () =>
                {
                    if (_game.Sim.ChooseHeir(index)) { Haptics.Play(HapticKind.Selection); _heirKey = -1; }
                });
                UiKit.Box(_heirs[i].GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(i * 332f, -1000f), new Vector2(316f, 160f));
                var label = UiKit.ButtonLabel(_heirs[i]);
                label.richText = true;
                label.enableWordWrapping = true;
                label.fontSizeMin = 16f;
            }
            _challengeButton = UiKit.Button(page, "Challenge", "", UiType.Label, theme.Paper, theme.Ink, CycleChallenge);
            UiKit.Stretch(_challengeButton.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -1290f), new Vector2(0f, -1184f));
            _challenge = UiKit.ButtonLabel(_challengeButton);
            _challenge.enableWordWrapping = true;
            _challenge.fontSizeMin = 16f;

            var go = UiKit.Button(page, "Continue", Strings.Get("gen.continue") + "  »", UiType.Heading, theme.Accent, theme.Ink, Finish);
            UiKit.Box(go.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 4f), new Vector2(992f, 150f));
            _panel.SetActive(false);
        }

        /// <summary>The page for a farm just handed on: the seeds count up with a tick.</summary>
        public void Show(RetireEvent e, Action onDone)
        {
            Fill(e.Generation - 1, e.SeedsEarned);
            _counting = true;
            _shownSeeds = 0;
            WriteSeeds(0);
            Present(onDone);
        }

        /// <summary>The same page reopened to change the heir: nothing counts, the seeds are already in the jar.</summary>
        public void ShowHeirs(Action onDone)
        {
            var album = _game.State.Album;
            var page = album.Count > 0 ? album[album.Count - 1] : null;
            Fill(page != null ? page.Generation : _game.State.Generation.Generation - 1, page != null ? page.Seeds : 0);
            _counting = false;
            _shownSeeds = _seedsTarget;
            WriteSeeds(_seedsTarget);
            Present(onDone);
        }

        private void Present(Action onDone)
        {
            _onDone = onDone;
            _t = 0f;
            _lastTick = 0f;
            _heirKey = -1;
            _panel.transform.SetAsLastSibling();
            _panel.SetActive(true);
            _group.alpha = 0f;
            _open = true;
            _game.InputBlocked = true;
        }

        private void Fill(int generation, int seeds)
        {
            _seedsTarget = seeds;
            var album = _game.State.Album;
            AlbumEntry page = null;
            foreach (var entry in album) if (entry.Generation == generation) page = entry;
            if (page == null && album.Count > 0) page = album[album.Count - 1];
            _title.text = Strings.Format("gen.title", ("gen", generation));
            _years.text = page == null ? "" : Strings.Format("gen.years", ("years", page.Years));
            string key = "gen.flavour." + (generation + 1);
            string flavour = Strings.Get(key);
            _flavour.text = flavour == key ? Strings.Get("gen.flavour.default") : flavour;
            // The album line, one item per margin note; the years already stand in the corner, so they are left out.
            string line = page == null ? "" : Strings.Format("album.line", ("years", page.Years), ("coins", NumberFormat.Short(page.Coins)),
                ("seeds", page.Seeds), ("harvests", NumberFormat.Whole(page.Harvests)));
            int firstBreak = line.IndexOf(" · ", StringComparison.Ordinal);
            _lines.text = firstBreak < 0 ? line : line.Substring(firstBreak + 3).Replace(" · ", "\n");
            for (int i = 0; i < _stars.Length; i++)
            {
                bool earned = page != null && i < page.BestGrade;
                _stars[i].color = earned ? _theme.Gold : new Color(_theme.InkMuted.r, _theme.InkMuted.g, _theme.InkMuted.b, 0.25f);
            }
            // The photograph: the field as that generation left it, drawn from its numbers, the same every time it is looked at.
            var rnd = new System.Random(1000 + generation * 7);
            double worked = page == null ? 0.5 : Math.Min(0.95, 0.3 + page.Harvests / 400.0);
            for (int i = 0; i < _cells.Length; i++)
            {
                double k = rnd.NextDouble();
                _cells[i].color = k < worked ? _theme.BedGrown : k < worked + 0.25 ? _theme.BedTilled : _theme.BedHard;
            }
            SplitSeedsTemplate();
        }

        private void CycleChallenge()
        {
            var next = (ChallengeKind)(((int)_game.State.Generation.Challenge + 1) % 3);
            if (_game.Sim.SetChallenge(next)) { Haptics.Play(HapticKind.Selection); _heirKey = -1; }
        }

        private void RefreshHeirs()
        {
            var g = _game.State.Generation;
            bool show = _game.State.Phase == Phase.Heritage && g.HeirOffer[0] != HeirTrait.None;
            int key = show ? (int)g.Trait * 1000 + (int)g.Challenge * 100 + (int)g.HeirOffer[0] * 49 + (int)g.HeirOffer[1] * 7 + (int)g.HeirOffer[2] : -2;
            if (key == _heirKey) return;
            _heirKey = key;
            _heirTitle.gameObject.SetActive(show);
            _challengeButton.gameObject.SetActive(show);
            for (int i = 0; i < _heirs.Length; i++)
            {
                _heirs[i].gameObject.SetActive(show);
                if (!show) continue;
                var t = g.HeirOffer[i];
                UiKit.ButtonLabel(_heirs[i]).text = "<b>" + Strings.Get("heir." + t) + "</b>\n<size=78%>" + Strings.Get("heir." + t + ".desc") + "</size>";
                _heirs[i].targetGraphic.color = t == g.Trait ? _theme.Accent : _theme.Paper;
            }
            if (!show) return;
            _challenge.text = Strings.Get("challenge." + g.Challenge);
            _challengeButton.targetGraphic.color = g.Challenge != ChallengeKind.None ? _theme.Seed : _theme.Paper;
            _challenge.color = g.Challenge != ChallengeKind.None ? _theme.Card : _theme.Ink;
        }

        /// <summary>Continue; the smoke test and the tour drive it through here.</summary>
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
            int at = t.IndexOf("{seeds}", StringComparison.Ordinal);
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
            if (_counting)
            {
                float count = Mathf.Clamp01((_t - 0.6f) / 1.4f);
                int shown = Mathf.RoundToInt(Prims.EaseOutQuad(count) * _seedsTarget);
                if (shown != _shownSeeds) { _shownSeeds = shown; WriteSeeds(shown); }
                if (count < 1f && _t - _lastTick > 0.08f) { _lastTick = _t; _audio.Play(SfxId.CoinArrive, 0.6f); }
            }
            RefreshHeirs();
        }
    }
}
