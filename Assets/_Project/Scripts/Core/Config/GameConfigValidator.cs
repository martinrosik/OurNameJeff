using System;
using System.Collections.Generic;

namespace ResortEmpire.Core
{
    /// <summary>
    /// Checks a <see cref="GameConfig"/> for data that would break the game. When a ticket adds settings with
    /// hard rules from the Build doc (e.g. WR1, WR2), add their checks here too.
    /// </summary>
    public static class GameConfigValidator
    {
        public static List<string> Validate(GameConfig config)
        {
            var errors = new List<string>();
            if (config == null)
            {
                errors.Add("Config is missing.");
                return errors;
            }

            if (config.wave == null) errors.Add("Wave settings are missing.");
            else
            {
                if (config.wave.waveTimer < 1f) errors.Add($"Wave timer must be at least 1 s (is {config.wave.waveTimer}).");
                if (config.wave.bottleneckThreshold < 1) errors.Add($"bottleneckThreshold must be at least 1 (is {config.wave.bottleneckThreshold}).");
                if (config.wave.bottleneckHysteresis <= 0f) errors.Add($"bottleneckHysteresis must be above 0 s (is {config.wave.bottleneckHysteresis}).");
                if (config.wave.serviceTime <= 0f) errors.Add($"Service time must be above 0 s (is {config.wave.serviceTime}).");
                if (config.wave.utilizationStaffed <= 0f) errors.Add($"utilizationStaffed must be above 0 (is {config.wave.utilizationStaffed}).");
                if (config.wave.utilizationEmpty <= 0f) errors.Add($"WR2: utilizationEmpty must be above 0 (is {config.wave.utilizationEmpty}), or an empty building never finishes.");
            }

            ValidateNeeds(config, errors);
            ValidateBuildings(config, errors);

            if (config.waves == null || config.waves.Count == 0)
            {
                errors.Add("At least one wave is required.");
            }
            else if (config.waves.Contains(null))
            {
                errors.Add("Wave list contains an empty entry.");
            }

            return errors;
        }

        private static void ValidateNeeds(GameConfig config, List<string> errors)
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
        }

        private static void ValidateBuildings(GameConfig config, List<string> errors)
        {
            var seen = new HashSet<BuildingId>();
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
                    errors.Add($"Building {b.building} has no levels (level 1 sets its capacity).");
                    continue;
                }

                for (int i = 0; i < b.levels.Count; i++)
                {
                    if (b.levels[i].seats < 1) errors.Add($"Building {b.building} level {i + 1} needs at least 1 seat.");
                    if (i > 0 && b.levels[i].seats < b.levels[i - 1].seats) errors.Add($"Building {b.building}: seats must not drop at level {i + 1}.");
                }

                if (b.staffedAtStart) staffed++;
            }

            if (seen.Count == 0)
            {
                errors.Add("At least one building is required.");
            }
            else if (staffed != seen.Count - 1)
            {
                errors.Add($"Workers must be one fewer than buildings at the start (buildings {seen.Count}, staffed {staffed}). The empty building is the player's allocation decision.");
            }
        }
    }
}
