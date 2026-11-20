using System;
using UnityEngine;

namespace ResortEmpire.Core
{
    /// <summary>
    /// Wave timing (Build doc §4.8 parameter table). Defaults are the Build doc values; tune them in the
    /// config asset, not here.
    /// <para>
    /// Service (OUR-46, OUR-47) uses the Build doc numbers (§4.4, §4.8): 12 s per guest per seat with a Worker,
    /// about 36 s without (serviceTime 12 ÷ utilization 1.0 / 0.33). The Jira titles said 0.33 s / 1 s; see
    /// docs/design-conflicts.md row 10.
    /// </para>
    /// </summary>
    [Serializable]
    public sealed class WaveSettings
    {
        [Tooltip("WAVE_INCOMING: the guest boat sails in after Start Wave, in seconds (doc: ~2–4 s).")]
        [Min(0f)] public float incomingDuration = 3f;

        [Tooltip("OUR-34: arrival phase in seconds (doc: 90 s, TUNING 45–180). Each need's demand arrives evenly over it; unit k arrives at (k + 1) × waveTimer ÷ demand. It closes arrivals, never the wave (WR3).")]
        [Min(1f)] public float waveTimer = 90f;

        [Tooltip("OUR-46: seconds one seat needs to serve one guest at utilization 1.0 (Build doc: 12 s, TUNING 4–24; with utilization_empty the most sensitive number, tune them together). Service is progress at utilization ÷ serviceTime per second.")]
        [Min(0.01f)] public float serviceTime = 12f;

        [Tooltip("OUR-46: service speed of a building with a Worker (doc: 1.0).")]
        [Min(0.01f)] public float utilizationStaffed = 1f;

        [Tooltip("OUR-47: service speed of a building without a Worker (doc: 0.33, TUNING 0.1–0.6). With serviceTime 12 a guest takes about 36 s. WR2: must stay above 0, or the wave could never end.")]
        [Min(0.01f)] public float utilizationEmpty = 0.33f;

        [Tooltip("OUR-38: a need turns BOTTLENECK when more than this many guests wait in its queue (and the queue is growing). Seated guests don't count (doc: 4, TUNING 1–10).")]
        [Min(1)] public int bottleneckThreshold = 4;

        [Tooltip("OUR-38 / OUR-39: seconds the queue must have grown before the warning shows or comes back, so it doesn't flicker (doc: 3 s, TUNING 1–10).")]
        [Min(0.1f)] public float bottleneckHysteresis = 3f;

        [Tooltip("Wave difficulty: every wave is harder than the one before it. When the wave table's next wave isn't bigger in total for the needs on the island (waves 4 and 7 debut needs whose buildings aren't built), the previous wave's demand and guests grow by this share instead, rounded up (0.125 = +12.5 %). Kept below the table's ~25 % growth, so the waves after it keep their table numbers.")]
        [Min(0.01f)] public float growthIfNotHarder = 0.125f;

        [Tooltip("Sudden rush (Build doc §4.6): extra guests at the building that has no Worker right then. Off in the tutorial.")]
        public bool suddenRushEnabled = true;

        [Tooltip("Sudden rush: the wave (1-based) that always has one (Build doc rush_wave: 2). Earlier waves never have one.")]
        [Min(1)] public int suddenRushWave = 2;

        [Tooltip("Sudden rush: chance (0–1) that a later wave has one too. Not in the Build doc (it only has wave 2); waves that debut a need never get one (§4.8). TUNING.")]
        [Range(0f, 1f)] public float suddenRushChance = 0.5f;

        [Tooltip("Sudden rush: wave time it starts, in seconds (Build doc rush_time_s: 45). A fixed moment inside the arrivals, before waveTimer.")]
        [Min(0f)] public float suddenRushTime = 45f;

        [Tooltip("Sudden rush: extra units of the building's need, added on top of the wave's demand (Build doc rush_units: 6).")]
        [Min(1)] public int suddenRushUnits = 6;

        [Tooltip("Sudden rush: seconds over which those extra units arrive (Build doc rush_duration_s: 10).")]
        [Min(0.1f)] public float suddenRushDuration = 10f;

        /// <summary>Seconds one seat takes to serve one guest, with or without a Worker.</summary>
        public double GetServiceSeconds(bool hasWorker) => serviceTime / (double)(hasWorker ? utilizationStaffed : utilizationEmpty);
    }
}
