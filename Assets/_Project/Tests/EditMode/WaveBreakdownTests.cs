using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ResortEmpire.Core;
using ResortEmpire.Editor;
using ResortEmpire.Game;

namespace ResortEmpire.Tests
{
    /// <summary>
    /// Wave evaluation details (UI doc S4, Build doc AC6): the "What went well / Needs improvement" lines on Wave Complete.
    /// A need went great from <see cref="WaveBreakdownSettings.wentGreatSatisfaction"/> of its own satisfaction; the rest
    /// need a Worker (served without one for most of the wave) or more capacity.
    /// </summary>
    public class WaveBreakdownTests
    {
        private GameConfig _config;

        private WaveBreakdownSettings Settings => _config.breakdown;

        [SetUp]
        public void CreateConfig()
        {
            _config = PrototypeConfigDefaults.Create();
            _config.wave.incomingDuration = 0f; // skip the boat so Tick(t) is wave time t
            _config.wave.suddenRushEnabled = false; // the plain waves of the table
        }

        [TearDown]
        public void DestroyConfig() => UnityEngine.Object.DestroyImmediate(_config);

        /// <summary>Plays wave 1 untouched (the Restaurant starts without a Worker) until everyone is served.</summary>
        private ResortGame PlayWave1Untouched()
        {
            var game = new ResortGame(_config);
            Assert.IsTrue(game.StartWave());
            for (int i = 0; i < 100_000 && game.Phase == GamePhase.WaveActive; i++) game.Tick(0.1);
            Assert.AreEqual(GamePhase.WaveResolving, game.Phase, "The wave ends once everyone is served.");
            return game;
        }

        /// <summary>A wave of one need with <paramref name="seats"/> seats, served at <paramref name="speed"/> progress per second.</summary>
        private WaveSimulation OneNeedWave(NeedId need, int units, double waveTimer, int seats, System.Func<double> speed)
        {
            var forecast = new WaveForecast(0, 0, new List<NeedDemand> { new NeedDemand(need, units) });
            return new WaveSimulation(forecast, waveTimer, _ => seats, _ => speed(), _config.wave);
        }

        private double StaffedSpeed => 1.0 / _config.wave.GetServiceSeconds(true);
        private double EmptySpeed => 1.0 / _config.wave.GetServiceSeconds(false);

        [Test]
        public void Wave1UntouchedRoomsAndDrinksWentGreatAndFoodNeedsAWorker()
        {
            WaveBreakdown breakdown = PlayWave1Untouched().LastWaveResult.Breakdown;

            CollectionAssert.AreEqual(new[] { NeedId.Rooms, NeedId.Drinks }, breakdown.WentGreat);
            CollectionAssert.AreEqual(new[] { NeedId.Food }, breakdown.NeedsWorker, "The Restaurant served without a Worker all wave.");
            CollectionAssert.IsEmpty(breakdown.NeedsCapacity);
        }

        [Test]
        public void SlowServiceSecondsCountOnlyTheTimeWithoutAWorker()
        {
            bool hasWorker = true;
            WaveSimulation wave = OneNeedWave(NeedId.Food, 6, 90, 3, () => hasWorker ? StaffedSpeed : EmptySpeed);

            wave.AdvanceTo(10);
            Assert.AreEqual(0, wave.GetSlowServiceSeconds(NeedId.Food), 1e-6, "Staffed: never slow.");

            hasWorker = false;
            wave.AdvanceTo(30);
            Assert.AreEqual(20, wave.GetSlowServiceSeconds(NeedId.Food), 1e-6);

            hasWorker = true;
            wave.AdvanceTo(50);
            Assert.AreEqual(20, wave.GetSlowServiceSeconds(NeedId.Food), 1e-6, "A Worker back stops the count.");
            Assert.AreEqual(0, wave.GetSlowServiceSeconds(NeedId.Rooms), "A need that isn't in the wave.");
        }

        [Test]
        public void AStaffedBuildingThatCannotKeepUpNeedsCapacity()
        {
            // 20 Food guests over 30 s into 1 staffed seat (12 s each): the queue only grows.
            WaveSimulation wave = OneNeedWave(NeedId.Food, 20, 30, 1, () => StaffedSpeed);
            wave.AdvanceTo(1000);
            Assert.IsTrue(wave.IsFinished);

            WaveBreakdown breakdown = WaveBreakdown.Create(wave, 1000, _config.satisfaction, Settings);

            CollectionAssert.IsEmpty(breakdown.WentGreat);
            CollectionAssert.IsEmpty(breakdown.NeedsWorker);
            CollectionAssert.AreEqual(new[] { NeedId.Food }, breakdown.NeedsCapacity);
        }

        [Test]
        public void ANeedWithoutAWorkerForLessThanTheShareNeedsCapacity()
        {
            Settings.noWorkerShare = 1f;

            WaveBreakdown breakdown = PlayWave1Untouched().LastWaveResult.Breakdown;

            CollectionAssert.IsEmpty(breakdown.NeedsWorker);
            CollectionAssert.AreEqual(new[] { NeedId.Food }, breakdown.NeedsCapacity);
        }

        [Test]
        public void EveryNeedWentGreatWhenTheBarIsLow()
        {
            Settings.wentGreatSatisfaction = 0f;

            WaveBreakdown breakdown = PlayWave1Untouched().LastWaveResult.Breakdown;

            CollectionAssert.AreEqual(new[] { NeedId.Rooms, NeedId.Drinks, NeedId.Food }, breakdown.WentGreat);
            CollectionAssert.IsEmpty(breakdown.NeedsWorker.Concat(breakdown.NeedsCapacity));
        }

        [Test]
        public void ANeedsSatisfactionUsesOnlyItsOwnGuests()
        {
            ResortGame game = PlayWave1Untouched();

            Assert.AreEqual(100, WaveBreakdown.GetNeedSatisfaction(game.CurrentWave, NeedId.Rooms, _config.satisfaction), 1e-6);
            Assert.Less(WaveBreakdown.GetNeedSatisfaction(game.CurrentWave, NeedId.Food, _config.satisfaction), game.LastWaveResult.Satisfaction,
                "Food alone scores below the whole wave.");
        }

        [Test]
        public void AResultWithoutABreakdownHasEmptyLists()
        {
            var result = new WaveResult(0, 12, 22, 22, 100, 100, WaveRating.Perfect);

            CollectionAssert.IsEmpty(result.Breakdown.WentGreat);
            CollectionAssert.IsEmpty(result.Breakdown.NeedsWorker);
            CollectionAssert.IsEmpty(result.Breakdown.NeedsCapacity);
        }

        [Test]
        public void TheLinesNameTheNeeds()
        {
            var game = new ResortGame(_config);

            Assert.AreEqual("Food went great!", GameText.WentGreat(game, new[] { NeedId.Food }));
            Assert.AreEqual("Rooms & Drinks went great!", GameText.WentGreat(game, new[] { NeedId.Rooms, NeedId.Drinks }));
            Assert.AreEqual("Rooms, Drinks & Food went great!", GameText.WentGreat(game, new[] { NeedId.Rooms, NeedId.Drinks, NeedId.Food }));
            Assert.AreEqual("Food needs a Worker", GameText.NeedsWorker(game, new[] { NeedId.Food }));
            Assert.AreEqual("Drinks & Food need Workers", GameText.NeedsWorker(game, new[] { NeedId.Drinks, NeedId.Food }));
            Assert.AreEqual("Food capacity needs improvement", GameText.NeedsCapacity(game, new[] { NeedId.Food }));
        }

        [Test]
        public void TheValidatorRejectsMissingBreakdownSettings()
        {
            _config.breakdown = null;

            List<string> errors = GameConfigValidator.Validate(_config);
            Assert.That(errors.Any(e => e.Contains("breakdown settings are missing")));
        }

        [Test]
        public void ShippedConfigHasTheDefaultBreakdownNumbers()
        {
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<GameConfig>(PrototypeConfigDefaults.AssetPath);
            Assert.IsNotNull(asset);

            Assert.AreEqual(Settings.wentGreatSatisfaction, asset.breakdown.wentGreatSatisfaction);
            Assert.AreEqual(Settings.noWorkerShare, asset.breakdown.noWorkerShare);
        }
    }
}
