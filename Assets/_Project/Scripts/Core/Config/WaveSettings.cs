using System;
using UnityEngine;

namespace ResortEmpire.Core
{
    /// <summary>
    /// Wave timing (Build doc §4.8 parameter table). Defaults are the Build doc values; tune them in the
    /// config asset, not here.
    /// <para>
    /// Planned additions (see docs/tasks): <c>serviceTime</c> 12 s and
    /// <c>utilizationStaffed</c> 1.0 / <c>utilizationEmpty</c> 0.33 (OUR-46, OUR-47),
    /// <c>bottleneckThreshold</c> 4 and <c>bottleneckHysteresis</c> 3 s (OUR-38, OUR-39).
    /// </para>
    /// </summary>
    [Serializable]
    public sealed class WaveSettings
    {
        [Tooltip("WAVE_INCOMING: the guest boat sails in after Start Wave, in seconds (doc: ~2–4 s).")]
        [Min(0f)] public float incomingDuration = 3f;

        [Tooltip("OUR-34: arrival phase in seconds (doc: 90 s, TUNING 45–180). Each need's demand arrives evenly over it; unit k arrives at (k + 1) × waveTimer ÷ demand. It closes arrivals, never the wave (WR3).")]
        [Min(1f)] public float waveTimer = 90f;
    }
}
