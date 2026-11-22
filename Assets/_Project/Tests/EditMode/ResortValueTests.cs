using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ResortEmpire.Core;
using ResortEmpire.Editor;

namespace ResortEmpire.Tests
{
    /// <summary>
    /// Resort Value (GDD §7.6, Build doc §5.5): the status number grows after each wave by its rating and never drops.
    /// A weak wave adds 0. The amounts are placeholders in the config (<see cref="ResortValueSettings"/>).
    /// </summary>
    public class ResortValueTests
    {
        private GameConfig _config;

        private ResortValueSettings Settings => _config.resortValue;

        [SetUp]
        public void CreateConfig()
        {
            _config = PrototypeConfigDefaults.Create();
            _config.wave.incomingDuration = 0f; // skip the boat so Tick(t) is wave time t
            _config.wave.suddenRushEnabled = false; // the plain waves of the table
        }

        [TearDown]
        public void DestroyConfig() => UnityEngine.Object.DestroyImmediate(_config);

        /// <summary>Plays the next wave untouched until everyone is served (WAVE_RESOLVING).</summary>
        private static WaveResult PlayWave(ResortGame game)
        {
            Assert.IsTrue(game.StartWave());
            for (int i = 0; i < 100_000 && game.Phase == GamePhase.WaveActive; i++) game.Tick(0.1);
            Assert.AreEqual(GamePhase.WaveResolving, game.Phase, "The wave ends once everyone is served.");
            return game.LastWaveResult;
        }

        /// <summary>Plays the next wave, completes it and returns to AWAITING_WAVE.</summary>
        private static WaveResult PlayAndContinue(ResortGame game)
        {
            WaveResult result = PlayWave(game);
            Assert.IsTrue(game.FinishResolving());
            Assert.IsTrue(game.ContinueAfterWave());
            return result;
        }

        [Test]
        public void ANewGameStartsAtTheStartingValue()
        {
            Assert.AreEqual(0, new ResortGame(_config).ResortValue);

            Settings.startingValue = 300;
            Assert.AreEqual(300, new ResortGame(_config).ResortValue);
        }

        [TestCase(WaveRating.OneStar, 0)]
        [TestCase(WaveRating.TwoStars, 0)]
        [TestCase(WaveRating.ThreeStars, 50)]
        [TestCase(WaveRating.FourStars, 100)]
        [TestCase(WaveRating.FiveStars, 150)]
        [TestCase(WaveRating.Perfect, 200)]
        public void TheGainFollowsTheRating(WaveRating rating, int expected)
        {
            Assert.AreEqual(expected, Settings.GetGain(rating));
        }

        [Test]
        public void TheGainIsNeverNegativeEvenIfTheConfigSaysSo()
        {
            Settings.gainPerRating = new[] { -50, 0, 50, 100, 150, 200 };

            Assert.AreEqual(0, Settings.GetGain(WaveRating.OneStar));
        }

        [Test]
        public void TheGainIsZeroWhenTheConfigHasNoValueForTheRating()
        {
            Settings.gainPerRating = new[] { 10, 20 };
            Assert.AreEqual(0, Settings.GetGain(WaveRating.Perfect));

            Settings.gainPerRating = null;
            Assert.AreEqual(0, Settings.GetGain(WaveRating.Perfect));
        }

        [Test]
        public void AWellHandledWaveAddsItsGainOnWaveComplete()
        {
            _config.wave.utilizationEmpty = _config.wave.utilizationStaffed; // no slow building, so nobody is delayed
            var game = new ResortGame(_config);

            WaveResult result = PlayWave(game);
            Assert.AreEqual(WaveRating.Perfect, result.Rating, "Nobody is delayed in wave 1.");
            Assert.AreEqual(200, result.ResortValueGain);
            Assert.AreEqual(0, game.ResortValue, "Not before Wave Complete.");

            Assert.IsTrue(game.FinishResolving());
            Assert.AreEqual(200, game.ResortValue);

            Assert.IsTrue(game.ContinueAfterWave());
            Assert.AreEqual(200, game.ResortValue, "Continue doesn't add it again.");
        }

        [Test]
        public void AWeakWaveAddsZeroAndNeverTakesAnyAway()
        {
            Settings.startingValue = 500;
            _config.waves[0].food = 30; // a big Food demand on the empty Restaurant
            var game = new ResortGame(_config);

            WaveResult result = PlayAndContinue(game);

            Assert.Less(result.Rating, WaveRating.ThreeStars);
            Assert.AreEqual(0, result.ResortValueGain);
            Assert.AreEqual(500, game.ResortValue, "A weak wave leaves Resort Value as it was.");
        }

        [Test]
        public void ResortValueNeverDropsOverAllWaves()
        {
            var game = new ResortGame(_config);
            int expected = 0;

            while (game.HasNextWave)
            {
                int before = game.ResortValue;
                WaveResult result = PlayAndContinue(game);
                expected += Settings.GetGain(result.Rating);

                Assert.GreaterOrEqual(game.ResortValue, before, $"Wave {result.WaveNumber} must not lower Resort Value.");
                Assert.AreEqual(expected, game.ResortValue, $"After wave {result.WaveNumber}: the sum of every wave's gain.");
            }

            Assert.AreEqual(_config.waves.Count, game.CompletedWaves);
        }

        [Test]
        public void TheGainCanBeTunedInTheConfig()
        {
            Settings.gainPerRating = new[] { 1, 2, 3, 4, 5, 999 };
            _config.wave.utilizationEmpty = _config.wave.utilizationStaffed; // no slow building, so wave 1 is Perfect
            var game = new ResortGame(_config);

            PlayAndContinue(game);

            Assert.AreEqual(999, game.ResortValue, "Wave 1 is Perfect.");
        }

        [Test]
        public void TheTutorialsPracticeWaveKeepsResortValueRules()
        {
            var tutorial = UnityEngine.ScriptableObject.CreateInstance<TutorialConfig>();
            GameConfig practice = tutorial.CreatePracticeConfig(_config);
            try
            {
                CollectionAssert.AreEqual(Settings.gainPerRating, practice.resortValue.gainPerRating);
                Assert.AreEqual(Settings.startingValue, practice.resortValue.startingValue);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(practice);
                UnityEngine.Object.DestroyImmediate(tutorial);
            }
        }

        [Test]
        public void TheDefaultsPassTheValidator()
        {
            Assert.IsEmpty(GameConfigValidator.Validate(_config));
        }

        [Test]
        public void TheValidatorRejectsANegativeGain()
        {
            Settings.gainPerRating = new[] { -10, 0, 50, 100, 150, 200 };

            Assert.That(GameConfigValidator.Validate(_config).Any(e => e.Contains("Resort Value") && e.Contains("never drops")));
        }

        [Test]
        public void TheValidatorRejectsAGainThatDropsWithABetterRating()
        {
            Settings.gainPerRating = new[] { 0, 0, 50, 40, 150, 200 };

            Assert.That(GameConfigValidator.Validate(_config).Any(e => e.Contains("gainPerRating must not drop")));
        }

        [Test]
        public void TheValidatorNeedsAGainForEveryRating()
        {
            Settings.gainPerRating = new[] { 0, 0, 50 };

            Assert.That(GameConfigValidator.Validate(_config).Any(e => e.Contains("gainPerRating needs 6 values")));
        }

        [Test]
        public void TheValidatorRejectsANegativeStartingValue()
        {
            Settings.startingValue = -1;

            Assert.That(GameConfigValidator.Validate(_config).Any(e => e.Contains("startingValue")));
        }

        [Test]
        public void TheValidatorRejectsMissingResortValueSettings()
        {
            _config.resortValue = null;

            List<string> errors = GameConfigValidator.Validate(_config);
            Assert.That(errors.Any(e => e.Contains("Resort Value settings are missing")));
        }

        [Test]
        public void ShippedConfigHasTheDefaultResortValueNumbers()
        {
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<GameConfig>(PrototypeConfigDefaults.AssetPath);
            Assert.IsNotNull(asset);

            Assert.AreEqual(Settings.startingValue, asset.resortValue.startingValue);
            CollectionAssert.AreEqual(Settings.gainPerRating, asset.resortValue.gainPerRating);
        }
    }
}
