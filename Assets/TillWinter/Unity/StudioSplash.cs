using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace TillWinter.Unity
{
    /// <summary>
    /// The EFS Games mark on the way in. It greets an app run, not every trip to the menu, so a static flag keeps it
    /// to once per process. A tap anywhere cuts to the lift: the mark is a signature, not a toll.
    ///
    /// The cover is dark because the logo is a pale line drawing — on the menu's own cream it would be invisible.
    /// </summary>
    public sealed class StudioSplash : MonoBehaviour
    {
        private const float FadeIn = 0.85f;
        private const float Hold = 1f;
        private const float Lift = 0.5f;

        /// <summary>Behind the mark: dark enough for a pale logo to read, warm enough to belong to this game.</summary>
        private static readonly Color Cover = new Color(0.09f, 0.08f, 0.07f, 1f);

        private static bool _played;

        /// <summary>Plays the mark over <paramref name="canvas"/> if it has not run yet this launch.</summary>
        public static void PlayOnce(RectTransform canvas)
        {
            if (_played || canvas == null) return;
            var sprite = Resources.Load<Sprite>("efs-logo");
            if (sprite == null) return; // no mark, no splash: never block the game on a missing asset
            _played = true;
            canvas.gameObject.AddComponent<StudioSplash>().Begin(canvas, sprite);
        }

        private Image _cover, _logo;

        private void Begin(RectTransform canvas, Sprite sprite)
        {
            _cover = UiKit.Panel(canvas, "StudioSplash", Cover, false, true); // raycast on: it swallows the skip tap
            UiKit.Stretch(_cover.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _cover.rectTransform.SetAsLastSibling();

            _logo = UiKit.Panel(_cover.transform, "Logo", Color.white, false, false);
            _logo.sprite = sprite;
            _logo.type = Image.Type.Simple;
            _logo.preserveAspect = true;
            UiKit.Stretch(_logo.rectTransform, new Vector2(0.15f, 0.4f), new Vector2(0.85f, 0.6f), Vector2.zero, Vector2.zero);
            SetAlpha(_logo, 0f);
            StartCoroutine(Play());
        }

        private IEnumerator Play()
        {
            bool skipped = false;
            for (float t = 0f; t < FadeIn && !skipped; t += Time.unscaledDeltaTime)
            {
                SetAlpha(_logo, Mathf.SmoothStep(0f, 1f, t / FadeIn));
                skipped = Tapped();
                yield return null;
            }
            SetAlpha(_logo, 1f);
            for (float t = 0f; t < Hold && !skipped; t += Time.unscaledDeltaTime)
            {
                skipped = Tapped();
                yield return null;
            }
            for (float t = 0f; t < Lift; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.SmoothStep(1f, 0f, t / Lift);
                SetAlpha(_cover, k);
                SetAlpha(_logo, k);
                yield return null;
            }
            Destroy(_cover.gameObject);
            Destroy(this);
        }

        private static bool Tapped()
        {
            var pointer = UnityEngine.InputSystem.Pointer.current;
            return pointer != null && pointer.press.wasPressedThisFrame;
        }

        private static void SetAlpha(Image image, float alpha)
        {
            var c = image.color;
            image.color = new Color(c.r, c.g, c.b, alpha);
        }
    }
}
