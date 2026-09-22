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
        private const int TipCount = 14; // v2.8: eight more, for the systems the first six never mention

        /// <summary>Set by the title scene from the save: the seedling takes the season the farm is in.</summary>
        public static Color? LeafTint;

        private Image _cover;
        private TMP_Text _label;
        private CanvasGroup _group;
        private RectTransform _sprout;
        private Image _stem, _leafL, _leafR;

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
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand; // same fit as the game canvas

            var rt = (RectTransform)canvasGo.transform;
            _cover = UiKit.Panel(rt, "Cover", theme.BootFade, false, true);
            UiKit.Stretch(_cover.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _group = _cover.gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _label = UiKit.Label(_cover.transform, "Label", Strings.Get("ui.loading"), UiType.Title, theme.SheetInk, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Box(_label.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -190f), new Vector2(900f, 90f));
            // A tip under the word, a different one each load.
            var tip = UiKit.Label(_cover.transform, "Tip", Strings.Get("tip." + Random.Range(0, TipCount)), UiType.Body, theme.SheetMuted, TextAnchor.UpperCenter);
            tip.enableWordWrapping = true;
            UiKit.Box(tip.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0f, -270f), new Vector2(820f, 140f));

            // A seed growing while you wait, rather than a word on its own: soil line, a stem that rises and two
            // leaves that open off it. Built from the same primitives every other screen uses.
            _sprout = UiKit.Rect("Sprout", _cover.transform);
            UiKit.Box(_sprout, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(260f, 260f));
            var soil = UiKit.Panel(_sprout, "Soil", theme.SheetInk, true, false);
            UiKit.Box(soil.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(190f, 14f));
            _stem = UiKit.Panel(_sprout, "Stem", theme.SheetButton, true, false);
            UiKit.Box(_stem.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(16f, 150f));
            var leafColor = LeafTint ?? theme.SheetButton;
            _leafL = UiKit.CircleImage(_sprout, "LeafL", leafColor, Vector2.zero, 10f);
            _leafR = UiKit.CircleImage(_sprout, "LeafR", leafColor, Vector2.zero, 10f);
            foreach (var leaf in new[] { _leafL, _leafR })
            {
                var leafRt = leaf.rectTransform;
                leafRt.anchorMin = leafRt.anchorMax = new Vector2(0.5f, 0f);
                leafRt.sizeDelta = new Vector2(96f, 44f); // a stretched circle reads as a leaf
            }
            _leafL.rectTransform.pivot = new Vector2(1f, 0.5f);
            _leafR.rectTransform.pivot = new Vector2(0f, 0.5f);
            StartCoroutine(Run(sceneName));
        }

        private IEnumerator Run(string sceneName)
        {
            Time.timeScale = 1f;
            for (float t = 0f; t < FadeIn; t += Mathf.Min(Time.unscaledDeltaTime, 1f / 30f))
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
                Grow(Mathf.Repeat(Time.unscaledTime * 0.55f, 1f));
                yield return null;
            }
            Grow(1f);
            yield return new WaitForSecondsRealtime(HoldAfterLoad);

            for (float t = 0f; t < FadeOut; t += Mathf.Min(Time.unscaledDeltaTime, 1f / 30f)) // the first frame after a load is long
            {
                _group.alpha = 1f - UiMotion.EaseOut(t / FadeOut);
                yield return null;
            }
            _active = null;
            Destroy(gameObject);
        }

        /// <summary>One cycle of the seed growing: the stem rises first, then each leaf opens in turn.</summary>
        private void Grow(float t)
        {
            if (_stem == null) return;
            float stem = Mathf.Clamp01(t / 0.55f);
            _stem.rectTransform.sizeDelta = new Vector2(16f, 20f + 130f * UiMotion.EaseOut(stem));
            float left = Mathf.Clamp01((t - 0.4f) / 0.3f);
            float right = Mathf.Clamp01((t - 0.6f) / 0.3f);
            // The leaves ride the tip of the stem and open outwards from it, tilted up like a seedling's.
            float tip = _stem.rectTransform.sizeDelta.y;
            _leafL.rectTransform.anchoredPosition = new Vector2(-4f, tip - 18f);
            _leafR.rectTransform.anchoredPosition = new Vector2(4f, tip - 30f);
            _leafL.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -24f);
            _leafR.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 24f);
            _leafL.rectTransform.localScale = new Vector3(UiMotion.EaseOut(left), UiMotion.EaseOut(left), 1f);
            _leafR.rectTransform.localScale = new Vector3(UiMotion.EaseOut(right), UiMotion.EaseOut(right), 1f);
        }
    }
}
