using NUnit.Framework;
using ResortEmpire.Core;
using ResortEmpire.Editor;

namespace ResortEmpire.Tests
{
    /// <summary>
    /// OUR-45: moving Workers before and during the wave. Baseline: Reception and Beach Bar have a Worker, the
    /// Restaurant doesn't; service 0.33 s with a Worker, 1 s without; Food unit 0 arrives at 15 s.
    /// </summary>
    public class WorkerMoveTests
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

        [Test]
        public void MovingAWorkerBeforeTheWaveLeavesTheSourceEmpty()
        {
            var game = new ResortGame(_config);
            BuildingId movedFrom = default, movedTo = default;
            game.WorkerMoved += (from, to) => (movedFrom, movedTo) = (from, to);

            Assert.AreEqual(WorkerMoveResult.Moved, game.MoveWorker(BuildingId.Reception, BuildingId.Restaurant));

            Assert.IsFalse(game.GetBuilding(BuildingId.Reception).HasWorker, "AC2b: the source is left without a Worker.");
            Assert.IsTrue(game.GetBuilding(BuildingId.Restaurant).HasWorker);
            Assert.AreEqual(2, game.WorkerCount, "Moving never creates or removes Workers.");
            Assert.AreEqual(BuildingId.Reception, movedFrom);
            Assert.AreEqual(BuildingId.Restaurant, movedTo);
        }

        [Test]
        public void RefusedMovesChangeNothing()
        {
            var game = new ResortGame(_config);

            Assert.AreEqual(WorkerMoveResult.SameBuilding, game.MoveWorker(BuildingId.Reception, BuildingId.Reception));
            Assert.AreEqual(WorkerMoveResult.NoWorkerAtSource, game.MoveWorker(BuildingId.Restaurant, BuildingId.Reception));
            Assert.AreEqual(WorkerMoveResult.TargetFull, game.MoveWorker(BuildingId.Reception, BuildingId.BeachBar));
            Assert.AreEqual(WorkerMoveResult.BuildingNotOnIsland, game.MoveWorker(BuildingId.Reception, BuildingId.Pool));

            Assert.IsTrue(game.GetBuilding(BuildingId.Reception).HasWorker);
            Assert.IsTrue(game.GetBuilding(BuildingId.BeachBar).HasWorker);
            Assert.IsFalse(game.GetBuilding(BuildingId.Restaurant).HasWorker);
        }

        [Test]
        public void WorkersCanMoveWhileTheBoatComesInAndDuringTheWave()
        {
            _config.wave.incomingDuration = 3f;
            var game = new ResortGame(_config);
            game.StartWave();

            Assert.AreEqual(GamePhase.WaveIncoming, game.Phase);
            Assert.AreEqual(WorkerMoveResult.Moved, game.MoveWorker(BuildingId.Reception, BuildingId.Restaurant));

            game.Tick(3f + 50f);
            Assert.AreEqual(GamePhase.WaveActive, game.Phase);
            Assert.AreEqual(WorkerMoveResult.Moved, game.MoveWorker(BuildingId.Restaurant, BuildingId.Reception));
        }

        [Test]
        public void WorkersCannotMoveOnceTheWaveIsOver()
        {
            var game = new ResortGame(_config);
            game.StartWave();
            game.Tick(95);
            Assert.AreEqual(GamePhase.WaveResolving, game.Phase);
            Assert.IsFalse(game.CanMoveWorkers);
            Assert.AreEqual(WorkerMoveResult.NotAllowedNow, game.MoveWorker(BuildingId.Reception, BuildingId.Restaurant));

            game.FinishResolving();
            Assert.AreEqual(WorkerMoveResult.NotAllowedNow, game.MoveWorker(BuildingId.Reception, BuildingId.Restaurant));
        }

        [Test]
        public void AMoveDuringTheWaveChangesTheSpeedAtOnceForSeatedGuests()
        {
            var game = new ResortGame(_config);
            game.StartWave();
            game.Tick(15.5); // the Food guest at the empty Restaurant is half done (0.5 s of 1 s)

            game.MoveWorker(BuildingId.Reception, BuildingId.Restaurant); // the rest takes 0.5 × 0.33 = 0.165 s
            Assert.AreEqual(1.0, game.GetServiceSeconds(BuildingId.Reception), 1e-6, "The Reception is slow now.");

            game.Tick(0.16);
            Assert.AreEqual(0, game.GetNeedStatus(NeedId.Food).Served);

            game.Tick(0.01);
            Assert.AreEqual(1, game.GetNeedStatus(NeedId.Food).Served);
        }

        [Test]
        public void ThePlacementStaysAfterTheWave()
        {
            var game = new ResortGame(_config);
            game.MoveWorker(BuildingId.BeachBar, BuildingId.Restaurant);

            game.StartWave();
            game.Tick(95);
            game.FinishResolving();

            Assert.AreEqual(GamePhase.WaveComplete, game.Phase);
            Assert.IsFalse(game.GetBuilding(BuildingId.BeachBar).HasWorker);
            Assert.IsTrue(game.GetBuilding(BuildingId.Restaurant).HasWorker);
        }
    }
}
