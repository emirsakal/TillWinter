using UnityEngine;
using UnityEngine.UI;

namespace TillWinter.Unity
{
    /// <summary>Corner toggle + panel: time scale, +1000 coins, skip to Winter, ring offset, ring radius override, spawn crow.</summary>
    public sealed class DebugPanel : MonoBehaviour
    {
        private GameController _game;
        private AudioManager _audio;
        private GameObject _panel;
        private Text _timeLabel, _offsetLabel, _radiusLabel, _info;
        private Slider _radius;

        public void Init(GameController game, AudioManager audio, RectTransform canvas)
        {
            _game = game;
            _audio = audio;

            var toggle = UiKit.Button(canvas, "DebugToggle", "DBG", 30, new Color(0f, 0f, 0f, 0.45f), UiKit.Paper, Toggle);
            UiKit.Box(toggle.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-30f, -30f), new Vector2(120f, 80f));

            var panel = UiKit.Panel(canvas, "DebugPanel", new Color(0.06f, 0.06f, 0.08f, 0.9f), true, true);
            _panel = panel.gameObject;
            UiKit.Stretch(panel.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(30f, 30f), new Vector2(-30f, 700f));
            var rt = panel.rectTransform;

            float y = -30f;
            _timeLabel = SliderRow(rt, ref y, "Time scale", 0.5f, 8f, _game.TimeScale, v => { _game.TimeScale = v; });
            _offsetLabel = SliderRow(rt, ref y, "Ring offset", 0f, 2f, _game.RingOffsetPlots, v => { _game.RingOffsetPlots = v; });
            _radiusLabel = SliderRow(rt, ref y, "Ring radius override", 0f, 4f, 0f, v => { _game.Sim.DebugSetRingRadiusOverride(v < 0.25f ? (float?)null : v); }, out _radius);

            y -= 20f;
            float bw = (1080f - 60f - 60f - 40f) / 3f;
            ButtonAt(rt, "+1000 coins", 0, y, bw, () => _game.Sim.DebugAddCoins(1000));
            ButtonAt(rt, "Skip to Winter", 1, y, bw, () => _game.Sim.DebugSkipToWinter());
            ButtonAt(rt, "Spawn crow", 2, y, bw, () => _game.Sim.DebugSpawnCrow());
            y -= 110f;

            _info = UiKit.Label(rt, "Info", "", 26, new Color(0.75f, 0.75f, 0.8f), TextAnchor.UpperLeft);
            UiKit.Stretch(_info.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(30f, 20f), new Vector2(-30f, y));

            _panel.SetActive(false);
        }

        private Text SliderRow(RectTransform parent, ref float y, string label, float min, float max, float value, UnityEngine.Events.UnityAction<float> onChanged)
            => SliderRow(parent, ref y, label, min, max, value, onChanged, out _);

        private Text SliderRow(RectTransform parent, ref float y, string label, float min, float max, float value, UnityEngine.Events.UnityAction<float> onChanged, out Slider slider)
        {
            var text = UiKit.Label(parent, label, label, 30, UiKit.Paper, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiKit.Stretch(text.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(30f, y - 44f), new Vector2(-30f, y));
            y -= 50f;
            slider = UiKit.Slider(parent, label + " Slider", min, max, value, onChanged);
            UiKit.Stretch(slider.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(30f, y - 56f), new Vector2(-30f, y));
            y -= 70f;
            return text;
        }

        private void ButtonAt(RectTransform parent, string label, int column, float y, float width, UnityEngine.Events.UnityAction action)
        {
            var b = UiKit.Button(parent, label, label, 28, UiKit.Accent, UiKit.Ink, () => { _audio.Play(SfxId.UiClick); action(); });
            var rt = b.GetComponent<RectTransform>();
            UiKit.Box(rt, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f + column * (width + 20f), y), new Vector2(width, 90f));
            rt.pivot = new Vector2(0f, 1f);
        }

        private void Toggle()
        {
            _audio.Play(SfxId.UiClick);
            _panel.SetActive(!_panel.activeSelf);
        }

        private void LateUpdate()
        {
            if (!_panel.activeSelf) return;
            _timeLabel.text = "Time scale  x" + _game.TimeScale.ToString("0.0");
            _offsetLabel.text = "Ring offset  " + _game.RingOffsetPlots.ToString("0.00") + " plots (toward top)";
            var ov = _game.Sim.DebugRingRadiusOverride;
            _radiusLabel.text = "Ring radius override  " + (ov.HasValue ? ov.Value.ToString("0.0") + " plots" : "off (upgrade-driven " + _game.State.RingRadius.ToString("0.0") + ")");
            var s = _game.State;
            _info.text = "Year " + s.Year + "  " + s.Season + "  t=" + s.YearTime.ToString("0.0") + "/" + s.YearLength.ToString("0") + "s"
                         + "   coins " + s.Coins.ToString("0")
                         + "\nfield " + s.GridSize + "x" + s.GridSize + "   crows " + s.Crows.Count + "   apprentice " + (s.Apprentice.Owned ? "owned" : "no")
                         + "   audio " + (_audio.UsingKenneyClips ? "kenney" : "generated")
                         + "\nfps " + (1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime)).ToString("0");
        }
    }
}
