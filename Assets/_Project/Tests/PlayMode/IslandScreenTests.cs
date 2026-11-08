using System.Collections;
using System.Linq;
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

        private float _savedServiceTime;

        private ResortGame Game => _controller.Game;

        /// <summary>
        /// Stops the Core from serving anyone, so guests stay in their buildings and queues build up. The config is
        /// the real asset, so <see cref="RestoreServiceTime"/> puts the value back.
        /// </summary>
        private void PauseService() => Game.Config.wave.serviceTime = 1_000_000f;

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
            _savedServiceTime = Game.Config.wave.serviceTime;
        }

        [TearDown]
        public void RestoreServiceTime()
        {
            if (_controller != null) _controller.Game.Config.wave.serviceTime = _savedServiceTime;
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
        public IEnumerator GuestsWalkFromTheDockToTheirBuilding()
        {
            GuestCrowdView guests = _island.Guests;
            Assert.IsNotNull(guests, "The island has no walking guests.");

            Game.StartWave();
            Game.Tick(Game.Config.wave.incomingDuration + 12);
            yield return null;

            Assert.AreEqual(1, guests.SpawnedCount, "Guest 1 (Rooms + Drinks) steps off at 11.25 s.");
            Assert.AreEqual(1, guests.WalkingCount);

            SpriteRenderer walker = guests.GetComponentInChildren<SpriteRenderer>();
            Assert.IsNotNull(walker.sprite, "Guests are drawn with the baked walk frames.");
            Vector3 start = walker.transform.position;
            for (int i = 0; i < 20; i++) yield return null;
            Assert.AreNotEqual(start, walker.transform.position, "Guests walk.");
        }

        [UnityTest]
        public IEnumerator ASeatFillsOnlyWhenTheGuestGetsThere()
        {
            GuestCrowdView guests = _island.Guests;
            BuildingView reception = _island.GetView(BuildingId.Reception);

            Game.StartWave();
            Game.Tick(Game.Config.wave.incomingDuration + 11.3); // unit 0 arrives at 11.25 s, served at 11.58 s
            yield return null;

            Assert.AreEqual(1, Game.GetSeatedCount(BuildingId.Reception), "The Core seats the unit on arrival.");
            Assert.AreEqual(0, reception.ShownSeated, "The seat stays free while the guest is still walking.");

            guests.FastForward(15f);
            yield return null;

            Assert.AreEqual(1, reception.ShownSeated, "The seat fills when the guest walks in.");
        }

        [UnityTest]
        public IEnumerator OUR45_TappingABuildingFindsIt()
        {
            yield return null;

            foreach (BuildingId id in new[] { BuildingId.Reception, BuildingId.Restaurant, BuildingId.BeachBar })
            {
                BuildingView view = _island.GetView(id);
                Assert.AreSame(view, _island.GetBuildingAt(view.transform.position), $"A tap on the {id} finds it.");
            }

            Assert.IsNull(_island.GetBuildingAt(new Vector2(500f, 500f)), "A tap on the sea finds nothing.");
        }

        [UnityTest]
        public IEnumerator OUR45_TheSheetMovesAWorkerToTheEmptyBuilding()
        {
            yield return null;
            BuildingView restaurant = _island.GetView(BuildingId.Restaurant);
            BuildingView reception = _island.GetView(BuildingId.Reception);
            Assert.IsFalse(restaurant.ShowsWorker);

            _ui.OpenWorkerAllocation(BuildingId.Restaurant);
            yield return null;

            Assert.IsTrue(_ui.Workers.IsOpen);
            Assert.AreEqual(2, _ui.Workers.Options.Count, "Take from Reception / Take from Beach Bar.");
            StringAssert.StartsWith("TAKE FROM RECEPTION", _ui.Workers.Options[0].GetComponentInChildren<TMPro.TMP_Text>().text);
            StringAssert.Contains("Reception will be left without a Worker", _ui.Workers.Options[0].GetComponentInChildren<TMPro.TMP_Text>().text);

            _ui.Workers.Options[0].onClick.Invoke();
            yield return null;

            Assert.IsFalse(_ui.Workers.IsOpen, "Moving is instant: the sheet closes.");
            Assert.IsTrue(restaurant.ShowsWorker, "The W badge moved to the Restaurant ...");
            Assert.IsFalse(reception.ShowsWorker, "... and the Reception shows No Worker.");
            Assert.AreEqual("No Worker", reception.StatusText);
        }

        [UnityTest]
        public IEnumerator OUR45_WorkersCanBeMovedDuringTheWave()
        {
            Game.StartWave();
            Game.Tick(Game.Config.wave.incomingDuration + 30);
            yield return null;

            _ui.OpenWorkerAllocation(BuildingId.BeachBar);
            yield return null;
            Assert.AreEqual(1, _ui.Workers.Options.Count, "Move to Restaurant.");
            StringAssert.StartsWith("MOVE TO RESTAURANT", _ui.Workers.Options[0].GetComponentInChildren<TMPro.TMP_Text>().text);

            _ui.Workers.Options[0].onClick.Invoke();
            yield return null;

            Assert.IsTrue(_island.GetView(BuildingId.Restaurant).ShowsWorker);
            Assert.IsFalse(_island.GetView(BuildingId.BeachBar).ShowsWorker);
        }

        [UnityTest]
        public IEnumerator AGuestStaysInItsFirstBuildingUntilItIsServed()
        {
            GuestCrowdView guests = _island.Guests;

            Game.StartWave();
            Game.Tick(Game.Config.wave.incomingDuration + 11.3); // Rooms unit 0 seated at 11.25 s, served at 11.58 s
            yield return null;
            guests.FastForward(20f);
            yield return null;

            Assert.AreEqual(2, guests.Plan.Guests[0].Count, "Guest 1 needs Rooms and Drinks.");
            Assert.AreEqual(1, guests.GetDeliveredCount(NeedId.Rooms), "It went into the Reception ...");
            Assert.AreEqual(0, guests.GetDeliveredCount(NeedId.Drinks), "... and waits there until it is served.");
            Assert.AreEqual(1, guests.InsideCount);
            Assert.AreEqual(0, guests.WalkingCount);

            Game.Tick(0.3); // OUR-46: served 0.33 s after sitting down
            yield return null;
            guests.FastForward(0.1f);

            Assert.AreEqual(0, guests.InsideCount, "Served: it leaves the Reception ...");
            Assert.AreEqual(1, guests.WalkingCount, "... and walks on to the Beach Bar.");
            yield return null;
            Assert.AreEqual(0, _island.GetView(BuildingId.Reception).ShownSeated, "Its seat square empties when it walks out.");
        }

        [UnityTest]
        public IEnumerator EveryGuestGoesToItsFirstBuildingAndWaitsToBeServed()
        {
            GuestCrowdView guests = _island.Guests;
            PauseService();

            Game.StartWave();
            Game.Tick(Game.Config.wave.incomingDuration + Game.Config.wave.waveTimer);
            yield return null;
            guests.FastForward(60f);
            yield return null;

            Assert.AreEqual(guests.Plan.Guests.Count, guests.SpawnedCount, "Every guest stepped off the boat.");
            Assert.AreEqual(0, guests.WalkingCount, "Every guest is inside its first building.");

            foreach (BuildingId id in new[] { BuildingId.Reception, BuildingId.Restaurant, BuildingId.BeachBar })
            {
                NeedId need = Game.GetBuilding(id).Need;
                int firstVisits = guests.Plan.Guests.Count(g => g.Visits[0].Need == need);
                BuildingView view = _island.GetView(id);

                Assert.AreEqual(firstVisits, guests.GetDeliveredCount(need), $"{id}: guests whose first need is {need}.");
                Assert.AreEqual(Mathf.Min(3, firstVisits), view.ShownSeated, $"{id}: seats taken by those guests.");
                Assert.AreEqual(Mathf.Max(0, firstVisits - 3), view.ShownWaiting, $"{id}: the rest as queue squares.");
            }
        }

        [UnityTest]
        public IEnumerator OUR35_WhenEveryoneIsServedTheGuestsBoardAndTheWaveEnds()
        {
            GuestCrowdView guests = _island.Guests;

            Game.StartWave();
            Game.Tick(Game.Config.wave.incomingDuration + Game.Config.wave.waveTimer);
            yield return null;
            guests.FastForward(60f);

            // Normally everyone is served by now (0.33 s / 1 s per guest); finish anyone left directly.
            foreach (NeedId need in new[] { NeedId.Rooms, NeedId.Drinks, NeedId.Food })
            {
                while (Game.CurrentWave.FinishService(need)) { }
            }

            Game.Tick(0.1);
            Assert.AreEqual(GamePhase.WaveResolving, Game.Phase, "Everyone is served.");
            yield return null;
            Assert.AreEqual(GamePhase.WaveResolving, Game.Phase, "The guests are still on the island.");
            Assert.IsFalse(guests.AllBoarded);
            Assert.AreEqual("WAVE 1: GUESTS LEAVING", _ui.Hud.StatusText);

            guests.FastForward(120f);
            yield return null;
            yield return null;

            Assert.IsTrue(guests.AllBoarded, "Every guest made its visits and walked back to the boat.");
            Assert.AreEqual(GamePhase.WaveComplete, Game.Phase);
            Assert.AreEqual(1, Game.CompletedWaves);
            Assert.AreEqual("WAVE 1 COMPLETE", _ui.Hud.StatusText);
            foreach (NeedId need in new[] { NeedId.Rooms, NeedId.Drinks, NeedId.Food })
            {
                Assert.AreEqual(guests.Plan.CountVisits(need), guests.GetDeliveredCount(need), $"Every {need} visit was made.");
            }
        }
    }
}
