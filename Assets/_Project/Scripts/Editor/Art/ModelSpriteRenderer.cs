using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ResortEmpire.Editor
{
    /// <summary>
    /// Bakes a 3D model (FBX/GLB already imported into the project) into a transparent PNG sprite, seen from
    /// the isometric angle the Figma uses. Renders in an isolated preview scene, super-samples for smooth edges,
    /// and trims empty borders. Used for the Kenney CC0 placeholder art; artists can use it for any model:
    /// select models in the Project window → Resort Empire/Art/Render Selected Models To PNG.
    /// </summary>
    public static class ModelSpriteRenderer
    {
        public static readonly Vector3 CameraEuler = new Vector3(30f, 45f, 0f);
        private const string ShaderName = "Hidden/ResortEmpire/ModelSpriteBake";
        private const int Padding = 4;

        [MenuItem("Resort Empire/Art/Render Selected Models To PNG", priority = 400)]
        private static void RenderSelected()
        {
            foreach (Object selected in Selection.objects)
            {
                string path = AssetDatabase.GetAssetPath(selected);
                if (selected is GameObject model && !string.IsNullOrEmpty(path))
                {
                    string output = Path.ChangeExtension(path, null) + "_render.png";
                    Render(model, null, output);
                    Debug.Log($"Rendered {path} → {output}");
                }
            }

            AssetDatabase.Refresh();
        }

        /// <param name="model">Imported model asset.</param>
        /// <param name="texture">Texture to use; null keeps each material's own main texture.</param>
        /// <param name="outputPath">Where to write the PNG (inside or outside the project).</param>
        /// <param name="size">Longest side of the square render before trimming.</param>
        public static void Render(GameObject model, Texture texture, string outputPath, int size = 512, int supersample = 3)
        {
            Shader shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogError($"Shader {ShaderName} not found.");
                return;
            }

            Scene scene = EditorSceneManager.NewPreviewScene();
            var materials = new System.Collections.Generic.List<Material>();
            RenderTexture target = null;
            try
            {
                var instance = (GameObject)Object.Instantiate(model);
                SceneManager.MoveGameObjectToScene(instance, scene);

                Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0)
                {
                    Debug.LogWarning($"{model.name} has no renderers.");
                    return;
                }

                Bounds bounds = renderers[0].bounds;
                foreach (Renderer renderer in renderers)
                {
                    var replaced = new Material[renderer.sharedMaterials.Length];
                    for (int i = 0; i < replaced.Length; i++)
                    {
                        Material original = renderer.sharedMaterials[i];
                        var material = new Material(shader);
                        material.mainTexture = texture != null ? texture : original != null ? original.mainTexture : null;
                        materials.Add(material);
                        replaced[i] = material;
                    }

                    renderer.sharedMaterials = replaced;
                    bounds.Encapsulate(renderer.bounds);
                }

                var cameraObject = new GameObject("BakeCamera");
                SceneManager.MoveGameObjectToScene(cameraObject, scene);
                var camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false;
                camera.scene = scene;
                camera.orthographic = true;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
                camera.aspect = 1f;
                cameraObject.transform.rotation = Quaternion.Euler(CameraEuler);

                Fit(camera, bounds);

                int pixels = size * supersample;
                target = RenderTexture.GetTemporary(pixels, pixels, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                camera.targetTexture = target;

                var request = new RenderPipeline.StandardRequest { destination = target };
                if (RenderPipeline.SupportsRenderRequest(camera, request))
                {
                    RenderPipeline.SubmitRenderRequest(camera, request);
                }
                else
                {
                    camera.Render();
                }

                Color32[] full = ReadBack(target, pixels);
                Color32[] small = Downsample(full, pixels, supersample);
                Texture2D trimmed = Trim(small, size);
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
                File.WriteAllBytes(outputPath, trimmed.EncodeToPNG());
                Object.DestroyImmediate(trimmed);
            }
            finally
            {
                if (target != null) RenderTexture.ReleaseTemporary(target);
                foreach (Material material in materials) Object.DestroyImmediate(material);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static void Fit(Camera camera, Bounds bounds)
        {
            Transform view = camera.transform;
            float minRight = float.MaxValue, maxRight = float.MinValue, minUp = float.MaxValue, maxUp = float.MinValue;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 offset = corner - bounds.center;
                float right = Vector3.Dot(offset, view.right);
                float up = Vector3.Dot(offset, view.up);
                minRight = Mathf.Min(minRight, right);
                maxRight = Mathf.Max(maxRight, right);
                minUp = Mathf.Min(minUp, up);
                maxUp = Mathf.Max(maxUp, up);
            }

            Vector3 centre = bounds.center + view.right * ((minRight + maxRight) * 0.5f) + view.up * ((minUp + maxUp) * 0.5f);
            float half = Mathf.Max(maxRight - minRight, maxUp - minUp) * 0.5f;
            camera.orthographicSize = half * 1.04f;

            float distance = bounds.extents.magnitude * 4f + 1f;
            view.position = centre - view.forward * distance;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = distance * 2f + bounds.extents.magnitude * 2f;
        }

        private static Color32[] ReadBack(RenderTexture target, int pixels)
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            var texture = new Texture2D(pixels, pixels, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, pixels, pixels), 0, 0);
            texture.Apply();
            RenderTexture.active = previous;
            Color32[] data = texture.GetPixels32();
            Object.DestroyImmediate(texture);
            return data;
        }

        /// <summary>Box filter with alpha weighting, so edges don't pick up the black background.</summary>
        private static Color32[] Downsample(Color32[] source, int sourceSize, int factor)
        {
            int size = sourceSize / factor;
            var result = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float r = 0, g = 0, b = 0, a = 0;
                    for (int sy = 0; sy < factor; sy++)
                    {
                        for (int sx = 0; sx < factor; sx++)
                        {
                            Color32 c = source[(y * factor + sy) * sourceSize + x * factor + sx];
                            float alpha = c.a / 255f;
                            r += c.r * alpha;
                            g += c.g * alpha;
                            b += c.b * alpha;
                            a += alpha;
                        }
                    }

                    int samples = factor * factor;
                    result[y * size + x] = a > 0
                        ? new Color32((byte)(r / a), (byte)(g / a), (byte)(b / a), (byte)Mathf.RoundToInt(a / samples * 255f))
                        : new Color32(0, 0, 0, 0);
                }
            }

            return result;
        }

        private static Texture2D Trim(Color32[] pixels, int size)
        {
            int minX = size, minY = size, maxX = -1, maxY = -1;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    if (pixels[y * size + x].a <= 2) continue;
                    minX = Mathf.Min(minX, x);
                    maxX = Mathf.Max(maxX, x);
                    minY = Mathf.Min(minY, y);
                    maxY = Mathf.Max(maxY, y);
                }
            }

            if (maxX < 0)
            {
                minX = minY = 0;
                maxX = maxY = size - 1;
            }

            minX = Mathf.Max(0, minX - Padding);
            minY = Mathf.Max(0, minY - Padding);
            maxX = Mathf.Min(size - 1, maxX + Padding);
            maxY = Mathf.Min(size - 1, maxY + Padding);

            int width = maxX - minX + 1;
            int height = maxY - minY + 1;
            var cropped = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                System.Array.Copy(pixels, (minY + y) * size + minX, cropped, y * width, width);
            }

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels32(cropped);
            texture.Apply();
            return texture;
        }
    }
}
