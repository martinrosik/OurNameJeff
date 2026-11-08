using System.Collections;
using NUnit.Framework;
using ResortEmpire.Core;
using ResortEmpire.Game;
using ResortEmpire.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ResortEmpire.Tests
{
    /// <summary>
    /// The Game scene through its UI: the island with 3 buildings and their indicators (OUR-31, OUR-48, OUR-50),
    /// the forecast (OUR-33) and Welcome Guests (OUR-27). Time is driven with Tick() so tests run fast.
    /// </summary>
    public class IslandScreenTests
    {
        private GameController _controller;
        private GreyboxUI _ui;
        private IslandView _island;

        private ResortGame Game => _controller.Game;

        [UnitySetUp]
        public IEnumerator LoadGameScene()
        {
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            yield return null; // Start() builds the UI
            yield return null; // first LateUpdate refreshes views

            _controller = Object.FindAnyObjectByType<GameController>();
            _ui = Object.FindAnyObjectByType<GreyboxUI>();
            _island = Object.FindAnyObjectByType<IslandView>();
            Assert.IsNotNull(_controller, "Game scene has no GameController.");
            Assert.IsNotNull(_ui, "Game scene has no GreyboxUI.");
            Assert.IsNotNull(_island, "Game scene has no IslandView.");
            _controller.SimulationSpeed = 0f; // tests drive time themselves
        }

        [UnityTest]
        public IEnumerator OUR31_IslandShowsTheThreeStartingBuildings()
        {
            yield return null;

            Assert.IsNotNull(_island.GetView(BuildingId.Reception));
            Assert.IsNotNull(_island.GetView(BuildingId.Restaurant));
            Assert.IsNotNull(_island.GetView(BuildingId.BeachBar));
            Assert.IsNull(_island.GetView(BuildingId.Pool));
        }

        [UnityTest]
        public IEnumerator OUR48_WorkerIndicatorShowsWhoHasAWorker()
        {
            yield return null;

            Assert.IsTrue(_island.GetView(BuildingId.Reception).ShowsWorker);
            Assert.IsTrue(_island.GetView(BuildingId.BeachBar).ShowsWorker);
            Assert.IsFalse(_island.GetView(BuildingId.Restaurant).ShowsWorker);
            Assert.AreEqual("No Worker", _island.GetView(BuildingId.Restaurant).StatusText);
            Assert.AreEqual(string.Empty, _island.GetView(BuildingId.Reception).StatusText);
        }

        [UnityTest]
        public IEnumerator OUR50_CapacityIndicatorShowsThreeSeatsPerBuilding()
        {
            yield return null;

            foreach (BuildingId id in new[] { BuildingId.Reception, BuildingId.Restaurant, BuildingId.BeachBar })
            {
                Assert.AreEqual(3, _island.GetView(id).ShownSeats, $"{id} shows its level-1 capacity.");
            }
        }

        [UnityTest]
        public IEnumerator OUR33_ForecastIsShownBeforeTheWave()
        {
            yield return null;

            Assert.AreEqual(GamePhase.AwaitingWave, Game.Phase);
            Assert.IsTrue(_ui.Hud.ForecastVisible);
            Assert.AreEqual(3, _ui.Hud.VisibleForecastRows, "Rooms, Drinks, Food.");
            Assert.AreEqual("READY FOR GUESTS", _ui.Hud.StatusText);
        }

        [UnityTest]
        public IEnumerator OUR27_WelcomeGuestsStartsTheWave()
        {
            Assert.IsTrue(_ui.Hud.StartWaveButton.gameObject.activeInHierarchy);

            _ui.Hud.StartWaveButton.onClick.Invoke();
            yield return null;

            Assert.AreEqual(GamePhase.WaveIncoming, Game.Phase);
            Assert.AreEqual("BOAT ARRIVING", _ui.Hud.StatusText);
            Assert.IsFalse(_ui.Hud.StartWaveButton.gameObject.activeInHierarchy);
            Assert.IsFalse(_ui.Hud.ForecastVisible);

            Game.Tick(Game.Config.wave.incomingDuration + 30);
            yield return null;

            Assert.AreEqual(GamePhase.WaveActive, Game.Phase);
            Assert.AreEqual("WAVE 1 IN PROGRESS", _ui.Hud.StatusText);
        }

        [UnityTest]
        public IEnumerator OUR34_ArrivingGuestsFillSeatsThenQueueInFrontOfTheBuilding()
        {
            Game.StartWave();
            Game.Tick(Game.Config.wave.incomingDuration + Game.Config.wave.waveTimer);
            yield return null;

            BuildingView reception = _island.GetView(BuildingId.Reception);
            Assert.AreEqual(3, Game.GetSeatedCount(BuildingId.Reception));
            Assert.AreEqual(5, reception.ShownWaiting, "Rooms 8: 3 seated, 5 waiting.");
            Assert.AreEqual(3, _island.GetView(BuildingId.Restaurant).ShownWaiting, "Food 6: 3 seated, 3 waiting.");
        }
    }
}
