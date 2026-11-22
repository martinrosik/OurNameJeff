using UnityEngine;
using UnityEngine.SceneManagement;

namespace ResortEmpire.Game
{
    /// <summary>
    /// The first launch of a build opens the tutorial instead of the Game scene, until the player finishes or skips it
    /// (<see cref="TutorialProgress"/>). In the Editor, Play starts whichever scene is open, so teammates and the
    /// PlayMode tests aren't sent to the tutorial; open Tutorial.unity to play it there.
    /// </summary>
    internal static class FirstLaunchTutorial
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void OpenTutorialOnFirstLaunch()
        {
            if (!ShouldOpenTutorial(Application.isEditor, TutorialProgress.IsDone, SceneManager.GetActiveScene().name)) return;
            GameScenes.LoadTutorial();
        }

        internal static bool ShouldOpenTutorial(bool isEditor, bool isDone, string firstScene) =>
            !isEditor && !isDone && firstScene == GameScenes.Game;
    }
}
