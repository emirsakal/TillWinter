using TillWinter.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TillWinter.Unity
{
    /// <summary>
    /// "While you were away" (GDD §9): duration, coins, harvests, a line per source, OK. The coins are already
    /// in the sim; the HUD holds them back until OK so the counter punch is visible. Tapping the field applies OK.
    /// </summary>
    public sealed class AwayCard : MonoBehaviour
    {
        private GameController _game;
        private AudioManager _audio;
        private HudView _hud;
        private GameObject _panel;
        private TMP_Text _duration, _coins, _capped;
        private readonly TMP_Text[] _sourceLines = new TMP_Text[3];
        private readonly CanvasGroup[] _sourceGroups = new CanvasGroup[3];
        private int _sourceCount;
        private float _reveal;
        private bool _open;

        public bool IsOpen => _open;
        private OfflineReport _shown;

        public void Init(GameController game, AudioManager audio, RectTransform canvas, HudView hud)
        {
            _game = game;
            _audio = audio;
            _hud = hud;
            var theme = HudTheme.Load();
            var dim = UiKit.Panel(canvas, "AwayCard", theme.SheetOverlay, false, true);
            _panel = dim.gameObject;
            // The dim catches every tap (so the field never gets it): a tap anywhere is OK.
            var anywhere = _panel.AddComponent<Button>();
            anywhere.transition = Selectable.Transition.None;
            anywhere.onClick.AddListener(Apply);
            var box = UiKit.Card(dim.transform, "Box", theme.CardDark); // was a navy outside the palette
            UiKit.Box(box.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 640f));
            UiKit.SheetDecor(box, 162f);
            var title = UiKit.Label(box.transform, "Title", Strings.Get("ui.away_title"), UiType.Title, theme.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Box(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(860f, 70f));
            _duration = UiKit.Label(box.transform, "Duration", "", UiType.Body, theme.TextMuted, TextAnchor.MiddleCenter);
            UiKit.Box(_duration.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(860f, 44f));
            UiKit.CircleImage(box.transform, "CoinIcon", theme.Coin, new Vector2(-150f, 40f), 56f);
            _coins = UiKit.Label(box.transform, "Coins", "", UiType.Big, theme.Coin, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Box(_coins.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(40f, 40f), new Vector2(400f, 80f));
            for (int i = 0; i < _sourceLines.Length; i++)
            {
                _sourceLines[i] = UiKit.Label(box.transform, "Source" + i, "", UiType.Label, theme.TextMuted, TextAnchor.MiddleCenter);
                UiKit.Box(_sourceLines[i].rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0f, -20f - i * 44f), new Vector2(800f, 42f));
                _sourceGroups[i] = _sourceLines[i].gameObject.AddComponent<CanvasGroup>();
                _sourceGroups[i].alpha = 0f;
            }
            _capped = UiKit.Label(box.transform, "Capped", "", UiType.Caption, theme.HintAccent, TextAnchor.MiddleCenter);
            UiKit.Box(_capped.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 170f), new Vector2(800f, 34f));
            var ok = UiKit.Button(box.transform, "Ok", Strings.Get("ui.ok"), UiType.Heading, theme.HintAccent, theme.YearCardText, Apply);
            UiKit.Box(ok.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(400f, 110f));
            _panel.AddComponent<SheetTransition>().Page = box.rectTransform;
            _panel.SetActive(false);
        }

        public void Show(OfflineReport report)
        {
            // Away again before OK: this stretch adds to the one still on the card, nothing is dropped.
            if (_open)
                report = new OfflineReport(_shown.SecondsSimulated + report.SecondsSimulated, _shown.CoinsEarned + report.CoinsEarned,
                    _shown.Harvests + report.Harvests, _shown.Capped || report.Capped,
                    _shown.HarvestsApprentice + report.HarvestsApprentice, _shown.HarvestsTractor + report.HarvestsTractor,
                    _shown.HeirloomsFound + report.HeirloomsFound);
            _shown = report;
            int minutes = Mathf.RoundToInt((float)report.SecondsSimulated / 60f);
            _duration.text = Strings.Format("away.duration", ("hours", minutes / 60), ("minutes", minutes % 60));
            _coins.text = "+" + NumberFormat.Short(report.CoinsEarned);
            _sourceCount = 0;
            if (report.HarvestsApprentice > 0) SetSource(Strings.Format("away.apprentices", ("count", report.HarvestsApprentice)));
            if (report.HarvestsTractor > 0) SetSource(Strings.Format("away.tractor", ("count", report.HarvestsTractor)));
            if (report.HeirloomsFound > 0) SetSource(Strings.Format("away.heirlooms", ("count", report.HeirloomsFound)));
            if (_sourceCount == 0) SetSource(Strings.Get("away.nothing"));
            for (int i = _sourceCount; i < _sourceLines.Length; i++) _sourceLines[i].text = "";
            _reveal = 0f;
            _capped.text = report.Capped ? Strings.Get("away.capped") : "";
            _hud.HeldCoins = report.CoinsEarned;
            _panel.SetActive(true);
            _open = true;
            _game.InputBlocked = true;
        }

        private void SetSource(string text)
        {
            if (_sourceCount >= _sourceLines.Length) return;
            _sourceLines[_sourceCount].text = text;
            _sourceCount++;
        }

        /// <summary>OK: the held coins fly into the counter instead of appearing there.</summary>
        public void Apply()
        {
            if (!_open) return;
            double earned = _hud.HeldCoins;
            _hud.HeldCoins = 0;
            if (earned > 0) _hud.FlyCoins(new Vector2(-150f, 40f), earned, 12);
            else _hud.Punch();
            _audio.Play(SfxId.Purchase);
            _panel.SetActive(false);
            _open = false;
            _game.InputBlocked = _game.State.Phase != Phase.Year;
        }

        private void Update()
        {
            if (_open)
            {
                // The source lines arrive one after another instead of all at once.
                _reveal += Time.unscaledDeltaTime;
                for (int i = 0; i < _sourceLines.Length; i++)
                {
                    float k = Mathf.Clamp01((_reveal - 0.35f - i * 0.22f) / UiMotion.Normal);
                    _sourceGroups[i].alpha = i < _sourceCount ? UiMotion.EaseOut(k) : 0f;
                }
            }
        }
    }
}
