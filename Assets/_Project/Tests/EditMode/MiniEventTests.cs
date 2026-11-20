using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ResortEmpire.Core;
using ResortEmpire.Editor;
using UnityEngine;

namespace ResortEmpire.Tests
{
    /// <summary>
    /// Mini-event: once per wave, at a random moment, the least busy building gets a rush of the guests that were
    /// going to come to it anyway. Nobody new comes. Wave 1 baseline: Rooms 8, Drinks 8, Food 6; the Reception and
    /// the Beach Bar have a Worker (12 s per guest), the Restaurant doesn't (36.4 s).
    /// </summary>
    public class MiniEventTests
    {
        private static readonly NeedId[] IslandNeeds = { NeedId.Rooms, NeedId.Drinks, NeedId.Food };

        private GameConfig _config;

        /// <summary>A random source that always returns the same fraction, so the mini-event's moment is known.</summary>
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
        public void DestroyConfig() => UnityEngine.Object.DestroyImmediate(_config);

        private ResortGame Started(System.Random random)
        {
            var game = new ResortGame(_config, random);
            Assert.IsTrue(game.StartWave());
            return game;
        }

        private static void RunTo(ResortGame game, double waveTime, double step)
        {
            while (game.Phase == GamePhase.WaveActive && game.WaveTime + step <= waveTime) game.Tick(step);
            double rest = waveTime - game.WaveTime;
            if (rest > 0 && game.Phase == GamePhase.WaveActive) game.Tick(rest);
        }

        [Test]
        public void ConfigDefaults()
        {
            Assert.IsTrue(_config.wave.miniEventEnabled);
            Assert.AreEqual(30f, _config.wave.miniEventEarliest);
            Assert.AreEqual(60f, _config.wave.miniEventLatest);
            Assert.AreEqual(6, _config.wave.miniEventUnits, "Build doc rush_units.");
            Assert.AreEqual(10f, _config.wave.miniEventDuration, "Build doc rush_duration_s.");
        }

        [Test]
        public void ItStartsAtARandomMomentInsideTheWindow()
        {
            foreach (int seed in Enumerable.Range(1, 20))
            {
                ResortGame game = Started(new System.Random(seed));
                double time = game.CurrentWave.ScheduledMiniEventTime;
                Assert.That(time, Is.InRange(30.0, 60.0), $"seed {seed}");

                RunTo(game, 61, 0.5);
                Assert.IsNotNull(game.CurrentWave.MiniEvent, $"seed {seed}: once per wave, before 60 s.");
                Assert.AreEqual(time, game.CurrentWave.MiniEvent.StartTime, 1e-6);
            }
        }

        [Test]
        public void ItGoesToTheLeastBusyBuilding()
        {
            // At 30 s: Rooms and Drinks have 1 guest on 3 seats each, the empty Restaurant 2 (Food 15 s and 30 s).
            ResortGame game = Started(new FixedRandom(0));
            var loads = new Dictionary<NeedId, double>();
            game.MiniEventStarted += _ =>
            {
                foreach (NeedId need in IslandNeeds)
                {
                    NeedStatus status = game.CurrentWave.GetNeedStatus(need);
                    loads[need] = (status.Seated + status.Waiting) / 3.0;
                }
            };

            RunTo(game, 31, 0.1);

            MiniEvent miniEvent = game.CurrentWave.MiniEvent;
            Assert.IsNotNull(miniEvent);
            Assert.AreEqual(30, miniEvent.StartTime, 1e-6);
            Assert.AreEqual(loads.Values.Min(), loads[miniEvent.Need], 1e-9, "The least busy building gets the rush.");
            Assert.AreNotEqual(NeedId.Food, miniEvent.Need, "The Restaurant is the busiest one at 30 s.");
        }

        [Test]
        public void TheBuildingsNextGuestsArriveWithinTenSeconds()
        {
            ResortGame game = Started(new FixedRandom(0));
            RunTo(game, 30.5, 0.1);
            MiniEvent miniEvent = game.CurrentWave.MiniEvent;
            Assert.AreEqual(NeedId.Rooms, miniEvent.Need, "Rooms and Drinks tie; Rooms comes first.");
            Assert.AreEqual(6, miniEvent.Units);

            // Rooms units 2..7 were due at 33.75 … 90 s; now one every 10 / 6 s from 30 s.
            double[] times = Enumerable.Range(2, 6).Select(k => game.CurrentWave.GetArrivalTime(NeedId.Rooms, k)).ToArray();
            double[] expected = Enumerable.Range(1, 6).Select(i => 30 + i * 10.0 / 6).ToArray();
            for (int i = 0; i < 6; i++) Assert.AreEqual(expected[i], times[i], 1e-6, $"Rooms unit {i + 2}");

            RunTo(game, 40, 0.1);
            Assert.AreEqual(8, game.GetNeedStatus(NeedId.Rooms).Arrived, "Every Rooms guest is here by 40 s.");
            Assert.AreEqual(3, game.GetNeedStatus(NeedId.Drinks).Arrived, "Other buildings keep their steady arrivals (11.25, 22.5, 33.75 s).");
        }

        [Test]
        public void NobodyNewComes()
        {
            ResortGame game = Started(new System.Random(7));
            Dictionary<NeedId, int> totals = IslandNeeds.ToDictionary(n => n, n => game.CurrentWave.GetTotal(n));

            RunTo(game, 600, 0.5);

            Assert.IsNotNull(game.CurrentWave.MiniEvent);
            foreach (NeedId need in IslandNeeds)
            {
                NeedStatus status = game.CurrentWave.GetNeedStatus(need);
                Assert.AreEqual(totals[need], status.Total, $"{need}: the wave's demand stays the same.");
                Assert.AreEqual(totals[need], status.Arrived, $"{need}: every guest came, none more.");
                Assert.AreEqual(totals[need], status.Served, $"{need}: and everyone was served.");
            }

            Assert.AreEqual(GamePhase.WaveResolving, game.Phase);
            Assert.AreEqual(22, game.LastWaveResult.UnitsServed);
        }

        [Test]
        public void LateInTheWaveOnlyTheGuestsStillToComeAreRushed()
        {
            ResortGame game = Started(new FixedRandom(0.999)); // at ~60 s
            RunTo(game, 61, 0.1);

            MiniEvent miniEvent = game.CurrentWave.MiniEvent;
            Assert.IsNotNull(miniEvent);
            int arrivedBefore = Enumerable.Range(0, game.CurrentWave.GetTotal(miniEvent.Need))
                .Count(k => game.CurrentWave.GetArrivalTime(miniEvent.Need, k) <= miniEvent.StartTime + 1e-6);
            Assert.AreEqual(game.CurrentWave.GetTotal(miniEvent.Need) - arrivedBefore, miniEvent.Units,
                "Fewer than 6 left: only those come early.");
            Assert.Less(miniEvent.Units, 6);
        }

        [Test]
        public void ArrivalTimesStayInOrder()
        {
            foreach (int seed in Enumerable.Range(1, 20))
            {
                ResortGame game = Started(new System.Random(seed));
                RunTo(game, 61, 0.5);
                foreach (NeedId need in IslandNeeds)
                {
                    double[] times = Enumerable.Range(0, game.CurrentWave.GetTotal(need))
                        .Select(k => game.CurrentWave.GetArrivalTime(need, k)).ToArray();
                    CollectionAssert.IsOrdered(times, $"seed {seed}, {need}");
                    Assert.LessOrEqual(times.Last(), 90 + 1e-6, "Nobody comes later than before.");
                }
            }
        }

        [Test]
        public void TheResultDoesNotDependOnTheFrameRate()
        {
            ResortGame smooth = Started(new System.Random(3));
            ResortGame choppy = Started(new System.Random(3));

            foreach (double checkpoint in new[] { 20, 35.3, 47, 60, 75, 120 })
            {
                RunTo(smooth, checkpoint, 1.0 / 60.0);
                RunTo(choppy, checkpoint, 0.7);
                foreach (NeedId need in IslandNeeds)
                {
                    Assert.AreEqual(smooth.GetNeedStatus(need).Arrived, choppy.GetNeedStatus(need).Arrived, $"{need} at {checkpoint} s");
                    Assert.AreEqual(smooth.GetNeedStatus(need).Waiting, choppy.GetNeedStatus(need).Waiting, $"{need} at {checkpoint} s");
                }
            }

            Assert.AreEqual(smooth.CurrentWave.MiniEvent.Need, choppy.CurrentWave.MiniEvent.Need);
            Assert.AreEqual(smooth.CurrentWave.MiniEvent.StartTime, choppy.CurrentWave.MiniEvent.StartTime, 1e-6);
        }

        [Test]
        public void NoMiniEventWithoutARandomSourceOrWhenTurnedOff()
        {
            ResortGame steady = Started(null);
            RunTo(steady, 100, 0.5);
            Assert.IsNull(steady.CurrentWave.MiniEvent, "Tests that need steady arrivals pass no random source.");
            Assert.AreEqual(double.PositiveInfinity, steady.CurrentWave.ScheduledMiniEventTime);

            _config.wave.miniEventEnabled = false;
            ResortGame off = Started(new System.Random(1));
            RunTo(off, 100, 0.5);
            Assert.IsNull(off.CurrentWave.MiniEvent);
        }

        [Test]
        public void TheTutorialPracticeWaveHasNoMiniEvent()
        {
            var tutorial = ScriptableObject.CreateInstance<TutorialConfig>();
            GameConfig practice = tutorial.CreatePracticeConfig(_config);
            try
            {
                Assert.IsFalse(practice.wave.miniEventEnabled);
                Assert.IsTrue(_config.wave.miniEventEnabled, "The prototype config itself is unchanged.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(practice);
                UnityEngine.Object.DestroyImmediate(tutorial);
            }
        }

        [Test]
        public void TheGuestsAreSharedOutAgainSoTheRushedOnesStepOffEarly()
        {
            ResortGame game = Started(new FixedRandom(0));
            GuestPlan before = GuestPlan.Build(game.CurrentWave);
            RunTo(game, 30.5, 0.1);

            GuestPlan after = before.Replan(game.CurrentWave, 3);

            for (int i = 0; i < 3; i++) Assert.AreSame(before.Guests[i], after.Guests[i], "Guests already on the island keep their plan.");
            foreach (NeedId need in IslandNeeds)
            {
                Assert.AreEqual(game.CurrentWave.GetTotal(need), after.CountVisits(need), $"{need}: same visits as the demand.");
            }

            double[] firstVisits = after.Guests.Skip(3).Select(g => g.Visits[0].Time).ToArray();
            CollectionAssert.IsOrdered(firstVisits, "The other guests step off in the new arrival order.");
            Assert.That(after.Guests.Skip(3).First().Visits[0].Time, Is.LessThan(33), "The first rushed guest comes right away.");
        }

        [Test]
        public void ValidatorChecksTheWindow()
        {
            _config.wave.miniEventLatest = 95f;
            Assert.That(GameConfigValidator.Validate(_config).Any(e => e.Contains("miniEventLatest")));

            _config.wave.miniEventLatest = 20f;
            Assert.That(GameConfigValidator.Validate(_config).Any(e => e.Contains("miniEventEarliest")));

            _config.wave.miniEventEnabled = false;
            Assert.IsFalse(GameConfigValidator.Validate(_config).Any(e => e.Contains("Mini-event")), "Not checked when it is off.");
        }
    }
}
