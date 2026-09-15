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
        private TMP_Text _statsLabels, _statsValues, _version, _haptics, _motion;
        private Button _hapticsButton, _motionButton, _devButton, _langEn, _langTr;
        private readonly Button[] _quality = new Button[3];
        private HoldButton _reset;
        private RectTransform _resetFill;
        private float _resetHeld;
        private bool _resetDone;
        private Action _afterStats;
        private bool _fromMenu;

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
            _pauseButton = UiKit.Button(safe, "PauseButton", "II", 40, _theme.PauseButton, _theme.Text, Open);
            UiKit.Box(_pauseButton.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-30f, -30f), new Vector2(120f, 100f));

            BuildPause(canvas);
            BuildSettings(canvas);
            BuildCredits(canvas);
            BuildStats(canvas);
        }

        // ------------------------------------------------------------------ sheets

        private void BuildPause(RectTransform canvas)
        {
            _pause = Sheet(canvas, "PauseSheet", "pause.title", 664f, out var p);
            Btn(p, "pause.resume", Resume, -150f);
            Btn(p, "pause.settings", () => Show(_settings), -150f - RowHeight);
            Btn(p, "pause.stats", () => ShowStats(() => Show(_pause)), -150f - 2f * RowHeight);
            Btn(p, "pause.main_menu", ToMainMenu, -150f - 3f * RowHeight, ButtonWidth, 0f, _theme.SheetIdle);
        }

        private void BuildSettings(RectTransform canvas)
        {
            _settings = Sheet(canvas, "SettingsSheet", "settings.title", 1480f, out var p);
            var s = SettingsStore.Current;
            float y = -150f;

            RowLabel(p, "settings.language", y);
            _langEn = Btn(p, "settings.lang.en", () => SetLanguage(GameLanguage.English), y, 200f, 120f);
            _langTr = Btn(p, "settings.lang.tr", () => SetLanguage(GameLanguage.Turkish), y, 200f, 330f);
            y -= RowHeight;

            RowLabel(p, "settings.sfx", y);
            SliderRow(p, "Sfx", s.SfxVolume, v => { SettingsStore.Current.SfxVolume = v; _audio.ApplyVolumes(); }, y);
            y -= RowHeight;
            RowLabel(p, "settings.ambience", y);
            SliderRow(p, "Ambience", s.AmbienceVolume, v => { SettingsStore.Current.AmbienceVolume = v; _audio.ApplyVolumes(); }, y);
            y -= RowHeight;

            RowLabel(p, "settings.haptics", y);
            _hapticsButton = Btn(p, "settings.on", ToggleHaptics, y, ControlWidth, ControlX);
            _haptics = UiKit.ButtonLabel(_hapticsButton);
            y -= RowHeight;

            RowLabel(p, "settings.reduce_motion", y);
            _motionButton = Btn(p, "settings.off", ToggleMotion, y, ControlWidth, ControlX);
            _motion = UiKit.ButtonLabel(_motionButton);
            y -= RowHeight - 10f;
            Text(p, "MotionHint", Strings.Get("settings.reduce_motion_hint"), y, 26, _theme.SheetMuted, TextAnchor.MiddleLeft, 44f);
            y -= 60f;

            RowLabel(p, "settings.quality", y);
            string[] q = { "settings.quality.auto", "settings.quality.low", "settings.quality.default" };
            for (int i = 0; i < 3; i++)
            {
                int choice = i - 1;
                _quality[i] = Btn(p, q[i], () => { QualityTiers.ApplyChoice(choice); RefreshSettings(); }, y, 132f, 88f + i * 137f);
                UiKit.ButtonLabel(_quality[i]).fontSize = 28;
            }
            y -= RowHeight + 20f;

            Btn(p, "settings.credits", () => Show(_credits), y);
            y -= RowHeight;
            _devButton = Btn(p, "settings.developer", () => { Resume(); DeveloperToggle?.Invoke(); }, y, ButtonWidth, 0f, _theme.SheetIdle);
            y -= RowHeight + 16f;

            // Reset save: hold for three seconds; the fill shows the progress.
            var reset = UiKit.Panel(p, "ResetSave", _theme.SheetDanger, true, true);
            UiKit.Box(reset.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(ButtonWidth, RowHeight - 14f));
            _reset = reset.gameObject.AddComponent<HoldButton>();
            var fill = UiKit.Panel(reset.transform, "Fill", _theme.SheetInk, true, false);
            _resetFill = fill.rectTransform;
            UiKit.Stretch(_resetFill, Vector2.zero, new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
            var resetText = UiKit.Label(reset.transform, "Label", Strings.Get("settings.reset"), 38, _theme.SheetButtonText, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Stretch(resetText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            y -= RowHeight - 10f;
            Text(p, "ResetHint", Strings.Get("settings.reset_hold"), y, 26, _theme.SheetMuted, TextAnchor.MiddleCenter, 44f);
            y -= 70f;

            _version = Text(p, "Version", "", y, 24, _theme.SheetMuted, TextAnchor.MiddleCenter, 40f);
            y -= 70f;
            Btn(p, "settings.back", () => { SettingsStore.Save(); if (_fromMenu) CloseSheets(); else Show(_pause); }, y);
        }

        private void BuildCredits(RectTransform canvas)
        {
            _credits = Sheet(canvas, "CreditsSheet", "credits.title", 1100f, out var p);
            float y = -160f;
            Text(p, "MadeBy", Strings.Get("credits.made_by"), y, 44, _theme.SheetInk, TextAnchor.MiddleCenter, 70f).fontStyle = FontStyles.Bold;
            y -= 90f;
            Text(p, "Unity", Strings.Get("credits.unity"), y, 32, _theme.SheetInk, TextAnchor.MiddleCenter);
            y -= 110f;
            Text(p, "Kenney", Strings.Get("credits.kenney"), y, 32, _theme.SheetInk, TextAnchor.MiddleCenter);
            y -= 70f;
            Text(p, "KitsArt", Strings.Get("credits.kits_art"), y, 26, _theme.SheetMuted, TextAnchor.MiddleCenter, 110f);
            y -= 120f;
            Text(p, "KitsAudio", Strings.Get("credits.kits_audio"), y, 26, _theme.SheetMuted, TextAnchor.MiddleCenter, 110f);
            y -= 150f;
            Text(p, "Font", Strings.Get("credits.font"), y, 30, _theme.SheetInk, TextAnchor.MiddleCenter);
            y -= 140f;
            Btn(p, "settings.back", () => Show(_settings), y);
        }

        /// <summary>Main menu entry: Settings (and Credits from it); Back closes the sheets instead of showing Pause.</summary>
        public void OpenSettingsFrom(Action unused)
        {
            _fromMenu = true;
            Show(_settings);
        }

        /// <summary>Main menu entry: Credits; Back goes to Settings, whose Back then closes.</summary>
        public void OpenCreditsFrom(Action unused)
        {
            _fromMenu = true;
            Show(_credits);
        }

        /// <summary>Hides every sheet without resuming play or showing the pause button (main menu).</summary>
        public void CloseSheets()
        {
            HideAll();
            SettingsStore.Save();
            _fromMenu = false;
        }

        /// <summary>Saves and loads the title scene (Menu.unity).</summary>
        private void ToMainMenu()
        {
            Resume();
            _save.SaveNow();
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneNames.Menu);
        }

        private void BuildStats(RectTransform canvas)
        {
            _stats = Sheet(canvas, "StatsSheet", "stats.title", 1180f, out var p);
            _statsLabels = Text(p, "Labels", "", -140f, 34, _theme.SheetInk, TextAnchor.UpperLeft, 860f);
            _statsValues = Text(p, "Values", "", -140f, 34, _theme.SheetInk, TextAnchor.UpperRight, 860f);
            _statsValues.fontStyle = FontStyles.Bold;
            _statsLabels.lineSpacing = _statsValues.lineSpacing = 18f;
            Btn(p, "stats.continue", ContinueFromStats, -1040f);
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

        private void ToggleHaptics()
        {
            SettingsStore.Current.HapticsEnabled = !SettingsStore.Current.HapticsEnabled;
            Haptics.Play(HapticKind.Selection);
            RefreshSettings();
        }

        private void ToggleMotion()
        {
            SettingsStore.Current.ReduceMotion = !SettingsStore.Current.ReduceMotion;
            RefreshSettings();
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

        private void Reload()
        {
            if (_game != null) _game.SetPaused(false);
            else Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private void RefreshSettings()
        {
            var s = SettingsStore.Current;
            _haptics.text = Strings.Get(s.HapticsEnabled ? "settings.on" : "settings.off");
            Tint(_hapticsButton, s.HapticsEnabled);
            _motion.text = Strings.Get(s.ReduceMotion ? "settings.on" : "settings.off");
            Tint(_motionButton, s.ReduceMotion);
            Tint(_langEn, GameLanguage.Current == GameLanguage.English);
            Tint(_langTr, GameLanguage.Current == GameLanguage.Turkish);
            for (int i = 0; i < 3; i++) Tint(_quality[i], s.QualityTier == i - 1);
            _devButton.gameObject.SetActive(DeveloperToggle != null);
            var info = BuildInfo.Current;
            _version.text = info != null && info.Build > 0
                ? Strings.Format("settings.version", ("version", info.Version), ("build", info.Build))
                : Strings.Format("settings.version_editor", ("version", Application.version));
        }

        private void Tint(Button b, bool on)
        {
            if (b != null && b.targetGraphic != null) b.targetGraphic.color = on ? _theme.SheetButton : _theme.SheetIdle;
        }

        private void FillStats()
        {
            var g = _game.State.Generation;
            const string indent = "      ";
            _statsLabels.text = string.Join("\n",
                Strings.Get("stats.generations"), Strings.Get("stats.years"), Strings.Get("stats.coins"),
                Strings.Get("stats.harvests"),
                indent + Strings.Get("stats.by_ring"), indent + Strings.Get("stats.by_apprentice"), indent + Strings.Get("stats.by_tractor"),
                Strings.Get("stats.crows"), Strings.Get("stats.golden"), Strings.Get("stats.best_combo"), Strings.Get("stats.time"));
            long minutes = (long)(g.TimePlayedSeconds / 60.0);
            _statsValues.text = string.Join("\n",
                g.Generation.ToString(), g.YearsTotal.ToString(), NumberFormat.Short(g.LifetimeCoinsTotal),
                g.Harvests.ToString(),
                g.HarvestsRing.ToString(), g.HarvestsApprentice.ToString(), g.HarvestsTractor.ToString(),
                g.CrowsScared.ToString(), g.GoldenHarvests.ToString(), g.BestCombo.ToString(),
                Strings.Format("stats.time_value", ("hours", minutes / 60), ("minutes", minutes % 60)));
        }

        // ------------------------------------------------------------------ builders

        private GameObject Sheet(RectTransform canvas, string name, string titleKey, float height, out RectTransform page)
        {
            var overlay = UiKit.Panel(canvas, name, _theme.SheetOverlay, false, true);
            UiKit.Stretch(overlay.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var paper = UiKit.Panel(overlay.transform, "Page", _theme.SheetPaper, true, true);
            page = paper.rectTransform;
            UiKit.Box(page, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(PageWidth, height));
            Text(page, "Title", Strings.Get(titleKey), -30f, 56, _theme.SheetInk, TextAnchor.MiddleCenter, 90f).fontStyle = FontStyles.Bold;
            overlay.gameObject.SetActive(false);
            return overlay.gameObject;
        }

        private Button Btn(RectTransform page, string key, UnityAction onClick, float y, float width = ButtonWidth, float x = 0f, Color? bg = null)
        {
            var b = UiKit.Button(page, key, Strings.Get(key), 36, bg ?? _theme.SheetButton, _theme.SheetButtonText, onClick);
            UiKit.Box(b.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(x, y), new Vector2(width, RowHeight - 14f));
            return b;
        }

        private void RowLabel(RectTransform page, string key, float y)
        {
            var t = UiKit.Label(page, key, Strings.Get(key), 34, _theme.SheetInk, TextAnchor.MiddleLeft);
            UiKit.Box(t.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(LabelX, y), new Vector2(LabelWidth, RowHeight - 14f));
        }

        private void SliderRow(RectTransform page, string name, float value, UnityAction<float> onChanged, float y)
        {
            var slider = UiKit.Slider(page, name, 0f, 1f, value, onChanged);
            UiKit.Box(slider.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(ControlX, y - 20f), new Vector2(ControlWidth, 50f));
        }

        private TMP_Text Text(RectTransform page, string name, string text, float y, int size, Color color, TextAnchor anchor = TextAnchor.MiddleLeft, float height = 60f)
        {
            var t = UiKit.Label(page, name, text, size, color, anchor);
            UiKit.Box(t.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(PageWidth - 100f, height));
            return t;
        }
    }
}
