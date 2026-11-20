using System;
using System.Collections.Generic;
using UnityEngine;

namespace ResortEmpire.Core
{
    /// <summary>
    /// The tutorial's practice wave (<c>Assets/_Project/Data/Config_Tutorial.asset</c>). The tutorial plays the
    /// prototype config's rules with only this wave, so tuning the prototype config also tunes the tutorial.
    /// <para>
    /// Not in the Build doc. Every need arrives faster than a building without a Worker serves it, but slower than a
    /// building with one: the building without a Worker falls behind before arrivals close, wherever the player put
    /// the Workers, and a Worker fixes it at once (OUR-39 "catching up"). <c>TutorialConfigTests</c> checks both
    /// whenever the numbers change, here or in the prototype config.
    /// </para>
    /// </summary>
    [CreateAssetMenu(fileName = "Config_Tutorial", menuName = "Resort Empire/Tutorial Config", order = 1)]
    public sealed class TutorialConfig : ScriptableObject
    {
        [Tooltip("The one wave the tutorial plays. Each need must arrive faster than a building without a Worker serves it, and slower than one with a Worker (TutorialConfigTests checks it).")]
        public WaveDefinition practiceWave = new WaveDefinition();

        [Tooltip("Arrival phase of the practice wave in seconds. Shorter than a real wave's waveTimer, so the tutorial doesn't drag.")]
        [Min(1f)] public float arrivalSeconds = 40f;

        /// <summary>
        /// A runtime copy of <paramref name="baseConfig"/> that plays only the practice wave. The assets stay
        /// unchanged; destroy the copy when it's no longer used.
        /// </summary>
        public GameConfig CreatePracticeConfig(GameConfig baseConfig)
        {
            if (baseConfig == null) throw new ArgumentNullException(nameof(baseConfig));

            GameConfig practice = Instantiate(baseConfig);
            practice.name = $"{baseConfig.name} (practice wave)";
            practice.wave.waveTimer = arrivalSeconds;
            practice.wave.miniEventEnabled = false; // the practice wave teaches one problem at a time
            practice.waves = new List<WaveDefinition> { Copy(practiceWave) };
            return practice;
        }

        private static WaveDefinition Copy(WaveDefinition wave) =>
            new WaveDefinition
            {
                guests = wave.guests,
                rooms = wave.rooms,
                drinks = wave.drinks,
                food = wave.food,
                relax = wave.relax,
                oceanView = wave.oceanView,
                intendedBeat = wave.intendedBeat,
            };
    }
}
