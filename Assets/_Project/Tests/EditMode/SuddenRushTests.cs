using System.Linq;
using NUnit.Framework;
using ResortEmpire.Core;
using ResortEmpire.Editor;
using UnityEngine;

namespace ResortEmpire.Tests
{
    /// <summary>
    /// Sudden rush (Build doc §4.6, §4.8): at 45 s the building without a Worker gets 6 extra guests over 10 s, on top
    /// of the wave's demand. Always in wave 2; later waves may get one (suddenRushChance), never wave 1 and never a
    /// wave that debuts a need (W4, W7). Wave 2: Rooms 12, Drinks 16, Food 10; the Restaurant starts without a Worker.
    /// </summary>
    public class SuddenRushTests
    {
        private static readonly NeedId[] IslandNeeds = { NeedId.Rooms, NeedId.Drinks, NeedId.Food };

        private GameConfig _config;

        /// <summary>A random source that always returns the same fraction.</summary>
        private sealed class FixedRandom : System.Random
        {
            private readonly double _value;
            public FixedRandom(double value) => _value = value;
            public override double NextDouble() => _value;
            protected override double Sample() => _value;
        }

        [SetUp]
        public void CreateConfig()
        {
            _config = PrototypeConfigDefaults.Create();
            _config.wave.incomingDuration = 0f; // skip the boat so Tick(t) is wave time t
        }

        [TearDown]
        public void DestroyConfig() => Object.DestroyImmediate(_config);

        /// <summary>Plays the current wave to the end and continues to the next one (AWAITING_WAVE).</summary>
        private static void FinishWave(ResortGame game)
        {
            game.Tick(5000);
            Assert.AreEqual(GamePhase.WaveResolving, game.Phase);
            game.FinishResolving();
            game.ContinueAfterWave();
        }

        /// <summary>A game in wave 2, wave time 0.</summary>
        private ResortGame StartWave2(System.Random random = null)
        {
            var game = new ResortGame(_config, random);
            game.StartWave();
            Assert.IsFalse(game.CurrentWave.HasRushPlanned, "Wave 1 never has a Sudden rush.");
            FinishWave(game);
            Assert.IsTrue(game.StartWave());
            Assert.AreEqual(1, game.CurrentWaveIndex);
            return game;
        }

        private static void RunTo(ResortGame game, double waveTime, double step)
        {
            while (game.Phase == GamePhase.WaveActive && game.WaveTime + step <= waveTime) game.Tick(step);
            double rest = waveTime - game.WaveTime;
            if (rest > 0 && game.Phase == GamePhase.WaveActive) game.Tick(rest);
        }

        [Test]
        public void ConfigHasTheBuildDocNumbers()
        {
            Assert.IsTrue(_config.wave.suddenRushEnabled);
            Assert.AreEqual(2, _config.wave.suddenRushWave, "rush_wave");
            Assert.AreEqual(45f, _config.wave.suddenRushTime, "rush_time_s");
            Assert.AreEqual(6, _config.wave.suddenRushUnits, "rush_units");
            Assert.AreEqual(10f, _config.wave.suddenRushDuration, "rush_duration_s");
        }

        [Test]
        public void Wave2AlwaysHasItAt45sOnTheBuildingWithoutAWorker()
        {
            ResortGame game = StartWave2(); // no random source: wave 2 still gets it
            Assert.IsTrue(game.CurrentWave.HasRushPlanned);

            RunTo(game, 44.9, 0.1);
            Assert.IsNull(game.CurrentWave.SuddenRush);

            RunTo(game, 45.1, 0.1);
            SuddenRush rush = game.CurrentWave.SuddenRush;
            Assert.IsNotNull(rush);
            Assert.AreEqual(45, rush.StartTime, 1e-6);
            Assert.AreEqual(BuildingId.Restaurant, rush.Building, "The Restaurant has no Worker.");
            Assert.AreEqual(NeedId.Food, rush.Need);
            Assert.AreEqual(6, rush.Units);
            Assert.AreEqual(GamePhase.WaveActive, game.Phase, "The wave doesn't pause.");
        }

        [Test]
        public void ItAddsSixExtraGuestsOverTenSeconds()
        {
            ResortGame game = StartWave2();
            RunTo(game, 45.1, 0.1);

            Assert.AreEqual(16, game.GetNeedStatus(NeedId.Food).Total, "Food 10 + 6: the total grows (Food x/10 -> x/16).");
            Assert.AreEqual(10, game.GetForecast(1).GetDemand(NeedId.Food), "The forecast never shows it.");

            // Steady Food arrives every 9 s (5 by 45 s); the 6 extra ones every 10/6 s from 45 s.
            RunTo(game, 55, 0.1);
            Assert.AreEqual(5 + 6 + 1, game.GetNeedStatus(NeedId.Food).Arrived, "5 steady + 6 rush by 55 s, plus the steady one at 54 s.");
            Assert.AreEqual(12, game.GetNeedStatus(NeedId.Rooms).Total, "Other needs are untouched.");
            Assert.AreEqual(16, game.GetNeedStatus(NeedId.Drinks).Total);

            double[] times = Enumerable.Range(0, 16).Select(k => game.CurrentWave.GetArrivalTime(NeedId.Food, k)).ToArray();
            CollectionAssert.IsOrdered(times);
            Assert.AreEqual(90, times.Last(), 1e-6, "The last Food guest still comes at 90 s.");
        }

        [Test]
        public void ItFollowsTheWorkersTheDocsExample()
        {
            var game = new ResortGame(_config);
            game.StartWave();
            FinishWave(game);

            // Before wave 2: the Beach Bar's Worker goes to the Restaurant, so the Beach Bar is the empty one.
            Assert.AreEqual(WorkerMoveResult.Moved, game.MoveWorker(BuildingId.BeachBar, BuildingId.Restaurant));
            game.StartWave();
            RunTo(game, 45.1, 0.1);

            Assert.AreEqual(BuildingId.BeachBar, game.CurrentWave.SuddenRush.Building);
            Assert.AreEqual(22, game.GetNeedStatus(NeedId.Drinks).Total, "Build doc §4.6: Drinks .../16 -> .../22.");
        }

        [Test]
        public void EveryExtraGuestIsServedAndCounted()
        {
            ResortGame game = StartWave2();

            game.Tick(5000);

            Assert.AreEqual(GamePhase.WaveResolving, game.Phase);
            Assert.AreEqual(12 + 16 + 10 + 6, game.LastWaveResult.TotalUnits);
            Assert.AreEqual(game.LastWaveResult.TotalUnits, game.LastWaveResult.UnitsServed, "Nobody leaves (WR4).");
        }

        [Test]
        public void LaterWavesMayHaveOneButNeverWave1OrADebutWave()
        {
            _config.wave.suddenRushChance = 1f;
            var always = new ResortGame(_config, new FixedRandom(0));
            Assert.IsFalse(always.HasSuddenRush(0), "Wave 1 never (before the rush wave).");
            Assert.IsTrue(always.HasSuddenRush(1), "Wave 2 always.");
            Assert.IsTrue(always.HasSuddenRush(2));
            Assert.IsFalse(always.HasSuddenRush(3), "Wave 4 debuts Relax.");
            Assert.IsTrue(always.HasSuddenRush(4));
            Assert.IsTrue(always.HasSuddenRush(5));
            Assert.IsFalse(always.HasSuddenRush(6), "Wave 7 debuts Ocean View.");
            Assert.IsTrue(always.HasSuddenRush(7));

            _config.wave.suddenRushChance = 0f;
            var never = new ResortGame(_config, new FixedRandom(0));
            Assert.IsTrue(never.HasSuddenRush(1), "Wave 2 always, whatever the chance.");
            Assert.IsFalse(never.HasSuddenRush(2));
            Assert.IsFalse(never.HasSuddenRush(7));

            var noRandom = new ResortGame(_config);
            _config.wave.suddenRushChance = 1f;
            Assert.IsFalse(noRandom.HasSuddenRush(2), "Without a random source only the rush wave has one.");
        }

        [Test]
        public void TheChanceDecidesForLaterWaves()
        {
            _config.wave.suddenRushChance = 0.5f;
            Assert.IsTrue(new ResortGame(_config, new FixedRandom(0.49)).HasSuddenRush(2));
            Assert.IsFalse(new ResortGame(_config, new FixedRandom(0.51)).HasSuddenRush(2));
        }

        [Test]
        public void DebutWavesAreReadFromTheWaveTable()
        {
            CollectionAssert.AreEqual(
                new[] { 3, 6 },
                Enumerable.Range(0, _config.waves.Count).Where(i => ResortGame.IsDebutWave(_config, i)).ToArray(),
                "W4 (Relax) and W7 (Ocean View).");
        }

        [Test]
        public void TurnedOffMeansNoRushAtAll()
        {
            _config.wave.suddenRushEnabled = false;
            ResortGame game = StartWave2(new FixedRandom(0));

            RunTo(game, 60, 0.5);

            Assert.IsFalse(game.CurrentWave.HasRushPlanned);
            Assert.IsNull(game.CurrentWave.SuddenRush);
            Assert.AreEqual(10, game.GetNeedStatus(NeedId.Food).Total);
        }

        [Test]
        public void TheResultDoesNotDependOnTheFrameRate()
        {
            ResortGame smooth = StartWave2();
            ResortGame choppy = StartWave2();

            foreach (double checkpoint in new[] { 30, 45, 46.3, 50, 55.5, 70, 120 })
            {
                RunTo(smooth, checkpoint, 1.0 / 60.0);
                RunTo(choppy, checkpoint, 0.7);
                foreach (NeedId need in IslandNeeds)
                {
                    Assert.AreEqual(smooth.GetNeedStatus(need).Arrived, choppy.GetNeedStatus(need).Arrived, $"{need} arrived at {checkpoint} s");
                    Assert.AreEqual(smooth.GetNeedStatus(need).Waiting, choppy.GetNeedStatus(need).Waiting, $"{need} waiting at {checkpoint} s");
                    Assert.AreEqual(smooth.GetNeedStatus(need).Total, choppy.GetNeedStatus(need).Total, $"{need} total at {checkpoint} s");
                }
            }
        }

        [Test]
        public void TheTutorialPracticeWaveHasNone()
        {
            var tutorial = ScriptableObject.CreateInstance<TutorialConfig>();
            GameConfig practice = tutorial.CreatePracticeConfig(_config);
            try
            {
                Assert.IsFalse(practice.wave.suddenRushEnabled);
                Assert.IsTrue(_config.wave.suddenRushEnabled, "The prototype config itself is unchanged.");
            }
            finally
            {
                Object.DestroyImmediate(practice);
                Object.DestroyImmediate(tutorial);
            }
        }

        [Test]
        public void TheExtraGuestsGetGuestsOnTheIsland()
        {
            ResortGame game = StartWave2();
            GuestPlan before = GuestPlan.Build(game.CurrentWave);
            RunTo(game, 45.1, 0.1);

            GuestPlan after = before.Replan(game.CurrentWave, 4, game.CurrentWave.SuddenRush.Units);

            for (int i = 0; i < 4; i++) Assert.AreSame(before.Guests[i], after.Guests[i], "Guests already off the boat keep their plan.");
            Assert.AreEqual(16, after.CountVisits(NeedId.Food), "Every Food unit, the extra ones too, has a guest.");
            Assert.AreEqual(12, after.CountVisits(NeedId.Rooms));
            Assert.AreEqual(16, after.CountVisits(NeedId.Drinks));
            Assert.Greater(after.Guests.Count, before.Guests.Count, "More guests come for the extra units.");
        }

        [Test]
        public void ValidatorChecksTheRush()
        {
            _config.wave.suddenRushTime = 90f;
            Assert.That(GameConfigValidator.Validate(_config).Any(e => e.Contains("suddenRushTime")));

            _config.wave.suddenRushTime = 45f;
            _config.wave.suddenRushChance = 1.5f;
            Assert.That(GameConfigValidator.Validate(_config).Any(e => e.Contains("suddenRushChance")));

            _config.wave.suddenRushEnabled = false;
            Assert.IsFalse(GameConfigValidator.Validate(_config).Any(e => e.Contains("Sudden rush")), "Not checked when it is off.");
        }
    }
}
