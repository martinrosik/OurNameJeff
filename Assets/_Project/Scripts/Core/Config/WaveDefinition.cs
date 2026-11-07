using System;
using UnityEngine;

namespace ResortEmpire.Core
{
    /// <summary>
    /// One scripted wave (Build doc §4.8 "Progresia vĺn"). Demand is in need units, not guests;
    /// a guest carries about 2 needs on average. Demand for a need whose building isn't bought yet
    /// never spawns (WR9), so the table can list it anyway.
    /// </summary>
    [Serializable]
    public sealed class WaveDefinition
    {
        [Tooltip("Guests shown arriving (visual only; the simulation works with need units).")]
        [Min(0)] public int guests;

        [Min(0)] public int rooms;
        [Min(0)] public int drinks;
        [Min(0)] public int food;
        [Min(0)] public int relax;
        [Min(0)] public int oceanView;

        [Tooltip("Optional Sudden rush mini-event (prototype: wave 2 only, never in a debut wave).")]
        public RushEventDefinition rush = new RushEventDefinition();

        [Tooltip("Designer note: what this wave is meant to test.")]
        [TextArea(1, 3)] public string intendedBeat;

        public int GetDemand(NeedId need)
        {
            switch (need)
            {
                case NeedId.Rooms: return rooms;
                case NeedId.Drinks: return drinks;
                case NeedId.Food: return food;
                case NeedId.Relax: return relax;
                case NeedId.OceanView: return oceanView;
                default: return 0;
            }
        }

        public void SetDemand(NeedId need, int units)
        {
            switch (need)
            {
                case NeedId.Rooms: rooms = units; break;
                case NeedId.Drinks: drinks = units; break;
                case NeedId.Food: food = units; break;
                case NeedId.Relax: relax = units; break;
                case NeedId.OceanView: oceanView = units; break;
            }
        }
    }

    /// <summary>
    /// Sudden rush (Build doc §4.6): extra demand on the building that is unstaffed at <see cref="startTime"/>,
    /// so moving a Worker always solves it. Doesn't pause the wave and isn't in the forecast.
    /// </summary>
    [Serializable]
    public sealed class RushEventDefinition
    {
        public bool enabled;

        [Tooltip("rush_time_s: seconds into the arrival phase (doc e.g. 45).")]
        [Min(0f)] public float startTime = 45f;

        [Tooltip("rush_units: extra need units (doc e.g. 6).")]
        [Min(0)] public int units = 6;

        [Tooltip("rush_duration_s: the extra units arrive evenly over this many seconds (doc e.g. 10).")]
        [Min(0f)] public float duration = 10f;
    }
}
