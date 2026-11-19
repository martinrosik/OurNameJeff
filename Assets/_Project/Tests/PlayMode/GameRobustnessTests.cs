using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using ResortEmpire.Core;
using ResortEmpire.Game;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ResortEmpire.Tests
{
    /// <summary>
    /// The game stays playable when the config and the scene don't match: a building without a view on the island,
    /// a view whose building leaves and comes back, and a broken config at the start. Each test plays on a copy of the
    /// config, so the shipped asset is never changed.
    /// </summary>
    public class GameRobustnessTests
    {
        private GameController _controller;
        private IslandView _island;
        private readonly List<Object> _copies = new List<Object>();

        private ResortGame Game => _controller.Game;

        [UnitySetUp]
        public IEnumerator LoadGameScene()
        {
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            yield return null; // Start() builds the UI
            yield return null; // first LateUpdate refreshes views

            _controller = Object.FindAnyObjectByType<GameController>();
            _island = Object.FindAnyObjectByType<IslandView>();
            Assert.IsNotNull(_controller, "Game scene has no GameController.");
            Assert.IsNotNull(_island, "Game scene has no IslandView.");
            _controller.SimulationSpeed = 0f; // tests drive time themselves
        }

        [TearDown]
        public void DestroyCopies()
        {
            foreach (Object copy in _copies)
            {
                if (copy != null) Object.Destroy(copy);
            }

            _copies.Clear();
        }

        private GameConfig CopyOfShippedConfig()
        {
            GameConfig copy = Object.Instantiate(Game.Config);
            _copies.Add(copy);
            return copy;
        }

        private static void SetConfig(GameController controller, GameConfig config) =>
            typeof(GameController).GetField("config", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(controller, config);

        /// <summary>Starts a new game on <paramref name="config"/> in the loaded scene.</summary>
        private void PlayWith(GameConfig config)
        {
            SetConfig(_controller, config);
            _controller.NewGame();
        }

        [UnityTest]
        public IEnumerator ABuildingWithoutAViewOnTheIslandDoesNotFreezeTheWave()
        {
            // The Pool joins the config, but the scene has no picture of it (the scene wasn't rebuilt).
            GameConfig config = CopyOfShippedConfig();
            config.buildings.Add(new BuildingDefinition
            {
                building = BuildingId.Pool,
                displayName = "Pool",
                need = NeedId.Relax,
                staffedAtStart = true,
                levels = new List<BuildingLevel> { new BuildingLevel { seats = 3 } },
            });
            config.waves[0].relax = 4;
            PlayWith(config);
            Assert.IsNull(_island.GetView(BuildingId.Pool), "The scene really has no Pool view.");

            LogAssert.Expect(LogType.Warning, new Regex("Pool has no BuildingView"));
            Game.StartWave();
            Game.Tick(Game.Config.wave.incomingDuration + Game.Config.wave.waveTimer + 600);
            yield return null;
            _island.Guests.FastForward(600f);
            yield return null;
            yield return null;

            Assert.AreEqual(GamePhase.WaveComplete, Game.Phase, "Before the fix the wave stayed in GUESTS LEAVING forever.");
            Assert.AreEqual(4, _island.Guests.GetDeliveredCount(NeedId.Relax), "The Pool's guests made their visits at the plaza.");
            Assert.IsTrue(_island.Guests.AllBoarded);
        }

        [UnityTest]
        public IEnumerator ABuildingViewComesBackOnceItsBuildingIsOnTheIslandAgain()
        {
            GameConfig shipped = Game.Config;
            GameConfig withoutRestaurant = CopyOfShippedConfig();
            withoutRestaurant.buildings.RemoveAll(b => b.building == BuildingId.Restaurant);
            BuildingView restaurant = _island.GetView(BuildingId.Restaurant);

            PlayWith(withoutRestaurant);
            yield return null;
            Assert.IsFalse(restaurant.gameObject.activeSelf, "Hidden while the Restaurant isn't on the island.");

            PlayWith(shipped);
            yield return null;
            Assert.IsTrue(restaurant.gameObject.activeSelf, "Shown again once the Restaurant is back.");
            Assert.AreEqual(3, restaurant.ShownSeats);
        }
    }
}
