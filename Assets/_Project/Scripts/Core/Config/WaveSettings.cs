using System;
using UnityEngine;

namespace ResortEmpire.Core
{
    /// <summary>
    /// Wave timing (Build doc §4.8 parameter table). Defaults are the Build doc values; tune them in the
    /// config asset, not here.
    /// <para>
    /// Planned additions (see docs/tasks): <c>bottleneckThreshold</c> 4 and <c>bottleneckHysteresis</c> 3 s
    /// (OUR-38, OUR-39).
    /// </para>
    /// <para>
    /// Service (OUR-46, OUR-47) uses the Jira numbers: 0.33 s per guest with a Worker, 1 s without
    /// (serviceTime 0.33 ÷ utilization 1.0 / 0.33). The Build doc says serviceTime 12 s; see
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

        [Tooltip("OUR-46: seconds one seat needs to serve one guest at utilization 1.0 (Jira: 0.33 s; Build doc: 12 s, TUNING 4–24). Service is progress at utilization ÷ serviceTime per second.")]
        [Min(0.01f)] public float serviceTime = 0.33f;

        [Tooltip("OUR-46: service speed of a building with a Worker (doc: 1.0).")]
        [Min(0.01f)] public float utilizationStaffed = 1f;

        [Tooltip("OUR-47: service speed of a building without a Worker (doc: 0.33, TUNING 0.1–0.6). With serviceTime 0.33 a guest takes 1 s. WR2: must stay above 0, or the wave could never end.")]
        [Min(0.01f)] public float utilizationEmpty = 0.33f;

        /// <summary>Seconds one seat takes to serve one guest, with or without a Worker.</summary>
        public double GetServiceSeconds(bool hasWorker) => serviceTime / (double)(hasWorker ? utilizationStaffed : utilizationEmpty);
    }
}
