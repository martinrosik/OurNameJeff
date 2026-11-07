using System.Collections.Generic;
using ResortEmpire.Core;
using UnityEngine;

namespace ResortEmpire.Editor
{
    /// <summary>
    /// The Build doc's prototype numbers (§4.8, as of 2026-09-29), used to create
    /// <c>Assets/_Project/Data/Config_Prototype.asset</c> and as a fixed baseline in tests.
    /// Tune the asset, not this file. All values are "e.g."/TUNING in the doc.
    /// </summary>
    public static class PrototypeConfigDefaults
    {
        public const string AssetPath = "Assets/_Project/Data/Config_Prototype.asset";

        /// <summary>A new, unsaved config with the Build doc values. Destroy it if you don't save it.</summary>
        public static GameConfig Create()
        {
            var config = ScriptableObject.CreateInstance<GameConfig>();
            config.name = "Config_Prototype";

            // Scalar settings: the field initializers already hold the Build doc defaults.
            config.wave = new WaveSettings();
            config.satisfaction = new SatisfactionSettings();
            config.economy = new EconomySettings();
            config.energy = new EnergySettings();

            config.needs = new List<NeedDefinition>
            {
                Need(NeedId.Rooms, "Rooms", 1f),
                Need(NeedId.Drinks, "Drinks", 1f),
                Need(NeedId.Food, "Food", 1f),
                Need(NeedId.Relax, "Relax", 1f),
                Need(NeedId.OceanView, "Ocean View", 1.5f),
            };

            // Start: 3 buildings, 2 Workers. Restaurant starts empty (wave 1 has the least Food).
            config.buildings = new List<BuildingDefinition>
            {
                Building(BuildingId.Reception, "Reception", NeedId.Rooms, owned: true, staffed: true),
                Building(BuildingId.Restaurant, "Restaurant", NeedId.Food, owned: true, staffed: false),
                Building(BuildingId.BeachBar, "Beach Bar", NeedId.Drinks, owned: true, staffed: true),
                Unlockable(BuildingId.Pool, "Pool", NeedId.Relax, afterWave: 3, cost: 1200, resortValue: 100),
                Unlockable(BuildingId.Bungalows, "Bungalows", NeedId.OceanView, afterWave: 6, cost: 2500, resortValue: 150),
            };

            config.waves = new List<WaveDefinition>
            {
                Wave(12, 8, 8, 6, 0, 0, "No bottleneck; the player learns to read the panel."),
                Wave(18, 12, 16, 10, 0, 0, "First bottleneck: everything above ~7, the empty building falls behind. Sudden rush at ~45 s.", rush: true),
                Wave(24, 16, 18, 14, 0, 0, "Pressure on two needs, then Pool unlocks."),
                Wave(26, 16, 18, 14, 8, 0, "Relax debut (soft); other needs hold."),
                Wave(34, 20, 22, 18, 16, 0, "First 4-need bottleneck."),
                Wave(40, 25, 28, 22, 20, 0, "Peak before the unlock, then Bungalows unlock."),
                Wave(42, 25, 28, 22, 20, 10, "Ocean View debut (soft, low volume, higher weight); other needs hold."),
                Wave(52, 31, 35, 28, 25, 16, "Full lineup, prototype finale."),
            };

            return config;
        }

        private static NeedDefinition Need(NeedId id, string name, float weight) =>
            new NeedDefinition { need = id, displayName = name, satisfactionWeight = weight };

        private static BuildingDefinition Building(BuildingId id, string name, NeedId need, bool owned, bool staffed) =>
            new BuildingDefinition
            {
                building = id,
                displayName = name,
                need = need,
                unlockedAtStart = owned,
                staffedAtStart = staffed,
                levels = Levels(),
            };

        private static BuildingDefinition Unlockable(BuildingId id, string name, NeedId need, int afterWave, int cost, int resortValue)
        {
            BuildingDefinition building = Building(id, name, need, owned: false, staffed: false);
            building.unlockAfterWave = afterWave;
            building.unlockCost = cost;
            building.unlockResortValue = resortValue;
            return building;
        }

        // Seats 3 → 5 → 7, costs –, 500, 1,200. Resort Value gains are placeholders (not in the doc).
        private static List<BuildingLevel> Levels() => new List<BuildingLevel>
        {
            new BuildingLevel { seats = 3, cost = 0, resortValue = 0 },
            new BuildingLevel { seats = 5, cost = 500, resortValue = 20 },
            new BuildingLevel { seats = 7, cost = 1200, resortValue = 40 },
        };

        private static WaveDefinition Wave(int guests, int rooms, int drinks, int food, int relax, int oceanView, string beat, bool rush = false) =>
            new WaveDefinition
            {
                guests = guests,
                rooms = rooms,
                drinks = drinks,
                food = food,
                relax = relax,
                oceanView = oceanView,
                intendedBeat = beat,
                rush = new RushEventDefinition { enabled = rush, startTime = 45f, units = 6, duration = 10f },
            };
    }
}
