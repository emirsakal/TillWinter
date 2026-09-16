#if TW_DEBUG || UNITY_EDITOR
// Compiled out of release builds (build pipeline: TW_DEBUG only for development builds).
using System.Collections.Generic;
using TillWinter.Core;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace TillWinter.Unity
{
    /// <summary>
    /// Corner toggle + panel: time scale, ring offset, ring radius override, +1000 coins, skip to Winter,
    /// spawn crow, force Ripe on all plots, a per-node level editor, and the resolved stats.
    /// </summary>
    public sealed class DebugPanel : MonoBehaviour
    {
        private GameController _game;
        private AudioManager _audio;
        private GameObject _panel;
        private TMP_Text _timeLabel, _offsetLabel, _radiusLabel, _info, _stats, _nodeLevel;
        private UnityEngine.UI.Button _qualityBtn;

        private SaveController _save;
        private AwayCard _away;

        public void Init(GameController game, AudioManager audio, RectTransform canvas, SaveController save, AwayCard away)
        {
            _game = game;
            _audio = audio;
            _save = save;
            _away = away;

            // Its own toggle in the play scene while the game is in development: a panel that covers the field has to
            // be one tap away from gone, not three taps deep in Settings.
            var toggle = UiKit.Button(canvas, "DebugToggle", "DEV", UiType.Label, new Color(0.06f, 0.06f, 0.08f, 0.75f), UiKit.Paper, Toggle);
            UiKit.Box(toggle.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -24f), new Vector2(104f, 72f));

            var panel = UiKit.Panel(canvas, "DebugPanel", new Color(0.06f, 0.06f, 0.08f, 0.92f), true, true);
            _panel = panel.gameObject;
            UiKit.Stretch(panel.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(30f, 30f), new Vector2(-30f, 1550f));
            var rt = panel.rectTransform;

            float y = -24f;
            _timeLabel = SliderRow(rt, ref y, "Time scale", 0.5f, 8f, _game.TimeScale, v => { _game.TimeScale = v; });
            _offsetLabel = SliderRow(rt, ref y, "Ring offset", 0f, 2f, _game.RingOffsetPlots, v => { _game.RingOffsetPlots = v; });
            _radiusLabel = SliderRow(rt, ref y, "Ring radius override", 0f, 4f, 0f, v => { _game.Sim.DebugSetRingRadiusOverride(v < 0.25f ? (float?)null : v); });

            y -= 10f;
            float bw = (1080f - 60f - 60f - 60f) / 4f;
            ButtonAt(rt, "+1000 coins", 0, y, bw, () => _game.Sim.DebugAddCoins(1000));
            ButtonAt(rt, "Skip to Winter", 1, y, bw, () => _game.Sim.DebugSkipToWinter());
            ButtonAt(rt, "Spawn crow", 2, y, bw, () => _game.Sim.DebugSpawnCrow());
            ButtonAt(rt, "Ripe all", 3, y, bw, () => _game.Sim.DebugForceRipeAll());
            y -= 100f;
            ButtonAt(rt, "+50 seeds", 0, y, bw, () => _game.Sim.DebugAddSeeds(50));
            ButtonAt(rt, "Force CanRetire", 1, y, bw, () => _game.Sim.DebugAddLifetimeCoins(_game.Sim.Config.HeritageThreshold));
            ButtonAt(rt, "Offline 1 h", 2, y, bw, () => { var r = _game.Sim.SimulateOffline(3600); if (r.CoinsEarned > 0) _away.Show(r); });
            ButtonAt(rt, "Delete save", 3, y, bw, () => _save.DeleteSave());
            y -= 100f;
            ButtonAt(rt, "Spawn cloud", 0, y, bw, () => _game.Sim.DebugSpawnCloud());
            ButtonAt(rt, "Next golden", 1, y, bw, () => _game.Sim.DebugNextHarvestGolden());
            ButtonAt(rt, "Tractor sweep", 2, y, bw, () => _game.Sim.DebugForceTractorSweep());
            ButtonAt(rt, "+30 s greenhouse", 3, y, bw, () => _game.Sim.DebugAddGreenhouseSeconds(30f));
            y -= 100f;
            ButtonAt(rt, "Balance table", 0, y, bw, PrintBalance);
            ButtonAt(rt, "Max Heritage", 2, y, bw, () => _game.Sim.DebugMaxHeritage());
            ButtonAt(rt, "Close", 3, y, bw, Toggle);
            _qualityBtn = ButtonAt(rt, "Quality: " + QualityTiers.Current, 1, y, bw, () => { QualityTiers.Toggle(); UiKit.ButtonLabel(_qualityBtn).text = "Quality: " + QualityTiers.Current; });
            y -= 100f;

            _info = UiKit.Label(rt, "Info", "", 24, new Color(0.75f, 0.75f, 0.8f), TextAnchor.UpperLeft);
            UiKit.Stretch(_info.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(30f, y - 110f), new Vector2(-30f, y));
            y -= 116f;

            // Node level editor: dropdown + - / +, then the resolved stats.
            var ids = new List<string>();
            foreach (var n in _game.Sim.Nodes) ids.Add(n.Id);
            foreach (var n in _game.Sim.HeritageNodes) ids.Add(n.Id);
            _nodeIds = ids;
            var dd = UiKit.Dropdown(rt, "NodeDropdown", ids, i => { _nodeIndex = i; });
            UiKit.Box(dd.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, y), new Vector2(560f, 64f));
            _nodeLevel = UiKit.Label(rt, "NodeLevel", "", 26, UiKit.Paper, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Box(_nodeLevel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(690f, y - 32f), new Vector2(160f, 64f));
            var minus = UiKit.Button(rt, "-", "-", 30, new Color(0.35f, 0.3f, 0.3f), UiKit.Paper, () => AdjustSelected(-1));
            UiKit.Box(minus.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(860f, y - 32f), new Vector2(80f, 64f));
            var plus = UiKit.Button(rt, "+", "+", 30, new Color(0.3f, 0.4f, 0.32f), UiKit.Paper, () => AdjustSelected(+1));
            UiKit.Box(plus.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(950f, y - 32f), new Vector2(80f, 64f));
            y -= 80f;

            _stats = UiKit.Label(rt, "Stats", "", 21, new Color(0.85f, 0.88f, 0.92f), TextAnchor.UpperLeft);
            UiKit.Stretch(_stats.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(30f, 20f), new Vector2(-30f, y));

            _panel.SetActive(false);
        }

        private List<string> _nodeIds = new List<string>();
        private int _nodeIndex;

        private void AdjustSelected(int delta)
        {
            if (_nodeIndex >= 0 && _nodeIndex < _nodeIds.Count) Adjust(_nodeIds[_nodeIndex], delta);
        }

        private void Adjust(string id, int delta)
        {
            _audio.Play(SfxId.UiClick);
            var sim = _game.Sim;
            int level = sim.State.GetLevel(id) + delta;
            sim.DebugSetLevel(id, level);
        }

        private TMP_Text SliderRow(RectTransform parent, ref float y, string label, float min, float max, float value, UnityEngine.Events.UnityAction<float> onChanged)
        {
            var text = UiKit.Label(parent, label, label, 28, UiKit.Paper, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiKit.Stretch(text.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(30f, y - 40f), new Vector2(-30f, y));
            y -= 44f;
            var slider = UiKit.Slider(parent, label + " Slider", min, max, value, onChanged);
            UiKit.Stretch(slider.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(30f, y - 50f), new Vector2(-30f, y));
            y -= 60f;
            return text;
        }

        private UnityEngine.UI.Button ButtonAt(RectTransform parent, string label, int column, float y, float width, UnityEngine.Events.UnityAction action)
        {
            var b = UiKit.Button(parent, label, label, 26, UiKit.Accent, UiKit.Ink, () => { _audio.Play(SfxId.UiClick); action(); });
            var rt = b.GetComponent<RectTransform>();
            UiKit.Box(rt, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f + column * (width + 20f), y), new Vector2(width, 84f));
            rt.pivot = new Vector2(0f, 1f);
            return b;
        }

        /// <summary>Plays one generation from a copy of the current state and logs the year table.</summary>
        private void PrintBalance()
        {
            var copy = FarmSim.FromSave(_game.Sim.ToSave(), _game.Sim.Config);
            if (copy == null) { Debug.LogWarning("[Balance] could not copy the current state"); return; }
            var player = new TillWinter.Core.Balance.AutoPlayer(copy, 7) { MaxTicks = 400_000 };
            player.Run(1);
            Debug.Log("[Balance] from current state, one generation, seed 7\n" + player.ToTable());
        }

        public void Toggle()
        {
            _audio.Play(SfxId.UiClick);
            _panel.SetActive(!_panel.activeSelf);
        }

        private void LateUpdate()
        {
            if (!_panel.activeSelf) return;
            var sim = _game.Sim;
            var s = sim.State;
            var st = s.Stats;
            _timeLabel.text = "Time scale  x" + _game.TimeScale.ToString("0.0");
            _offsetLabel.text = "Ring offset  " + _game.RingOffsetPlots.ToString("0.00") + " plots (toward top)";
            var ov = sim.DebugRingRadiusOverride;
            _radiusLabel.text = "Ring radius override  " + (ov.HasValue ? ov.Value.ToString("0.0") + " plots" : "off (almanac " + st.RingRadius.ToString("0.00") + ")");
            _info.text = s.Phase + "  gen " + s.Generation.Generation + "  seeds " + s.Seeds + "  lifetime " + s.Generation.LifetimeCoinsThisGeneration.ToString("0") + "/" + sim.Config.HeritageThreshold.ToString("0")
                         + "\nsave: " + _save.Path + "  (" + _save.LastResult + ")"
                         + "\nYear " + s.Year + "  " + s.Season + "  t=" + s.YearTime.ToString("0.0") + "/" + s.YearLength.ToString("0") + "s"
                         + "   coins " + s.Coins.ToString("0") + "   field " + s.GridSize + "x" + s.GridSize + "   crows " + s.Crows.Count
                         + "\napprentices " + s.Apprentices.Count + "   audio " + (_audio.UsingKenneyClips ? "kenney" : "generated")
                         + "   fps " + (1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime)).ToString("0")
                         + "\n" + BuildInfo.Summary + "   cold start " + AppLifecycle.ColdStartSeconds.ToString("0.00") + " s"
                         + "\nquality " + QualityTiers.Current + " (" + QualityTiers.Reason + ")   ring offset " + _game.RingOffsetPlots.ToString("0.00");

            if (_nodeIndex >= 0 && _nodeIndex < _nodeIds.Count)
                _nodeLevel.text = s.GetLevel(_nodeIds[_nodeIndex]) + " / " + sim.GetMaxLevel(_nodeIds[_nodeIndex]);

            _stats.text =
                "ring radius " + st.RingRadius.ToString("0.00") +
                "\nring water x" + st.RingWaterMult.ToString("0.00") +
                "\nring grow x" + st.RingGrowMult.ToString("0.00") +
                "\nring harvest x" + st.RingHarvestMult.ToString("0.00") +
                "\nring bonus x" + st.RingBonusMult.ToString("0.00") +
                "\nsoil x" + st.SoilMultiplier.ToString("0.00") +
                "\nirrigation " + st.IrrigationFactor.ToString("0.00") +
                "\nsun " + st.SunFactor.ToString("0.00") +
                "\ncrop value x" + st.CropValueMult.ToString("0.00") +
                "\napprentices " + st.ApprenticeCount +
                "\n  speed " + st.ApprenticeSpeed.ToString("0.0") +
                "\n  harvest " + st.ApprenticeHarvestTime.ToString("0.00") + " s" +
                "\n  yield x" + st.ApprenticeYield.ToString("0.00") +
                "\ncrow chance " + (st.CrowSpawnChance * 100f).ToString("0") + "%" +
                "\nyear " + st.YearLength.ToString("0") + " s" +
                "\nfrost warn " + st.FrostWarningSeconds.ToString("0") + " s" +
                "\nmax tier " + st.MaxTierUnlocked +
                "\ngrid target " + st.TargetGridSize +
                "\ntractor " + st.TractorLevel + " greenhouse " + st.GreenhouseLevel + " combo " + st.RingComboLevel + " bounty " + st.CrowBountyLevel +
                "\ncloud " + (st.RainCloudUnlocked ? "on" : "off") + " golden " + (st.GoldenCropChance * 100).ToString("0") + "% immunity " + st.ScarecrowImmunity;
        }
    }
}
#endif
