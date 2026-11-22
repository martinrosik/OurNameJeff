using System;
using UnityEngine;

namespace ResortEmpire.Core
{
    /// <summary>
    /// Resort Value (GDD §7.6, Build doc §5.5): the resort's status number. It only grows and is never spent.
    /// In the prototype it grows after each wave by the wave's rating. A weak rating adds 0, never less.
    /// The Build doc gives no amounts yet: these defaults are placeholders to tune in the config asset.
    /// </summary>
    [Serializable]
    public sealed class ResortValueSettings
    {
        [Tooltip("Resort Value at the start of a new game.")]
        [Min(0)] public int startingValue = 0;

        [Tooltip("Resort Value added after a wave, by its rating: 1, 2, 3, 4, 5 stars and Perfect. 0 or more, never negative: Resort Value never drops. Placeholder numbers, not from the Build doc.")]
        public int[] gainPerRating = { 0, 0, 50, 100, 150, 200 };

        /// <summary>How much a wave with <paramref name="rating"/> adds. Never negative.</summary>
        public int GetGain(WaveRating rating)
        {
            int index = (int)rating - 1;
            if (gainPerRating == null || index < 0 || index >= gainPerRating.Length) return 0;
            return Math.Max(0, gainPerRating[index]);
        }
    }
}
