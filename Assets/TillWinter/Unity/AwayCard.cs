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
        private TMP_Text _duration, _coins, _sources, _capped;
        private bool _open;

        public bool IsOpen => _open;

        public void Init(GameController game, AudioManager audio, RectTransform canvas, HudView hud)
        {
            _game = game;
            _audio = audio;
            _hud = hud;
            var theme = HudTheme.Load();
            var dim = UiKit.Panel(canvas, "AwayCard", new Color(0f, 0f, 0f, 0.55f), false, true);
            _panel = dim.gameObject;
            var box = UiKit.Panel(dim.transform, "Box", new Color(0.12f, 0.14f, 0.2f, 0.98f), true, true);
            UiKit.Box(box.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 640f));
            var title = UiKit.Label(box.transform, "Title", Strings.Get("ui.away_title"), UiType.Title, theme.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Box(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(860f, 70f));
            _duration = UiKit.Label(box.transform, "Duration", "", UiType.Body, theme.TextMuted, TextAnchor.MiddleCenter);
            UiKit.Box(_duration.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -125f), new Vector2(860f, 44f));
            UiKit.CircleImage(box.transform, "CoinIcon", theme.Coin, new Vector2(-150f, 40f), 56f);
            _coins = UiKit.Label(box.transform, "Coins", "", UiType.Big, theme.Coin, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Box(_coins.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(40f, 40f), new Vector2(400f, 80f));
            _sources = UiKit.Label(box.transform, "Sources", "", UiType.Label, theme.TextMuted, TextAnchor.UpperCenter);
            UiKit.Box(_sources.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(800f, 110f));
            _capped = UiKit.Label(box.transform, "Capped", "", UiType.Caption, theme.HintAccent, TextAnchor.MiddleCenter);
            UiKit.Box(_capped.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 170f), new Vector2(800f, 34f));
            var ok = UiKit.Button(box.transform, "Ok", Strings.Get("ui.ok"), UiType.Heading, theme.HintAccent, new Color(0.12f, 0.1f, 0.08f), Apply);
            UiKit.Box(ok.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(400f, 110f));
            _panel.AddComponent<SheetTransition>().Page = box.rectTransform;
            _panel.SetActive(false);
        }

        public void Show(OfflineReport report)
        {
            int minutes = Mathf.RoundToInt((float)report.SecondsSimulated / 60f);
            _duration.text = Strings.Format("away.duration", ("hours", minutes / 60), ("minutes", minutes % 60));
            _coins.text = "+" + NumberFormat.Short(report.CoinsEarned);
            string lines = "";
            if (report.HarvestsApprentice > 0) lines += Strings.Format("away.apprentices", ("count", report.HarvestsApprentice)) + "\n";
            if (report.HarvestsTractor > 0) lines += Strings.Format("away.tractor", ("count", report.HarvestsTractor)) + "\n";
            if (lines.Length == 0) lines = Strings.Get("away.nothing");
            _sources.text = lines.TrimEnd();
            _capped.text = report.Capped ? Strings.Get("away.capped") : "";
            _hud.HeldCoins = report.CoinsEarned;
            _panel.SetActive(true);
            _open = true;
            _game.InputBlocked = true;
        }

        /// <summary>OK: release the held coins into the counter with a punch.</summary>
        public void Apply()
        {
            if (!_open) return;
            _hud.HeldCoins = 0;
            _hud.Punch();
            _audio.Play(SfxId.Purchase);
            _panel.SetActive(false);
            _open = false;
            _game.InputBlocked = _game.State.Phase != Phase.Year;
        }

        private void Update()
        {
            // Tapping anywhere (the field) applies OK.
            if (_open && _game.Pointer != null && _game.Pointer.Current.Tapped) Apply();
        }
    }
}
