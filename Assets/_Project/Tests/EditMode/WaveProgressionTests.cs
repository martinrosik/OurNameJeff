using System.Collections.Generic;
using NUnit.Framework;
using ResortEmpire.Core;
using ResortEmpire.Editor;
using UnityEditor;

namespace ResortEmpire.Tests
{
    /// <summary>
    /// Wave difficulty: every wave is harder than the one before it. Table waves that grow keep their numbers; waves
    /// 4 and 7 (whose new need has no building yet) grow by growthIfNotHarder over the wave before them.
    /// </summary>
    public class WaveProgressionTests
    {
        private GameConfig _config;

        [SetUp]
        public void CreateConfig()
        {
            _config = PrototypeConfigDefaults.Create();
            _config.wave.incomingDuration = 0f; // skip the boat so Tick(t) is wave time t
            _config.wave.serviceTime = 1_000_000f; // the tests serve guests by hand
        }

        [TearDown]
        public void DestroyConfig() => UnityEngine.Object.DestroyImmediate(_config);

        private static void AssertDemand(WaveForecast forecast, int guests, int rooms, int drinks, int food)
        {
            string wave = $"Wave {forecast.WaveNumber}";
            Assert.AreEqual(guests, forecast.Guests, $"{wave} guests");
            Assert.AreEqual(rooms, forecast.GetDemand(NeedId.Rooms), $"{wave} Rooms");
            Assert.AreEqual(drinks, forecast.GetDemand(NeedId.Drinks), $"{wave} Drinks");
            Assert.AreEqual(food, forecast.GetDemand(NeedId.Food), $"{wave} Food");
        }

        private static void PlayWave(ResortGame game)
        {
            Assert.IsTrue(game.StartWave());
            game.Tick(game.Config.wave.waveTimer);
            foreach (NeedId need in game.CurrentWave.Needs)
            {
                while (game.CurrentWave.FinishService(need)) { }
            }

            game.Tick(0.5);
            Assert.IsTrue(game.FinishResolving());
            Assert.IsTrue(game.ContinueAfterWave());
        }

        [Test]
        public void EveryWaveIsHarderThanTheOneBeforeIt()
        {
            var game = new ResortGame(_config);

            for (int i = 1; i < _config.waves.Count; i++)
            {
                WaveForecast previous = game.GetForecast(i - 1);
                WaveForecast current = game.GetForecast(i);

                Assert.Greater(WaveProgression.GetTotalDemand(current), WaveProgression.GetTotalDemand(previous), $"Wave {i + 1} has more need units than wave {i}.");
                Assert.GreaterOrEqual(current.Guests, previous.Guests, $"Wave {i + 1} brings at least as many guests as wave {i}.");
                foreach (NeedDemand entry in current.Demand)
                {
                    Assert.GreaterOrEqual(entry.Units, previous.GetDemand(entry.Need), $"Wave {i + 1}: {entry.Need} never drops.");
                }
            }
        }

        [Test]
        public void TableWavesThatGrowKeepTheirBuildDocNumbers()
        {
            var game = new ResortGame(_config);

            AssertDemand(game.GetForecast(0), 12, 8, 8, 6);
            AssertDemand(game.GetForecast(1), 18, 12, 16, 10);
            AssertDemand(game.GetForecast(2), 24, 16, 18, 14);
            AssertDemand(game.GetForecast(4), 34, 20, 22, 18);
            AssertDemand(game.GetForecast(5), 40, 25, 28, 22);
            AssertDemand(game.GetForecast(7), 52, 31, 35, 28);
        }

        [Test]
        public void DebutWavesWithoutTheirBuildingGrowOverTheWaveBefore()
        {
            var game = new ResortGame(_config);

            // Wave 3 × 1.125 rounded up: 24 → 27 guests, 16 → 18, 18 → 20.25 → 21, 14 → 15.75 → 16.
            AssertDemand(game.GetForecast(3), 27, 18, 21, 16);
            // Wave 6 × 1.125 rounded up: 40 → 45 guests, 25 → 28.1 → 29, 28 → 31.5 → 32, 22 → 24.75 → 25.
            AssertDemand(game.GetForecast(6), 45, 29, 32, 25);
        }

        [Test]
        public void ForecastNumbersAndWaveNumbersStayInOrder()
        {
            var game = new ResortGame(_config);

            for (int i = 0; i < _config.waves.Count; i++)
            {
                Assert.AreEqual(i, game.GetForecast(i).WaveIndex);
            }
        }

        [Test]
        public void AfterAGrownWaveTheNextTableWaveMustStillBeBigger()
        {
            _config.wave.growthIfNotHarder = 0.5f; // wave 4 grows to 24/27/21 = 72, more than wave 5's 60 in the table
            var game = new ResortGame(_config);

            AssertDemand(game.GetForecast(3), 36, 24, 27, 21);
            AssertDemand(game.GetForecast(4), 54, 36, 41, 32); // wave 5 isn't harder than the grown wave 4, so it grows again
        }

        [Test]
        public void GrowthRoundsUpWithoutFloatNoise()
        {
            _config.wave.growthIfNotHarder = 0.1f;
            _config.waves[0] = new WaveDefinition { guests = 10, rooms = 10, drinks = 10, food = 10 };
            _config.waves[1] = new WaveDefinition { guests = 10, rooms = 10, drinks = 10, food = 10 };
            var game = new ResortGame(_config);

            AssertDemand(game.GetForecast(1), 11, 11, 11, 11);
        }

        [Test]
        public void TheWavePlayedUsesTheGrownDemand()
        {
            var game = new ResortGame(_config);
            for (int i = 0; i < 3; i++) PlayWave(game);

            Assert.IsTrue(game.StartWave());

            Assert.AreEqual(3, game.CurrentWave.WaveIndex);
            Assert.AreEqual(27, game.CurrentWave.GuestCount);
            Assert.AreEqual(18, game.CurrentWave.GetTotal(NeedId.Rooms));
            Assert.AreEqual(21, game.CurrentWave.GetTotal(NeedId.Drinks));
            Assert.AreEqual(16, game.CurrentWave.GetTotal(NeedId.Food));
            Assert.AreEqual(0, game.CurrentWave.GetTotal(NeedId.Relax), "Relax still waits for the Pool (WR9).");
        }

        [Test]
        public void TheGameStillEndsAfterTheLastTableWave()
        {
            var game = new ResortGame(_config);
            for (int i = 0; i < _config.waves.Count; i++) PlayWave(game);

            Assert.IsFalse(game.HasNextWave);
            Assert.IsNull(game.GetNextWaveForecast());
        }

        [Test]
        public void ValidatorRejectsAGrowthThatCannotMakeAWaveHarder()
        {
            _config.wave.growthIfNotHarder = 0f;

            List<string> errors = GameConfigValidator.Validate(_config);

            Assert.That(errors, Has.Some.Contains("growthIfNotHarder"));
        }

        [Test]
        public void ShippedConfigAssetMakesEveryWaveHarder()
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameConfig>(PrototypeConfigDefaults.AssetPath);
            Assert.IsNotNull(asset);
            var game = new ResortGame(asset);

            Assert.AreEqual(0.125f, asset.wave.growthIfNotHarder, 1e-6f);
            for (int i = 1; i < asset.waves.Count; i++)
            {
                Assert.Greater(WaveProgression.GetTotalDemand(game.GetForecast(i)), WaveProgression.GetTotalDemand(game.GetForecast(i - 1)), $"Wave {i + 1}");
            }
        }
    }
}
