using UnityEngine;

namespace ResortEmpire.UI
{
    /// <summary>
    /// Wave Complete breakdown icons: a check in a circle ("went great") and a warning triangle ("needs improvement"),
    /// drawn once at runtime as white outlines so <see cref="WaveCompleteView"/> can tint them, and sharp at any scale.
    /// Shapes follow a 24-unit icon grid (2-unit strokes, round ends); replaced by real art in the art pass.
    /// </summary>
    internal static class ResultIconArt
    {
        private const int Size = 64;
        private const int Samples = 4;
        private const float GridUnits = 24f;
        private const float HalfStroke = 1f;

        private static Sprite _check;
        private static Sprite _warning;

        public static Sprite Check => _check != null ? _check : _check = Draw("ResultIcon_Check", IsCheck);
        public static Sprite Warning => _warning != null ? _warning : _warning = Draw("ResultIcon_Warning", IsWarning);

        private static bool IsCheck(Vector2 p) =>
            Mathf.Abs(Vector2.Distance(p, new Vector2(12f, 12f)) - 10f) <= HalfStroke
            || IsOnSegment(p, new Vector2(9f, 12f), new Vector2(11f, 14f))
            || IsOnSegment(p, new Vector2(11f, 14f), new Vector2(15f, 10f));

        private static bool IsWarning(Vector2 p)
        {
            var apex = new Vector2(12f, 3.6f);
            var left = new Vector2(2.4f, 20.2f);
            var right = new Vector2(21.6f, 20.2f);
            return IsOnSegment(p, apex, left) || IsOnSegment(p, left, right) || IsOnSegment(p, right, apex)
                || IsOnSegment(p, new Vector2(12f, 9.5f), new Vector2(12f, 13.5f))
                || IsOnSegment(p, new Vector2(12f, 17f), new Vector2(12f, 17f));
        }

        /// <summary>Within half a stroke of the segment a–b (grid units, y down), which gives round ends and joins.</summary>
        private static bool IsOnSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = ab.sqrMagnitude > 0f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            return Vector2.Distance(p, a + ab * t) <= HalfStroke;
        }

        private static Sprite Draw(string name, System.Func<Vector2, bool> isInside)
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };

            float unitsPerPixel = GridUnits / Size;
            var pixels = new Color[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    // Anti-aliasing: the share of Samples × Samples points inside the shape becomes the alpha.
                    int inside = 0;
                    for (int sy = 0; sy < Samples; sy++)
                    {
                        for (int sx = 0; sx < Samples; sx++)
                        {
                            float gridX = (x + (sx + 0.5f) / Samples) * unitsPerPixel;
                            float gridY = GridUnits - (y + (sy + 0.5f) / Samples) * unitsPerPixel;
                            if (isInside(new Vector2(gridX, gridY))) inside++;
                        }
                    }

                    pixels[y * Size + x] = new Color(1f, 1f, 1f, inside / (float)(Samples * Samples));
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);

            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size, 0, SpriteMeshType.FullRect);
            sprite.name = name;
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }
    }
}
