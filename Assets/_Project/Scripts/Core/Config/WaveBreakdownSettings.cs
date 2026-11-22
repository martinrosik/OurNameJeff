using System;
using UnityEngine;

namespace ResortEmpire.Core
{
    /// <summary>
    /// Wave Complete breakdown "What went well / Needs improvement" (UI doc S4, Build doc AC6): which needs went great and
    /// what the others need. Not in the Build doc: placeholder numbers to tune in the config asset.
    /// </summary>
    [Serializable]
    public sealed class WaveBreakdownSettings
    {
        [Tooltip("A need went great when its own satisfaction (same formula as Wave Satisfaction, only its guests) reaches this %. Below it, the need is listed under Needs improvement. Placeholder: the 4-star threshold.")]
        [Range(0f, 100f)] public float wentGreatSatisfaction = 80f;

        [Tooltip("A need that needs improvement asks for a Worker when its building served without one (slowly) for more than this share of the wave (0.5 = half). Otherwise it asks for more capacity.")]
        [Range(0f, 1f)] public float noWorkerShare = 0.5f;
    }
}
