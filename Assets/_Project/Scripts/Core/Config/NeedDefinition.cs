using System;
using UnityEngine;

namespace ResortEmpire.Core
{
    /// <summary>One guest need (Rooms, Drinks, Food, Relax, Ocean View).</summary>
    [Serializable]
    public sealed class NeedDefinition
    {
        public NeedId need;

        [Tooltip("In-game name, English (e.g. \"Ocean View\").")]
        public string displayName;
    }
}
