using UnityEngine;

namespace ResortEmpire.Game
{
    /// <summary>
    /// Whether the player has finished or skipped the tutorial on this device (PlayerPrefs). Until it's done, a build
    /// opens the tutorial on launch (<see cref="FirstLaunchTutorial"/>). Moves into the save file once save/load exists.
    /// </summary>
    public static class TutorialProgress
    {
        internal const string Key = "ResortEmpire.TutorialDone";

        public static bool IsDone => PlayerPrefs.GetInt(Key, 0) == 1;

        public static void MarkDone()
        {
            PlayerPrefs.SetInt(Key, 1);
            PlayerPrefs.Save();
        }

        /// <summary>Forgets it, so the next launch opens the tutorial again (Editor menu, tests).</summary>
        public static void Reset()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }
    }
}
