using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ResortEmpire.Core;
using ResortEmpire.Editor;
using ResortEmpire.Game;

namespace ResortEmpire.Tests
{
    /// <summary>
    /// Wave evaluation (Build doc §4.4, AC1b, AC4b, WR1): Wave Satisfaction from every unit's delay (arrival → served,
    /// minus a staffed service time; design-conflicts row 13), the two floors, the Ocean View weight and the star rating
    /// shown on Wave Complete.
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

        /// <summary>
        /// 1 seat each, served by hand with the default staffed service time (12 s). Rooms (arrives at 20 s) and Ocean View
        /// unit 0 (10 s) are served within 12 s: no delay. Ocean View unit 1 (20 s) waits for the seat until 22 s and is
        /// served at 64 s: 44 s - 12 s = 32 s of delay, so it counts only the unit floor.
        /// </summary>
        private static WaveSimulation OceanViewWaveServedByHand()
        {
            WaveSimulation wave = HandServedWave(20, 1, (NeedId.Rooms, 1), (NeedId.OceanView, 2));
            wave.AdvanceTo(22);
            Assert.IsTrue(wave.FinishService(NeedId.OceanView));
            Assert.IsTrue(wave.FinishService(NeedId.Rooms));
            wave.AdvanceTo(64);
            Assert.IsTrue(wave.FinishService(NeedId.OceanView));
            return wave;
        }

        /// <summary>The delay of every unit of <paramref name="need"/> in wave 1, played with the starting Workers.</summary>
        private IReadOnlyList<double> GetWave1Delays(NeedId need)
        {
            var game = new ResortGame(_config);
            Assert.IsTrue(game.StartWave());
            for (int i = 0; i < 100_000 && game.Phase == GamePhase.WaveActive; i++) game.Tick(0.1);
            return game.CurrentWave.GetDelays(need);
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

        [TestCase(0, GuestMood.Happy)]
        [TestCase(10, GuestMood.Happy)]
        [TestCase(10.5, GuestMood.Neutral)]
        [TestCase(29.5, GuestMood.Neutral)]
        [TestCase(30, GuestMood.Unhappy)]
        [TestCase(120, GuestMood.Unhappy)]
        public void GuestMoodFollowsTheSameThresholdsAsTheUnitContribution(double delaySeconds, GuestMood expected)
        {
            Assert.AreEqual(expected, WaveSatisfaction.GetGuestMood(delaySeconds, Settings));
        }

        [Test]
        public void GuestMoodFollowsTunedThresholds()
        {
            Settings.patienceGrace = 5f;
            Settings.patienceFloorAfter = 15f;

            Assert.AreEqual(GuestMood.Neutral, WaveSatisfaction.GetGuestMood(8, Settings));
            Assert.AreEqual(GuestMood.Unhappy, WaveSatisfaction.GetGuestMood(15, Settings));
        }

        [Test]
        public void TheDelayOfAUnitIsKnownOnceItIsServed()
        {
            WaveSimulation wave = HandServedWave(20, 1, (NeedId.Food, 2)); // arrivals at 10 s and 20 s

            wave.AdvanceTo(35);
            Assert.IsFalse(wave.TryGetDelay(NeedId.Food, 0, out _), "Unit 0 is still being served.");

            Assert.IsTrue(wave.FinishService(NeedId.Food));
            Assert.IsTrue(wave.TryGetDelay(NeedId.Food, 0, out double first));
            Assert.AreEqual(13, first, 1e-9, "Unit 0 was there from 10 s to 35 s: 25 s, 13 s more than a staffed service.");
            Assert.IsFalse(wave.TryGetDelay(NeedId.Food, 1, out _), "Unit 1 only sat down now.");

            wave.AdvanceTo(40);
            Assert.IsTrue(wave.FinishService(NeedId.Food));
            Assert.IsTrue(wave.TryGetDelay(NeedId.Food, 1, out double second));
            Assert.AreEqual(8, second, 1e-9, "Unit 1 was there from 20 s to 40 s.");

            Assert.IsFalse(wave.TryGetDelay(NeedId.Rooms, 0, out _), "Rooms isn't in this wave.");
            Assert.IsFalse(wave.TryGetDelay(NeedId.Food, -1, out _));
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
        public void AC1b_AFreeSeatMeansNoQueueButSlowServiceStillCountsAsDelay()
        {
            WaveSimulation wave = HandServedWave(30, 3, (NeedId.Food, 3)); // arrivals at 10, 20 and 30 s, 3 free seats

            wave.AdvanceTo(22);
            Assert.IsTrue(wave.FinishService(NeedId.Food)); // unit 0: 12 s, a staffed service
            wave.AdvanceTo(56);
            Assert.IsTrue(wave.FinishService(NeedId.Food)); // unit 1: 36 s, a service without a Worker
            Assert.IsTrue(wave.FinishService(NeedId.Food)); // unit 2: 26 s

            CollectionAssert.AreEqual(new double[] { 0, 24, 14 }, wave.GetDelays(NeedId.Food));
            Assert.Less(WaveSatisfaction.Calculate(wave, Settings), 100, "Slow service costs satisfaction even without a queue.");
        }

        [Test]
        public void AWaitForASeatCountsAsDelay()
        {
            WaveSimulation wave = HandServedWave(20, 1, (NeedId.Food, 2)); // arrivals at 10 s and 20 s

            wave.AdvanceTo(22);
            Assert.IsTrue(wave.FinishService(NeedId.Food)); // unit 0 served after 12 s; unit 1 waited 2 s for the seat
            Assert.AreEqual(1, wave.GetDelays(NeedId.Food).Count, "Unit 1 is being served now.");

            wave.AdvanceTo(34);
            Assert.IsTrue(wave.FinishService(NeedId.Food));
            CollectionAssert.AreEqual(new double[] { 0, 2 }, wave.GetDelays(NeedId.Food));
        }

        [Test]
        public void Wave1_GuestsAtTheRestaurantWithoutAWorkerAreNotHappyEvenWithoutAQueue()
        {
            double slowServiceDelay = _config.wave.GetServiceSeconds(false) - _config.wave.GetServiceSeconds(true);

            IReadOnlyList<double> food = GetWave1Delays(NeedId.Food);
            IReadOnlyList<double> rooms = GetWave1Delays(NeedId.Rooms);

            Assert.AreEqual(_config.waves[0].food, food.Count);
            foreach (double delay in food)
            {
                Assert.AreEqual(slowServiceDelay, delay, 1e-3, "No queue at the Restaurant, but its service takes about 24 s longer.");
                Assert.AreNotEqual(GuestMood.Happy, WaveSatisfaction.GetGuestMood(delay, Settings), "A slow service never gets a smile.");
            }

            foreach (double delay in rooms)
            {
                Assert.AreEqual(GuestMood.Happy, WaveSatisfaction.GetGuestMood(delay, Settings), "The staffed Reception serves on time.");
            }
        }

        [Test]
        public void OceanViewWeighsMoreThanTheOtherNeeds()
        {
            WaveSimulation wave = OceanViewWaveServedByHand();

            // (100 × 1 + 100 × 1.5 + 25 × 1.5) ÷ (1 + 1.5 + 1.5) = 71.875; unweighted it would be 75.
            Assert.AreEqual(71.875, WaveSatisfaction.Calculate(wave, Settings), 1e-6);
        }

        [Test]
        public void LiveSatisfaction_TheVisitsSeenSoFarUseTheWaveFormula()
        {
            WaveSimulation wave = OceanViewWaveServedByHand();

            var seen = new List<(NeedId, double)> { (NeedId.Rooms, 0), (NeedId.OceanView, 0), (NeedId.OceanView, 32) };
            Assert.AreEqual(WaveSatisfaction.Calculate(wave, Settings), WaveSatisfaction.Calculate(seen, Settings), 1e-6,
                "Once every visit is seen, the live bar ends on the wave's result.");
            Assert.AreEqual(100, WaveSatisfaction.Calculate(new List<(NeedId, double)>(), Settings), 1e-6, "Nobody waited yet.");
            Assert.AreEqual(Settings.waveFloor, WaveSatisfaction.Calculate(new[] { (NeedId.Food, 120.0) }, Settings), 1e-6,
                "The wave floor holds for the live bar too.");
        }

        [Test]
        public void GuestFeedback_GladWhenServedQuicklyAnnoyedAfterALongWait()
        {
            var game = new ResortGame(_config);
            var happy = new HashSet<string>();
            var unhappy = new HashSet<string>();
            for (int number = 1; number <= 6; number++)
            {
                happy.Add(GameText.GuestFeedback(game, NeedId.Food, GuestMood.Happy, number));
                unhappy.Add(GameText.GuestFeedback(game, NeedId.Food, GuestMood.Unhappy, number));
                Assert.IsNotEmpty(GameText.GuestFeedback(game, NeedId.Food, GuestMood.Neutral, number));
            }

            Assert.Greater(happy.Count, 1, "Guests in a row don't all say the same.");
            CollectionAssert.IsEmpty(happy.Intersect(unhappy), "A happy guest never says what an annoyed one says.");
            Assert.IsTrue(happy.Concat(unhappy).All(line => line.StartsWith("\"") && line.EndsWith("\"")), "Quoted, like OUR-38's complaint.");
            Assert.IsTrue(happy.Any(line => line.Contains("Restaurant")), "Some lines name the building the guest left.");
            Assert.IsTrue(happy.Concat(unhappy).All(line => line.Length <= 40), "Each fits on one line of the side panel.");
        }

        [Test]
        public void AC4b_WhenEveryoneWaitedLongTheWaveFloorGivesOneStar()
        {
            WaveSimulation longWait = HandServedWave(1, 1, (NeedId.Drinks, 10)); // all arrive within 1 s, 1 seat
            for (int served = 0; served < 10; served++)
            {
                longWait.AdvanceTo(longWait.Time + 100);
                Assert.IsTrue(longWait.FinishService(NeedId.Drinks));
            }

            double satisfaction = WaveSatisfaction.Calculate(longWait, Settings);
            Assert.AreEqual(10, longWait.GetDelays(NeedId.Drinks).Count);
            Assert.Less(Settings.unitFloor, Settings.waveFloor, "Every guest counts only the unit floor (25 %), below the wave floor.");
            Assert.AreEqual(Settings.waveFloor, satisfaction, 1e-6, "Wave floor: never below 40 %.");
            Assert.AreEqual(WaveRating.OneStar, WaveSatisfaction.GetRating(satisfaction, Settings));
        }

        [Test]
        public void Wave1WithTheRestaurantLeftWithoutAWorkerIsNotPerfect()
        {
            WaveResult result = PlayWave(new ResortGame(_config), 0.1);

            Assert.Less(result.Satisfaction, 100, "The Restaurant's slow service costs satisfaction.");
            Assert.Less(result.Rating, WaveRating.Perfect);
            Assert.GreaterOrEqual(result.Rating, WaveRating.ThreeStars, "No queue anywhere, so it stays a good wave.");
        }

        [Test]
        public void Wave1IsPerfectWhenNoBuildingIsSlow()
        {
            _config.wave.utilizationEmpty = _config.wave.utilizationStaffed; // a building without a Worker serves at full speed

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
