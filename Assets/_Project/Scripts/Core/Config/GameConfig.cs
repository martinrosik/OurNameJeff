using System.Collections.Generic;
using UnityEngine;

namespace ResortEmpire.Core
{
    /// <summary>
    /// Every tunable game rule in one asset (<c>Assets/_Project/Data/Config_Prototype.asset</c>).
    /// Code never hard-codes these numbers. Treat it as read-only at runtime.
    /// New tickets add their settings here (see docs/tasks).
    /// </summary>
    [CreateAssetMenu(fileName = "Config_Prototype", menuName = "Resort Empire/Game Config", order = 0)]
    public sealed class GameConfig : ScriptableObject
    {
        public WaveSettings wave = new WaveSettings();

        [Tooltip("One entry per need.")]
        public List<NeedDefinition> needs = new List<NeedDefinition>();

        [Tooltip("Buildings on the island at the start. Each serves exactly one need.")]
        public List<BuildingDefinition> buildings = new List<BuildingDefinition>();

        [Tooltip("Scripted waves in order (prototype: 8).")]
        public List<WaveDefinition> waves = new List<WaveDefinition>();

        public NeedDefinition GetNeed(NeedId id)
        {
            foreach (NeedDefinition need in needs)
            {
                if (need != null && need.need == id) return need;
            }

            return null;
        }

        public BuildingDefinition GetBuilding(BuildingId id)
        {
            foreach (BuildingDefinition building in buildings)
            {
                if (building != null && building.building == id) return building;
            }

            return null;
        }

        public BuildingDefinition GetBuildingForNeed(NeedId need)
        {
            foreach (BuildingDefinition building in buildings)
            {
                if (building != null && building.need == need) return building;
            }

            return null;
        }

        /// <summary>Errors that make the config unusable. Empty list = valid.</summary>
        public List<string> Validate() => GameConfigValidator.Validate(this);
    }
}
