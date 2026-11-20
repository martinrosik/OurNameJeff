using System.IO;
using ResortEmpire.UI;
using UnityEditor;
using UnityEngine;

namespace ResortEmpire.Editor
{
    /// <summary>
    /// Creates the white placeholder sprites the greybox tints at runtime:
    /// a square, a circle and a 9-sliced rounded rectangle. Replaced by real art in the Figma/art pass.
    /// Wave evaluation: also a five-pointed star for the rating on Wave Complete.
    /// Tutorial: a curved arrow and a 9-sliced outline frame (<see cref="GenerateTutorialSprites"/>).
    /// </summary>
    public static class GreyboxArtGenerator
    {
        public const string Folder = "Assets/_Project/Art/Greybox";
        public const string SquarePath = Folder + "/Greybox_Square.png";
        public const string CirclePath = Folder + "/Greybox_Circle.png";
        public const string RoundedPath = Folder + "/Greybox_Rounded.png";
        public const string StarPath = Folder + "/Greybox_Star.png";

        private const int Size = 64;
        private const int CornerRadius = 16;

        // The star is drawn larger than the other shapes, so its points stay sharp at the Figma @2x size.
        private const int StarSize = 128;
        private const float StarInnerRadius = 0.48f;
        private const int StarSamples = 4;

        private static readonly Vector2[] StarCorners = GetStarCorners();

        [MenuItem("Resort Empire/Greybox/Generate Placeholder Sprites", priority = 300)]
        public static void Generate()
        {
            Directory.CreateDirectory(Folder);
            Write(SquarePath, (x, y) => 1f);
            Write(CirclePath, (x, y) => Coverage(Distance(x, y, Size * 0.5f, Size * 0.5f), Size * 0.5f));
            Write(RoundedPath, RoundedCoverage);
            Write(StarPath, StarCoverage, StarSize);
            AssetDatabase.Refresh();

            Configure(SquarePath, Vector4.zero);
            Configure(CirclePath, Vector4.zero);
            Configure(RoundedPath, new Vector4(CornerRadius + 2, CornerRadius + 2, CornerRadius + 2, CornerRadius + 2));
            Configure(StarPath, Vector4.zero, StarSize);
        }

        public static Sprite Square => AssetDatabase.LoadAssetAtPath<Sprite>(SquarePath);
        public static Sprite Circle => AssetDatabase.LoadAssetAtPath<Sprite>(CirclePath);
        public static Sprite Rounded => AssetDatabase.LoadAssetAtPath<Sprite>(RoundedPath);
        public static Sprite Star => AssetDatabase.LoadAssetAtPath<Sprite>(StarPath);
        public static Sprite TutorialArrow => AssetDatabase.LoadAssetAtPath<Sprite>(TutorialArrowPath);
        public static Sprite TutorialFrame => AssetDatabase.LoadAssetAtPath<Sprite>(TutorialFramePath);

        // Tutorial: the curved arrow from a bubble to its target, and the outline frame around the target.
        public const string TutorialArrowPath = Folder + "/Greybox_TutorialArrow.png";
        public const string TutorialFramePath = Folder + "/Greybox_TutorialFrame.png";
        private const int ArrowSamples = 3;

        /// <summary>Draws the tutorial's arrow and frame (shapes from <see cref="TutorialArt"/>), leaving the other sprites as they are.</summary>
        [MenuItem("Resort Empire/Greybox/Generate Tutorial Sprites", priority = 302)]
        public static void GenerateTutorialSprites()
        {
            Directory.CreateDirectory(Folder);
            Write(TutorialArrowPath, ArrowCoverage, TutorialArt.ArrowWidth, TutorialArt.ArrowHeight);
            Write(TutorialFramePath, FrameCoverage, TutorialArt.FrameSize);
            AssetDatabase.Refresh();

            int border = TutorialArt.FrameCornerRadius + 2;
            Configure(TutorialArrowPath, Vector4.zero, TutorialArt.ArrowWidth);
            Configure(TutorialFramePath, new Vector4(border, border, border, border), TutorialArt.FrameSize);
        }

        /// <summary>The curve (a thick quadratic Bézier with a round tail) plus the head, anti-aliased by supersampling.</summary>
        private static float ArrowCoverage(int x, int y)
        {
            Vector2 direction = TutorialArt.ArrowHeadDirection;
            var normal = new Vector2(-direction.y, direction.x);
            Vector2 tip = TutorialArt.ArrowTip;
            Vector2 back = TutorialArt.ArrowEnd - direction * TutorialArt.ArrowHeadBack;
            Vector2[] head =
            {
                tip,
                back + normal * TutorialArt.ArrowHeadHalfWidth,
                back - normal * TutorialArt.ArrowHeadHalfWidth,
            };

            int inside = 0;
            for (int sy = 0; sy < ArrowSamples; sy++)
            {
                for (int sx = 0; sx < ArrowSamples; sx++)
                {
                    var point = new Vector2(x + (sx + 0.5f) / ArrowSamples, y + (sy + 0.5f) / ArrowSamples);
                    if (IsInsidePolygon(point, head) || DistanceToCurve(point) <= TutorialArt.ArrowStrokeRadius) inside++;
                }
            }

            return inside / (float)(ArrowSamples * ArrowSamples);
        }

        private static float DistanceToCurve(Vector2 point)
        {
            const int Segments = 48;
            float best = float.MaxValue;
            Vector2 previous = TutorialArt.ArrowStart;
            for (int i = 1; i <= Segments; i++)
            {
                float t = i / (float)Segments;
                Vector2 next = (1 - t) * (1 - t) * TutorialArt.ArrowStart + 2 * (1 - t) * t * TutorialArt.ArrowControl + t * t * TutorialArt.ArrowEnd;
                best = Mathf.Min(best, DistanceToSegment(point, previous, next));
                previous = next;
            }

            return best;
        }

        private static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(point, a + ab * t);
        }

        /// <summary>A rounded rectangle's outline, for a 9-sliced frame.</summary>
        private static float FrameCoverage(int x, int y)
        {
            float size = TutorialArt.FrameSize;
            float radius = TutorialArt.FrameCornerRadius;
            float cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
            float cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
            float distance = Distance(x, y, cx, cy);
            float outer = Coverage(distance, radius);
            float inner = Coverage(distance, radius - TutorialArt.FrameThickness);
            return outer - inner;
        }

        private static void Write(string path, System.Func<int, int, float> alpha, int size = Size) => Write(path, alpha, size, size);

        private static void Write(string path, System.Func<int, int, float> alpha, int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(alpha(x, y))));
                }
            }

            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
        }

        private static float RoundedCoverage(int x, int y)
        {
            float cx = Mathf.Clamp(x + 0.5f, CornerRadius, Size - CornerRadius);
            float cy = Mathf.Clamp(y + 0.5f, CornerRadius, Size - CornerRadius);
            return Coverage(Distance(x, y, cx, cy), CornerRadius);
        }

        /// <summary>A five-pointed star, point up, anti-aliased by sampling each pixel StarSamples × StarSamples times.</summary>
        private static float StarCoverage(int x, int y)
        {
            int inside = 0;
            for (int sy = 0; sy < StarSamples; sy++)
            {
                for (int sx = 0; sx < StarSamples; sx++)
                {
                    var point = new Vector2(x + (sx + 0.5f) / StarSamples, y + (sy + 0.5f) / StarSamples);
                    if (IsInsidePolygon(point, StarCorners)) inside++;
                }
            }

            return inside / (float)(StarSamples * StarSamples);
        }

        private static Vector2[] GetStarCorners()
        {
            var corners = new Vector2[10];

            // A star is 1.902 × its radius wide and 1.809 × tall (point up), so the width decides the radius; 2 px margin keeps every tip inside the image.
            float outerRadius = (StarSize - 4f) / (2f * Mathf.Sin(72f * Mathf.Deg2Rad));
            float starHeight = outerRadius * (1f + Mathf.Cos(36f * Mathf.Deg2Rad));
            var centre = new Vector2(StarSize * 0.5f, (StarSize - starHeight) * 0.5f + outerRadius * Mathf.Cos(36f * Mathf.Deg2Rad));
            for (int i = 0; i < corners.Length; i++)
            {
                float radius = i % 2 == 0 ? outerRadius : outerRadius * StarInnerRadius;
                float angle = (90f + i * 36f) * Mathf.Deg2Rad;
                corners[i] = centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }

            return corners;
        }

        // Even-odd ray casting, so it also works for the star's inward corners.
        private static bool IsInsidePolygon(Vector2 point, Vector2[] corners)
        {
            bool inside = false;
            for (int i = 0, j = corners.Length - 1; i < corners.Length; j = i++)
            {
                bool crosses = (corners[i].y > point.y) != (corners[j].y > point.y);
                if (crosses && point.x < (corners[j].x - corners[i].x) * (point.y - corners[i].y) / (corners[j].y - corners[i].y) + corners[i].x)
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        private static float Distance(int x, int y, float cx, float cy) =>
            Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));

        // One pixel of anti-aliasing at the edge.
        private static float Coverage(float distance, float radius) => Mathf.Clamp01(radius - distance + 0.5f);

        private static void Configure(string path, Vector4 border, int size = Size)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = size;
            importer.spriteBorder = border;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }
    }
}
