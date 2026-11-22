using System;
using System.Collections.Generic;
using NUnit.Framework;
using ResortEmpire.Core;
using ResortEmpire.Editor;
using Object = UnityEngine.Object;

namespace ResortEmpire.Tests
{
    /// <summary>The tutorial's steps (<see cref="TutorialScript"/>) on the practice wave, driven through the Core game.</summary>
    public class TutorialScriptTests
    {
        private const double Step = 0.25;

        private readonly List<Object> _created = new List<Object>();
        private ResortGame _game;
        private TutorialScript _script;

        [SetUp]
        public void CreateGame()
        {
            TutorialConfig tutorial = Track(TutorialConfigDefaults.Create());
            _game = new ResortGame(Track(tutorial.CreatePracticeConfig(Track(PrototypeConfigDefaults.Create()))));
            _script = new TutorialScript(_game);
        }

        [TearDown]
        public void DestroyCreated()
        {
            _script.Dispose();
            foreach (Object created in _created)
            {
                if (created != null) Object.DestroyImmediate(created);
            }

            _created.Clear();
        }

        private T Track<T>(T created) where T : Object
        {
            _created.Add(created);
            return created;
        }

        /// <summary>Plays the game in small steps, updating the script each time, until <paramref name="done"/> holds.</summary>
        private void PlayUntil(Func<bool> done, double maxSeconds = 600)
        {
            for (double played = 0; !done(); played += Step)
            {
                Assert.Less(played, maxSeconds, "Waited too long.");
                _game.Tick(Step);
                _script.Update();
            }
        }

        private void NextTimes(int count)
        {
            for (int i = 0; i < count; i++)
            {
                Assert.IsTrue(_script.Next(), $"Next on {_script.Step}.");
            }
        }

        [Test]
        public void StartsWithWelcomeAndInfoStepsWaitForNext()
        {
            Assert.AreEqual(TutorialStep.Welcome, _script.Step);
            Assert.AreEqual(TutorialStepKind.Info, _script.Kind);

            _script.Update();
            Assert.AreEqual(TutorialStep.Welcome, _script.Step, "Nothing moves on by itself.");

            NextTimes(4);
            Assert.AreEqual(TutorialStep.MoveWorker, _script.Step);
            Assert.AreEqual(TutorialStepKind.Action, _script.Kind);
            Assert.IsFalse(_script.Next(), "An action step ends only with the action.");
            Assert.IsFalse(_script.PausesGame, "Before the wave nothing runs, so nothing needs pausing.");
        }

        [Test]
        public void MoveWorkerEndsWithAnyMoveMadeDuringIt()
        {
            Assert.AreEqual(WorkerMoveResult.Moved, _game.MoveWorker(BuildingId.Reception, BuildingId.Restaurant));
            NextTimes(4);
            Assert.AreEqual(TutorialStep.MoveWorker, _script.Step, "A move before the step doesn't count.");

            Assert.AreEqual(WorkerMoveResult.Moved, _game.MoveWorker(BuildingId.BeachBar, BuildingId.Reception));
            _script.Update();
            Assert.AreEqual(TutorialStep.WelcomeGuests, _script.Step);
        }

        [Test]
        public void WhileMovingAWorkerOnlyTheEmptyBuildingOpens()
        {
            Assert.IsTrue(_script.AllowsWorkerSheet(BuildingId.Reception), "Info steps don't limit it (their dim stops taps).");

            NextTimes(4);
            Assert.AreEqual(TutorialStep.MoveWorker, _script.Step);
            Assert.AreEqual(BuildingId.Restaurant, _script.FocusBuilding, "The bubble points at the building without a Worker.");
            Assert.IsTrue(_script.AllowsWorkerSheet(BuildingId.Restaurant));
            Assert.IsFalse(_script.AllowsWorkerSheet(BuildingId.Reception), "A building with its Worker stays shut.");
            Assert.IsFalse(_script.AllowsWorkerSheet(BuildingId.BeachBar));
            Assert.IsFalse(_script.AllowsWaveStart, "Welcome Guests waits for the move.");

            Assert.AreEqual(WorkerMoveResult.Moved, _game.MoveWorker(BuildingId.Reception, BuildingId.Restaurant));
            _script.Update();
            Assert.AreEqual(TutorialStep.WelcomeGuests, _script.Step);
            Assert.IsTrue(_script.AllowsWaveStart);
            Assert.IsFalse(_script.AllowsWorkerSheet(BuildingId.Reception), "Now only Welcome Guests works.");
            Assert.IsNull(_script.FocusBuilding);
        }

        [Test]
        public void DuringTheOverloadOnlyTheBuildingFallingBehindOpens()
        {
            NextTimes(4);
            Assert.AreEqual(WorkerMoveResult.Moved, _game.MoveWorker(BuildingId.Reception, BuildingId.Restaurant));
            _script.Update();
            Assert.IsTrue(_game.StartWave());
            _script.Update();
            Assert.IsTrue(_script.AllowsWorkerSheet(BuildingId.BeachBar), "Hints don't limit it.");

            PlayUntil(() => _script.Step == TutorialStep.NeedsPanel);
            NextTimes(1);
            PlayUntil(() => _script.Step == TutorialStep.Overload);
            Assert.AreEqual(BuildingId.Reception, _script.FocusBuilding);
            Assert.IsTrue(_script.AllowsWorkerSheet(BuildingId.Reception));
            Assert.IsFalse(_script.AllowsWorkerSheet(BuildingId.Restaurant));
            Assert.IsFalse(_script.AllowsWorkerSheet(BuildingId.BeachBar));

            Assert.AreEqual(WorkerMoveResult.Moved, _game.MoveWorker(BuildingId.BeachBar, BuildingId.Reception));
            _script.Update();
            Assert.AreEqual(TutorialStep.CatchingUp, _script.Step);
            Assert.AreEqual(BuildingId.Reception, _script.FocusBuilding, "The bubble still talks about the Reception.");
        }

        [Test]
        public void StartingTheWaveEarlySkipsTheStepsBeforeIt()
        {
            Assert.IsTrue(_game.StartWave());
            _script.Update();

            Assert.AreEqual(TutorialStep.BoatArriving, _script.Step);
            Assert.AreEqual(TutorialStepKind.Hint, _script.Kind);
        }

        [Test]
        public void ThePracticeWaveWalksThroughEveryStep()
        {
            var steps = new List<TutorialStep> { _script.Step };
            _script.StepChanged += steps.Add;

            NextTimes(4);
            Assert.AreEqual(WorkerMoveResult.Moved, _game.MoveWorker(BuildingId.Reception, BuildingId.Restaurant));
            _script.Update();
            Assert.IsTrue(_game.StartWave());
            _script.Update();

            PlayUntil(() => _script.Step == TutorialStep.NeedsPanel);
            Assert.AreEqual(GamePhase.WaveActive, _game.Phase);
            Assert.IsTrue(_script.PausesGame, "The wave waits while the player reads about the panel.");
            NextTimes(1);
            Assert.IsFalse(_script.PausesGame);

            PlayUntil(() => _script.Step == TutorialStep.Overload);
            Assert.AreEqual(BuildingId.Reception, _script.OverloadedBuilding, "Reception lost its Worker, so it falls behind.");
            Assert.IsTrue(_script.PausesGame, "The wave waits for the fix.");
            _script.Update();
            Assert.AreEqual(TutorialStep.Overload, _script.Step, "It waits for a Worker at the Reception.");

            Assert.AreEqual(WorkerMoveResult.Moved, _game.MoveWorker(BuildingId.BeachBar, BuildingId.Reception));
            _script.Update();
            Assert.AreEqual(TutorialStep.CatchingUp, _script.Step);
            Assert.AreEqual(NeedState.Recovering, _game.GetBuildingState(BuildingId.Reception));
            NextTimes(1);

            PlayUntil(() => _game.Phase == GamePhase.WaveResolving);
            Assert.AreEqual(TutorialStep.FinishWave, _script.Step);
            Assert.IsTrue(_game.FinishResolving()); // the island does this once the guests are back on the boat
            _script.Update();
            Assert.AreEqual(TutorialStep.Result, _script.Step);
            Assert.AreEqual(TutorialStepKind.Final, _script.Kind);

            Assert.IsTrue(_script.Finish());
            Assert.IsTrue(_script.IsFinished);
            CollectionAssert.AreEqual(
                new[]
                {
                    TutorialStep.Welcome, TutorialStep.Buildings, TutorialStep.Workers, TutorialStep.Forecast,
                    TutorialStep.MoveWorker, TutorialStep.WelcomeGuests, TutorialStep.BoatArriving, TutorialStep.NeedsPanel,
                    TutorialStep.WatchQueues, TutorialStep.Overload, TutorialStep.CatchingUp, TutorialStep.FinishWave,
                    TutorialStep.Result, TutorialStep.Finished,
                },
                steps);
        }

        [Test]
        public void AWaveWithoutAnOverloadGoesStraightToTheResult()
        {
            _game.Config.waves[0].rooms = 2;
            _game.Config.waves[0].drinks = 2;
            _game.Config.waves[0].food = 2;

            Assert.IsTrue(_game.StartWave());
            _script.Update();
            PlayUntil(() => _script.Step == TutorialStep.NeedsPanel);
            NextTimes(1);
            PlayUntil(() => _game.Phase == GamePhase.WaveResolving);
            Assert.AreEqual(TutorialStep.FinishWave, _script.Step);
            Assert.IsNull(_script.OverloadedBuilding);

            Assert.IsTrue(_game.FinishResolving());
            _script.Update();
            Assert.AreEqual(TutorialStep.Result, _script.Step);
        }

        [Test]
        public void FinishWorksOnlyOnTheLastStep()
        {
            Assert.IsFalse(_script.Finish());
            Assert.AreEqual(TutorialStep.Welcome, _script.Step);
        }
    }
}
