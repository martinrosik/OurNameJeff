using System;
using UnityEngine;

namespace ResortEmpire.Core
{
    /// <summary>
    /// Coins and Resort Value (Build doc §4.8 "Príjem z vlny").
    /// Coins = units served in the wave × coinsPerUnit × coinsRatingMultipliers[rating].
    /// </summary>
    [Serializable]
    public sealed class EconomySettings
    {
        [Tooltip("Coins the player starts with. Not in the Build doc; placeholder.")]
        [Min(0)] public int startingCoins = 0;

        [Tooltip("coins_per_unit: Coins per served need unit, derived back from prices (doc: 16). Change it whenever prices change. TUNING 5–40.")]
        [Min(0)] public int coinsPerUnit = 16;

        [Tooltip("coins_rating_mult: multiplier for 1★, 2★, 3★, 4★, 5★, Perfect (6 values). TUNING.")]
        public float[] coinsRatingMultipliers = { 0.4f, 0.6f, 0.8f, 1.0f, 1.2f, 1.3f };

        [Tooltip("Resort Value at the start. Not in the Build doc; placeholder.")]
        [Min(0)] public int startingResortValue = 100;

        [Tooltip("Resort Value gained per finished wave by rating 1★ … Perfect (6 values). Not in the Build doc; placeholder.")]
        public int[] resortValueByRating = { 5, 10, 15, 20, 25, 30 };
    }
}
