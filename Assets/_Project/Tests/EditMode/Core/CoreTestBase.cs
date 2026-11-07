using System;
using System.Collections.Generic;
using NUnit.Framework;
using ResortEmpire.Core;
using ResortEmpire.Editor;
using UnityEngine;

namespace ResortEmpire.Tests
{
    /// <summary>
    /// Shared setup for Core tests: a fresh config with the Build doc values for every test,
    /// destroyed afterwards. Tests reshape the config (usually the wave list) before creating the game.
    /// </summary>
    public abstract class CoreTestBase
    {
        protected GameConfig Config { get; private set; }

        [SetUp]
        public void CreateConfig()
        {
            Config = PrototypeConfigDefaults.Create();
        }

        [TearDown]
        public void DestroyConfig()
        {
            if (Config != null)
            {
                UnityEngine.Object.DestroyImmediate(Config);
            }
        }

        /// <summary>Replaces the wave list with one wave. Default layout: Reception and Beach Bar staffed, Restaurant empty.</summary>
        protected void UseSingleWave(int rooms, int drinks, int food, int relax = 0, int oceanView = 0, bool rush = false)
        {
            Config.waves = new List<WaveDefinition>
            {
                new WaveDefinition
                {
                    guests = 10,
                    rooms = rooms,
                    drinks = drinks,
                    food = food,
                    relax = relax,
                    oceanView = oceanView,
                    rush = new RushEventDefinition { enabled = rush, startTime = 45f, units = 6, duration = 10f },
                },
            };
        }

        protected ResortGame NewGame() => new ResortGame(Config);

        /// <summary>Starts the next wave and skips WAVE_INCOMING.</summary>
        protected static void StartWaveAndArrive(ResortGame game)
        {
            Assert.IsTrue(game.StartWave(), "StartWave was refused.");
            game.Tick(game.Config.wave.incomingDuration);
            Assert.AreEqual(GamePhase.WaveActive, game.Phase);
        }

        /// <summary>Ticks in fixed steps until <paramref name="condition"/> holds. Fails if it never does.</summary>
        protected static void TickUntil(ResortGame game, Func<bool> condition, double step = 0.1, double maxSeconds = 3600)
        {
            for (double elapsed = 0; elapsed < maxSeconds; elapsed += step)
            {
                if (condition())
                {
                    return;
                }

                game.Tick(step);
            }

            Assert.Fail($"Condition not reached within {maxSeconds} s (phase {game.Phase}).");
        }

        /// <summary>Plays the current/next wave to WAVE_COMPLETE without any player input.</summary>
        protected static WaveResult PlayWaveToEnd(ResortGame game, double step = 0.1)
        {
            if (game.Phase == GamePhase.AwaitingWave)
            {
                Assert.IsTrue(game.StartWave(), "StartWave was refused.");
            }

            TickUntil(game, () => game.Phase == GamePhase.WaveComplete, step);
            return game.LastWaveResult;
        }

        protected static double WaveTime(ResortGame game) => game.CurrentWave.Time;
    }
}
