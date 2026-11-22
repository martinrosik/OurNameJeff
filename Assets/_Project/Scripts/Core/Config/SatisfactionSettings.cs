using System;
using UnityEngine;

namespace ResortEmpire.Core
{
    /// <summary>
    /// Wave Satisfaction and rating (Build doc §4.4, §4.8 parameter table). Defaults are the Build doc values; tune
    /// them in the config asset, not here. All values are percent (0–100) or seconds of delay (arrival → served,
    /// minus a staffed service time).
    /// </summary>
    [Serializable]
    public sealed class SatisfactionSettings
    {
        [Tooltip("patience_grace: a guest delayed up to this many seconds counts 100 % (doc: 10 s, TUNING 3–30). Delay = arrival → served, minus a staffed service time, so a wait for a seat and slow service without a Worker both count.")]
        [Min(0f)] public float patienceGrace = 10f;

        [Tooltip("patience_floor_after: from this many seconds of delay a guest counts only unitFloor; between patienceGrace and this it falls linearly (doc: 30 s, TUNING 15–60).")]
        [Min(0f)] public float patienceFloorAfter = 30f;

        [Tooltip("unit_floor: the least one guest ever counts, in % (doc: 25 %, TUNING 0–40). WR1: unitFloor < waveFloor < the 3-star threshold.")]
        [Range(0f, 100f)] public float unitFloor = 25f;

        [Tooltip("wave_floor: the least a whole wave ever scores, in %, so the worst result is 1 star (doc: 40 %, TUNING 20–50).")]
        [Range(0f, 100f)] public float waveFloor = 40f;

        [Tooltip("rating_thresholds: Wave Satisfaction % needed for 1, 2, 3, 4, 5 stars and Perfect (doc: 40 / 55 / 70 / 80 / 90 / 100, TUNING).")]
        public float[] ratingThresholds = { 40f, 55f, 70f, 80f, 90f, 100f };

        [Tooltip("ocean_view_satisfaction_weight: how much an Ocean View guest counts against one of any other need (doc: 1.5, TUNING 1.0–3.0).")]
        [Min(0.01f)] public float oceanViewSatisfactionWeight = 1.5f;

        /// <summary>Weight of one guest of a need in the Wave Satisfaction average: 1.0, or more for Ocean View.</summary>
        public double GetNeedWeight(NeedId need) => need == NeedId.OceanView ? oceanViewSatisfactionWeight : 1.0;
    }
}
