using System.IO;
using UnityEditor;
using UnityEngine;

namespace ResortEmpire.Editor
{
    /// <summary>
    /// Creates the white placeholder sprites the greybox tints at runtime:
    /// a square, a circle and a 9-sliced rounded rectangle. Replaced by real art in the Figma/art pass.
    /// </summary>
    public static class GreyboxArtGenerator
    {
        public const string Folder = "Assets/_Project/Art/Greybox";
        public const string SquarePath = Folder + "/Greybox_Square.png";
        public const string CirclePath = Folder + "/Greybox_Circle.png";
        public const string RoundedPath = Folder + "/Greybox_Rounded.png";

        private const int Size = 64;
        private const int CornerRadius = 16;

        [MenuItem("Resort Empire/Greybox/Generate Placeholder Sprites", priority = 300)]
        public static void Generate()
        {
            Directory.CreateDirectory(Folder);
            Write(SquarePath, (x, y) => 1f);
            Write(CirclePath, (x, y) => Coverage(Distance(x, y, Size * 0.5f, Size * 0.5f), Size * 0.5f));
            Write(RoundedPath, RoundedCoverage);
            AssetDatabase.Refresh();

            Configure(SquarePath, Vector4.zero);
            Configure(CirclePath, Vector4.zero);
            Configure(RoundedPath, new Vector4(CornerRadius + 2, CornerRadius + 2, CornerRadius + 2, CornerRadius + 2));
        }

        public static Sprite Square => AssetDatabase.LoadAssetAtPath<Sprite>(SquarePath);
        public static Sprite Circle => AssetDatabase.LoadAssetAtPath<Sprite>(CirclePath);
        public static Sprite Rounded => AssetDatabase.LoadAssetAtPath<Sprite>(RoundedPath);

        private static void Write(string path, System.Func<int, int, float> alpha)
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
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

        private static float Distance(int x, int y, float cx, float cy) =>
            Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));

        // One pixel of anti-aliasing at the edge.
        private static float Coverage(float distance, float radius) => Mathf.Clamp01(radius - distance + 0.5f);

        private static void Configure(string path, Vector4 border)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = Size;
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
