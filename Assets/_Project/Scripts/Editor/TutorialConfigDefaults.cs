using ResortEmpire.Core;
using UnityEngine;

namespace ResortEmpire.Editor
{
    /// <summary>
    /// The tutorial's practice wave, used to create <c>Assets/_Project/Data/Config_Tutorial.asset</c> and as a
    /// baseline in tests. Not from the Build doc: tune the asset, and keep <c>TutorialConfigTests</c> green.
    /// </summary>
    public static class TutorialConfigDefaults
    {
        public const string AssetPath = "Assets/_Project/Data/Config_Tutorial.asset";

        /// <summary>A new, unsaved tutorial config. Destroy it if you don't save it.</summary>
        public static TutorialConfig Create()
        {
            var config = ScriptableObject.CreateInstance<TutorialConfig>();
            config.name = "Config_Tutorial";
            config.arrivalSeconds = 40f;

            // 9 of each need over 40 s (0.225 a second). A building with a Worker serves 3 seats ÷ 12 s = 0.25 a second,
            // so it keeps up and shows "catching up" as soon as it gets a Worker. One without (~0.08 a second) falls
            // behind at about 36 s, just before arrivals close. More per second and a fixed building would still
            // show "queue growing"; fewer and nothing falls behind before 40 s.
            config.practiceWave = new WaveDefinition
            {
                guests = 14,
                rooms = 9,
                drinks = 9,
                food = 9,
                intendedBeat = "Tutorial: whichever building has no Worker falls behind at about 36 s, and a Worker fixes it at once.",
            };

            return config;
        }
    }
}
