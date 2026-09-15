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
        private TMP_Text _title, _flavour, _seeds, _tap;
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
            var bg = UiKit.Panel(canvas, "GenerationCard", theme.Overlay, false, true);
            _panel = bg.gameObject;
            _group = _panel.AddComponent<CanvasGroup>();
            var btn = _panel.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => { if (_t > 0.5f) Finish(); });
            _title = UiKit.Label(bg.transform, "Title", "", 84, theme.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Box(_title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 160f), new Vector2(1000f, 110f));
            _flavour = UiKit.Label(bg.transform, "Flavour", "", 34, theme.Ink, TextAnchor.MiddleCenter, FontStyle.Italic);
            UiKit.Box(_flavour.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(900f, 90f));
            UiKit.CircleImage(bg.transform, "Seed", theme.Seed, new Vector2(-410f, -60f), 56f);
            _seeds = UiKit.Label(bg.transform, "Seeds", "", 60, theme.Seed, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiKit.Box(_seeds.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(40f, -60f), new Vector2(760f, 80f));
            _seeds.enableWordWrapping = false;
            _tap = UiKit.Label(bg.transform, "Tap", Strings.Get("gen.tap_to_continue"), 26, theme.InkMuted, TextAnchor.MiddleCenter);
            UiKit.Box(_tap.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 120f), new Vector2(600f, 40f));
            _panel.SetActive(false);
        }

        public void Show(RetireEvent e, Action onDone)
        {
            _onDone = onDone;
            _seedsTarget = e.SeedsEarned;
            _t = 0f;
            _lastTick = 0f;
            _title.text = Strings.Format("gen.title", ("gen", e.Generation));
            string key = "gen.flavour." + e.Generation;
            string flavour = Strings.Get(key);
            _flavour.text = flavour == key ? Strings.Get("gen.flavour.default") : flavour;
            _shownSeeds = 0;
            _seeds.text = Strings.Format("gen.seeds", ("seeds", 0));
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

        private void Update()
        {
            if (!_open) return;
            float dt = Time.unscaledDeltaTime;
            _t += dt;
            _group.alpha = Mathf.Clamp01(_t / 0.4f);
            float count = Mathf.Clamp01((_t - 0.6f) / 1.4f);
            int shown = Mathf.RoundToInt(Prims.EaseOutQuad(count) * _seedsTarget);
            if (shown != _shownSeeds) { _shownSeeds = shown; _seeds.text = Strings.Format("gen.seeds", ("seeds", shown)); }
            if (count < 1f && _t - _lastTick > 0.08f) { _lastTick = _t; _audio.Play(SfxId.CoinArrive, 0.6f); }
            _tap.alpha = _t > 0.5f ? 0.5f + 0.5f * Mathf.Abs(Mathf.Sin(_t * 2f)) : 0f;
            if (_t > 6f) Finish();
        }
    }
}
