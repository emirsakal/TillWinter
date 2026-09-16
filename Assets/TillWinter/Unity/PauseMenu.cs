using System;
using TillWinter.Core;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TillWinter.Unity
{
    /// <summary>
    /// Corner pause button and the four sheets behind it (S9): Pause, Settings, Credits, Statistics.
    /// Opening pauses the sim (no tick at all). Colours come from HudTheme, text from Strings.
    /// A language change or a save reset saves/erases and reloads the scene so every label is rebuilt.
    /// </summary>
    public sealed class PauseMenu : MonoBehaviour
    {
        public const float ResetHoldSeconds = 3f;
        private const float PageWidth = 940f, RowHeight = 104f, ButtonWidth = 700f;
        private const float SettingsHeight = 1480f; // one row shorter since the developer row left
        private const float LabelX = -230f, LabelWidth = 380f, ControlX = 225f, ControlWidth = 410f;

        public static PauseMenu Instance { get; private set; }
        /// <summary>Set by the bootstrap in TW_DEBUG / editor builds: opens the debug panel.</summary>
        public Action DeveloperToggle;

        private GameController _game;
        private AudioManager _audio;
        private SaveController _save;
        private HudTheme _theme;
        private Button _pauseButton;
        private GameObject _pause, _settings, _credits, _stats;
        private TMP_Text _version;
        private Button _langEn, _langTr;
        private UiSwitch _hapticsSwitch, _motionSwitch, _largeTextSwitch;
        private Image _applying;
        private TMP_Text _sfxValue, _ambienceValue;
        private TMP_Text[] _statValues;
        private float _sampleAt;
        private readonly Button[] _quality = new Button[3];
        private HoldButton _reset;
        private RectTransform _resetFill;
        private float _resetHeld;
        private bool _resetDone;
        private Action _afterStats;
        private bool _fromMenu;
        /// <summary>Credits reached through Settings, so Back owes the player a trip back to Settings; straight from the menu it owes them nothing.</summary>
        private bool _creditsFromSettings;

        public bool IsOpen => _pause.activeSelf || _settings.activeSelf || _credits.activeSelf || _stats.activeSelf;

        public void Init(GameController game, AudioManager audio, RectTransform canvas, SaveController save)
        {
            Instance = this;
            _game = game;
            _audio = audio;
            _save = save;
            _theme = HudTheme.Load();

            var safe = UiKit.Rect("PauseSafe", canvas);
            SafeArea.Apply(safe);
            // Solid, like every other button: a translucent face on a translucent lip read as a grey smudge.
            _pauseButton = UiKit.Button(safe, "PauseButton", "II", UiType.Heading, _theme.SheetIdle, _theme.SheetButtonText, Open);
            UiKit.Box(_pauseButton.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f, 54f), new Vector2(130f, 110f)); // bottom right, opposite the retire chip

            BuildPause(canvas);
            BuildSettings(canvas);
            BuildCredits(canvas);
            BuildStats(canvas);
        }

        // ------------------------------------------------------------------ sheets

        private void BuildPause(RectTransform canvas)
        {
            _pause = Sheet(canvas, "PauseSheet", "pause.title", 620f, out var p);
            Btn(p, "pause.resume", Resume, -150f);
            Btn(p, "pause.settings", () => Show(_settings), -150f - RowHeight);
            Btn(p, "pause.stats", () => ShowStats(() => Show(_pause)), -150f - 2f * RowHeight);
            Btn(p, "pause.main_menu", ToMainMenu, -150f - 3f * RowHeight, ButtonWidth, 0f, _theme.SheetIdle);
        }

        private void BuildSettings(RectTransform canvas)
        {
            _settings = Sheet(canvas, "SettingsSheet", "settings.title", SettingsHeight, out var p);
            var s = SettingsStore.Current;
            float y = -150f;

            RowLabel(p, "settings.language", y);
            _langEn = Btn(p, "settings.lang.en", () => SetLanguage(GameLanguage.English), y, 200f, 120f);
            _langTr = Btn(p, "settings.lang.tr", () => SetLanguage(GameLanguage.Turkish), y, 200f, 330f);
            y -= RowHeight;

            RowLabel(p, "settings.sfx", y);
            _sfxValue = SliderRow(p, "Sfx", s.SfxVolume, v => { SettingsStore.Current.SfxVolume = v; _audio.ApplyVolumes(); Sample(); }, y);
            y -= RowHeight;
            RowLabel(p, "settings.ambience", y);
            _ambienceValue = SliderRow(p, "Ambience", s.AmbienceVolume, v => { SettingsStore.Current.AmbienceVolume = v; _audio.ApplyVolumes(); Sample(); }, y);
            y -= RowHeight;

            RowLabel(p, "settings.haptics", y);
            _hapticsSwitch = SwitchRow(p, "Haptics", s.HapticsEnabled, on => { SettingsStore.Current.HapticsEnabled = on; if (on) Haptics.Play(HapticKind.Medium); }, y);
            y -= RowHeight;

            RowLabel(p, "settings.reduce_motion", y);
            _motionSwitch = SwitchRow(p, "ReduceMotion", s.ReduceMotion, on => SettingsStore.Current.ReduceMotion = on, y);
            y -= RowHeight - 10f;
            Text(p, "MotionHint", Strings.Get("settings.reduce_motion_hint"), y, UiType.Label, _theme.SheetMuted, TextAnchor.MiddleLeft, 44f);
            y -= 60f;

            RowLabel(p, "settings.large_text", y);
            _largeTextSwitch = SwitchRow(p, "LargeText", s.LargeText, on =>
            {
                SettingsStore.Current.LargeText = on;
                UiType.Scale = on ? UiType.LargeScale : 1f;
                Reload(); // every label was sized when it was built
            }, y);
            y -= RowHeight;

            RowLabel(p, "settings.quality", y);
            string[] q = { "settings.quality.auto", "settings.quality.low", "settings.quality.default" };
            for (int i = 0; i < 3; i++)
            {
                int choice = i - 1;
                _quality[i] = Btn(p, q[i], () => { QualityTiers.ApplyChoice(choice); RefreshSettings(); }, y, 134f, 88f + i * 137f); // same right edge as the switches
                UiKit.ButtonLabel(_quality[i]).fontSizeMax = UiType.Size(28);
            }
            y -= RowHeight + 20f;

            // The developer panel is opened from its own button in the play scene now, not buried in Settings.
            Btn(p, "settings.credits", () => { _creditsFromSettings = true; Show(_credits); }, y);
            y -= RowHeight + 16f;

            // Reset save: hold for three seconds; the fill shows the progress.
            // Same face-on-a-lip shape as every other button, so the one dangerous button does not look like a different kind of thing.
            var resetLip = UiKit.Panel(p, "ResetSaveLip", UiKit.LipColor(_theme.SheetDanger), true, false);
            UiKit.Box(resetLip.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(ButtonWidth, RowHeight - 14f));
            var reset = UiKit.Panel(p, "ResetSave", _theme.SheetDanger, true, true);
            UiKit.Box(reset.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(ButtonWidth, RowHeight - 21f));
            _reset = reset.gameObject.AddComponent<HoldButton>();
            var fill = UiKit.Panel(reset.transform, "Fill", _theme.SheetInk, true, false);
            _resetFill = fill.rectTransform;
            UiKit.Stretch(_resetFill, Vector2.zero, new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
            var resetText = UiKit.Label(reset.transform, "Label", Strings.Get("settings.reset"), UiType.Body, _theme.SheetButtonText, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Stretch(resetText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            y -= RowHeight - 10f;
            Text(p, "ResetHint", Strings.Get("settings.reset_hold"), y, UiType.Label, _theme.SheetMuted, TextAnchor.MiddleCenter, 44f);
            y -= 70f;

            _version = Text(p, "Version", "", y, UiType.Caption, _theme.SheetMuted, TextAnchor.MiddleCenter, 40f);
            y -= 70f;
            Btn(p, "settings.back", () => { SettingsStore.Save(); if (_fromMenu) CloseSheets(); else Show(_pause); }, y);
        }

        private void BuildCredits(RectTransform canvas)
        {
            _credits = Sheet(canvas, "CreditsSheet", "credits.title", 820f, out var page);
            UiKit.ScrollView(page, "Scroll", out var p);
            UiKit.Stretch((RectTransform)p.parent, Vector2.zero, Vector2.one, new Vector2(0f, 120f), new Vector2(0f, -110f));
            p.sizeDelta = new Vector2(0f, 1000f);
            float y = -20f;
            // Grouped tightly: the lines used to sit in tall boxes that left gaps bigger than the text.
            Text(p, "MadeBy", Strings.Get("credits.made_by"), y, UiType.Heading, _theme.SheetInk, TextAnchor.MiddleCenter, 70f).fontStyle = FontStyles.Bold;
            y -= 90f;
            Text(p, "Unity", Strings.Get("credits.unity"), y, UiType.Body, _theme.SheetInk, TextAnchor.MiddleCenter);
            y -= 90f;
            Text(p, "Kenney", Strings.Get("credits.kenney"), y, UiType.Body, _theme.SheetInk, TextAnchor.MiddleCenter);
            y -= 60f;
            Text(p, "KitsArt", Strings.Get("credits.kits_art"), y, UiType.Label, _theme.SheetMuted, TextAnchor.MiddleCenter, 50f);
            y -= 50f;
            Text(p, "KitsAudio", Strings.Get("credits.kits_audio"), y, UiType.Label, _theme.SheetMuted, TextAnchor.MiddleCenter, 50f);
            y -= 90f;
            Text(p, "Font", Strings.Get("credits.font"), y, UiType.Label, _theme.SheetInk, TextAnchor.MiddleCenter);
            // Back retraces the way in: Settings if Credits was opened from there, otherwise straight out.
            Btn(page, "settings.back", () =>
            {
                if (_creditsFromSettings) { _creditsFromSettings = false; Show(_settings); }
                else if (_fromMenu) CloseSheets();
                else Show(_pause);
            }, -700f);
        }

        /// <summary>Main menu entry: Settings (and Credits from it); Back closes the sheets instead of showing Pause.</summary>
        public void OpenSettingsFrom(Action unused)
        {
            _fromMenu = true;
            Show(_settings);
        }

        /// <summary>Main menu entry: Credits; Back closes it, because the player never passed through Settings to get here.</summary>
        public void OpenCreditsFrom(Action unused)
        {
            _fromMenu = true;
            _creditsFromSettings = false;
            Show(_credits);
        }

        /// <summary>Hides every sheet without resuming play or showing the pause button (main menu).</summary>
        public void CloseSheets()
        {
            HideAll();
            SettingsStore.Save();
            _fromMenu = false;
            _creditsFromSettings = false;
        }

        /// <summary>Saves and loads the title scene (Menu.unity).</summary>
        private void ToMainMenu()
        {
            Resume();
            _save.SaveNow();
            Time.timeScale = 1f;
            SceneLoader.Load(SceneNames.Menu);
        }

        private void BuildStats(RectTransform canvas)
        {
            _stats = Sheet(canvas, "StatsSheet", "stats.title", 1300f, out var page); // tall enough that every row shows above the button
            UiKit.ScrollView(page, "Scroll", out var rows);
            UiKit.Stretch((RectTransform)rows.parent, Vector2.zero, Vector2.one, new Vector2(40f, 150f), new Vector2(-40f, -110f));
            rows.sizeDelta = new Vector2(0f, StatKeys.Length * StatRow + 20f);
            _statValues = new TMP_Text[StatKeys.Length];
            for (int i = 0; i < StatKeys.Length; i++)
            {
                var row = UiKit.Rect("Row " + StatKeys[i], rows);
                UiKit.Box(row, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -i * StatRow), new Vector2(800f, StatRow));
                if (i > 0)
                {
                    var line = UiKit.Panel(row, "Divider", new Color(_theme.SheetInk.r, _theme.SheetInk.g, _theme.SheetInk.b, 0.12f), false, false);
                    UiKit.Box(line.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(800f, 2f));
                }
                UiIcons.Row(row, StatIcons[i], _theme.SheetMuted, 6f, 34f);
                var label = UiKit.Label(row, "Label", Strings.Get(StatKeys[i]), UiType.Body, _theme.SheetInk, TextAnchor.MiddleLeft);
                UiKit.Stretch(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(56f, 0f), new Vector2(-260f, 0f));
                bool highlight = StatKeys[i] == "stats.coins" || StatKeys[i] == "stats.best_combo"; // the two numbers players compare
                _statValues[i] = UiKit.Label(row, "Value", "", highlight ? UiType.Heading : UiType.Body,
                    highlight ? _theme.SheetButton : _theme.SheetInk, TextAnchor.MiddleRight, FontStyle.Bold);
                UiKit.Stretch(_statValues[i].rectTransform, Vector2.zero, Vector2.one, new Vector2(0f, 0f), new Vector2(-6f, 0f));
            }
            Btn(page, "stats.continue", ContinueFromStats, -1180f);
        }

        // ------------------------------------------------------------------ flow

        public void Open()
        {
            if (IsOpen) return;
            _game.SetPaused(true);
            Haptics.Play(HapticKind.Selection);
            Show(_pause);
        }

        public void Resume()
        {
            HideAll();
            SettingsStore.Save();
            _game?.SetPaused(false);
            _pauseButton.gameObject.SetActive(true);
        }

        public void SetButtonVisible(bool visible) => _pauseButton.gameObject.SetActive(visible);

        /// <summary>
        /// Bottom right during a year; top right while the Winter screen is up, whose own buttons fill the bottom
        /// edge (the pause button used to sit on top of Next Year).
        /// </summary>
        public void PlaceButton(bool top)
        {
            var rt = _pauseButton.GetComponent<RectTransform>();
            if (top) UiKit.Box(rt, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -24f), new Vector2(110f, 96f));
            else UiKit.Box(rt, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f, 54f), new Vector2(130f, 110f));
        }

        /// <summary>The statistics sheet; <paramref name="after"/> runs on Continue (null = resume play).</summary>
        public void ShowStats(Action after)
        {
            _afterStats = after;
            if (_game != null && !_game.Paused) _game.SetPaused(true);
            FillStats();
            Show(_stats);
        }

        private void ContinueFromStats()
        {
            var after = _afterStats;
            _afterStats = null;
            if (after != null) after();
            else Resume();
        }

        private void Show(GameObject sheet)
        {
            HideAll();
            sheet.SetActive(true);
            sheet.transform.SetAsLastSibling();
            if (sheet == _settings) RefreshSettings();
        }

        private void HideAll()
        {
            _pause.SetActive(false);
            _settings.SetActive(false);
            _credits.SetActive(false);
            _stats.SetActive(false);
            _resetHeld = 0f;
        }

        private void Update()
        {
            if (_resetDone || !_settings.activeSelf) return;
            _resetHeld = _reset.Held ? _resetHeld + Time.unscaledDeltaTime : 0f;
            _resetFill.anchorMax = new Vector2(Mathf.Clamp01(_resetHeld / ResetHoldSeconds), 1f);
            if (_resetHeld >= ResetHoldSeconds) ResetSave();
        }

        // ------------------------------------------------------------------ settings actions

        private void SetLanguage(string lang)
        {
            if (GameLanguage.Current == lang && SettingsStore.Current.Language == lang) return;
            SettingsStore.Current.Language = lang;
            SettingsStore.Save();
            _save?.SaveNow();
            Reload();
        }

        /// <summary>A short click while dragging a volume slider, so the level is audible (rate-limited).</summary>
        private void Sample()
        {
            if (_audio == null || Time.unscaledTime < _sampleAt) return;
            _sampleAt = Time.unscaledTime + 0.12f;
            _audio.Play(SfxId.CoinArrive);
        }

        private void ResetSave()
        {
            _resetDone = true;
            Haptics.Play(HapticKind.Heavy);
            if (_save != null)
            {
                _save.Detach();
                _save.DeleteSave();
            }
            else SaveController.DeleteFiles(SaveController.FilePath); // title scene: no farm is running
            Debug.Log("[TillWinter] " + Strings.Get("settings.reset_done"));
            Reload();
        }

        /// <summary>A language or text-size change rebuilds every label, so the scene reloads behind a short cover.</summary>
        private void Reload()
        {
            if (_applying == null)
            {
                _applying = UiKit.Panel((RectTransform)transform, "Applying", _theme.SheetOverlay, false, true);
                UiKit.Stretch(_applying.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                var label = UiKit.Label(_applying.transform, "Text", Strings.Get("settings.applying"), UiType.Title, _theme.SheetButtonText, TextAnchor.MiddleCenter, FontStyle.Bold);
                UiKit.Stretch(label.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            }
            _applying.transform.SetAsLastSibling();
            _applying.gameObject.SetActive(true);
            SettingsStore.Save();
            StartCoroutine(ReloadAfterCover());
        }

        private System.Collections.IEnumerator ReloadAfterCover()
        {
            yield return new WaitForSecondsRealtime(UiMotion.Slow);
            if (_game != null) _game.SetPaused(false);
            else Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private void RefreshSettings()
        {
            var s = SettingsStore.Current;
            _hapticsSwitch.Set(s.HapticsEnabled);
            _motionSwitch.Set(s.ReduceMotion);
            _largeTextSwitch.Set(s.LargeText);
            _sfxValue.text = Mathf.RoundToInt(s.SfxVolume * 100f) + "%";
            _ambienceValue.text = Mathf.RoundToInt(s.AmbienceVolume * 100f) + "%";
            Tint(_langEn, GameLanguage.Current == GameLanguage.English);
            Tint(_langTr, GameLanguage.Current == GameLanguage.Turkish);
            for (int i = 0; i < 3; i++) Tint(_quality[i], s.QualityTier == i - 1);
            var info = BuildInfo.Current;
            _version.text = info != null && info.Build > 0
                ? Strings.Format("settings.version", ("version", info.Version), ("build", info.Build))
                : Strings.Format("settings.version_editor", ("version", Application.version));
        }

        private void Tint(Button b, bool on)
        {
            if (b != null && b.targetGraphic != null) b.targetGraphic.color = on ? _theme.SheetButton : _theme.SheetIdle;
        }

        private const float StatRow = 86f;

        private static readonly string[] StatKeys =
        {
            "stats.generations", "stats.years", "stats.coins", "stats.harvests", "stats.by_ring", "stats.by_apprentice",
            "stats.by_tractor", "stats.crows", "stats.golden", "stats.best_combo", "stats.time",
        };

        private static readonly string[] StatIcons =
        {
            UiIcons.Generation, UiIcons.Year, UiIcons.Coin, UiIcons.Harvest, UiIcons.Ring, UiIcons.Apprentice,
            UiIcons.Tractor, UiIcons.Crow, UiIcons.Golden, UiIcons.Combo, UiIcons.Time,
        };

        private void FillStats()
        {
            var g = _game.State.Generation;
            long minutes = (long)(g.TimePlayedSeconds / 60.0);
            string[] values =
            {
                g.Generation.ToString(), g.YearsTotal.ToString(), NumberFormat.Short(g.LifetimeCoinsTotal),
                g.Harvests.ToString(), g.HarvestsRing.ToString(), g.HarvestsApprentice.ToString(),
                g.HarvestsTractor.ToString(), g.CrowsScared.ToString(), g.GoldenHarvests.ToString(),
                g.BestCombo.ToString(), Strings.Format("stats.time_value", ("hours", minutes / 60), ("minutes", minutes % 60)),
            };
            for (int i = 0; i < _statValues.Length && i < values.Length; i++) _statValues[i].text = values[i];
        }

        // ------------------------------------------------------------------ builders

        private GameObject Sheet(RectTransform canvas, string name, string titleKey, float height, out RectTransform page)
        {
            var overlay = UiKit.Panel(canvas, name, _theme.SheetOverlay, false, true);
            UiKit.Stretch(overlay.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var safe = UiKit.Rect("Safe", overlay.transform);
            SafeArea.Apply(safe); // a notch must never cut a sheet's buttons
            var paper = UiKit.Panel(safe, "Page", _theme.SheetPaper, true, true);
            page = paper.rectTransform;
            UiKit.Box(page, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(PageWidth, height));
            Text(page, "Title", Strings.Get(titleKey), -30f, UiType.Title, _theme.SheetInk, TextAnchor.MiddleCenter, 90f).fontStyle = FontStyles.Bold;
            overlay.gameObject.AddComponent<SheetTransition>().Page = page; // every sheet opens the same way
            overlay.gameObject.SetActive(false);
            return overlay.gameObject;
        }

        private Button Btn(RectTransform page, string key, UnityAction onClick, float y, float width = ButtonWidth, float x = 0f, Color? bg = null)
        {
            var b = UiKit.Button(page, key, Strings.Get(key), UiType.Body, bg ?? _theme.SheetButton, _theme.SheetButtonText, onClick);
            UiKit.Box(b.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(x, y), new Vector2(width, RowHeight - 14f));
            return b;
        }

        private void RowLabel(RectTransform page, string key, float y)
        {
            var t = UiKit.Label(page, key, Strings.Get(key), UiType.Body, _theme.SheetInk, TextAnchor.MiddleLeft);
            UiKit.Box(t.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(LabelX, y), new Vector2(LabelWidth, RowHeight - 14f));
        }

        /// <summary>A volume row: the slider plus the percentage to its right. Returns the value label.</summary>
        private TMP_Text SliderRow(RectTransform page, string name, float value, UnityAction<float> onChanged, float y)
        {
            var slider = UiKit.Slider(page, name, 0f, 1f, value, onChanged);
            UiKit.Box(slider.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(ControlX - 40f, y - 20f), new Vector2(ControlWidth - 80f, 50f));
            var text = UiKit.Label(page, name + "Value", "", UiType.Label, _theme.SheetMuted, TextAnchor.MiddleRight);
            UiKit.Box(text.rectTransform, new Vector2(0.5f, 1f), new Vector2(1f, 1f), new Vector2(ControlX + ControlWidth * 0.5f, y - 20f), new Vector2(90f, 50f));
            return text;
        }

        /// <summary>A settings row with a real switch instead of a button whose label says On or Off.</summary>
        private UiSwitch SwitchRow(RectTransform page, string name, bool value, Action<bool> changed, float y)
        {
            var sw = UiKit.Switch(page, name, value, _theme.SheetButton, _theme.SheetIdle, _theme.SheetButtonText);
            UiKit.Box((RectTransform)sw.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(ControlX + 100f, y - 11f), new Vector2(140f, 68f)); // centred on its label, like the sliders
            sw.Changed += on => { changed(on); SettingsStore.Save(); };
            return sw;
        }

        private TMP_Text Text(RectTransform page, string name, string text, float y, int size, Color color, TextAnchor anchor = TextAnchor.MiddleLeft, float height = 60f)
        {
            var t = UiKit.Label(page, name, text, size, color, anchor);
            UiKit.Box(t.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(PageWidth - 100f, height));
            return t;
        }
    }
}
