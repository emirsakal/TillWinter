using TillWinter.Core;
using TMPro;
using UnityEngine;

namespace TillWinter.Unity
{
    /// <summary>
    /// The end of a daily farm (GDD §8.4 v2.4): when its one year is over, a card shows the day's score and the best
    /// kept on this device, and takes the player back to the title screen. Nothing is saved to the family farm.
    /// </summary>
    public sealed class DailyResult : MonoBehaviour
    {
        private GameController _game;
        private AudioManager _audio;
        private GameObject _card;
        private TMP_Text _score, _best;
        private HudTheme _theme;

        public void Init(GameController game, AudioManager audio, RectTransform canvas)
        {
            _game = game;
            _audio = audio;
            _theme = HudTheme.Load();
            var overlay = UiKit.Panel(canvas, "DailyResult", _theme.SheetOverlay, false, true);
            UiKit.Stretch(overlay.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _card = overlay.gameObject;
            var page = UiKit.Card(overlay.transform, "Page", _theme.SheetPaper, true);
            UiKit.Box(page.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(860f, 640f));
            var title = UiKit.Label(page.transform, "Title", Strings.Format("daily.title", ("date", FormatDay(game.State.Daily))), UiType.Title, _theme.SheetInk, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Box(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(800f, 90f));
            _score = UiKit.Label(page.transform, "Score", "", UiType.Title, _theme.SheetButton, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Box(_score.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -200f), new Vector2(800f, 110f));
            _best = UiKit.Label(page.transform, "Best", "", UiType.Body, _theme.SheetMuted, TextAnchor.MiddleCenter);
            UiKit.Box(_best.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -310f), new Vector2(800f, 60f));
            var back = UiKit.Button(page.transform, "DailyMenu", Strings.Get("daily.back"), UiType.Heading, _theme.SheetButton, _theme.SheetButtonText, BackToMenu);
            UiKit.Box(back.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(560f, 120f));
            _card.SetActive(false);
            game.Sim.WinterStarted += Show;
        }

        private void OnDestroy()
        {
            if (_game != null && _game.Sim != null) _game.Sim.WinterStarted -= Show;
        }

        /// <summary>The day in the language's own order (daily.date: 9/19/2026 in English, 19.09.2026 in Turkish).</summary>
        private static string FormatDay(int yyyymmdd) =>
            Strings.Format("daily.date", ("d", (yyyymmdd % 100).ToString("00")), ("m", (yyyymmdd / 100 % 100).ToString("00")), ("y", yyyymmdd / 10000));

        private void Show()
        {
            double coins = _game.State.CoinsThisYear;
            var settings = SettingsStore.Current;
            bool record = settings.DailyBestDate != _game.State.Daily || coins > settings.DailyBestCoins;
            if (record)
            {
                settings.DailyBestDate = _game.State.Daily;
                settings.DailyBestCoins = coins;
                SettingsStore.Save();
            }
            _score.text = Strings.Format("daily.score", ("coins", NumberFormat.Short(coins)));
            _best.text = record ? Strings.Get("daily.record") : Strings.Format("daily.best", ("coins", NumberFormat.Short(settings.DailyBestCoins)));
            _card.SetActive(true);
            _card.transform.SetAsLastSibling();
            Haptics.Play(HapticKind.Medium);
            _audio?.Play(SfxId.GoalMet);
        }

        private void BackToMenu()
        {
            GameSession.Daily = 0;
            Time.timeScale = 1f;
            SceneLoader.Load(SceneNames.Menu);
        }
    }
}
