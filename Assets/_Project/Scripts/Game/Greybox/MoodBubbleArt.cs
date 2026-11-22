using System.Collections.Generic;
using ResortEmpire.Core;
using UnityEngine;

namespace ResortEmpire.Game
{
    /// <summary>
    /// Placeholder sprites for the guest mood bubbles (<see cref="GuestCrowdView"/>): a white speech bubble with a
    /// dark outline and a tail pointing down at the guest, holding a smiling, flat or frowning face. Drawn once at
    /// runtime, so the scene needs no new assets; replaced by real art in the art pass. Pixel coordinates, origin
    /// bottom-left; the sprite's pivot is the tip of the tail.
    /// </summary>
    public static class MoodBubbleArt
    {
        public const int Size = 64;
        private const int Samples = 3;

        private static readonly Vector2 BubbleCentre = new Vector2(32f, 35f);
        private const float BubbleRadius = 26f;
        private const float OutlineWidth = 3f;
        private static readonly Vector2[] Tail = { new Vector2(25f, 14f), new Vector2(39f, 14f), new Vector2(32f, 3f) };

        private static readonly Vector2 FaceCentre = new Vector2(32f, 36f);
        private const float FaceRadius = 21.5f;
        private const float EyeRadius = 3.1f;
        private const float MouthHalfWidth = 1.7f;

        private static readonly Color Outline = new Color(0.17f, 0.17f, 0.20f);
        private static readonly Color Fill = Color.white;
        public static readonly Color HappyColor = new Color(0.36f, 0.80f, 0.42f);
        public static readonly Color NeutralColor = new Color(0.98f, 0.80f, 0.25f);
        public static readonly Color UnhappyColor = new Color(0.93f, 0.40f, 0.34f);

        private static readonly Dictionary<GuestMood, Sprite> Sprites = new Dictionary<GuestMood, Sprite>();

        /// <summary>The bubble for <paramref name="mood"/>, drawn on first use.</summary>
        public static Sprite Get(GuestMood mood)
        {
            if (Sprites.TryGetValue(mood, out Sprite sprite) && sprite != null) return sprite;

            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                name = $"MoodBubble_{mood}",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };

            var pixels = new Color[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    pixels[y * Size + x] = SamplePixel(mood, x, y);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);

            var pivot = new Vector2(Tail[2].x / Size, Tail[2].y / Size);
            sprite = Sprite.Create(texture, new Rect(0, 0, Size, Size), pivot, Size, 0, SpriteMeshType.FullRect);
            sprite.name = texture.name;
            sprite.hideFlags = HideFlags.DontSave;
            Sprites[mood] = sprite;
            return sprite;
        }

        public static Color GetFaceColor(GuestMood mood)
        {
            switch (mood)
            {
                case GuestMood.Happy: return HappyColor;
                case GuestMood.Neutral: return NeutralColor;
                default: return UnhappyColor;
            }
        }

        /// <summary>Averages Samples × Samples points of one pixel (anti-aliasing), weighting colors by their alpha.</summary>
        private static Color SamplePixel(GuestMood mood, int x, int y)
        {
            Color sum = Color.clear;
            for (int sy = 0; sy < Samples; sy++)
            {
                for (int sx = 0; sx < Samples; sx++)
                {
                    Color c = Sample(mood, new Vector2(x + (sx + 0.5f) / Samples, y + (sy + 0.5f) / Samples));
                    sum += new Color(c.r * c.a, c.g * c.a, c.b * c.a, c.a);
                }
            }

            float count = Samples * Samples;
            float alpha = sum.a / count;
            return alpha > 0f ? new Color(sum.r / sum.a, sum.g / sum.a, sum.b / sum.a, alpha) : Color.clear;
        }

        private static Color Sample(GuestMood mood, Vector2 p)
        {
            bool inOuter = Vector2.Distance(p, BubbleCentre) <= BubbleRadius + OutlineWidth || InTail(p, OutlineWidth);
            if (!inOuter) return Color.clear;

            bool inInner = Vector2.Distance(p, BubbleCentre) <= BubbleRadius || InTail(p, 0f);
            if (!inInner) return Outline;

            if (Vector2.Distance(p, FaceCentre) > FaceRadius) return Fill;
            return IsFeature(mood, p) ? Outline : GetFaceColor(mood);
        }

        /// <summary>Eyes and mouth: a smile, a straight line or a frown.</summary>
        private static bool IsFeature(GuestMood mood, Vector2 p)
        {
            if (Vector2.Distance(p, new Vector2(24.8f, 41.5f)) <= EyeRadius) return true;
            if (Vector2.Distance(p, new Vector2(39.2f, 41.5f)) <= EyeRadius) return true;

            switch (mood)
            {
                case GuestMood.Happy:
                    return p.y < 35f && Mathf.Abs(Vector2.Distance(p, new Vector2(32f, 37.5f)) - 9.5f) <= MouthHalfWidth;
                case GuestMood.Neutral:
                    return Mathf.Abs(p.y - 27.5f) <= MouthHalfWidth && Mathf.Abs(p.x - 32f) <= 7.8f;
                default:
                    return p.y > 23.5f && Mathf.Abs(Vector2.Distance(p, new Vector2(32f, 18.5f)) - 9.5f) <= MouthHalfWidth;
            }
        }

        /// <summary>Inside the tail triangle, grown by <paramref name="grow"/> pixels for the outline.</summary>
        private static bool InTail(Vector2 p, float grow)
        {
            for (int i = 0; i < Tail.Length; i++)
            {
                Vector2 a = Tail[i];
                Vector2 b = Tail[(i + 1) % Tail.Length];
                Vector2 edge = b - a;

                // The triangle's corners run clockwise, so the inside is to the right of every edge.
                var outward = new Vector2(-edge.y, edge.x).normalized;
                if (Vector2.Dot(p - a, outward) > grow) return false;
            }

            return true;
        }
    }
}
