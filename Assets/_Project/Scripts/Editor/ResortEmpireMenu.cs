using UnityEditor;
using UnityEditor.SceneManagement;

namespace ResortEmpire.Editor
{
    /// <summary>
    /// Shortcuts under the "Resort Empire" menu in the Editor menu bar.
    /// </summary>
    internal static class ResortEmpireMenu
    {
        private const string GameScenePath = "Assets/_Project/Scenes/Game.unity";

        [MenuItem("Resort Empire/Open Game Scene", priority = 0)]
        private static void OpenGameScene()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene(GameScenePath);
            }
        }

        [MenuItem("Resort Empire/Open Tutorial Scene", priority = 1)]
        private static void OpenTutorialScene()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene(TutorialSceneBuilder.TutorialScenePath);
            }
        }

        /// <summary>Forgets that the tutorial was played on this machine (a build then opens it on launch again).</summary>
        [MenuItem("Resort Empire/Reset Tutorial Progress", priority = 2)]
        private static void ResetTutorialProgress()
        {
            ResortEmpire.Game.TutorialProgress.Reset();
            UnityEngine.Debug.Log("Tutorial progress reset: the tutorial counts as not played on this machine.");
        }
    }
}
