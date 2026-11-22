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
                if (config.wave.suddenRushEnabled)
                {
                    if (config.wave.suddenRushTime >= config.wave.waveTimer) errors.Add($"Sudden rush: suddenRushTime ({config.wave.suddenRushTime}) must be before waveTimer ({config.wave.waveTimer}), inside the arrivals (Build doc §4.9).");
                    if (config.wave.suddenRushWave < 1) errors.Add($"Sudden rush: suddenRushWave must be at least 1 (is {config.wave.suddenRushWave}).");
                    if (config.wave.suddenRushChance < 0f || config.wave.suddenRushChance > 1f) errors.Add($"Sudden rush: suddenRushChance must be between 0 and 1 (is {config.wave.suddenRushChance}).");
                    if (config.wave.suddenRushUnits < 1) errors.Add($"Sudden rush: suddenRushUnits must be at least 1 (is {config.wave.suddenRushUnits}).");
                    if (config.wave.suddenRushDuration <= 0f) errors.Add($"Sudden rush: suddenRushDuration must be above 0 s (is {config.wave.suddenRushDuration}).");
                }

                if (config.wave.bottleneckThreshold < 1) errors.Add($"bottleneckThreshold must be at least 1 (is {config.wave.bottleneckThreshold}).");
                if (config.wave.bottleneckHysteresis <= 0f) errors.Add($"bottleneckHysteresis must be above 0 s (is {config.wave.bottleneckHysteresis}).");
                if (config.wave.serviceTime <= 0f) errors.Add($"Service time must be above 0 s (is {config.wave.serviceTime}).");
                if (config.wave.utilizationStaffed <= 0f) errors.Add($"utilizationStaffed must be above 0 (is {config.wave.utilizationStaffed}).");
                if (config.wave.utilizationEmpty <= 0f) errors.Add($"WR2: utilizationEmpty must be above 0 (is {config.wave.utilizationEmpty}), or an empty building never finishes.");
                if (config.wave.growthIfNotHarder < 0.01f) errors.Add($"growthIfNotHarder must be at least 0.01 (is {config.wave.growthIfNotHarder}), or a wave could be no harder than the one before it.");
            }

            ValidateSatisfaction(config.satisfaction, errors);
            ValidateResortValue(config.resortValue, errors);
            if (config.breakdown == null) errors.Add("Wave Complete breakdown settings are missing.");
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

        private static void ValidateSatisfaction(SatisfactionSettings satisfaction, List<string> errors)
        {
            if (satisfaction == null)
            {
                errors.Add("Satisfaction settings are missing.");
                return;
            }

            if (satisfaction.patienceFloorAfter <= satisfaction.patienceGrace)
            {
                errors.Add($"patienceFloorAfter ({satisfaction.patienceFloorAfter} s) must be longer than patienceGrace ({satisfaction.patienceGrace} s).");
            }

            if (satisfaction.oceanViewSatisfactionWeight <= 0f) errors.Add($"oceanViewSatisfactionWeight must be above 0 (is {satisfaction.oceanViewSatisfactionWeight}).");

            float[] thresholds = satisfaction.ratingThresholds;
            if (thresholds == null || thresholds.Length != 6)
            {
                errors.Add("ratingThresholds needs 6 values: 1, 2, 3, 4, 5 stars and Perfect.");
                return;
            }

            for (int i = 1; i < thresholds.Length; i++)
            {
                if (thresholds[i] <= thresholds[i - 1]) errors.Add($"ratingThresholds must grow: value {i + 1} ({thresholds[i]}) is not above value {i} ({thresholds[i - 1]}).");
            }

            if (thresholds[5] > 100f) errors.Add($"The Perfect threshold must be at most 100 % (is {thresholds[5]}).");

            // WR1: otherwise a player who does nothing during the wave still gets a decent rating.
            if (!(satisfaction.unitFloor < satisfaction.waveFloor && satisfaction.waveFloor < thresholds[2]))
            {
                errors.Add($"WR1: unitFloor ({satisfaction.unitFloor} %) < waveFloor ({satisfaction.waveFloor} %) < the 3-star threshold ({thresholds[2]} %) must hold.");
            }
        }

        private static void ValidateResortValue(ResortValueSettings resortValue, List<string> errors)
        {
            if (resortValue == null)
            {
                errors.Add("Resort Value settings are missing.");
                return;
            }

            if (resortValue.startingValue < 0) errors.Add($"Resort Value: startingValue must be 0 or more (is {resortValue.startingValue}).");

            int[] gains = resortValue.gainPerRating;
            if (gains == null || gains.Length != 6)
            {
                errors.Add("Resort Value: gainPerRating needs 6 values: 1, 2, 3, 4, 5 stars and Perfect.");
                return;
            }

            for (int i = 0; i < gains.Length; i++)
            {
                // Resort Value only grows (GDD §7.6): a weak wave adds 0, never takes any away.
                if (gains[i] < 0) errors.Add($"Resort Value: gainPerRating value {i + 1} ({gains[i]}) must be 0 or more, Resort Value never drops.");
                if (i > 0 && gains[i] < gains[i - 1]) errors.Add($"Resort Value: gainPerRating must not drop with a better rating: value {i + 1} ({gains[i]}) is below value {i} ({gains[i - 1]}).");
            }
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
