using System.Collections.Generic;
using UnityEngine;

namespace ResortEmpire.UI
{
    /// <summary>
    /// Where the tutorial bubble goes and where its arrow runs. All rects are in one 2D space (the tutorial canvas's
    /// safe area). Plain math, so EditMode tests can check it without a scene.
    /// </summary>
    internal static class TutorialLayout
    {
        /// <summary>Room between the bubble and its target, for the arrow.</summary>
        public const float ArrowGap = 64f;

        /// <summary>Space kept clear around the target and the screen edge.</summary>
        public const float Margin = 8f;

        /// <summary>Longest arrow drawn; a bubble further away gets an arrow of this length at the target's end.</summary>
        public const float MaxArrowLength = 130f;

        /// <summary>Shortest arrow worth drawing.</summary>
        public const float MinArrowLength = 24f;

        public static Rect RectAt(Vector2 center, Vector2 size) => new Rect(center - size * 0.5f, size);

        /// <summary>
        /// The bubble's centre. With a target: beside it (right, left, below, above, then the corners), inside
        /// <paramref name="bounds"/>, clear of the target and of every rect in <paramref name="avoid"/>; of the spots
        /// that fit, the one that needed the least nudging to stay on screen. Without a target, or when nothing fits:
        /// <paramref name="fallback"/>, kept on screen.
        /// </summary>
        public static Vector2 PlaceBubble(Rect bounds, Vector2 size, Rect? target, IReadOnlyList<Rect> avoid, Vector2 fallback) =>
            TryPlaceBubble(bounds, size, target, avoid, out Vector2 center) ? center : Clamp(fallback, size, bounds);

        /// <summary>
        /// Like <see cref="PlaceBubble"/>, but false when no spot fits, so the caller can try a smaller bubble. Without a
        /// target nothing needs placing: false.
        /// </summary>
        public static bool TryPlaceBubble(Rect bounds, Vector2 size, Rect? target, IReadOnlyList<Rect> avoid, out Vector2 center)
        {
            center = Vector2.zero;
            if (target == null) return false;

            Rect t = target.Value;
            float dx = ArrowGap + size.x * 0.5f;
            float dy = ArrowGap + size.y * 0.5f;
            float diagonal = ArrowGap * 0.7f;
            var candidates = new[]
            {
                new Vector2(t.xMax + dx, t.center.y),
                new Vector2(t.xMin - dx, t.center.y),
                new Vector2(t.center.x, t.yMin - dy),
                new Vector2(t.center.x, t.yMax + dy),
                new Vector2(t.xMax + diagonal + size.x * 0.5f, t.yMin - diagonal - size.y * 0.5f),
                new Vector2(t.xMin - diagonal - size.x * 0.5f, t.yMin - diagonal - size.y * 0.5f),
                new Vector2(t.xMax + diagonal + size.x * 0.5f, t.yMax + diagonal + size.y * 0.5f),
                new Vector2(t.xMin - diagonal - size.x * 0.5f, t.yMax + diagonal + size.y * 0.5f),
            };

            Rect keepClear = Expand(t, Margin);
            bool found = false;
            float bestNudge = float.MaxValue;
            foreach (Vector2 candidate in candidates)
            {
                Vector2 spot = Clamp(candidate, size, bounds);
                Rect bubble = RectAt(spot, size);
                if (bubble.Overlaps(keepClear) || OverlapsAny(bubble, avoid) || !Contains(bounds, bubble)) continue;

                float nudge = Vector2.Distance(spot, candidate);
                if (nudge < bestNudge - 0.5f)
                {
                    center = spot;
                    bestNudge = nudge;
                    found = true;
                }
            }

            return found;
        }

        private static bool Contains(Rect outer, Rect inner) =>
            inner.xMin >= outer.xMin - 0.5f && inner.xMax <= outer.xMax + 0.5f && inner.yMin >= outer.yMin - 0.5f && inner.yMax <= outer.yMax + 0.5f;

        /// <summary>
        /// The arrow from the bubble's edge to the target's edge, along the line between their centres. False when the
        /// two touch or overlap, so there's nothing to point across.
        /// </summary>
        public static bool TryGetArrow(Rect bubble, Rect target, out Vector2 from, out Vector2 to)
        {
            from = to = Vector2.zero;
            Vector2 direction = target.center - bubble.center;
            if (direction.sqrMagnitude < 1f) return false;
            direction.Normalize();

            from = bubble.center + direction * (ExitDistance(bubble, direction) + 4f);
            to = target.center - direction * (ExitDistance(target, direction) + 6f);
            float length = Vector2.Dot(to - from, direction);
            if (length < MinArrowLength) return false;

            if (length > MaxArrowLength) from = to - direction * MaxArrowLength;
            return true;
        }

        public static Rect Expand(Rect rect, float by) =>
            new Rect(rect.xMin - by, rect.yMin - by, rect.width + by * 2f, rect.height + by * 2f);

        private static bool OverlapsAny(Rect rect, IReadOnlyList<Rect> others)
        {
            if (others == null) return false;
            foreach (Rect other in others)
            {
                if (rect.Overlaps(other)) return true;
            }

            return false;
        }

        private static Vector2 Clamp(Vector2 center, Vector2 size, Rect bounds)
        {
            float halfWidth = size.x * 0.5f;
            float halfHeight = size.y * 0.5f;
            float x = bounds.width >= size.x ? Mathf.Clamp(center.x, bounds.xMin + halfWidth, bounds.xMax - halfWidth) : bounds.center.x;
            float y = bounds.height >= size.y ? Mathf.Clamp(center.y, bounds.yMin + halfHeight, bounds.yMax - halfHeight) : bounds.center.y;
            return new Vector2(x, y);
        }

        /// <summary>How far from a rect's centre a ray in <paramref name="direction"/> (normalized) leaves it.</summary>
        private static float ExitDistance(Rect rect, Vector2 direction)
        {
            float x = Mathf.Abs(direction.x) > 1e-4f ? rect.width * 0.5f / Mathf.Abs(direction.x) : float.MaxValue;
            float y = Mathf.Abs(direction.y) > 1e-4f ? rect.height * 0.5f / Mathf.Abs(direction.y) : float.MaxValue;
            return Mathf.Min(x, y);
        }
    }
}
