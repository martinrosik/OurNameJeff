using System;

namespace ResortEmpire.Core
{
    /// <summary>
    /// Satisfaction formulas (Build doc §4.4). Waiting = time from arrival until seated;
    /// time being served never counts.
    /// </summary>
    public static class SatisfactionMath
    {
        private const double Epsilon = 1e-9;

        /// <summary>Thresholds are floats in the config (0.55f ≈ 0.5500000119), so compare with float-sized tolerance.</summary>
        private const double ThresholdTolerance = 1e-6;

        /// <summary>
        /// Contribution of one need unit: 100 % up to patienceGrace, then linear down to unitFloor
        /// at patienceFloorAfter, then unitFloor forever.
        /// </summary>
        public static double UnitContribution(double waitSeconds, SatisfactionSettings s)
        {
            if (waitSeconds <= s.patienceGrace + Epsilon) return 1.0;
            if (waitSeconds >= s.patienceFloorAfter) return s.unitFloor;

            double t = (waitSeconds - s.patienceGrace) / (s.patienceFloorAfter - s.patienceGrace);
            return 1.0 + (s.unitFloor - 1.0) * t;
        }

        /// <summary>
        /// Wave Satisfaction = max(waveFloor, Σ contribution × weight ÷ Σ weight).
        /// A wave without units counts as fully satisfied.
        /// </summary>
        public static double WaveSatisfaction(double weightedContributionSum, double weightSum, SatisfactionSettings s)
        {
            double raw = weightSum > 0 ? weightedContributionSum / weightSum : 1.0;
            return Math.Max(s.waveFloor, Math.Min(1.0, raw));
        }

        /// <summary>The best tier whose threshold is reached. Never below 1★.</summary>
        public static StarRating RatingFor(double satisfaction, SatisfactionSettings s)
        {
            float[] thresholds = s.ratingThresholds;
            var rating = StarRating.OneStar;
            for (int i = 0; i < thresholds.Length && i <= StarRating.Perfect.ToIndex(); i++)
            {
                if (satisfaction + ThresholdTolerance >= thresholds[i])
                {
                    rating = (StarRating)(i + 1);
                }
            }

            return rating;
        }
    }
}
