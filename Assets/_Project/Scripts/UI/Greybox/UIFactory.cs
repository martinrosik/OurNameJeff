using System;
using ResortEmpire.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ResortEmpire.UI
{
    /// <summary>
    /// Builds greybox uGUI elements from code: panels, text, buttons, bars.
    /// The greybox UI is created at runtime so it's quick to change; the Figma pass replaces it with prefabs.
    /// </summary>
    internal static class UIFactory
    {
        private const float RoundedCornerScale = 3f;

        private static Sprite _rounded;

        public static void Init(Sprite rounded) => _rounded = rounded;

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent != null ? parent.gameObject.layer : 5;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        /// <summary>Stretches to the parent with margins (left, right, top, bottom).</summary>
        public static RectTransform Fill(this RectTransform rect, float left = 0, float right = 0, float top = 0, float bottom = 0)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
            return rect;
        }

        /// <summary>Places a fixed-size rect at an anchor point.</summary>
        public static RectTransform At(this RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        public static Image Panel(string name, Transform parent, Color color, bool rounded = true)
        {
            RectTransform rect = Rect(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            if (rounded && _rounded != null)
            {
                image.sprite = _rounded;
                image.type = Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = RoundedCornerScale;
            }

            return image;
        }

        /// <summary>A panel that fills <see cref="GreyboxUI.SideArea"/>, so every side panel looks the same.</summary>
        public static Image SidePanel(string name, Transform sideArea)
        {
            Image panel = Panel(name, sideArea, GreyboxPalette.PanelBlue.WithAlpha(0.96f));
            panel.rectTransform.Fill();
            return panel;
        }

        public static TextMeshProUGUI Text(
            string name,
            Transform parent,
            string text,
            float size,
            Color color,
            TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft,
            FontStyles style = FontStyles.Normal)
        {
            RectTransform rect = Rect(name, parent);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = alignment;
            label.fontStyle = style;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Truncate;
            label.raycastTarget = false;
            return label;
        }

        public static Button Button(string name, Transform parent, string label, Color background, Color textColor, float fontSize, Action onClick)
        {
            Image image = Panel(name, parent, background);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            TextMeshProUGUI text = Text("Label", image.transform, label, fontSize, textColor, TextAlignmentOptions.Center, FontStyles.Bold);
            text.rectTransform.Fill(4, 4, 2, 2);
            text.textWrappingMode = TextWrappingModes.Normal;
            if (onClick != null)
            {
                button.onClick.AddListener(() => onClick());
            }

            return button;
        }

        public static void SetLabel(this Button button, string label)
        {
            TMP_Text text = button.GetComponentInChildren<TMP_Text>();
            if (text != null) text.text = label;
        }

        /// <summary>A horizontal bar; returns the fill rect, sized with <see cref="SetFill"/>.</summary>
        public static RectTransform Bar(string name, Transform parent, Color background, Color fill)
        {
            Image track = Panel(name, parent, background);
            Image fillImage = Panel("Fill", track.transform, fill);
            RectTransform fillRect = fillImage.rectTransform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(0f, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            return fillRect;
        }

        public static void SetFill(this RectTransform fill, float fraction)
        {
            fill.anchorMax = new Vector2(Mathf.Clamp01(fraction), 1f);
        }

        public static Color WithAlpha(this Color color, float alpha) => new Color(color.r, color.g, color.b, alpha);

        public static Color Lighten(this Color color, float amount) => Color.Lerp(color, Color.white, amount);

        public static readonly Color Ink = GreyboxPalette.Ink;
        public static readonly Color Paper = GreyboxPalette.Paper;
    }
}
