using UnityEngine;

namespace ResortEmpire.UI
{
    /// <summary>
    /// Shapes of the tutorial's placeholder sprites, shared by the Editor generator that draws them
    /// (GreyboxArtGenerator) and the view that places them (<see cref="TutorialView"/>). Pixel coordinates, origin
    /// bottom-left. Replaced by real art in the art pass.
    /// </summary>
    public static class TutorialArt
    {
        // Curved arrow: a thick quadratic curve that bows upward, ending in a head at the right.
        public const int ArrowWidth = 384;
        public const int ArrowHeight = 160;
        public static readonly Vector2 ArrowStart = new Vector2(26f, 58f);
        public static readonly Vector2 ArrowControl = new Vector2(192f, 148f);
        public static readonly Vector2 ArrowEnd = new Vector2(330f, 74f);
        public const float ArrowStrokeRadius = 10f;
        public const float ArrowHeadLength = 30f;
        public const float ArrowHeadBack = 14f;
        public const float ArrowHeadHalfWidth = 27f;

        /// <summary>Direction the head points: the curve's direction at its end.</summary>
        public static Vector2 ArrowHeadDirection => (ArrowEnd - ArrowControl).normalized;

        /// <summary>The tip of the head, where the arrow touches its target.</summary>
        public static Vector2 ArrowTip => ArrowEnd + ArrowHeadDirection * ArrowHeadLength;

        // Highlight frame: a rounded outline, 9-sliced so it fits any target.
        public const int FrameSize = 64;
        public const int FrameCornerRadius = 18;
        public const float FrameThickness = 5f;
    }
}
