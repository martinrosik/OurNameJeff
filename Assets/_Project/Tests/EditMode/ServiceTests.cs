using System.Linq;
using NUnit.Framework;
using ResortEmpire.Core;
using ResortEmpire.Editor;

namespace ResortEmpire.Tests
{
    /// <summary>
    /// OUR-46 / OUR-47: buildings serve their seated guests, 12 s per guest with a Worker and about 36.4 s without
    /// (Build doc: serviceTime 12 ÷ utilization 1.0 / 0.33). Wave 1 baseline: Reception and Beach Bar have a Worker, the
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
        public void ServiceTakes12SecondsWithAWorkerAndAbout36Without()
        {
            var game = new ResortGame(_config);

            Assert.AreEqual(12, game.GetServiceSeconds(BuildingId.Reception), 1e-4);
            Assert.AreEqual(12 / 0.33, game.GetServiceSeconds(BuildingId.Restaurant), 1e-4);
        }

        [Test]
        public void OUR46_AStaffedBuildingServesAGuestIn12Seconds()
        {
            ResortGame game = StartedGame();

            game.Tick(11.25);
            Assert.AreEqual(1, game.GetSeatedCount(BuildingId.Reception), "Seated on arrival.");

            game.Tick(11.99);
            Assert.AreEqual(0, game.GetNeedStatus(NeedId.Rooms).Served);

            game.Tick(0.02);
            NeedStatus rooms = game.GetNeedStatus(NeedId.Rooms);
            Assert.AreEqual(1, rooms.Served, "Served 12 s after sitting down.");
            Assert.AreEqual(1, rooms.Seated, "Its seat is free; only Rooms unit 1 (arrived at 22.5 s) sits there now.");
        }

        [Test]
        public void OUR47_AnEmptyBuildingServesAGuestInAbout36Seconds()
        {
            ResortGame game = StartedGame();
            Assert.IsFalse(game.GetBuilding(BuildingId.Restaurant).HasWorker);

            game.Tick(15);
            Assert.AreEqual(1, game.GetSeatedCount(BuildingId.Restaurant));

            game.Tick(36.3);
            Assert.AreEqual(0, game.GetNeedStatus(NeedId.Food).Served);

            game.Tick(0.1);
            Assert.AreEqual(1, game.GetNeedStatus(NeedId.Food).Served, "Served 12 / 0.33 = 36.36 s after sitting down.");
            Assert.AreEqual(2, game.GetSeatedCount(BuildingId.Restaurant), "Only Food units 1 and 2 (30 s, 45 s) are still seated.");
        }

        [Test]
        public void AWorkerArrivingSpeedsUpGuestsAlreadySeated()
        {
            ResortGame game = StartedGame();
            game.Tick(15 + 6 / 0.33); // the Food guest is half done (18.18 s of 36.36 s)

            game.GetBuilding(BuildingId.Restaurant).HasWorker = true; // the rest takes half of 12 s = 6 s

            game.Tick(5.99);
            Assert.AreEqual(0, game.GetNeedStatus(NeedId.Food).Served);

            game.Tick(0.02);
            Assert.AreEqual(1, game.GetNeedStatus(NeedId.Food).Served);
        }

        [Test]
        public void WhenASeatFreesTheFirstInLineSitsDown()
        {
            _config.wave.waveTimer = 1f; // Food 6 arrives every 1/6 s on the empty Restaurant (36.36 s per guest, 3 seats)
            ResortGame game = StartedGame();

            game.Tick(1.0);
            NeedStatus food = game.GetNeedStatus(NeedId.Food);
            Assert.AreEqual(3, food.Seated);
            Assert.AreEqual(3, food.Waiting);
            Assert.AreEqual(0, food.Served);

            game.Tick(35.6); // unit 0 (seated at 1/6 s) is done at 36.53 s, unit 1 not before 36.70 s; unit 3 takes the seat
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

            // OUR-35 "within 2 minutes": the last Food guest arrives at 90 s on the empty Restaurant and is served
            // 36.36 s later, so wave 1 ends at about 126 s, as the Build doc says for a well-handled wave.
            game.Tick(36);
            Assert.AreEqual(GamePhase.WaveActive, game.Phase, "The last Food guest is still being served.");

            game.Tick(0.5);
            Assert.AreEqual(GamePhase.WaveResolving, game.Phase);
            Assert.AreEqual(22, game.LastWaveResult.UnitsServed);
            Assert.AreEqual(90 + 12 / 0.33, game.LastWaveResult.Duration, 0.5);
            Assert.LessOrEqual(game.LastWaveResult.Duration, 130, "Wave 1 ends within about 2 minutes.");
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
