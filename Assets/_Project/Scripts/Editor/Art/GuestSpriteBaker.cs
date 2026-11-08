using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ResortEmpire.Editor
{
    /// <summary>
    /// Bakes the animated Kenney "Mini Characters" (CC0) into walk-cycle sprite sheets for the guests on the island.
    /// <para>
    /// Download the pack from https://kenney.nl/assets/mini-characters, unzip it, then run
    /// <b>Resort Empire → Art → Bake Guest Characters</b> and pick its <c>Models/FBX format</c> folder. The models
    /// are imported into a temporary folder, rendered from the isometric angle the island uses
    /// (<see cref="ModelSpriteRenderer.CameraEuler"/>), and deleted again. Only the PNG sheets go into the repo.
    /// </para>
    /// Each sheet <c>Art/Guests/Guest_&lt;Name&gt;_Walk.png</c> has two rows of <see cref="Frames"/> frames:
    /// row "Front" walks towards the viewer and to the right (down-right on screen), row "Back" walks away and to
    /// the right (up-right). Walking left uses the same frames flipped. The pivot sits at the feet.
    /// </summary>
    public static class GuestSpriteBaker
    {
        public const string OutputFolder = "Assets/_Project/Art/Guests";
        public const int Frames = 8;
        public const int FrameSize = 192;
        private const int Supersample = 3;
        private const float PixelsPerUnit = 256f;
        private const string TempFolder = "Assets/_GuestBakeTemp";
        private const string WalkClip = "walk";

        /// <summary>Only ordinary people from the pack: no zombie, robot, alien, knight, soldier or raider.</summary>
        public static readonly string[] Characters =
        {
            "character-female-a", "character-female-b", "character-female-c", "character-female-d",
            "character-female-e", "character-female-f",
            "character-male-a", "character-male-b", "character-male-c", "character-male-d",
            "character-male-e", "character-male-f", "character-male-g",
        };

        /// <summary>Sheet rows, top to bottom, with the model's yaw for each. The models face +Z.</summary>
        private static readonly (string Name, float Yaw)[] Rows =
        {
            ("Front", 180f), // -Z: towards the camera and to the right on screen
            ("Back", 90f),   // +X: away from the camera and to the right on screen
        };

        [MenuItem("Resort Empire/Art/Bake Guest Characters", priority = 401)]
        private static void BakeFromMenu()
        {
            string folder = EditorUtility.OpenFolderPanel("Kenney Mini Characters: pick the 'Models/FBX format' folder", string.Empty, string.Empty);
            if (string.IsNullOrEmpty(folder)) return;

            List<string> written = Bake(folder);
            Debug.Log($"Baked {written.Count} guest sprite sheets into {OutputFolder}.");
        }

        /// <summary>Bakes every character in <see cref="Characters"/> from an unzipped "FBX format" folder.</summary>
        /// <returns>Project paths of the written sheets.</returns>
        public static List<string> Bake(string fbxFolder, string clipName = WalkClip)
        {
            string colormap = Path.Combine(fbxFolder, "Textures", "colormap.png");
            if (!File.Exists(colormap)) throw new FileNotFoundException("The pack's Textures/colormap.png is missing.", colormap);

            var written = new List<string>();
            try
            {
                Directory.CreateDirectory(TempFolder);
                File.Copy(colormap, Path.Combine(TempFolder, "colormap.png"), true);
                foreach (string character in Characters)
                {
                    string source = Path.Combine(fbxFolder, character + ".fbx");
                    if (!File.Exists(source)) throw new FileNotFoundException($"{character}.fbx is missing.", source);
                    File.Copy(source, Path.Combine(TempFolder, character + ".fbx"), true);
                }

                AssetDatabase.Refresh();

                foreach (string character in Characters)
                {
                    var importer = (ModelImporter)AssetImporter.GetAtPath($"{TempFolder}/{character}.fbx");
                    importer.animationType = ModelImporterAnimationType.Generic;
                    importer.importAnimation = true;
                    importer.materialImportMode = ModelImporterMaterialImportMode.None;
                    importer.SaveAndReimport();
                }

                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TempFolder}/colormap.png");
                Directory.CreateDirectory(OutputFolder);
                foreach (string character in Characters)
                {
                    var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{TempFolder}/{character}.fbx");
                    AnimationClip walk = FindClip($"{TempFolder}/{character}.fbx", clipName);
                    string output = $"{OutputFolder}/Guest_{SheetName(character)}_Walk.png";
                    Vector2 pivot = BakeSheet(model, walk, texture, output);
                    written.Add(output);
                    pivots[output] = pivot;
                }
            }
            finally
            {
                AssetDatabase.DeleteAsset(TempFolder);
                if (Directory.Exists(TempFolder)) Directory.Delete(TempFolder, true);
            }

            AssetDatabase.Refresh();
            foreach (string sheet in written) Slice(sheet, pivots[sheet]);
            pivots.Clear();
            return written;
        }

        private static readonly Dictionary<string, Vector2> pivots = new Dictionary<string, Vector2>();

        /// <summary>"character-female-a" → "FemaleA".</summary>
        public static string SheetName(string character)
        {
            string[] parts = character.Replace("character-", string.Empty).Split('-');
            string name = string.Empty;
            foreach (string part in parts) name += char.ToUpperInvariant(part[0]) + part.Substring(1);
            return name;
        }

        private static AnimationClip FindClip(string modelPath, string clipName)
        {
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(modelPath))
            {
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__", StringComparison.Ordinal) && clip.name == clipName)
                {
                    return clip;
                }
            }

            throw new InvalidOperationException($"{modelPath} has no '{clipName}' animation.");
        }

        /// <summary>Renders the walk cycle in every row direction into one sheet. Returns the feet pivot (0–1).</summary>
        private static Vector2 BakeSheet(GameObject model, AnimationClip clip, Texture texture, string outputPath)
        {
            Shader shader = Shader.Find(ModelSpriteRenderer.ShaderName);
            if (shader == null) throw new InvalidOperationException($"Shader {ModelSpriteRenderer.ShaderName} not found.");

            Scene scene = EditorSceneManager.NewPreviewScene();
            var material = new Material(shader) { mainTexture = texture };
            var meshes = new List<Mesh>();
            RenderTexture target = null;
            try
            {
                var instance = (GameObject)UnityEngine.Object.Instantiate(model);
                SceneManager.MoveGameObjectToScene(instance, scene);

                // Skinned meshes are baked into plain meshes per frame, so each render shows exactly the sampled pose.
                var skinned = instance.GetComponentsInChildren<SkinnedMeshRenderer>();
                var proxies = new List<(SkinnedMeshRenderer Source, Transform Proxy, Mesh Mesh)>();
                foreach (SkinnedMeshRenderer renderer in skinned)
                {
                    var proxy = new GameObject(renderer.name + "_Baked");
                    SceneManager.MoveGameObjectToScene(proxy, scene);
                    var mesh = new Mesh();
                    meshes.Add(mesh);
                    proxy.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var meshRenderer = proxy.AddComponent<MeshRenderer>();
                    var replaced = new Material[renderer.sharedMaterials.Length];
                    for (int i = 0; i < replaced.Length; i++) replaced[i] = material;
                    meshRenderer.sharedMaterials = replaced;
                    renderer.enabled = false;
                    proxies.Add((renderer, proxy.transform, mesh));
                }

                foreach (MeshRenderer renderer in instance.GetComponentsInChildren<MeshRenderer>())
                {
                    var replaced = new Material[renderer.sharedMaterials.Length];
                    for (int i = 0; i < replaced.Length; i++) replaced[i] = material;
                    renderer.sharedMaterials = replaced;
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
                Transform view = cameraObject.transform;
                view.rotation = Quaternion.Euler(ModelSpriteRenderer.CameraEuler);

                // Pass 1: one frame box for every pose, so the character doesn't jitter between frames.
                float minRight = float.MaxValue, maxRight = float.MinValue, minUp = float.MaxValue, maxUp = float.MinValue;
                float maxDepth = 0f;
                for (int row = 0; row < Rows.Length; row++)
                {
                    for (int frame = 0; frame < Frames; frame++)
                    {
                        Pose(instance, clip, Rows[row].Yaw, frame, proxies);
                        foreach (Vector3 point in Points(instance, proxies))
                        {
                            float right = Vector3.Dot(point, view.right);
                            float up = Vector3.Dot(point, view.up);
                            minRight = Mathf.Min(minRight, right);
                            maxRight = Mathf.Max(maxRight, right);
                            minUp = Mathf.Min(minUp, up);
                            maxUp = Mathf.Max(maxUp, up);
                            maxDepth = Mathf.Max(maxDepth, point.magnitude);
                        }
                    }
                }

                // Keep the feet (the model origin) centred horizontally so flipping for "walk left" doesn't shift it.
                float halfWidth = Mathf.Max(Mathf.Abs(minRight), Mathf.Abs(maxRight));
                float half = Mathf.Max(halfWidth, (maxUp - minUp) * 0.5f) * 1.06f;
                float centreUp = (minUp + maxUp) * 0.5f;
                camera.orthographicSize = half;
                float distance = maxDepth * 4f + 1f;
                view.position = view.up * centreUp - view.forward * distance;
                camera.nearClipPlane = 0.01f;
                camera.farClipPlane = distance * 2f + maxDepth * 2f;
                var pivot = new Vector2(0.5f, 0.5f - centreUp / (2f * half));

                // Pass 2: render each pose into its cell.
                int pixels = FrameSize * Supersample;
                target = RenderTexture.GetTemporary(pixels, pixels, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                var sheet = new Texture2D(FrameSize * Frames, FrameSize * Rows.Length, TextureFormat.RGBA32, false);
                sheet.SetPixels32(new Color32[sheet.width * sheet.height]);
                for (int row = 0; row < Rows.Length; row++)
                {
                    for (int frame = 0; frame < Frames; frame++)
                    {
                        Pose(instance, clip, Rows[row].Yaw, frame, proxies);
                        ModelSpriteRenderer.Submit(camera, target);
                        Color32[] cell = ModelSpriteRenderer.Downsample(ModelSpriteRenderer.ReadBack(target, pixels), pixels, Supersample);
                        sheet.SetPixels32(frame * FrameSize, (Rows.Length - 1 - row) * FrameSize, FrameSize, FrameSize, cell);
                    }
                }

                sheet.Apply();
                File.WriteAllBytes(outputPath, sheet.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(sheet);
                return pivot;
            }
            finally
            {
                if (target != null) RenderTexture.ReleaseTemporary(target);
                foreach (Mesh mesh in meshes) UnityEngine.Object.DestroyImmediate(mesh);
                UnityEngine.Object.DestroyImmediate(material);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static void Pose(GameObject instance, AnimationClip clip, float yaw, int frame, List<(SkinnedMeshRenderer Source, Transform Proxy, Mesh Mesh)> proxies)
        {
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, yaw, 0f));
            clip.SampleAnimation(instance, clip.length * frame / Frames);
            // Root motion would walk the model out of the frame: keep it on the spot.
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, yaw, 0f));

            foreach ((SkinnedMeshRenderer source, Transform proxy, Mesh mesh) in proxies)
            {
                source.BakeMesh(mesh, true);
                proxy.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            }
        }

        private static IEnumerable<Vector3> Points(GameObject instance, List<(SkinnedMeshRenderer Source, Transform Proxy, Mesh Mesh)> proxies)
        {
            foreach ((SkinnedMeshRenderer _, Transform proxy, Mesh mesh) in proxies)
            {
                Matrix4x4 matrix = Matrix4x4.TRS(proxy.position, proxy.rotation, Vector3.one);
                foreach (Vector3 vertex in mesh.vertices) yield return matrix.MultiplyPoint3x4(vertex);
            }

            foreach (MeshFilter filter in instance.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null) continue;
                foreach (Vector3 vertex in filter.sharedMesh.vertices) yield return filter.transform.TransformPoint(vertex);
            }
        }

        /// <summary>Imports a sheet as sprites: Guest_X_Walk_Front_0 … Guest_X_Walk_Back_7, pivot at the feet.</summary>
        private static void Slice(string path, Vector2 pivot)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            ISpriteEditorDataProvider provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();

            string baseName = Path.GetFileNameWithoutExtension(path);
            var rects = new List<SpriteRect>();
            var nameIds = new List<SpriteNameFileIdPair>();
            for (int row = 0; row < Rows.Length; row++)
            {
                for (int frame = 0; frame < Frames; frame++)
                {
                    var rect = new SpriteRect
                    {
                        name = $"{baseName}_{Rows[row].Name}_{frame}",
                        rect = new Rect(frame * FrameSize, (Rows.Length - 1 - row) * FrameSize, FrameSize, FrameSize),
                        alignment = SpriteAlignment.Custom,
                        pivot = pivot,
                        spriteID = GUID.Generate(),
                    };
                    rects.Add(rect);
                    nameIds.Add(new SpriteNameFileIdPair(rect.name, rect.spriteID));
                }
            }

            provider.SetSpriteRects(rects.ToArray());
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>()?.SetNameFileIdPairs(nameIds);
            provider.Apply();
            importer.SaveAndReimport();
        }
    }
}
