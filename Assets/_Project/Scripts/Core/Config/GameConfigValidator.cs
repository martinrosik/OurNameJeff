using System;
using System.Collections.Generic;

namespace ResortEmpire.Core
{
    /// <summary>
    /// Checks a <see cref="GameConfig"/> against the hard rules that tuning must never break
    /// (Build doc §4.8 WR1, WR2) and for data that would make the simulation misbehave.
    /// WR7/WR8 need a simulation run; see <see cref="WaveBalanceSimulator"/>.
    /// </summary>
    public static class GameConfigValidator
    {
        private const int RatingTierCount = 6;

        public static List<string> Validate(GameConfig config)
        {
            var errors = new List<string>();
            if (config == null)
            {
                errors.Add("Config is missing.");
                return errors;
            }

            ValidateWave(config.wave, errors);
            ValidateSatisfaction(config.satisfaction, errors);
            ValidateEconomy(config.economy, config.energy, errors);
            ValidateNeedsAndBuildings(config, errors);
            ValidateWaves(config, errors);
            return errors;
        }

        private static void ValidateWave(WaveSettings wave, List<string> errors)
        {
            if (wave == null)
            {
                errors.Add("Wave settings are missing.");
                return;
            }

            if (wave.waveTimer <= 0f) errors.Add("waveTimer must be above 0.");
            if (wave.serviceTime <= 0f) errors.Add("serviceTime must be above 0.");
            if (wave.utilizationEmpty <= 0f)
            {
                errors.Add("WR2: utilizationEmpty must be above 0, otherwise an unstaffed building never finishes its queue and the wave may never end.");
            }

            if (wave.utilizationStaffed < wave.utilizationEmpty)
            {
                errors.Add("utilizationStaffed must not be below utilizationEmpty: a Worker has to help.");
            }
        }

        private static void ValidateSatisfaction(SatisfactionSettings s, List<string> errors)
        {
            if (s == null)
            {
                errors.Add("Satisfaction settings are missing.");
                return;
            }

            if (s.patienceFloorAfter <= s.patienceGrace)
            {
                errors.Add("patienceFloorAfter must be greater than patienceGrace.");
            }

            if (!HasTiers(s.ratingThresholds))
            {
                errors.Add($"ratingThresholds needs {RatingTierCount} values (1★, 2★, 3★, 4★, 5★, Perfect).");
                return;
            }

            for (int i = 1; i < s.ratingThresholds.Length; i++)
            {
                if (s.ratingThresholds[i] <= s.ratingThresholds[i - 1])
                {
                    errors.Add("ratingThresholds must be strictly ascending.");
                    break;
                }
            }

            float threeStars = s.ratingThresholds[StarRating.ThreeStars.ToIndex()];
            if (!(s.unitFloor < s.waveFloor && s.waveFloor < threeStars))
            {
                errors.Add($"WR1: must hold unitFloor ({s.unitFloor:0.##}) < waveFloor ({s.waveFloor:0.##}) < 3★ threshold ({threeStars:0.##}). Otherwise doing nothing earns a decent rating.");
            }
        }

        private static void ValidateEconomy(EconomySettings economy, EnergySettings energy, List<string> errors)
        {
            if (economy == null || energy == null)
            {
                errors.Add("Economy or Energy settings are missing.");
                return;
            }

            if (!HasTiers(economy.coinsRatingMultipliers)) errors.Add($"coinsRatingMultipliers needs {RatingTierCount} values.");
            if (!HasTiers(economy.resortValueByRating)) errors.Add($"resortValueByRating needs {RatingTierCount} values.");
            if (!HasTiers(energy.dropByRating)) errors.Add($"Energy dropByRating needs {RatingTierCount} values.");
            if (energy.speedReductionPct < 0f || energy.speedReductionPct >= 1f) errors.Add("Energy speedReductionPct must be in [0, 1).");
        }

        private static void ValidateNeedsAndBuildings(GameConfig config, List<string> errors)
        {
            foreach (NeedId need in Enum.GetValues(typeof(NeedId)))
            {
                int definitions = 0;
                foreach (NeedDefinition n in config.needs)
                {
                    if (n != null && n.need == need) definitions++;
                }

                if (definitions != 1) errors.Add($"Need {need} must be defined exactly once (found {definitions}).");

                int servingBuildings = 0;
                foreach (BuildingDefinition b in config.buildings)
                {
                    if (b != null && b.need == need) servingBuildings++;
                }

                if (servingBuildings > 1) errors.Add($"Need {need} is served by {servingBuildings} buildings; the prototype model allows one.");
            }

            var seen = new HashSet<BuildingId>();
            int owned = 0;
            int staffed = 0;
            foreach (BuildingDefinition b in config.buildings)
            {
                if (b == null)
                {
                    errors.Add("Building list contains an empty entry.");
                    continue;
                }

                if (!seen.Add(b.building)) errors.Add($"Building {b.building} is defined twice.");
                if (b.levels == null || b.levels.Count == 0)
                {
                    errors.Add($"Building {b.building} has no levels.");
                    continue;
                }

                for (int i = 1; i < b.levels.Count; i++)
                {
                    if (b.levels[i].seats < b.levels[i - 1].seats)
                    {
                        errors.Add($"Building {b.building}: seats must not drop when upgrading (level {i + 1}).");
                    }
                }

                if (b.staffedAtStart && !b.unlockedAtStart) errors.Add($"Building {b.building} is staffed at start but not owned.");
                if (b.unlockedAtStart) owned++;
                if (b.unlockedAtStart && b.staffedAtStart) staffed++;
            }

            if (owned == 0)
            {
                errors.Add("At least one building must be owned at start.");
            }
            else if (staffed != owned - 1)
            {
                errors.Add($"Workers must be one fewer than owned buildings at start (owned {owned}, staffed {staffed}). The deficit is the source of the allocation decision.");
            }
        }

        private static void ValidateWaves(GameConfig config, List<string> errors)
        {
            if (config.waves == null || config.waves.Count == 0)
            {
                errors.Add("At least one wave is required.");
                return;
            }

            for (int i = 0; i < config.waves.Count; i++)
            {
                WaveDefinition wave = config.waves[i];
                if (wave == null)
                {
                    errors.Add($"Wave {i + 1} is empty.");
                    continue;
                }

                if (wave.rush != null && wave.rush.enabled && wave.rush.duration < 0f)
                {
                    errors.Add($"Wave {i + 1}: rush duration must not be negative.");
                }
            }
        }

        private static bool HasTiers<T>(T[] values) => values != null && values.Length == RatingTierCount;
    }
}
