using System.Collections.Generic;
using NUnit.Framework;
using ResortEmpire.Core;
using ResortEmpire.Editor;
using UnityEditor;
using UnityEngine;

namespace ResortEmpire.Tests
{
    /// <summary>
    /// The tutorial's practice wave (<see cref="TutorialConfig"/>): a valid copy of the prototype rules with one wave,
    /// in which the building without a Worker always falls behind and a Worker always fixes it at once. Checked for the
    /// defaults and for the shipped assets, so retuning either config can't quietly break the tutorial.
    /// </summary>
    public class TutorialConfigTests
    {
        private const double Step = 0.25;

        private static readonly BuildingId[] StartingBuildings = { BuildingId.Reception, BuildingId.Restaurant, BuildingId.BeachBar };

        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void DestroyCreated()
        {
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

        private GameConfig DefaultPractice() =>
            Track(Track(TutorialConfigDefaults.Create()).CreatePracticeConfig(Track(PrototypeConfigDefaults.Create())));

        [Test]
        public void PracticeConfigIsValidAndPlaysOnlyThePracticeWave()
        {
            GameConfig practice = DefaultPractice();

            CollectionAssert.IsEmpty(GameConfigValidator.Validate(practice));
            Assert.AreEqual(1, practice.waves.Count);
            Assert.AreEqual(40f, practice.wave.waveTimer);
            Assert.AreEqual(9, practice.waves[0].food);
        }

        [Test]
        public void PracticeConfigLeavesTheConfigAssetsUnchanged()
        {
            GameConfig baseConfig = Track(PrototypeConfigDefaults.Create());
            TutorialConfig tutorial = Track(TutorialConfigDefaults.Create());

            GameConfig practice = Track(tutorial.CreatePracticeConfig(baseConfig));
            practice.wave.serviceTime = 99f;
            practice.waves[0].food = 99;

            Assert.AreEqual(8, baseConfig.waves.Count, "The prototype waves stay.");
            Assert.AreEqual(90f, baseConfig.wave.waveTimer);
            Assert.AreEqual(12f, baseConfig.wave.serviceTime, "The copy has its own wave settings.");
            Assert.AreEqual(9, tutorial.practiceWave.food, "The copy has its own practice wave.");
        }

        [TestCaseSource(nameof(StartingBuildings))]
        public void TheBuildingWithoutAWorkerFallsBehindAndAWorkerFixesItAtOnce(BuildingId empty)
        {
            AssertGivesABuildingToFix(DefaultPractice(), empty);
        }

        [Test]
        public void ShippedConfigsGiveTheTutorialABuildingToFix()
        {
            var tutorial = AssetDatabase.LoadAssetAtPath<TutorialConfig>(TutorialConfigDefaults.AssetPath);
            var prototype = AssetDatabase.LoadAssetAtPath<GameConfig>(PrototypeConfigDefaults.AssetPath);
            Assert.IsNotNull(tutorial, $"Missing {TutorialConfigDefaults.AssetPath} (Resort Empire/Greybox/Rebuild Tutorial Scene creates it).");
            Assert.IsNotNull(prototype, $"Missing {PrototypeConfigDefaults.AssetPath}.");

            GameConfig practice = Track(tutorial.CreatePracticeConfig(prototype));
            CollectionAssert.IsEmpty(GameConfigValidator.Validate(practice));
            foreach (BuildingId empty in StartingBuildings)
            {
                AssertGivesABuildingToFix(practice, empty);
            }
        }

        /// <summary>
        /// With <paramref name="empty"/> as the building without a Worker: it falls behind before arrivals close,
        /// shows "catching up" in the same moment a Worker arrives (OUR-39), never falls behind again, and the wave
        /// then ends within two minutes.
        /// </summary>
        private static void AssertGivesABuildingToFix(GameConfig practice, BuildingId empty)
        {
            foreach (BuildingDefinition building in practice.buildings)
            {
                building.staffedAtStart = building.building != empty;
            }

            var game = new ResortGame(practice);
            Assert.IsTrue(game.StartWave());
            while (game.Phase != GamePhase.WaveResolving && game.GetBuildingState(empty) != NeedState.Bottleneck)
            {
                game.Tick(Step);
            }

            Assert.AreEqual(NeedState.Bottleneck, game.GetBuildingState(empty), $"{empty} without a Worker never fell behind.");
            Assert.Less(game.WaveTime, practice.wave.waveTimer, "It falls behind while guests still arrive.");

            BuildingId donor = empty == BuildingId.Reception ? BuildingId.Restaurant : BuildingId.Reception;
            Assert.AreEqual(WorkerMoveResult.Moved, game.MoveWorker(donor, empty));
            Assert.AreEqual(NeedState.Recovering, game.GetBuildingState(empty), "A Worker turns it to catching up at once.");

            while (game.Phase != GamePhase.WaveResolving && game.WaveTime < 600)
            {
                game.Tick(Step);
                Assert.AreNotEqual(NeedState.Bottleneck, game.GetBuildingState(empty), $"{empty} fell behind again at {game.WaveTime:0.0} s.");
            }

            Assert.AreEqual(GamePhase.WaveResolving, game.Phase);
            Assert.Less(game.WaveTime, 120.0, "The practice wave ends within two minutes once the player fixes it.");
        }
    }
}
