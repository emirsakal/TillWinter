using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace TillWinter.Unity
{
    /// <summary>Tiny uGUI builder so the whole UI can be made in code with the built-in font.</summary>
    public static class UiKit
    {
        public static readonly Color Ink = new Color(0.12f, 0.1f, 0.08f);
        public static readonly Color Paper = new Color(1f, 0.97f, 0.9f);
        public static readonly Color Accent = new Color(0.95f, 0.62f, 0.2f);
        public static readonly Color Good = new Color(0.36f, 0.68f, 0.32f);
        public static readonly Color Muted = new Color(0.55f, 0.55f, 0.55f);
        public static readonly Color CoinYellow = new Color(1f, 0.82f, 0.2f);

        private static Font _font;
        private static Sprite _rounded, _circle;

        public static Font Font => _font != null ? _font : (_font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
        public static Sprite Rounded => _rounded != null ? _rounded : (_rounded = Prims.RoundedRectSprite());
        public static Sprite Circle => _circle != null ? _circle : (_circle = Prims.CircleSprite());

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            return rt;
        }

        /// <summary>Anchors and offsets in one call. Offsets are in the parent's units (bottom-left, top-right).</summary>
        public static RectTransform Stretch(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            return rt;
        }

        /// <summary>Fixed size box anchored at an anchor point.</summary>
        public static RectTransform Box(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 anchoredPos, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            return rt;
        }

        public static Image Panel(Transform parent, string name, Color color, bool rounded = true, bool raycast = true)
        {
            var rt = Rect(name, parent);
            Stretch(rt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = raycast;
            if (rounded)
            {
                img.sprite = Rounded;
                img.type = Image.Type.Sliced;
                img.pixelsPerUnitMultiplier = 1f;
            }
            return img;
        }

        public static Text Label(Transform parent, string name, string text, int size, Color color, TextAnchor anchor = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Normal)
        {
            var rt = Rect(name, parent);
            Stretch(rt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.fontStyle = style;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        public static Button Button(Transform parent, string name, string text, int fontSize, Color bg, Color fg, UnityAction onClick)
        {
            var img = Panel(parent, name, bg);
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.highlightedColor = new Color(1.06f, 1.06f, 1.06f);
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f);
            colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.6f);
            colors.fadeDuration = 0.06f;
            btn.colors = colors;
            var label = Label(img.transform, "Label", text, fontSize, fg, TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(12f, 4f), new Vector2(-12f, -4f));
            if (onClick != null) btn.onClick.AddListener(onClick);
            return btn;
        }

        public static Slider Slider(Transform parent, string name, float min, float max, float value, UnityAction<float> onChanged)
        {
            var rt = Rect(name, parent);
            var slider = rt.gameObject.AddComponent<Slider>();

            var bg = Panel(rt, "Background", new Color(0f, 0f, 0f, 0.35f));
            Stretch(bg.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, -8f), new Vector2(0f, 8f));

            var fillArea = Rect("Fill Area", rt);
            Stretch(fillArea, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, -8f), new Vector2(0f, 8f));
            var fill = Panel(fillArea, "Fill", Accent);
            Stretch(fill.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var handleArea = Rect("Handle Slide Area", rt);
            Stretch(handleArea, Vector2.zero, Vector2.one, new Vector2(18f, 0f), new Vector2(-18f, 0f));
            var handle = Panel(handleArea, "Handle", Paper);
            handle.sprite = Circle;
            handle.type = Image.Type.Simple;
            Stretch(handle.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(-24f, -24f), new Vector2(24f, 24f));

            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = value;
            if (onChanged != null) slider.onValueChanged.AddListener(onChanged);
            return slider;
        }
    }
}
