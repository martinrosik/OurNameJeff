using System.Collections.Generic;
using UnityEngine;

namespace ResortEmpire.Core
{
    /// <summary>
    /// Every tunable game rule in one asset (<c>Assets/_Project/Data/Config_Prototype.asset</c>).
    /// The economist tunes this asset; code never hard-codes these numbers.
    /// Treat it as read-only at runtime.
    /// </summary>
    [CreateAssetMenu(fileName = "Config_Prototype", menuName = "Resort Empire/Game Config", order = 0)]
    public sealed class GameConfig : ScriptableObject
    {
        public WaveSettings wave = new WaveSettings();
        public SatisfactionSettings satisfaction = new SatisfactionSettings();
        public EconomySettings economy = new EconomySettings();
        public EnergySettings energy = new EnergySettings();

        [Tooltip("One entry per need.")]
        public List<NeedDefinition> needs = new List<NeedDefinition>();

        [Tooltip("One entry per building. Each serves exactly one need.")]
        public List<BuildingDefinition> buildings = new List<BuildingDefinition>();

        [Tooltip("Scripted waves in order (prototype: 8).")]
        public List<WaveDefinition> waves = new List<WaveDefinition>();

        public NeedDefinition GetNeed(NeedId id)
        {
            foreach (NeedDefinition need in needs)
            {
                if (need != null && need.need == id)
                {
                    return need;
                }
            }

            return null;
        }

        public BuildingDefinition GetBuilding(BuildingId id)
        {
            foreach (BuildingDefinition building in buildings)
            {
                if (building != null && building.building == id)
                {
                    return building;
                }
            }

            return null;
        }

        public BuildingDefinition GetBuildingForNeed(NeedId need)
        {
            foreach (BuildingDefinition building in buildings)
            {
                if (building != null && building.need == need)
                {
                    return building;
                }
            }

            return null;
        }

        /// <summary>
        /// Errors that break hard rules or make the config unusable. Empty list = valid.
        /// </summary>
        public List<string> Validate() => GameConfigValidator.Validate(this);
    }
}
