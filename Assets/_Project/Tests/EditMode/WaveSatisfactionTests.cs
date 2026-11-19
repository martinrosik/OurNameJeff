using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ResortEmpire.Core;
using ResortEmpire.Editor;

namespace ResortEmpire.Tests
{
    /// <summary>
    /// Wave evaluation (Build doc §4.4, AC1b, AC4b, WR1): Wave Satisfaction from every unit's wait (arrival → seated),
    /// the two floors, the Ocean View weight and the star rating shown on Wave Complete.
    /// </summary>
    public class WaveSatisfactionTests
    {
        private GameConfig _config;

        private SatisfactionSettings Settings => _config.satisfaction;

        [SetUp]
        public void CreateConfig()
        {
            _config = PrototypeConfigDefaults.Create();
            _config.wave.incomingDuration = 0f; // skip the boat so Tick(t) is wave time t
        }

        [TearDown]
        public void DestroyConfig() => UnityEngine.Object.DestroyImmediate(_config);

        /// <summary>Plays the next wave in steps of <paramref name="step"/> s until everyone is served.</summary>
        private static WaveResult PlayWave(ResortGame game, double step)
        {
            Assert.IsTrue(game.StartWave());
            for (int i = 0; i < 100_000 && game.Phase == GamePhase.WaveActive; i++) game.Tick(step);
            Assert.AreEqual(GamePhase.WaveResolving, game.Phase, "The wave ends once everyone is served.");
            return game.LastWaveResult;
        }

        /// <summary>A wave of hand-picked needs where nobody is served by itself (tests call FinishService).</summary>
        private static WaveSimulation HandServedWave(double waveTimer, int seats, params (NeedId Need, int Units)[] demand)
        {
            var needs = demand.Select(d => new NeedDemand(d.Need, d.Units)).ToList();
            return new WaveSimulation(new WaveForecast(0, 0, needs), waveTimer, _ => seats);
        }

        [TestCase(0, 100)]
        [TestCase(10, 100)]
        [TestCase(20, 62.5)]
        [TestCase(30, 25)]
        [TestCase(120, 25)]
        public void UnitContributionFallsLinearlyFromGraceToTheUnitFloor(double waitSeconds, double expected)
        {
            Assert.AreEqual(expected, WaveSatisfaction.GetUnitContribution(waitSeconds, Settings), 1e-6);
        }

        [TestCase(40, WaveRating.OneStar)]
        [TestCase(54.99, WaveRating.OneStar)]
        [TestCase(55, WaveRating.TwoStars)]
        [TestCase(70, WaveRating.ThreeStars)]
        [TestCase(80, WaveRating.FourStars)]
        [TestCase(89.99, WaveRating.FourStars)]
        [TestCase(90, WaveRating.FiveStars)]
        [TestCase(99.9, WaveRating.FiveStars)]
        [TestCase(100, WaveRating.Perfect)]
        public void RatingIsTheHighestThresholdReached(double satisfaction, WaveRating expected)
        {
            Assert.AreEqual(expected, WaveSatisfaction.GetRating(satisfaction, Settings));
        }

        [Test]
        public void TheWorstRatingIsOneStarNeverZero()
        {
            Assert.AreEqual(WaveRating.OneStar, WaveSatisfaction.GetRating(0, Settings));
        }

        [Test]
        public void AC1b_AUnitThatFindsAFreeSeatWaitsZeroSecondsHoweverLongItsServiceTakes()
        {
            WaveSimulation wave = HandServedWave(30, 3, (NeedId.Food, 3));

            wave.AdvanceTo(500);

            CollectionAssert.AreEqual(new double[] { 0, 0, 0 }, wave.GetWaitTimes(NeedId.Food));
            Assert.AreEqual(100, WaveSatisfaction.Calculate(wave, Settings), 1e-6);
        }

        [Test]
        public void WaitingEndsWhenTheUnitSitsDown()
        {
            WaveSimulation wave = HandServedWave(20, 1, (NeedId.Food, 2)); // arrivals at 10 s and 20 s

            wave.AdvanceTo(35);
            Assert.AreEqual(1, wave.GetWaitTimes(NeedId.Food).Count, "The second unit is still in the queue.");

            Assert.IsTrue(wave.FinishService(NeedId.Food));
            CollectionAssert.AreEqual(new double[] { 0, 15 }, wave.GetWaitTimes(NeedId.Food));
        }

        [Test]
        public void OceanViewWeighsMoreThanTheOtherNeeds()
        {
            // Rooms: 1 unit, seated at once (100 %). Ocean View: 1 seat, unit 0 seated at once, unit 1 waits 30 s (25 %).
            WaveSimulation wave = HandServedWave(20, 1, (NeedId.Rooms, 1), (NeedId.OceanView, 2));
            wave.AdvanceTo(50);
            Assert.IsTrue(wave.FinishService(NeedId.OceanView));

            // (100 × 1 + 100 × 1.5 + 25 × 1.5) ÷ (1 + 1.5 + 1.5) = 71.875; unweighted it would be 75.
            Assert.AreEqual(71.875, WaveSatisfaction.Calculate(wave, Settings), 1e-6);
        }

        [Test]
        public void AC4b_WhenEveryoneWaitedLongTheWaveFloorGivesOneStar()
        {
            WaveSimulation longWait = HandServedWave(1, 1, (NeedId.Drinks, 10)); // all arrive within 1 s, 1 seat
            for (int served = 0; served < 9; served++)
            {
                longWait.AdvanceTo(longWait.Time + 100);
                Assert.IsTrue(longWait.FinishService(NeedId.Drinks));
            }

            double satisfaction = WaveSatisfaction.Calculate(longWait, Settings);
            Assert.Less((100 + 9 * 25.0) / 10, Settings.waveFloor, "The plain average (32.5 %) is below the floor.");
            Assert.AreEqual(Settings.waveFloor, satisfaction, 1e-6, "Wave floor: never below 40 %.");
            Assert.AreEqual(WaveRating.OneStar, WaveSatisfaction.GetRating(satisfaction, Settings));
        }

        [Test]
        public void Wave1HandledByTheStartingWorkersIsPerfect()
        {
            WaveResult result = PlayWave(new ResortGame(_config), 0.1);

            Assert.AreEqual(100, result.Satisfaction, 1e-6);
            Assert.AreEqual(WaveRating.Perfect, result.Rating);
        }

        [Test]
        public void ABadlyHandledWaveRatesLowerButNeverBelowTheWaveFloor()
        {
            _config.waves[0].food = 30; // a big Food demand on the empty Restaurant

            WaveResult result = PlayWave(new ResortGame(_config), 0.1);

            Assert.Less(result.Satisfaction, 70, "A long Food queue costs stars.");
            Assert.GreaterOrEqual(result.Satisfaction, Settings.waveFloor - 1e-6);
            Assert.Less(result.Rating, WaveRating.ThreeStars);
            Assert.AreEqual(result.TotalUnits, result.UnitsServed, "Everyone is still served (WR4).");
        }

        [Test]
        public void MovingAWorkerToTheOverloadedBuildingRaisesTheRating()
        {
            _config.waves[0].food = 30;
            WaveResult passive = PlayWave(new ResortGame(_config), 0.1);

            var game = new ResortGame(_config);
            Assert.AreEqual(WorkerMoveResult.Moved, game.MoveWorker(BuildingId.Reception, BuildingId.Restaurant));
            WaveResult managed = PlayWave(game, 0.1);

            Assert.Greater(managed.Satisfaction, passive.Satisfaction, "I see the problem, I decide, it gets better.");
        }

        [Test]
        public void SatisfactionDoesNotDependOnTheFrameRate()
        {
            _config.waves[0].food = 30;

            WaveResult small = PlayWave(new ResortGame(_config), 1.0 / 60);
            WaveResult big = PlayWave(new ResortGame(_config), 0.7);

            Assert.AreEqual(small.Satisfaction, big.Satisfaction, 1e-6);
            Assert.AreEqual(small.Rating, big.Rating);
        }

        [Test]
        public void TheResultIsOnlyComputedWhenTheWaveEnds()
        {
            var game = new ResortGame(_config);
            game.StartWave();
            game.Tick(60);

            Assert.IsNull(game.LastWaveResult, "Satisfaction is never shown during the wave.");
        }

        [Test]
        public void ShippedConfigHasTheBuildDocSatisfactionNumbers()
        {
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<GameConfig>(PrototypeConfigDefaults.AssetPath);
            Assert.IsNotNull(asset);

            SatisfactionSettings shipped = asset.satisfaction;
            Assert.AreEqual(Settings.patienceGrace, shipped.patienceGrace);
            Assert.AreEqual(Settings.patienceFloorAfter, shipped.patienceFloorAfter);
            Assert.AreEqual(Settings.unitFloor, shipped.unitFloor);
            Assert.AreEqual(Settings.waveFloor, shipped.waveFloor);
            CollectionAssert.AreEqual(Settings.ratingThresholds, shipped.ratingThresholds);
            Assert.AreEqual(Settings.oceanViewSatisfactionWeight, shipped.oceanViewSatisfactionWeight);
        }

        [Test]
        public void WR1_TheValidatorRejectsAUnitFloorAboveTheWaveFloor()
        {
            Assert.IsEmpty(GameConfigValidator.Validate(_config));

            Settings.unitFloor = 60f;

            List<string> errors = GameConfigValidator.Validate(_config);
            Assert.That(errors.Any(e => e.Contains("WR1")));
        }

        [Test]
        public void TheValidatorRejectsThresholdsThatDoNotGrow()
        {
            Settings.ratingThresholds = new[] { 40f, 55f, 50f, 80f, 90f, 100f };

            Assert.That(GameConfigValidator.Validate(_config).Any(e => e.Contains("ratingThresholds")));
        }
    }
}
