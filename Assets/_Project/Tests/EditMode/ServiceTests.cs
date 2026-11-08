using System.Linq;
using NUnit.Framework;
using ResortEmpire.Core;
using ResortEmpire.Editor;

namespace ResortEmpire.Tests
{
    /// <summary>
    /// OUR-46 / OUR-47: buildings serve their seated guests, 0.33 s per guest with a Worker and 1 s without
    /// (serviceTime 0.33 ÷ utilization 1.0 / 0.33). Wave 1 baseline: Reception and Beach Bar have a Worker, the
    /// Restaurant doesn't; Rooms unit 0 arrives at 11.25 s, Food unit 0 at 15 s.
    /// </summary>
    public class ServiceTests
    {
        private GameConfig _config;

        [SetUp]
        public void CreateConfig()
        {
            _config = PrototypeConfigDefaults.Create();
            _config.wave.incomingDuration = 0f; // skip the boat so Tick(t) is wave time t
        }

        [TearDown]
        public void DestroyConfig() => UnityEngine.Object.DestroyImmediate(_config);

        private ResortGame StartedGame()
        {
            var game = new ResortGame(_config);
            Assert.IsTrue(game.StartWave());
            return game;
        }

        [Test]
        public void ServiceTakesAThirdOfASecondWithAWorkerAndOneSecondWithout()
        {
            var game = new ResortGame(_config);

            Assert.AreEqual(0.33, game.GetServiceSeconds(BuildingId.Reception), 1e-6);
            Assert.AreEqual(1.0, game.GetServiceSeconds(BuildingId.Restaurant), 1e-6);
        }

        [Test]
        public void OUR46_AStaffedBuildingServesAGuestIn033Seconds()
        {
            ResortGame game = StartedGame();

            game.Tick(11.25);
            Assert.AreEqual(1, game.GetSeatedCount(BuildingId.Reception), "Seated on arrival.");

            game.Tick(0.32);
            Assert.AreEqual(0, game.GetNeedStatus(NeedId.Rooms).Served);

            game.Tick(0.02);
            NeedStatus rooms = game.GetNeedStatus(NeedId.Rooms);
            Assert.AreEqual(1, rooms.Served, "Served 0.33 s after sitting down.");
            Assert.AreEqual(0, rooms.Seated, "The guest left its seat.");
        }

        [Test]
        public void OUR47_AnEmptyBuildingServesAGuestInOneSecond()
        {
            ResortGame game = StartedGame();
            Assert.IsFalse(game.GetBuilding(BuildingId.Restaurant).HasWorker);

            game.Tick(15);
            Assert.AreEqual(1, game.GetSeatedCount(BuildingId.Restaurant));

            game.Tick(0.99);
            Assert.AreEqual(0, game.GetNeedStatus(NeedId.Food).Served);

            game.Tick(0.02);
            Assert.AreEqual(1, game.GetNeedStatus(NeedId.Food).Served, "Served 1 s after sitting down.");
            Assert.AreEqual(0, game.GetSeatedCount(BuildingId.Restaurant));
        }

        [Test]
        public void AWorkerArrivingSpeedsUpGuestsAlreadySeated()
        {
            ResortGame game = StartedGame();
            game.Tick(15.5); // the Food guest is half done (0.5 s of 1 s)

            game.GetBuilding(BuildingId.Restaurant).HasWorker = true; // the rest takes 0.5 × 0.33 = 0.165 s

            game.Tick(0.16);
            Assert.AreEqual(0, game.GetNeedStatus(NeedId.Food).Served);

            game.Tick(0.01);
            Assert.AreEqual(1, game.GetNeedStatus(NeedId.Food).Served);
        }

        [Test]
        public void WhenASeatFreesTheFirstInLineSitsDown()
        {
            _config.wave.waveTimer = 1f; // Food 6 arrives every 1/6 s on the empty Restaurant (1 s per guest, 3 seats)
            ResortGame game = StartedGame();

            game.Tick(1.0);
            NeedStatus food = game.GetNeedStatus(NeedId.Food);
            Assert.AreEqual(3, food.Seated);
            Assert.AreEqual(3, food.Waiting);
            Assert.AreEqual(0, food.Served);

            game.Tick(0.2); // unit 0 (seated at 1/6 s) is done at 7/6 s; unit 3 takes its seat
            food = game.GetNeedStatus(NeedId.Food);
            Assert.AreEqual(1, food.Served);
            Assert.AreEqual(3, food.Seated);
            Assert.AreEqual(2, food.Waiting);
            CollectionAssert.AreEqual(new[] { 5 / 6.0, 1.0 }, game.CurrentWave.GetWaitingArrivalTimes(NeedId.Food).ToArray());
        }

        [Test]
        public void EveryoneIsServedAndTheWaveResolves()
        {
            ResortGame game = StartedGame();

            game.Tick(89.9);
            Assert.AreEqual(GamePhase.WaveActive, game.Phase);

            game.Tick(1.5); // the last units arrive at 90 s and need at most 1 s
            Assert.AreEqual(GamePhase.WaveResolving, game.Phase);
            Assert.AreEqual(22, game.LastWaveResult.UnitsServed);
        }

        [Test]
        public void ServiceDoesNotDependOnTheFrameRate()
        {
            _config.wave.waveTimer = 5f; // queues form, so seats free up in the middle of big steps
            ResortGame smooth = StartedGame();
            ResortGame choppy = StartedGame();

            foreach (double checkpoint in new[] { 0.5, 1.3, 2.2, 3.7, 5, 6.1, 8 })
            {
                RunTo(smooth, checkpoint, 1.0 / 60.0);
                RunTo(choppy, checkpoint, 0.7);

                foreach (NeedId need in new[] { NeedId.Rooms, NeedId.Drinks, NeedId.Food })
                {
                    NeedStatus a = smooth.GetNeedStatus(need);
                    NeedStatus b = choppy.GetNeedStatus(need);
                    Assert.AreEqual(a.Served, b.Served, $"{need} served at {checkpoint} s");
                    Assert.AreEqual(a.Seated, b.Seated, $"{need} seated at {checkpoint} s");
                    Assert.AreEqual(a.Waiting, b.Waiting, $"{need} waiting at {checkpoint} s");
                }
            }
        }

        [Test]
        public void ValidatorRejectsAZeroUtilizationWithoutAWorker()
        {
            _config.wave.utilizationEmpty = 0f;

            Assert.That(GameConfigValidator.Validate(_config).Any(e => e.Contains("WR2")));
        }

        private static void RunTo(ResortGame game, double waveTime, double step)
        {
            // Time only runs while the wave is active; once it resolves (everyone served) stop, or this never ends.
            while (game.Phase == GamePhase.WaveActive && game.WaveTime + step <= waveTime) game.Tick(step);
            double rest = waveTime - game.WaveTime;
            if (rest > 0 && game.Phase == GamePhase.WaveActive) game.Tick(rest);
        }
    }
}
