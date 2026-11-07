using System;
using UnityEngine;

namespace ResortEmpire.Core
{
    /// <summary>
    /// Wave timing and service speed (Build doc §4.8 parameter table).
    /// Defaults are the Build doc values; tune them in the config asset, not here.
    /// </summary>
    [Serializable]
    public sealed class WaveSettings
    {
        [Tooltip("wave_timer: arrival phase in seconds. Closes ARRIVALS, never the wave (WR3). TUNING 45–180.")]
        [Min(1f)] public float waveTimer = 90f;

        [Tooltip("WAVE_INCOMING: boat arrival before the first guest, in seconds (doc: ~2–4 s).")]
        [Min(0f)] public float incomingDuration = 3f;

        [Tooltip("service_time: seconds to serve one unit at full speed. The most sensitive number; tune together with utilizationEmpty. TUNING 4–24.")]
        [Min(0.1f)] public float serviceTime = 12f;

        [Tooltip("utilization_staffed: service speed with a Worker (fixed 1.0 in the prototype).")]
        [Min(0.01f)] public float utilizationStaffed = 1f;

        [Tooltip("utilization_empty: service speed without a Worker. Must stay above 0 (WR2). TUNING 0.1–0.6.")]
        [Min(0.01f)] public float utilizationEmpty = 0.33f;

        [Tooltip("bottleneck_threshold: waiting units (seated ones don't count) above which a need can show the warning. TUNING 1–10.")]
        [Min(0)] public int bottleneckThreshold = 4;

        [Tooltip("bottleneck_hysteresis_s: the queue must have grown over this many seconds before the warning shows (anti-flicker). TUNING 1–10.")]
        [Min(0f)] public float bottleneckHysteresis = 3f;
    }
}
