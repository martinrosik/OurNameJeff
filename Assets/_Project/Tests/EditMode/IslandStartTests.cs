using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ResortEmpire.Core;
using ResortEmpire.Editor;
using UnityEditor;

namespace ResortEmpire.Tests
{
    /// <summary>
    /// OUR-31 starting island, OUR-48 Worker placement, OUR-50 capacity, OUR-33 forecast, OUR-27 manual wave start.
    /// Uses the Build doc baseline (PrototypeConfigDefaults); the shipped asset is checked separately.
    /// </summary>
    public class IslandStartTests
    {
        private GameConfig _config;

        [SetUp]
        public void CreateConfig() => _config = PrototypeConfigDefaults.Create();

        [TearDown]
        public void DestroyConfig() => UnityEngine.Object.DestroyImmediate(_config);

        [Test]
        public void ShippedConfigAssetIsValid()
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameConfig>(PrototypeConfigDefaults.AssetPath);
            Assert.IsNotNull(asset, $"Missing {PrototypeConfigDefaults.AssetPath}. Use Resort Empire/Config/Create Prototype Config.");

            List<string> errors = GameConfigValidator.Validate(asset);
            Assert.IsEmpty(errors, string.Join("\n", errors));
        }

        [Test]
        public void OUR31_IslandStartsWithReceptionRestaurantAndBeachBar()
        {
            var game = new ResortGame(_config);

            CollectionAssert.AreEquivalent(
                new[] { BuildingId.Reception, BuildingId.Restaurant, BuildingId.BeachBar },
                game.Buildings.Select(b => b.Id));
            Assert.IsNull(game.GetBuilding(BuildingId.Pool), "Pool comes with a later unlock.");
        }

        [Test]
        public void OUR48_OneBuildingStartsWithoutAWorker()
        {
            var game = new ResortGame(_config);

            Assert.IsTrue(game.GetBuilding(BuildingId.Reception).HasWorker);
            Assert.IsTrue(game.GetBuilding(BuildingId.BeachBar).HasWorker);
            Assert.IsFalse(game.GetBuilding(BuildingId.Restaurant).HasWorker);
            Assert.AreEqual(game.Buildings.Count - 1, game.WorkerCount, "Always one fewer Worker than buildings.");
        }

        [Test]
        public void OUR50_EachBuildingHasThreeSeatsAtLevelOneAndNobodySeatedYet()
        {
            var game = new ResortGame(_config);

            foreach (BuildingRuntime building in game.Buildings)
            {
                Assert.AreEqual(1, building.Level);
                Assert.AreEqual(3, building.Seats, $"{building.Id} capacity at level 1.");
                Assert.AreEqual(0, game.GetSeatedCount(building.Id), "No guests exist until OUR-34.");
            }
        }

        [Test]
        public void OUR33_ForecastShowsWaveOneDemandForTheIslandsNeeds()
        {
            var game = new ResortGame(_config);

            WaveForecast forecast = game.GetNextWaveForecast();

            Assert.AreEqual(1, forecast.WaveNumber);
            Assert.AreEqual(12, forecast.Guests);
            CollectionAssert.AreEquivalent(new[] { NeedId.Rooms, NeedId.Drinks, NeedId.Food }, forecast.Demand.Select(d => d.Need));
            Assert.AreEqual(8, forecast.GetDemand(NeedId.Rooms));
            Assert.AreEqual(8, forecast.GetDemand(NeedId.Drinks));
            Assert.AreEqual(6, forecast.GetDemand(NeedId.Food));
        }

        [Test]
        public void OUR33_ForecastLeavesOutNeedsWithoutABuilding()
        {
            var game = new ResortGame(_config);

            WaveForecast wave4 = game.GetForecast(3);

            Assert.AreEqual(0, wave4.GetDemand(NeedId.Relax), "Relax waits for the Pool.");
            Assert.That(wave4.Demand.Select(d => d.Need), Has.No.Member(NeedId.Relax));
        }

        [Test]
        public void OUR27_StartWaveSailsTheBoatInThenTheWaveRunsUntilEveryoneIsServed()
        {
            var game = new ResortGame(_config);
            var phases = new List<GamePhase>();
            game.PhaseChanged += (from, to) => phases.Add(to);

            Assert.AreEqual(GamePhase.AwaitingWave, game.Phase);
            Assert.IsTrue(game.StartWave());
            Assert.AreEqual(GamePhase.WaveIncoming, game.Phase);
            Assert.AreEqual(_config.wave.incomingDuration, game.IncomingRemaining, 1e-6);

            game.Tick(_config.wave.incomingDuration + 1);
            Assert.AreEqual(GamePhase.WaveActive, game.Phase);
            Assert.AreEqual(1, game.WaveTime, 1e-6, "Time past the boat goes into the wave.");

            game.Tick(60);
            Assert.AreEqual(GamePhase.WaveActive, game.Phase, "The wave stays in progress while guests arrive.");

            // Since service (OUR-46) and the wave end (OUR-35) exist, a long wait ends the wave: everyone is served.
            game.Tick(10_000);
            Assert.AreEqual(GamePhase.WaveResolving, game.Phase);
            CollectionAssert.AreEqual(new[] { GamePhase.WaveIncoming, GamePhase.WaveActive, GamePhase.WaveResolving }, phases);
        }

        [Test]
        public void OUR27_StartWaveOnlyWorksWhileReadyForGuests()
        {
            var game = new ResortGame(_config);

            Assert.IsTrue(game.StartWave());
            Assert.IsFalse(game.StartWave(), "Only one wave at a time.");
        }

        [Test]
        public void ValidatorCatchesAMissingWorkerDeficit()
        {
            foreach (BuildingDefinition building in _config.buildings) building.staffedAtStart = true;

            List<string> errors = GameConfigValidator.Validate(_config);

            Assert.That(errors.Any(e => e.Contains("Workers must be one fewer")));
        }
    }
}
