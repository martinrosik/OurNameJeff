using System.Linq;
using NUnit.Framework;
using ResortEmpire.Core;
using ResortEmpire.Editor;

namespace ResortEmpire.Tests
{
    /// <summary>
    /// OUR-34: guests arrive evenly over the 90 s arrival phase (Build doc §4.4). Uses the Build doc baseline
    /// (PrototypeConfigDefaults): wave 1 is Rooms 8, Drinks 8, Food 6, 3 seats per building.
    /// </summary>
    public class WaveArrivalTests
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
            Assert.AreEqual(GamePhase.WaveActive, game.Phase);
            return game;
        }

        [Test]
        public void ConfigHasA90SecondWaveTimer()
        {
            Assert.AreEqual(90f, _config.wave.waveTimer);
        }

        [Test]
        public void ArrivalTimesForDemand8Over90SecondsAreEvenlySpaced()
        {
            ResortGame game = StartedGame();

            double[] times = Enumerable.Range(0, 8).Select(k => game.CurrentWave.GetArrivalTime(NeedId.Rooms, k)).ToArray();

            CollectionAssert.AreEqual(new[] { 11.25, 22.5, 33.75, 45, 56.25, 67.5, 78.75, 90 }, times);
        }

        [Test]
        public void ArrivedCountFollowsTheSchedule()
        {
            ResortGame game = StartedGame();
            Assert.AreEqual(0, game.GetNeedStatus(NeedId.Rooms).Arrived, "Nobody arrives at wave time 0.");

            game.Tick(45);
            Assert.AreEqual(4, game.GetNeedStatus(NeedId.Rooms).Arrived, "floor(45 x 8 / 90) = 4");

            game.Tick(45);
            Assert.AreEqual(8, game.GetNeedStatus(NeedId.Rooms).Arrived);

            game.Tick(110);
            Assert.AreEqual(8, game.GetNeedStatus(NeedId.Rooms).Arrived, "Nobody arrives after 90 s.");
        }

        [Test]
        public void EveryNeedGetsItsForecastDemandBy90Seconds()
        {
            ResortGame game = StartedGame();

            game.Tick(89.9);
            Assert.IsFalse(game.CurrentWave.ArrivalsClosed);
            Assert.AreEqual(7, game.GetNeedStatus(NeedId.Rooms).Arrived);
            Assert.AreEqual(5, game.GetNeedStatus(NeedId.Food).Arrived);

            game.Tick(0.1);
            Assert.IsTrue(game.CurrentWave.ArrivalsClosed);
            Assert.AreEqual(8, game.GetNeedStatus(NeedId.Rooms).Arrived);
            Assert.AreEqual(8, game.GetNeedStatus(NeedId.Drinks).Arrived);
            Assert.AreEqual(6, game.GetNeedStatus(NeedId.Food).Arrived);
            Assert.AreEqual(GamePhase.WaveActive, game.Phase, "The timer closes arrivals, never the wave (WR3).");
        }

        [Test]
        public void NothingArrivesWhileTheBoatIsSailingIn()
        {
            _config.wave.incomingDuration = 3f;
            var game = new ResortGame(_config);
            game.StartWave();

            game.Tick(2.9);
            Assert.AreEqual(GamePhase.WaveIncoming, game.Phase);
            Assert.AreEqual(0, game.GetNeedStatus(NeedId.Rooms).Arrived);

            game.Tick(0.1 + 11.25);
            Assert.AreEqual(1, game.GetNeedStatus(NeedId.Rooms).Arrived, "Arrival time counts from WAVE_ACTIVE.");
        }

        [Test]
        public void ArrivalsFillTheSeatsThenWaitInAQueue()
        {
            _config.wave.serviceTime = 1_000_000f; // nobody is served in this test, so the queue builds up
            ResortGame game = StartedGame();

            game.Tick(90);

            NeedStatus rooms = game.GetNeedStatus(NeedId.Rooms);
            Assert.AreEqual(8, rooms.Total);
            Assert.AreEqual(3, rooms.Seated, "3 seats at level 1.");
            Assert.AreEqual(5, rooms.Waiting);
            Assert.AreEqual(0, rooms.Served, "Service is switched off in this test.");
            Assert.AreEqual(3, game.GetSeatedCount(BuildingId.Reception));

            CollectionAssert.AreEqual(
                new[] { 45, 56.25, 67.5, 78.75, 90 },
                game.CurrentWave.GetWaitingArrivalTimes(NeedId.Rooms),
                "The queue keeps each unit's arrival time, oldest first.");
        }

        [Test]
        public void NeedsWithoutABuildingNeverSpawn()
        {
            _config.waves[0].relax = 10; // the table lists Relax, but there is no Pool yet (WR9)
            ResortGame game = StartedGame();

            game.Tick(200);

            NeedStatus relax = game.GetNeedStatus(NeedId.Relax);
            Assert.AreEqual(0, relax.Total);
            Assert.AreEqual(0, relax.Arrived);
            Assert.That(game.CurrentWave.Needs, Has.No.Member(NeedId.Relax));
            Assert.IsTrue(game.CurrentWave.ArrivalsClosed);
        }

        [Test]
        public void ResultsDoNotDependOnTheFrameRate()
        {
            ResortGame smooth = StartedGame();
            ResortGame choppy = StartedGame();

            foreach (double checkpoint in new[] { 5, 11.25, 30, 45, 61.3, 89.99, 90, 120 })
            {
                RunTo(smooth, checkpoint, 1.0 / 60.0);
                RunTo(choppy, checkpoint, 0.7);

                foreach (NeedId need in new[] { NeedId.Rooms, NeedId.Drinks, NeedId.Food })
                {
                    NeedStatus a = smooth.GetNeedStatus(need);
                    NeedStatus b = choppy.GetNeedStatus(need);
                    Assert.AreEqual(a.Arrived, b.Arrived, $"{need} arrived at {checkpoint} s");
                    Assert.AreEqual(a.Seated, b.Seated, $"{need} seated at {checkpoint} s");
                    Assert.AreEqual(a.Waiting, b.Waiting, $"{need} waiting at {checkpoint} s");
                }
            }
        }

        [Test]
        public void ValidatorRejectsAZeroWaveTimer()
        {
            _config.wave.waveTimer = 0f;

            Assert.That(GameConfigValidator.Validate(_config).Any(e => e.Contains("Wave timer")));
        }

        /// <summary>Ticks in steps of <paramref name="step"/>, then one short step to land on <paramref name="waveTime"/>.</summary>
        private static void RunTo(ResortGame game, double waveTime, double step)
        {
            // Time only runs while the wave is active; once it resolves (everyone served) stop, or this never ends.
            while (game.Phase == GamePhase.WaveActive && game.WaveTime + step <= waveTime) game.Tick(step);
            double rest = waveTime - game.WaveTime;
            if (rest > 0 && game.Phase == GamePhase.WaveActive) game.Tick(rest);
        }
    }
}
