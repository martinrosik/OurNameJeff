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
    }
}
