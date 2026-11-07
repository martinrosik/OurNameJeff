using System;
using System.Collections.Generic;
using UnityEngine;

namespace ResortEmpire.Core
{
    /// <summary>
    /// One building: which need it serves, its upgrade levels and how it unlocks (Build doc §4.5, §4.8).
    /// Unlocking is two-step: the gate makes it available, then the player buys it with Coins.
    /// </summary>
    [Serializable]
    public sealed class BuildingDefinition
    {
        public BuildingId building;

        [Tooltip("In-game name, English (e.g. \"Beach Bar\").")]
        public string displayName;

        [Tooltip("The need this building serves.")]
        public NeedId need;

        [Tooltip("Owned from the first session.")]
        public bool unlockedAtStart;

        [Tooltip("Starts with a Worker. Workers are always one fewer than owned buildings in the prototype.")]
        public bool staffedAtStart;

        [Tooltip("Gate: becomes available after this many completed waves (Pool: 3, Bungalows: 6). MVP switches this to a Reputation threshold.")]
        [Min(0)] public int unlockAfterWave;

        [Tooltip("Coins to buy it once available (doc e.g. Pool 1,200, Bungalows 2,500).")]
        [Min(0)] public int unlockCost;

        [Tooltip("Resort Value gained when bought. Not in the Build doc; placeholder.")]
        [Min(0)] public int unlockResortValue;

        [Tooltip("Level 1 first. Prototype: seats 3 → 5 → 7, costs –, 500, 1,200 (e.g.). Upgrades change seats only.")]
        public List<BuildingLevel> levels = new List<BuildingLevel>();

        public int MaxLevel => levels.Count;

        /// <summary>Level data, 1-based (level 1 = <c>levels[0]</c>).</summary>
        public BuildingLevel GetLevel(int level) => levels[Mathf.Clamp(level, 1, levels.Count) - 1];
    }

    [Serializable]
    public sealed class BuildingLevel
    {
        [Tooltip("capacity: guests served AT THE SAME TIME (not per second). Must be readable on the building art.")]
        [Min(1)] public int seats = 3;

        [Tooltip("Coins to upgrade TO this level. Ignored for level 1.")]
        [Min(0)] public int cost;

        [Tooltip("Resort Value gained when upgrading TO this level. Not in the Build doc; placeholder.")]
        [Min(0)] public int resortValue;
    }
}
