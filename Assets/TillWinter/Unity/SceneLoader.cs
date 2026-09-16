using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TillWinter.Unity
{
    /// <summary>
    /// Scene changes go through here: a cover fades in, the next scene loads in the background, then the cover fades
    /// out over it. Menu to farm and back used to cut straight to a half-built screen. The cover survives the load
    /// (DontDestroyOnLoad) and takes the boot colour, so the farm's own fade-in continues it.
    /// </summary>
    public sealed class SceneLoader : MonoBehaviour
    {
        private const float FadeIn = 0.25f;
        private const float HoldAfterLoad = 0.15f;
        private const float FadeOut = 0.35f;

        private static SceneLoader _active;

        private Image _cover;
        private TMP_Text _label;
        private CanvasGroup _group;

        /// <summary>Loads a scene by name behind a cover. Ignored while another load is running.</summary>
        public static void Load(string sceneName)
        {
            if (_active != null) return;
            var go = new GameObject("SceneLoader");
            DontDestroyOnLoad(go);
            _active = go.AddComponent<SceneLoader>();
            _active.Begin(sceneName);
        }

        private void Begin(string sceneName)
        {
            var theme = HudTheme.Load();
            var canvasGo = new GameObject("LoadingCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000; // above every screen, including sheets
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 2340f);
            scaler.matchWidthOrHeight = 0f;

            var rt = (RectTransform)canvasGo.transform;
            _cover = UiKit.Panel(rt, "Cover", theme.BootFade, false, true);
            UiKit.Stretch(_cover.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _group = _cover.gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _label = UiKit.Label(_cover.transform, "Label", Strings.Get("ui.loading"), UiType.Title, theme.SheetInk, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Box(_label.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 90f));
            StartCoroutine(Run(sceneName));
        }

        private IEnumerator Run(string sceneName)
        {
            Time.timeScale = 1f;
            for (float t = 0f; t < FadeIn; t += Time.unscaledDeltaTime)
            {
                _group.alpha = UiMotion.EaseOut(t / FadeIn);
                yield return null;
            }
            _group.alpha = 1f;

            var op = SceneManager.LoadSceneAsync(sceneName);
            op.allowSceneActivation = true;
            while (!op.isDone)
            {
                // The label breathes instead of cycling dots, so a slow load never looks frozen and nothing allocates.
                var c = _label.color;
                c.a = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 2.2f));
                _label.color = c;
                yield return null;
            }
            yield return new WaitForSecondsRealtime(HoldAfterLoad);

            for (float t = 0f; t < FadeOut; t += Time.unscaledDeltaTime)
            {
                _group.alpha = 1f - UiMotion.EaseOut(t / FadeOut);
                yield return null;
            }
            _active = null;
            Destroy(gameObject);
        }
    }
}
