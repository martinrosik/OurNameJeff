using System.Collections.Generic;
using System.Linq;
using ResortEmpire.Core;
using ResortEmpire.Game;
using ResortEmpire.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ResortEmpire.Editor
{
    /// <summary>
    /// Builds Tutorial.unity as a copy of Game.unity (the same island, camera and UI) plus a "Tutorial" root with the
    /// <see cref="TutorialController"/>. Game.unity is only read. "Rebuild Greybox Scene" calls this too, so the two
    /// islands never drift apart; re-run it by hand after editing Game.unity some other way. Also creates
    /// Config_Tutorial.asset and the tutorial sprites when they're missing, and puts both scenes in the Build Profile.
    /// </summary>
    public static class TutorialSceneBuilder
    {
        public const string GameScenePath = "Assets/_Project/Scenes/Game.unity";
        public const string TutorialScenePath = "Assets/_Project/Scenes/Tutorial.unity";

        // The host in the bubbles: a guest character facing the player (see GuestSpriteBaker).
        private const string HostSheet = "Guest_";

        [MenuItem("Resort Empire/Greybox/Rebuild Tutorial Scene", priority = 302)]
        public static void Build()
        {
            if (GreyboxArtGenerator.TutorialArrow == null || GreyboxArtGenerator.TutorialFrame == null)
            {
                GreyboxArtGenerator.GenerateTutorialSprites();
            }

            CreateTutorialConfigIfMissing();

            // Copy the Game scene as it is; opening it doesn't change it.
            Scene game = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            if (!EditorSceneManager.SaveScene(game, TutorialScenePath, saveAsCopy: true))
            {
                Debug.LogError($"Couldn't write {TutorialScenePath}.");
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(TutorialScenePath, OpenSceneMode.Single);

            // Load assets only after opening the scene (OpenScene unloads unused assets; see GreyboxSceneBuilder).
            var tutorialConfig = AssetDatabase.LoadAssetAtPath<TutorialConfig>(TutorialConfigDefaults.AssetPath);
            var controller = Object.FindAnyObjectByType<GameController>();
            var ui = Object.FindAnyObjectByType<GreyboxUI>();
            if (controller == null || ui == null)
            {
                Debug.LogError("Game.unity has no GameController or GreyboxUI. Run Resort Empire/Greybox/Rebuild Greybox Scene first.");
                return;
            }

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == "Tutorial") Object.DestroyImmediate(root);
            }

            var tutorialObject = new GameObject("Tutorial");
            var tutorial = tutorialObject.AddComponent<TutorialController>();
            var so = new SerializedObject(tutorial);
            so.FindProperty("controller").objectReferenceValue = controller;
            so.FindProperty("ui").objectReferenceValue = ui;
            so.FindProperty("tutorial").objectReferenceValue = tutorialConfig;
            so.FindProperty("arrowSprite").objectReferenceValue = GreyboxArtGenerator.TutorialArrow;
            so.FindProperty("frameSprite").objectReferenceValue = GreyboxArtGenerator.TutorialFrame;
            so.FindProperty("hostSprite").objectReferenceValue = LoadHostSprite();
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AddScenesToBuildProfile();
            Debug.Log("Tutorial scene rebuilt from Game.unity.");
        }

        /// <summary>Game.unity first (the app starts there), then Tutorial.unity.</summary>
        public static void AddScenesToBuildProfile()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            foreach (string path in new[] { GameScenePath, TutorialScenePath })
            {
                if (scenes.All(s => s.path != path)) scenes.Add(new EditorBuildSettingsScene(path, true));
            }

            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets(); // writes ProjectSettings/EditorBuildSettings.asset
        }

        private static void CreateTutorialConfigIfMissing()
        {
            if (AssetDatabase.LoadAssetAtPath<TutorialConfig>(TutorialConfigDefaults.AssetPath) != null) return;

            AssetDatabase.CreateAsset(TutorialConfigDefaults.Create(), TutorialConfigDefaults.AssetPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"Created {TutorialConfigDefaults.AssetPath}.");
        }

        /// <summary>The first front-facing frame of the first guest sheet, or null without baked guests.</summary>
        private static Sprite LoadHostSprite()
        {
            foreach (string guid in AssetDatabase.FindAssets($"{HostSheet} t:Texture2D", new[] { GuestSpriteBaker.OutputFolder }).OrderBy(g => AssetDatabase.GUIDToAssetPath(g)))
            {
                List<Sprite> front = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid))
                    .OfType<Sprite>()
                    .Where(s => s.name.Contains("_Front_"))
                    .OrderBy(s => s.name)
                    .ToList();
                if (front.Count > 0) return front[0];
            }

            Debug.LogWarning($"No guest sprites in {GuestSpriteBaker.OutputFolder} for the tutorial host. Run Resort Empire/Art/Bake Guest Characters.");
            return null;
        }
    }
}
