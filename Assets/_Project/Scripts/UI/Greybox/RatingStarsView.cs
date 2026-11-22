using ResortEmpire.Game;
using UnityEngine;
using UnityEngine.UI;

namespace ResortEmpire.UI
{
    /// <summary>
    /// Wave evaluation: five rating stars in a gentle arc on Wave Complete (UI doc S4: "rating, stars, soft").
    /// Earned stars are gold, the rest grey, so the player sees how far the top result is. Perfect shows all five.
    /// </summary>
    internal sealed class RatingStarsView
    {
        public const int StarCount = 5;
        public const float Height = 56f;

        // Left to right: the middle star is the biggest and highest, the outer ones lean outwards.
        private static readonly float[] StarSizes = { 32f, 40f, 50f, 40f, 32f };
        private static readonly float[] StarX = { -92f, -48f, 0f, 48f, 92f };
        private static readonly float[] StarCentreFromTop = { 36f, 30f, 26f, 30f, 36f };
        private static readonly float[] StarTilt = { 18f, 9f, 0f, -9f, -18f };

        // The outline is the same star drawn slightly bigger behind the fill, so the tips stay sharp (an Outline effect flattens them).
        private const float OutlineWidth = 2f;

        private readonly RectTransform _root;
        private readonly Image[] _stars = new Image[StarCount];
        private readonly Image[] _outlines = new Image[StarCount];
        private int _earned;

        public RatingStarsView(Transform parent)
        {
            _root = UIFactory.Rect("RatingStars", parent);

            var top = new Vector2(0.5f, 1f);
            var center = new Vector2(0.5f, 0.5f);
            for (int i = 0; i < StarCount; i++)
            {
                Image outline = UIFactory.Star($"Star{i + 1}", _root, GreyboxPalette.StarEmpty);
                outline.rectTransform.At(top, center, new Vector2(StarX[i], -StarCentreFromTop[i]), Vector2.one * StarSizes[i]);
                outline.rectTransform.localRotation = Quaternion.Euler(0f, 0f, StarTilt[i]);

                Image star = UIFactory.Star("Fill", outline.transform, GreyboxPalette.StarEmpty);
                star.rectTransform.At(center, center, Vector2.zero, Vector2.one * (StarSizes[i] - 2f * OutlineWidth));

                _stars[i] = star;
                _outlines[i] = outline;
            }
        }

        public bool IsShown => _root.gameObject.activeSelf;

        public RectTransform Root => _root;

        /// <summary>How many stars are gold right now (0 while hidden).</summary>
        public int EarnedCount => IsShown ? _earned : 0;

        /// <summary>Shows the arc with its top edge <paramref name="y"/> below the card's top, <paramref name="earned"/> stars gold.</summary>
        public void Show(int earned, float y)
        {
            _earned = earned;
            _root.At(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(240f, Height));
            for (int i = 0; i < StarCount; i++)
            {
                bool isEarned = i < earned;
                _stars[i].color = isEarned ? GreyboxPalette.StarEarned : GreyboxPalette.StarEmpty;
                _outlines[i].color = isEarned ? GreyboxPalette.StarEarnedOutline : GreyboxPalette.StarEmpty;
            }

            _root.gameObject.SetActive(true);
        }

        public void Hide() => _root.gameObject.SetActive(false);
    }
}
