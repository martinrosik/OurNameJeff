using System.Collections.Generic;
using ResortEmpire.Core;
using UnityEngine;

namespace ResortEmpire.Editor
{
    /// <summary>
    /// The Build doc's prototype starting content (§4.5, §4.8, as of 2026-09-29), used to create
    /// <c>Assets/_Project/Data/Config_Prototype.asset</c> and as a fixed baseline in tests.
    /// Tune the asset, not this file. All numbers are "e.g."/TUNING in the doc.
    /// </summary>
    public static class PrototypeConfigDefaults
    {
        public const string AssetPath = "Assets/_Project/Data/Config_Prototype.asset";

        /// <summary>A new, unsaved config with the Build doc values. Destroy it if you don't save it.</summary>
        public static GameConfig Create()
        {
            var config = ScriptableObject.CreateInstance<GameConfig>();
            config.name = "Config_Prototype";
            config.wave = new WaveSettings
            {
                incomingDuration = 3f,
                waveTimer = 90f, // OUR-34: arrival phase (Build doc §4.8 wave_timer)
            };

            config.needs = new List<NeedDefinition>
            {
                Need(NeedId.Rooms, "Rooms"),
                Need(NeedId.Drinks, "Drinks"),
                Need(NeedId.Food, "Food"),
                Need(NeedId.Relax, "Relax"),
                Need(NeedId.OceanView, "Ocean View"),
            };

            // OUR-31: the island starts with 3 buildings and 2 Workers. The Restaurant starts empty
            // (wave 1 has the least Food). Pool and Bungalows are unlocked in a later sprint.
            config.buildings = new List<BuildingDefinition>
            {
                Building(BuildingId.Reception, "Reception", NeedId.Rooms, staffed: true),
                Building(BuildingId.Restaurant, "Restaurant", NeedId.Food, staffed: false),
                Building(BuildingId.BeachBar, "Beach Bar", NeedId.Drinks, staffed: true),
            };

            // OUR-33 shows these before each wave. Relax and Ocean View are skipped until their buildings exist.
            config.waves = new List<WaveDefinition>
            {
                Wave(12, 8, 8, 6, 0, 0, "No bottleneck; the player learns to read the panel."),
                Wave(18, 12, 16, 10, 0, 0, "First bottleneck: everything above ~7, the empty building falls behind. Sudden rush at ~45 s."),
                Wave(24, 16, 18, 14, 0, 0, "Pressure on two needs, then Pool unlocks."),
                Wave(26, 16, 18, 14, 8, 0, "Relax debut (soft); other needs hold."),
                Wave(34, 20, 22, 18, 16, 0, "First 4-need bottleneck."),
                Wave(40, 25, 28, 22, 20, 0, "Peak before the unlock, then Bungalows unlock."),
                Wave(42, 25, 28, 22, 20, 10, "Ocean View debut (soft, low volume, higher weight); other needs hold."),
                Wave(52, 31, 35, 28, 25, 16, "Full lineup, prototype finale."),
            };

            return config;
        }

        private static NeedDefinition Need(NeedId id, string name) =>
            new NeedDefinition { need = id, displayName = name };

        // OUR-50: capacity per level, seats 3 → 5 → 7 (upgrades come in a later sprint).
        private static BuildingDefinition Building(BuildingId id, string name, NeedId need, bool staffed) =>
            new BuildingDefinition
            {
                building = id,
                displayName = name,
                need = need,
                staffedAtStart = staffed,
                levels = new List<BuildingLevel>
                {
                    new BuildingLevel { seats = 3 },
                    new BuildingLevel { seats = 5 },
                    new BuildingLevel { seats = 7 },
                },
            };

        private static WaveDefinition Wave(int guests, int rooms, int drinks, int food, int relax, int oceanView, string beat) =>
            new WaveDefinition
            {
                guests = guests,
                rooms = rooms,
                drinks = drinks,
                food = food,
                relax = relax,
                oceanView = oceanView,
                intendedBeat = beat,
            };
    }
}
