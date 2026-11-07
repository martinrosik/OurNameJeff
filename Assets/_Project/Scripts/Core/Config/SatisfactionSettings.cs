using System;
using UnityEngine;

namespace ResortEmpire.Core
{
    /// <summary>
    /// How waiting turns into Wave Satisfaction and stars (Build doc §4.4).
    /// Hard rule WR1: unitFloor &lt; waveFloor &lt; the 3★ threshold.
    /// </summary>
    [Serializable]
    public sealed class SatisfactionSettings
    {
        [Tooltip("patience_grace: a unit seated within this many seconds of arriving counts 100 %. Service time never counts as waiting. TUNING 3–30.")]
        [Min(0f)] public float patienceGrace = 10f;

        [Tooltip("patience_floor_after: from this wait (seconds) on, a unit counts unitFloor. Between grace and this it falls linearly. TUNING 15–60.")]
        [Min(0f)] public float patienceFloorAfter = 30f;

        [Tooltip("unit_floor: the least a single unit can contribute, however long it waited. TUNING 0–0.40.")]
        [Range(0f, 1f)] public float unitFloor = 0.25f;

        [Tooltip("wave_floor: the worst possible Wave Satisfaction (= 1★). TUNING 0.20–0.50.")]
        [Range(0f, 1f)] public float waveFloor = 0.40f;

        [Tooltip("rating_thresholds: minimum Wave Satisfaction for 1★, 2★, 3★, 4★, 5★, Perfect (6 values, ascending). TUNING.")]
        public float[] ratingThresholds = { 0.40f, 0.55f, 0.70f, 0.80f, 0.90f, 1.00f };
    }
}
