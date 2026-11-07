using System;
using UnityEngine;

namespace ResortEmpire.Core
{
    /// <summary>
    /// Energy: an optional boost, never a gate (Build doc §4.6, Energy spec).
    /// Prototype uses Mode A only: Speed Up temporarily shortens one building's service time.
    /// No regeneration over time; earned from waves and rewarded video.
    /// </summary>
    [Serializable]
    public sealed class EnergySettings
    {
        [Tooltip("Energy the player starts with. Not in the Build doc; placeholder.")]
        [Min(0)] public int startingEnergy = 0;

        [Tooltip("Cost of one Speed Up (doc: ⚡3).")]
        [Min(0)] public int boostCost = 3;

        [Tooltip("energy_speed_reduction_pct: how much a boost shortens service time (0.5 = half). TUNING.")]
        [Range(0f, 0.95f)] public float speedReductionPct = 0.5f;

        [Tooltip("energy_speed_duration_s: how long a boost lasts, in seconds of wave time.")]
        [Min(0f)] public float boostDuration = 15f;

        [Tooltip("Boosts allowed per building per wave (doc: 1).")]
        [Min(0)] public int maxBoostsPerBuildingPerWave = 1;

        [Tooltip("Energy dropped by a finished wave for 1★, 2★, 3★, 4★, 5★, Perfect (6 values). Doc lists [0,1,2,3,5]; Perfect assumed equal to 5★. TUNING.")]
        public int[] dropByRating = { 0, 1, 2, 3, 5, 5 };

        [Tooltip("Energy granted by one voluntary rewarded video. Not in the Build doc; placeholder (one boost).")]
        [Min(0)] public int rewardedVideoEnergy = 3;
    }
}
