using System;
using System.Collections.Generic;

namespace ResortEmpire.Core
{
    /// <summary>
    /// Wave Satisfaction (Build doc §4.4): computed once at the end of the wave from every need unit's delay
    /// (arrival → served, minus a staffed service time, so slow service counts too; design-conflicts row 13), never
    /// from the state at the end. During the wave the Wave Needs Panel shows the same
    /// formula over the guests that have walked out so far (designer decision, docs/design-conflicts.md row 4).
    /// <code>
    /// Wave Satisfaction = max(waveFloor, Σ(unit contribution × need weight) ÷ Σ need weight)
    /// </code>
    /// </summary>
    public static class WaveSatisfaction
    {
        /// <summary>Tolerance for float thresholds against double math (0.55f is 0.5500000119 as a double).</summary>
        private const double Tolerance = 1e-6;

        /// <summary>
        /// One unit's contribution in %: 100 up to patienceGrace, then linearly down to unitFloor at patienceFloorAfter,
        /// and unitFloor after that.
        /// </summary>
        public static double GetUnitContribution(double delaySeconds, SatisfactionSettings settings)
        {
            if (delaySeconds <= settings.patienceGrace + Tolerance) return 100.0;
            if (delaySeconds >= settings.patienceFloorAfter) return settings.unitFloor;

            double falloff = (delaySeconds - settings.patienceGrace) / (settings.patienceFloorAfter - settings.patienceGrace);
            return 100.0 - falloff * (100.0 - settings.unitFloor);
        }

        /// <summary>
        /// Guest mood bubbles: the face a guest shows for one visit, from the same delay and the same thresholds as its
        /// contribution: full marks up to patienceGrace, the floor from patienceFloorAfter, neutral in between.
        /// </summary>
        public static GuestMood GetGuestMood(double delaySeconds, SatisfactionSettings settings)
        {
            if (delaySeconds <= settings.patienceGrace + Tolerance) return GuestMood.Happy;
            if (delaySeconds >= settings.patienceFloorAfter - Tolerance) return GuestMood.Unhappy;
            return GuestMood.Neutral;
        }

        /// <summary>Wave Satisfaction in % (waveFloor–100) of every unit served so far in <paramref name="wave"/>.</summary>
        public static double Calculate(WaveSimulation wave, SatisfactionSettings settings) =>
            Calculate(GetAllDelays(wave), settings);

        /// <summary>
        /// Wave Satisfaction in % (waveFloor–100) of the given visits (need and delay in seconds). The live satisfaction
        /// bar uses it for the guests that have walked out so far; once everyone has, it equals the wave's result.
        /// </summary>
        public static double Calculate(IEnumerable<(NeedId Need, double DelaySeconds)> delays, SatisfactionSettings settings)
        {
            double weightedSum = 0;
            double weightTotal = 0;
            foreach ((NeedId need, double delay) in delays)
            {
                double weight = settings.GetNeedWeight(need);
                weightedSum += GetUnitContribution(delay, settings) * weight;
                weightTotal += weight;
            }

            // A wave without guests had nobody waiting.
            double average = weightTotal > 0 ? weightedSum / weightTotal : 100.0;
            return Math.Max(settings.waveFloor, average);
        }

        private static IEnumerable<(NeedId, double)> GetAllDelays(WaveSimulation wave)
        {
            foreach (NeedId need in wave.Needs)
            {
                foreach (double delay in wave.GetDelays(need))
                {
                    yield return (need, delay);
                }
            }
        }

        /// <summary>The highest tier whose threshold the satisfaction reaches; one star at the least.</summary>
        public static WaveRating GetRating(double satisfaction, SatisfactionSettings settings)
        {
            var rating = WaveRating.OneStar;
            int tiers = Math.Min(settings.ratingThresholds.Length, (int)WaveRating.Perfect);
            for (int tier = 0; tier < tiers; tier++)
            {
                if (satisfaction + Tolerance >= settings.ratingThresholds[tier]) rating = (WaveRating)(tier + 1);
            }

            return rating;
        }
    }
}
