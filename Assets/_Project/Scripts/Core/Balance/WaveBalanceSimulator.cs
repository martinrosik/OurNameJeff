using System;
using System.Collections.Generic;

namespace ResortEmpire.Core
{
    /// <summary>
    /// Headless balance check, the C# counterpart of the economist's wave_sim.py (Build doc §4.8).
    /// Simulates a passive player: Workers placed once before the wave, then no moves, no upgrades,
    /// no Energy. Unlocks are assumed bought as soon as their gate opens.
    /// <list type="bullet">
    /// <item>WR7: from Wave 3 on (except debut waves) the best passive placement at level 1 must NOT reach 5★.</item>
    /// <item>WR8: a fully upgraded resort must reach 5★ in every wave without Energy.</item>
    /// </list>
    /// </summary>
    public static class WaveBalanceSimulator
    {
        private const double MaxWaveSeconds = 3600;
        private static readonly int BuildingCount = Enum.GetValues(typeof(BuildingId)).Length;

        /// <summary>Runs one wave with a fixed layout and no player input.</summary>
        public static WaveResult RunWithoutIntervention(
            GameConfig config,
            int waveIndex,
            ICollection<BuildingId> owned,
            ICollection<BuildingId> staffed,
            bool maxLevel)
        {
            var buildings = new BuildingRuntime[BuildingCount];
            foreach (BuildingDefinition definition in config.buildings)
            {
                var building = new BuildingRuntime(definition)
                {
                    IsUnlocked = owned.Contains(definition.building),
                    HasWorker = staffed.Contains(definition.building),
                };
                building.Level = maxLevel ? definition.MaxLevel : 1;
                buildings[(int)definition.building] = building;
            }

            var wave = new WaveSimulation(config, waveIndex, buildings);
            while (!wave.IsFinished && wave.Time < MaxWaveSeconds)
            {
                wave.Advance(1.0);
            }

            return wave.BuildResult();
        }

        /// <summary>Tries every way to leave one owned building empty and returns the best result.</summary>
        public static PlacementResult FindBestPlacement(GameConfig config, int waveIndex, bool maxLevel)
        {
            List<BuildingId> owned = OwnedBeforeWave(config, waveIndex);
            PlacementResult best = null;

            foreach (BuildingId empty in owned)
            {
                var staffed = new List<BuildingId>(owned);
                staffed.Remove(empty);
                WaveResult result = RunWithoutIntervention(config, waveIndex, owned, staffed, maxLevel);
                if (best == null || result.Satisfaction > best.Result.Satisfaction + 1e-9)
                {
                    best = new PlacementResult(empty, result);
                }
            }

            return best;
        }

        /// <summary>Buildings owned when wave <paramref name="waveIndex"/> starts, if every unlock is bought right away.</summary>
        public static List<BuildingId> OwnedBeforeWave(GameConfig config, int waveIndex)
        {
            var owned = new List<BuildingId>();
            foreach (BuildingDefinition definition in config.buildings)
            {
                if (definition.unlockedAtStart || definition.unlockAfterWave <= waveIndex)
                {
                    owned.Add(definition.building);
                }
            }

            return owned;
        }

        /// <summary>A debut wave is the first wave after an unlock: its new need starts soft (Build doc §4.8 rules 2–3).</summary>
        public static bool IsDebutWave(GameConfig config, int waveIndex)
        {
            foreach (BuildingDefinition definition in config.buildings)
            {
                if (!definition.unlockedAtStart && definition.unlockAfterWave == waveIndex)
                {
                    return true;
                }
            }

            return false;
        }

        public static WaveBalanceReport BuildReport(GameConfig config)
        {
            var rows = new List<WaveBalanceRow>();
            for (int i = 0; i < config.waves.Count; i++)
            {
                PlacementResult passive = FindBestPlacement(config, i, maxLevel: false);
                PlacementResult prepared = FindBestPlacement(config, i, maxLevel: true);
                rows.Add(new WaveBalanceRow(i, config.waves[i].guests, IsDebutWave(config, i), passive, prepared));
            }

            return new WaveBalanceReport(rows);
        }
    }

    public sealed class PlacementResult
    {
        public PlacementResult(BuildingId emptyBuilding, WaveResult result)
        {
            EmptyBuilding = emptyBuilding;
            Result = result;
        }

        /// <summary>The building left without a Worker.</summary>
        public BuildingId EmptyBuilding { get; }

        public WaveResult Result { get; }
    }
}
