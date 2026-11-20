using UnityEngine;
using UnityEngine.SceneManagement;

namespace ResortEmpire.Game
{
    /// <summary>The game's scenes and the switch between them. Both are in the Build Profile's scene list.</summary>
    public static class GameScenes
    {
        public const string Game = "Game";
        public const string Tutorial = "Tutorial";

        /// <summary>Opens the island with the real waves, from wave 1.</summary>
        public static void LoadGame() => Load(Game);

        /// <summary>Opens the tutorial and its practice wave.</summary>
        public static void LoadTutorial() => Load(Tutorial);

        private static void Load(string scene)
        {
            Time.timeScale = 1f; // the tutorial pauses the wave with timeScale; never carry that over
            SceneManager.LoadScene(scene, LoadSceneMode.Single);
        }
    }
}
