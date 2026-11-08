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

        /// <summary>Seconds one seat takes to serve one guest, with or without a Worker.</summary>
        public double GetServiceSeconds(bool hasWorker) => serviceTime / (double)(hasWorker ? utilizationStaffed : utilizationEmpty);
    }
}
