using System;
using UnityEngine;

namespace ResortEmpire.Core
{
    /// <summary>One guest need and how much its waiting weighs in Wave Satisfaction.</summary>
    [Serializable]
    public sealed class NeedDefinition
    {
        public NeedId need;

        [Tooltip("In-game name, English (e.g. \"Ocean View\").")]
        public string displayName;

        [Tooltip("Weight in Wave Satisfaction. 1.0 for volume needs; higher for Ocean View (ocean_view_satisfaction_weight, doc e.g. 1.5, TUNING 1–3).")]
        [Min(0.01f)] public float satisfactionWeight = 1f;
    }
}
