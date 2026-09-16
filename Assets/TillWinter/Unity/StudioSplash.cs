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

        /// <summary>
        /// The first frames after a scene loads can each take a second or more, which would run the whole fade in one
        /// step and the mark would never be seen. Each frame advances the splash by at most a thirtieth of a second.
        /// </summary>
        private static float Step => Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);

        /// <summary>Plays the mark over <paramref name="canvas"/> if it has not run yet this launch.</summary>
        public static void PlayOnce(RectTransform canvas)
        {
            if (_played || canvas == null) return;
            // The PNG may be imported as a plain texture rather than a sprite; either way a sprite is made from it.
            var sprite = Resources.Load<Sprite>("efs-logo");
            if (sprite == null)
            {
                var tex = Resources.Load<Texture2D>("efs-logo");
                if (tex != null) sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            }
            if (sprite == null) return; // no mark, no splash: never block the game on a missing asset
            _played = true;
            canvas.gameObject.AddComponent<StudioSplash>().Begin(canvas, sprite);
        }

        private Image _cover, _logo, _glow;

        /// <summary>A warm light behind the mark (UiPalette.Honey, faint).</summary>
        private static readonly Color Glow = new Color(0.91f, 0.66f, 0.24f, 0.22f);

        private void Begin(RectTransform canvas, Sprite sprite)
        {
            _cover = UiKit.Panel(canvas, "StudioSplash", Cover, false, true); // raycast on: it swallows the skip tap
            UiKit.Stretch(_cover.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _cover.rectTransform.SetAsLastSibling();

            _glow = UiKit.Panel(_cover.transform, "Glow", Glow, false, false);
            _glow.sprite = Sprite.Create(Prims.RadialGradient(128, 0f, 1f), new Rect(0, 0, 128, 128), new Vector2(0.5f, 0.5f), 100f);
            _glow.type = Image.Type.Simple;
            UiKit.Box(_glow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1300f, 1300f));
            SetAlpha(_glow, 0f);
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
            for (float t = 0f; t < FadeIn && !skipped; t += Step)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / FadeIn);
                SetAlpha(_logo, k);
                SetAlpha(_glow, k * Glow.a);
                // The mark settles into place from slightly larger.
                _logo.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.06f, 1f, UiMotion.EaseOut(t / FadeIn));
                skipped = Tapped();
                yield return null;
            }
            SetAlpha(_logo, 1f);
            _logo.rectTransform.localScale = Vector3.one;
            for (float t = 0f; t < Hold && !skipped; t += Step)
            {
                SetAlpha(_glow, Glow.a * (0.85f + 0.15f * Mathf.Sin(t * 4f)));
                skipped = Tapped();
                yield return null;
            }
            for (float t = 0f; t < Lift; t += Step)
            {
                float k = Mathf.SmoothStep(1f, 0f, t / Lift);
                SetAlpha(_cover, k);
                SetAlpha(_logo, k);
                SetAlpha(_glow, k * Glow.a);
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
