using System;
using UnityEngine;

namespace ResortEmpire.Core
{
    /// <summary>
    /// One scripted wave (Build doc §4.8 "Progresia vĺn"). Demand is in need units, not guests; a guest carries
    /// about 2 needs on average. Needs without a building on the island are left out of the forecast.
    /// The table feeds the forecast (OUR-33), and OUR-34 spawns these units over the arrival phase.
    /// </summary>
    [Serializable]
    public sealed class WaveDefinition
    {
        [Tooltip("Guests shown arriving (visual count).")]
        [Min(0)] public int guests;

        [Min(0)] public int rooms;
        [Min(0)] public int drinks;
        [Min(0)] public int food;
        [Min(0)] public int relax;
        [Min(0)] public int oceanView;

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
    }
}
