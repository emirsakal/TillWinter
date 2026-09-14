using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace TillWinter.Unity
{
    /// <summary>Tiny uGUI builder (TextMeshPro text, Nunito SDF font) so the whole UI can be made in code.</summary>
    public static class UiKit
    {
        public static readonly Color Ink = new Color(0.12f, 0.1f, 0.08f);
        public static readonly Color Paper = new Color(1f, 0.97f, 0.9f);
        public static readonly Color Accent = new Color(0.95f, 0.62f, 0.2f);
        public static readonly Color Good = new Color(0.36f, 0.68f, 0.32f);
        public static readonly Color Muted = new Color(0.55f, 0.55f, 0.55f);
        public static readonly Color CoinYellow = new Color(1f, 0.82f, 0.2f);

        private static TMP_FontAsset _font;
        private static Sprite _rounded, _circle;

        /// <summary>Nunito SDF from Resources (Latin + Turkish), with the TMP default font as fallback for missing glyphs.</summary>
        public static TMP_FontAsset Font
        {
            get
            {
                if (_font != null) return _font;
                _font = Resources.Load<TMP_FontAsset>("NunitoSDF");
                if (_font == null) _font = TMP_Settings.defaultFontAsset;
                else if (TMP_Settings.defaultFontAsset != null && !_font.fallbackFontAssetTable.Contains(TMP_Settings.defaultFontAsset))
                    _font.fallbackFontAssetTable.Add(TMP_Settings.defaultFontAsset);
                return _font;
            }
        }

        public static Sprite Rounded => _rounded != null ? _rounded : (_rounded = Prims.RoundedRectSprite());
        public static Sprite Circle => _circle != null ? _circle : (_circle = Prims.CircleSprite());

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            return rt;
        }

        public static RectTransform Stretch(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            return rt;
        }

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

        public static Image CircleImage(Transform parent, string name, Color color, Vector2 anchoredPos, float diameter, bool raycast = false)
        {
            var img = Panel(parent, name, color, false, raycast);
            img.sprite = Circle;
            img.type = Image.Type.Simple;
            Box(img.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), anchoredPos, new Vector2(diameter, diameter));
            return img;
        }

        public static TMP_Text Label(Transform parent, string name, string text, int size, Color color, TextAnchor anchor = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Normal)
        {
            var rt = Rect(name, parent);
            Stretch(rt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = Map(anchor);
            t.fontStyle = style == FontStyle.Bold ? FontStyles.Bold : style == FontStyle.Italic ? FontStyles.Italic : FontStyles.Normal;
            t.enableWordWrapping = true;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            t.richText = false;
            return t;
        }

        /// <summary>Soft dark outline for text over the 3D field.</summary>
        public static void Outline(TMP_Text t, float width = 0.18f)
        {
            t.outlineWidth = width;
            t.outlineColor = new Color32(0, 0, 0, 150);
        }

        private static TextAlignmentOptions Map(TextAnchor a)
        {
            switch (a)
            {
                case TextAnchor.UpperLeft: return TextAlignmentOptions.TopLeft;
                case TextAnchor.UpperCenter: return TextAlignmentOptions.Top;
                case TextAnchor.UpperRight: return TextAlignmentOptions.TopRight;
                case TextAnchor.MiddleLeft: return TextAlignmentOptions.Left;
                case TextAnchor.MiddleRight: return TextAlignmentOptions.Right;
                case TextAnchor.LowerLeft: return TextAlignmentOptions.BottomLeft;
                case TextAnchor.LowerCenter: return TextAlignmentOptions.Bottom;
                case TextAnchor.LowerRight: return TextAlignmentOptions.BottomRight;
                default: return TextAlignmentOptions.Center;
            }
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

        public static TMP_Text ButtonLabel(Button b) => b.GetComponentInChildren<TMP_Text>();

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

        /// <summary>TMP dropdown with the given options.</summary>
        public static TMP_Dropdown Dropdown(Transform parent, string name, System.Collections.Generic.List<string> options, UnityAction<int> onChanged)
        {
            var img = Panel(parent, name, new Color(0.2f, 0.2f, 0.24f));
            var dd = img.gameObject.AddComponent<TMP_Dropdown>();
            dd.targetGraphic = img;
            var caption = Label(img.transform, "Caption", options.Count > 0 ? options[0] : "", 24, Paper, TextAnchor.MiddleLeft);
            Stretch(caption.rectTransform, Vector2.zero, Vector2.one, new Vector2(16f, 2f), new Vector2(-40f, -2f));
            dd.captionText = caption;

            var template = Rect("Template", img.transform);
            Stretch(template, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, -300f), new Vector2(0f, 0f));
            var templateImg = template.gameObject.AddComponent<Image>();
            templateImg.color = new Color(0.12f, 0.12f, 0.15f, 0.98f);
            var scroll = template.gameObject.AddComponent<ScrollRect>();
            var viewport = Rect("Viewport", template);
            Stretch(viewport, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            viewport.gameObject.AddComponent<RectMask2D>();
            viewport.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f);
            var content = Rect("Content", viewport);
            Stretch(content, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = new Vector2(0f, 44f);
            var item = Rect("Item", content);
            Stretch(item, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, -22f), new Vector2(0f, 22f));
            var toggle = item.gameObject.AddComponent<Toggle>();
            var itemBg = Panel(item, "Item Background", new Color(1f, 1f, 1f, 0.06f), false);
            var itemLabel = Label(item, "Item Label", "", 22, Paper, TextAnchor.MiddleLeft);
            Stretch(itemLabel.rectTransform, Vector2.zero, Vector2.one, new Vector2(16f, 0f), new Vector2(-8f, 0f));
            toggle.targetGraphic = itemBg;
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            dd.template = template;
            dd.itemText = itemLabel;
            template.gameObject.SetActive(false);
            dd.options.Clear();
            foreach (var o in options) dd.options.Add(new TMP_Dropdown.OptionData(o));
            if (onChanged != null) dd.onValueChanged.AddListener(onChanged);
            return dd;
        }
    }
}
