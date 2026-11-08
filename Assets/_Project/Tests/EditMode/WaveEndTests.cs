using System.Collections.Generic;
using NUnit.Framework;
using ResortEmpire.Core;
using ResortEmpire.Editor;

namespace ResortEmpire.Tests
{
    /// <summary>
    /// OUR-41 (serving the last guests after the 90 s arrivals) and OUR-35 (the wave ends once everyone is served,
    /// then WAVE_RESOLVING → WAVE_COMPLETE when the guests are back on the boat). Service timing is OUR-46, so these
    /// tests finish services directly with <see cref="WaveSimulation.FinishService"/>.
    /// </summary>
    public class WaveEndTests
    {
        private static readonly NeedId[] IslandNeeds = { NeedId.Rooms, NeedId.Drinks, NeedId.Food };

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

        private static void ServeEveryone(ResortGame game)
        {
            foreach (NeedId need in IslandNeeds)
            {
                while (game.CurrentWave.FinishService(need)) { }
            }
        }

        [Test]
        public void FinishServiceFreesTheSeatForTheFirstInLine()
        {
            ResortGame game = StartedGame();
            game.Tick(90);
            Assert.AreEqual(3, game.GetNeedStatus(NeedId.Rooms).Seated);
            Assert.AreEqual(5, game.GetNeedStatus(NeedId.Rooms).Waiting);

            Assert.IsTrue(game.CurrentWave.FinishService(NeedId.Rooms));

            NeedStatus rooms = game.GetNeedStatus(NeedId.Rooms);
            Assert.AreEqual(1, rooms.Served);
            Assert.AreEqual(3, rooms.Seated, "The first in line sits down at once.");
            Assert.AreEqual(4, rooms.Waiting);
            CollectionAssert.AreEqual(new[] { 56.25, 67.5, 78.75, 90 }, game.CurrentWave.GetWaitingArrivalTimes(NeedId.Rooms));
        }

        [Test]
        public void OUR41_AfterTheTimerNobodyNewComesButNobodyLeaves()
        {
            _config.waves[0].food = 20; // AC3/AC4: a big Food demand on the empty Restaurant
            ResortGame game = StartedGame();

            game.Tick(90.5);

            NeedStatus food = game.GetNeedStatus(NeedId.Food);
            Assert.IsTrue(game.CurrentWave.ArrivalsClosed);
            Assert.IsFalse(game.CurrentWave.IsFinished);
            Assert.AreEqual(GamePhase.WaveActive, game.Phase, "The timer closes arrivals, never the wave (WR3).");
            Assert.AreEqual(20, food.Total);
            Assert.Greater(food.Waiting, 0, "Guests still queue after 90 s.");

            game.Tick(10_000);
            Assert.AreEqual(GamePhase.WaveActive, game.Phase, "Nobody is sent home: the wave runs until everyone is served (WR4).");
            Assert.AreEqual(20, game.GetNeedStatus(NeedId.Food).Total);
        }

        [Test]
        public void OUR41_IsFinishedOnlyWhenTheLastGuestIsServed()
        {
            ResortGame game = StartedGame();
            game.Tick(90);

            foreach (NeedId need in IslandNeeds)
            {
                while (game.GetNeedStatus(need).Seated > 1) game.CurrentWave.FinishService(need);
            }

            Assert.IsFalse(game.CurrentWave.IsFinished, "Someone is still seated.");

            ServeEveryone(game);
            Assert.IsTrue(game.CurrentWave.IsFinished);
            Assert.AreEqual(22, game.CurrentWave.TotalServed);
        }

        [Test]
        public void OUR35_WaveResolvesWhenEveryoneIsServedThenCompletesWhenGuestsHaveBoarded()
        {
            ResortGame game = StartedGame();
            var phases = new List<GamePhase>();
            game.PhaseChanged += (from, to) => phases.Add(to);
            WaveResult completed = null;
            game.WaveCompleted += result => completed = result;

            game.Tick(90);
            ServeEveryone(game);
            game.Tick(0.5);

            Assert.AreEqual(GamePhase.WaveResolving, game.Phase, "Everyone served: the guests head back to the boat.");
            Assert.AreEqual(0, game.CompletedWaves);
            Assert.IsNull(completed);

            Assert.IsTrue(game.FinishResolving(), "The last guest boarded.");

            Assert.AreEqual(GamePhase.WaveComplete, game.Phase);
            CollectionAssert.AreEqual(new[] { GamePhase.WaveResolving, GamePhase.WaveComplete }, phases);
            Assert.AreEqual(1, game.CompletedWaves);
            Assert.AreSame(game.LastWaveResult, completed);
            Assert.AreEqual(1, completed.WaveNumber);
            Assert.AreEqual(22, completed.UnitsServed);
            Assert.AreEqual(completed.TotalUnits, completed.UnitsServed, "Everyone is always served.");
            Assert.AreEqual(90.5, completed.Duration, 1e-6);
            Assert.AreEqual(2, game.GetNextWaveForecast().WaveNumber, "The forecast moves on to wave 2.");
        }

        [Test]
        public void OUR35_TheWaveDoesNotEndWhileGuestsAreWaiting()
        {
            ResortGame game = StartedGame();
            game.Tick(89);
            ServeEveryone(game); // everyone so far, but arrivals are still open

            game.Tick(0.5);
            Assert.AreEqual(GamePhase.WaveActive, game.Phase, "Arrivals are still open.");

            game.Tick(0.5);
            Assert.AreEqual(GamePhase.WaveActive, game.Phase, "The last guests just arrived and wait to be served.");
            Assert.IsFalse(game.FinishResolving(), "Only a resolving wave can complete.");
        }

        [Test]
        public void OUR41_AWaveWithoutDemandEndsWhenArrivalsClose()
        {
            WaveDefinition wave = _config.waves[0];
            wave.rooms = wave.drinks = wave.food = 0;
            ResortGame game = StartedGame();

            game.Tick(89.9);
            Assert.AreEqual(GamePhase.WaveActive, game.Phase);

            game.Tick(0.1);
            Assert.AreEqual(GamePhase.WaveResolving, game.Phase);
        }
    }
}
