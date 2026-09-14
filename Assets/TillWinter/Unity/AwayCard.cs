using TillWinter.Core;
using UnityEngine;
using UnityEngine.UI;

namespace TillWinter.Unity
{
    /// <summary>Placeholder "While you were away" card (GDD §9): seconds, coins, OK.</summary>
    public sealed class AwayCard : MonoBehaviour
    {
        private GameController _game;
        private AudioManager _audio;
        private GameObject _panel;
        private Text _text;

        public void Init(GameController game, AudioManager audio, RectTransform canvas)
        {
            _game = game;
            _audio = audio;
            var dim = UiKit.Panel(canvas, "AwayCard", new Color(0f, 0f, 0f, 0.6f), false, true);
            _panel = dim.gameObject;
            var box = UiKit.Panel(dim.transform, "Box", new Color(0.12f, 0.14f, 0.2f, 0.98f), true, true);
            UiKit.Box(box.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(880f, 520f));
            var title = UiKit.Label(box.transform, "Title", "While you were away", 50, UiKit.Paper, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Box(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(840f, 70f));
            _text = UiKit.Label(box.transform, "Text", "", 32, new Color(0.85f, 0.88f, 0.95f), TextAnchor.MiddleCenter);
            UiKit.Stretch(_text.rectTransform, Vector2.zero, Vector2.one, new Vector2(40f, 160f), new Vector2(-40f, -120f));
            var ok = UiKit.Button(box.transform, "Ok", "OK", 38, UiKit.Accent, UiKit.Ink, () => { _audio.Play(SfxId.UiClick); Hide(); });
            UiKit.Box(ok.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(400f, 110f));
            _panel.SetActive(false);
        }

        public void Show(OfflineReport report)
        {
            int minutes = Mathf.RoundToInt((float)report.SecondsSimulated / 60f);
            string time = minutes >= 60 ? (minutes / 60) + " h " + (minutes % 60) + " min" : minutes + " min";
            _text.text = "Your helpers kept working for " + time + (report.Capped ? " (8 h cap)" : "") + ".\n\n" +
                         "+" + NumberFormat.Short(report.CoinsEarned) + " coins from " + report.Harvests + " harvests.";
            _panel.SetActive(true);
            _game.InputBlocked = true;
        }

        private void Hide()
        {
            _panel.SetActive(false);
            _game.InputBlocked = _game.State.Phase != Phase.Year;
        }
    }
}
