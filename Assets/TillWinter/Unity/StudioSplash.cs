using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace TillWinter.Unity
{
    /// <summary>
    /// The EFS Games mark on the way in. It greets an app run, not every trip to the menu, so a static flag keeps it
    /// to once per process. A tap anywhere cuts to the lift: the mark is a signature, not a toll.
    ///
    /// The cover is the menu's own green, the colour of the button the lift reveals: the same mark plays on the
    /// felt in PuttSeed, and a pale logo reads on a saturated green as well as it does on black, without the
    /// first frame of the app being a black screen.
    /// </summary>
    public sealed class StudioSplash : MonoBehaviour
    {
        private const float FadeIn = 0.85f;
        private const float Hold = 1f;
        private const float Lift = 0.5f;

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

        private Image _cover, _logo;

        private void Begin(RectTransform canvas, Sprite sprite)
        {
            var cover = HudTheme.Load().MenuPrimary;
            _cover = UiKit.Panel(canvas, "StudioSplash", new Color(cover.r, cover.g, cover.b, 1f), false, true); // raycast on: it swallows the skip tap
            AudioManager.Instance?.Play(SfxId.WinterChime, 0.35f); // the first second of the app makes a sound
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
            for (float t = 0f; t < FadeIn && !skipped; t += Step)
            {
                SetAlpha(_logo, Mathf.SmoothStep(0f, 1f, t / FadeIn));
                skipped = Tapped();
                yield return null;
            }
            SetAlpha(_logo, 1f);
            for (float t = 0f; t < Hold && !skipped; t += Step)
            {
                skipped = Tapped();
                yield return null;
            }
            for (float t = 0f; t < Lift; t += Step)
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
