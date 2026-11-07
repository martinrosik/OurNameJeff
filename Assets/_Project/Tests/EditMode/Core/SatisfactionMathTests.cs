using NUnit.Framework;
using ResortEmpire.Core;

namespace ResortEmpire.Tests
{
    /// <summary>Build doc §4.4: unit contribution curve, floors and rating tiers.</summary>
    public class SatisfactionMathTests
    {
        private SatisfactionSettings _settings;

        [SetUp]
        public void SetUp() => _settings = new SatisfactionSettings();

        [TestCase(0.0, 1.0)]
        [TestCase(10.0, 1.0)]
        [TestCase(20.0, 0.625)]
        [TestCase(30.0, 0.25)]
        [TestCase(300.0, 0.25)]
        public void UnitContributionFallsLinearlyFromGraceToFloor(double waitSeconds, double expected)
        {
            Assert.AreEqual(expected, SatisfactionMath.UnitContribution(waitSeconds, _settings), 1e-9);
        }

        [TestCase(0.40, StarRating.OneStar)]
        [TestCase(0.5499, StarRating.OneStar)]
        [TestCase(0.55, StarRating.TwoStars)]
        [TestCase(0.70, StarRating.ThreeStars)]
        [TestCase(0.80, StarRating.FourStars)]
        [TestCase(0.90, StarRating.FiveStars)]
        [TestCase(0.999, StarRating.FiveStars)]
        [TestCase(1.0, StarRating.Perfect)]
        public void RatingTiersFollowThresholds(double satisfaction, StarRating expected)
        {
            Assert.AreEqual(expected, SatisfactionMath.RatingFor(satisfaction, _settings));
        }

        [Test]
        public void WaveFloorMeansNeverZero()
        {
            // Every unit at the unit floor (25 %) still yields the wave floor (40 %) = 1★.
            double satisfaction = SatisfactionMath.WaveSatisfaction(10 * _settings.unitFloor, 10, _settings);

            Assert.AreEqual(_settings.waveFloor, satisfaction, 1e-9);
            Assert.AreEqual(StarRating.OneStar, SatisfactionMath.RatingFor(satisfaction, _settings));
        }

        [Test]
        public void WaveWithoutUnitsIsFullySatisfied()
        {
            Assert.AreEqual(1.0, SatisfactionMath.WaveSatisfaction(0, 0, _settings), 1e-9);
        }
    }
}
