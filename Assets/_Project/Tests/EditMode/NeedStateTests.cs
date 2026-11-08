using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ResortEmpire.Core;
using ResortEmpire.Editor;

namespace ResortEmpire.Tests
{
    /// <summary>
    /// OUR-38 / OUR-39: need (building) states of Build doc §4.4, AC1 and AC2, with the Build doc service time
    /// (12 s per guest per seat).
    /// The Restaurant starts without a Worker: 0.33 utilization, 3 seats = 0.0825 guests/s.
    /// </summary>
    public class NeedStateTests
    {
        private GameConfig _config;

        [SetUp]
        public void CreateConfig()
        {
            _config = PrototypeConfigDefaults.Create();
            _config.wave.incomingDuration = 0f;
            _config.wave.serviceTime = 12f;
        }

        [TearDown]
        public void DestroyConfig() => UnityEngine.Object.DestroyImmediate(_config);

        private (ResortGame Game, List<(NeedId Need, NeedState To, double Time, int Waiting)> Changes) Start()
        {
            var game = new ResortGame(_config);
            var changes = new List<(NeedId, NeedState, double, int)>();
            game.NeedStateChanged += (need, from, to) =>
                changes.Add((need, to, game.CurrentWave.Time, game.CurrentWave.GetNeedStatus(need).Waiting));
            Assert.IsTrue(game.StartWave());
            return (game, changes);
        }

        private static void RunTo(ResortGame game, double waveTime, double step)
        {
            while (game.Phase == GamePhase.WaveActive && game.WaveTime + step <= waveTime) game.Tick(step);
            double rest = waveTime - game.WaveTime;
            if (rest > 0 && game.Phase == GamePhase.WaveActive) game.Tick(rest);
        }

        [Test]
        public void ConfigHasTheBuildDocThresholds()
        {
            Assert.AreEqual(4, _config.wave.bottleneckThreshold);
            Assert.AreEqual(3f, _config.wave.bottleneckHysteresis);
        }

        [Test]
        public void StatesBeforeTheWaveAndForNeedsWithoutABuilding()
        {
            var game = new ResortGame(_config);

            Assert.AreEqual(NeedState.Ok, game.GetNeedState(NeedId.Food));
            Assert.AreEqual(NeedState.Locked, game.GetNeedState(NeedId.Relax), "No Pool yet (WR9).");
            Assert.AreEqual(NeedState.Locked, game.GetBuildingState(BuildingId.Pool));
        }

        [Test]
        public void AC1_TheEmptyRestaurantFallsBehindOnFood20()
        {
            _config.waves[0].food = 20;
            (ResortGame game, var changes) = Start();

            RunTo(game, 60, 0.1);

            Assert.AreEqual(NeedState.Bottleneck, game.GetNeedState(NeedId.Food));
            Assert.AreEqual(NeedState.Bottleneck, game.GetBuildingState(BuildingId.Restaurant));
            var entries = changes.Where(c => c.To == NeedState.Bottleneck).ToList();
            Assert.IsNotEmpty(entries);
            foreach (var entry in entries)
            {
                Assert.Greater(entry.Waiting, _config.wave.bottleneckThreshold, "Only a queue above the threshold is a bottleneck.");
            }

            Assert.That(changes.Where(c => c.Need != NeedId.Food), Is.Empty, "Rooms and Drinks (staffed) keep up.");
        }

        [Test]
        public void AStaffedReceptionWithFewGuestsNeverBottlenecks()
        {
            _config.waves[0].rooms = 4;
            (ResortGame game, var changes) = Start();

            RunTo(game, 200, 0.5);

            Assert.That(changes.Where(c => c.Need == NeedId.Rooms && c.To == NeedState.Bottleneck), Is.Empty);
            Assert.AreEqual(NeedState.Satisfied, game.GetNeedState(NeedId.Rooms));
        }

        [Test]
        public void ABottleneckRecoversAfterArrivalsCloseThenCalmsThenIsSatisfied()
        {
            _config.waves[0].food = 20;
            (ResortGame game, var changes) = Start();

            RunTo(game, 600, 0.25);

            CollectionAssert.AreEqual(
                new[] { NeedState.Bottleneck, NeedState.Recovering, NeedState.Ok, NeedState.Satisfied },
                changes.Where(c => c.Need == NeedId.Food).Select(c => c.To),
                "One warning, no flicker, and never straight from BOTTLENECK to SATISFIED.");

            var recovering = changes.First(c => c.Need == NeedId.Food && c.To == NeedState.Recovering);
            Assert.AreEqual(90, recovering.Time, 1e-6, "Catching up once no more guests arrive.");
            var ok = changes.First(c => c.Need == NeedId.Food && c.To == NeedState.Ok);
            Assert.LessOrEqual(ok.Waiting, _config.wave.bottleneckThreshold, "Calm again once the queue is at the threshold.");
            Assert.Greater(game.CurrentWave.GetBottleneckSeconds(NeedId.Food), 0);
        }

        [Test]
        public void AC2_MovingAWorkerThereTurnsTheWarningIntoCatchingUpAtOnce()
        {
            _config.waves[0].food = 20;
            (ResortGame game, var changes) = Start();
            RunTo(game, 60, 0.1);
            Assert.AreEqual(NeedState.Bottleneck, game.GetNeedState(NeedId.Food));

            Assert.AreEqual(WorkerMoveResult.Moved, game.MoveWorker(BuildingId.Reception, BuildingId.Restaurant));

            Assert.AreEqual(NeedState.Recovering, game.GetNeedState(NeedId.Food), "Right after the move, without a Tick.");
            Assert.AreEqual(NeedState.Recovering, changes.Last(c => c.Need == NeedId.Food).To);
            Assert.AreEqual(60, changes.Last(c => c.Need == NeedId.Food).Time, 1e-6);
        }

        [Test]
        public void AC2_ABuildingThatCannotKeepUpEvenWithAWorkerStaysBottlenecked()
        {
            _config.waves[0].food = 30; // 0.33 guests/s; a staffed Restaurant serves 3 / 12 = 0.25/s
            (ResortGame game, _) = Start();
            RunTo(game, 60, 0.1);
            Assert.AreEqual(NeedState.Bottleneck, game.GetNeedState(NeedId.Food));

            game.MoveWorker(BuildingId.Reception, BuildingId.Restaurant);
            RunTo(game, 80, 0.1);

            Assert.AreEqual(NeedState.Bottleneck, game.GetNeedState(NeedId.Food), "Upgrade, not a Worker, is the fix here (AC2d).");
        }

        [Test]
        public void TakingTheWorkerAwayAgainBringsTheWarningBack()
        {
            _config.waves[0].food = 20;
            (ResortGame game, _) = Start();
            RunTo(game, 40, 0.1);
            game.MoveWorker(BuildingId.Reception, BuildingId.Restaurant);
            Assert.AreEqual(NeedState.Recovering, game.GetNeedState(NeedId.Food));

            game.MoveWorker(BuildingId.Restaurant, BuildingId.Reception);
            RunTo(game, 50, 0.1);

            Assert.AreEqual(NeedState.Bottleneck, game.GetNeedState(NeedId.Food), "Not catching up any more and the queue grew.");
        }

        [Test]
        public void WaveOneWithTheDefaultConfigNeverWarnsAndEndsSatisfied()
        {
            (ResortGame game, var changes) = Start();

            RunTo(game, 200, 0.5);

            Assert.That(changes.Where(c => c.To == NeedState.Bottleneck), Is.Empty);
            foreach (NeedId need in new[] { NeedId.Rooms, NeedId.Drinks, NeedId.Food })
            {
                Assert.AreEqual(NeedState.Satisfied, game.GetNeedState(need), need.ToString());
            }
        }

        [Test]
        public void StatesDoNotDependOnTheFrameRate()
        {
            _config.waves[0].food = 20;
            (ResortGame smooth, var smoothChanges) = Start();
            (ResortGame choppy, var choppyChanges) = Start();

            RunTo(smooth, 400, 1.0 / 60.0);
            RunTo(choppy, 400, 0.7);

            Assert.AreEqual(smoothChanges.Count, choppyChanges.Count);
            for (int i = 0; i < smoothChanges.Count; i++)
            {
                Assert.AreEqual(smoothChanges[i].Need, choppyChanges[i].Need);
                Assert.AreEqual(smoothChanges[i].To, choppyChanges[i].To);
                Assert.AreEqual(smoothChanges[i].Time, choppyChanges[i].Time, 1e-6, $"Change {i} at the same wave time.");
            }
        }

        [Test]
        public void ValidatorRejectsAZeroThreshold()
        {
            _config.wave.bottleneckThreshold = 0;

            Assert.That(GameConfigValidator.Validate(_config).Any(e => e.Contains("bottleneckThreshold")));
        }
    }
}
