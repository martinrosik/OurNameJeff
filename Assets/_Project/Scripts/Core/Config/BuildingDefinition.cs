using System;
using System.Collections.Generic;
using UnityEngine;

namespace ResortEmpire.Core
{
    /// <summary>
    /// One building on the island: the need it serves, whether a Worker stands there at the start, and its
    /// capacity per level (Build doc §4.5, §4.8). The prototype starts with Reception, Restaurant and Beach Bar.
    /// </summary>
    [Serializable]
    public sealed class BuildingDefinition
    {
        public BuildingId building;

        [Tooltip("In-game name, English (e.g. \"Beach Bar\").")]
        public string displayName;

        [Tooltip("The need this building serves.")]
        public NeedId need;

        [Tooltip("Starts with a Worker. Workers are always one fewer than buildings in the prototype.")]
        public bool staffedAtStart;

        [Tooltip("Level 1 first. capacity = guests served AT THE SAME TIME. Prototype: 3 → 5 → 7 seats (upgrades come later).")]
        public List<BuildingLevel> levels = new List<BuildingLevel>();

        public int MaxLevel => levels.Count;

        /// <summary>Level data, 1-based (level 1 = <c>levels[0]</c>).</summary>
        public BuildingLevel GetLevel(int level) => levels[Mathf.Clamp(level, 1, levels.Count) - 1];
    }

    [Serializable]
    public sealed class BuildingLevel
    {
        [Tooltip("capacity: guests served at the same time (not per second). Shown as seat squares on the building.")]
        [Min(1)] public int seats = 3;
    }
}
