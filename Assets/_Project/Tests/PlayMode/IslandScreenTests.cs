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
            Game.Tick(Game.Config.wave.incomingDuration + 11.3); // Rooms unit 0 seated at 11.25 s, served at 23.25 s
            yield return null;
            guests.FastForward(20f);
            yield return null;

            Assert.AreEqual(2, guests.Plan.Guests[0].Count, "Guest 1 needs Rooms and Drinks.");
            Assert.AreEqual(1, guests.GetDeliveredCount(NeedId.Rooms), "It went into the Reception ...");
            Assert.AreEqual(0, guests.GetDeliveredCount(NeedId.Drinks), "... and waits there until it is served.");
            Assert.AreEqual(1, guests.InsideCount);
            Assert.AreEqual(0, guests.WalkingCount);

            Game.CurrentWave.FinishService(NeedId.Rooms); // OUR-46: served (12 s after sitting down)
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
        public IEnumerator OUR37_ThePanelReplacesTheForecastAfterWelcomeGuests()
        {
            yield return null;
            Assert.IsFalse(_ui.Needs.IsVisible, "Before the wave the side area shows the forecast.");

            _ui.Hud.StartWaveButton.onClick.Invoke();
            yield return null;

            Assert.IsTrue(_ui.Needs.IsVisible, "It opens while the boat comes in.");
            Assert.IsFalse(_ui.Hud.ForecastVisible);

            Game.Tick(Game.Config.wave.incomingDuration + 1);
            yield return null;

            Assert.IsTrue(_ui.Needs.IsVisible);
            Assert.AreEqual("WAVE 1", _ui.Needs.TitleText);
            Assert.AreEqual(3, _ui.Needs.Rows.Count(row => row.IsVisible), "Rooms, Drinks, Food.");
        }

        [UnityTest]
        public IEnumerator OUR37_RowsMatchTheNeedStatus()
        {
            Game.StartWave();
            Game.Tick(Game.Config.wave.incomingDuration + 60); // the first Food guest arrives at 15 s and takes about 36 s without a Worker
            yield return null;

            var needs = new[] { NeedId.Rooms, NeedId.Drinks, NeedId.Food };
            for (int i = 0; i < needs.Length; i++)
            {
                NeedStatus status = Game.GetNeedStatus(needs[i]);
                NeedRowView row = _ui.Needs.Rows[i];
                Assert.Greater(status.Served, 0, $"{needs[i]}: someone was served by now.");
                StringAssert.EndsWith($"{status.Served}/{status.Total}", row.ProgressText);
                Assert.AreEqual((float)status.Served / status.Total, row.BarFraction, 0.001f);
            }
        }

        [UnityTest]
        public IEnumerator OUR37_RowsSayWhoHasAWorkerAndWhenSeatsAreFull()
        {
            PauseService();
            Game.StartWave();
            Game.Tick(Game.Config.wave.incomingDuration + Game.Config.wave.waveTimer);
            yield return null;

            NeedRowView rooms = _ui.Needs.Rows[0];
            NeedRowView food = _ui.Needs.Rows[2];
            int roomsWaiting = Game.GetNeedStatus(NeedId.Rooms).Waiting;
            int foodWaiting = Game.GetNeedStatus(NeedId.Food).Waiting;

            Assert.AreEqual("Reception: Worker", rooms.StaffingText);
            Assert.AreEqual($"Full: 3/3 seats, {roomsWaiting} waiting", rooms.QueueText);
            Assert.AreEqual("Restaurant: No Worker (slow)", food.StaffingText);
            Assert.AreEqual($"{foodWaiting} waiting", food.QueueText, "Without a Worker the problem is not the size.");
        }

        [UnityTest]
        public IEnumerator OUR38_OUR39_TheRowSaysQueueGrowingThenCatchingUp()
        {
            // Food 20 on the empty Restaurant turns into a bottleneck. The asset is restored afterwards.
            int savedFood = Game.Config.waves[0].food;
            Game.Config.waves[0].food = 20;
            try
            {
                Game.StartWave();
                Game.Tick(Game.Config.wave.incomingDuration);
                for (int i = 0; i < 60; i++) Game.Tick(1);
                yield return null;

                NeedRowView food = _ui.Needs.Rows[2];
                Assert.AreEqual(NeedState.Bottleneck, Game.GetNeedState(NeedId.Food));
                Assert.AreEqual("Queue growing!", food.StateText);
                Assert.AreEqual(GreyboxPalette.Warning, food.StateColor);
                Assert.AreEqual("OK", _ui.Needs.Rows[0].StateText, "The Reception keeps up.");

                Game.MoveWorker(BuildingId.Reception, BuildingId.Restaurant);
                yield return null;

                Assert.AreEqual("Catching up", food.StateText, "Catching up in the same moment as the move.");
                Assert.AreEqual(GreyboxPalette.WorkerOn, food.StateColor);
            }
            finally
            {
                Game.Config.waves[0].food = savedFood;
            }
        }

        [UnityTest]
        public IEnumerator OUR37_TheHeaderSaysWhenAllGuestsHaveArrived()
        {
            PauseService();
            Game.StartWave();
            Game.Tick(Game.Config.wave.incomingDuration + 30);
            yield return null;
            Assert.AreEqual("Guests arriving...", _ui.Needs.ArrivalsText);

            Game.Tick(Game.Config.wave.waveTimer);
            yield return null;
            Assert.AreEqual("All guests arrived", _ui.Needs.ArrivalsText);
        }

        [UnityTest]
        public IEnumerator OUR37_TappingARowOpensWorkerAllocationForItsBuilding()
        {
            Game.StartWave();
            Game.Tick(Game.Config.wave.incomingDuration + 1);
            yield return null;

            _ui.Needs.Rows[2].Button.onClick.Invoke();
            yield return null;

            Assert.IsTrue(_ui.Workers.IsOpen);
            Assert.AreEqual(BuildingId.Restaurant, _ui.Workers.Building);
        }

        [UnityTest]
        public IEnumerator OUR37_ThePanelIsHiddenAfterTheWaveAndShowsTheFinalCounts()
        {
            GuestCrowdView guests = _island.Guests;

            Game.StartWave();
            Game.Tick(Game.Config.wave.incomingDuration + Game.Config.wave.waveTimer + 60);
            yield return null;

            Assert.AreEqual(GamePhase.WaveResolving, Game.Phase, "Everyone is served.");
            Assert.IsTrue(_ui.Needs.IsVisible, "The final counts stay up while the guests leave.");
            Assert.AreEqual("Rooms  8/8", _ui.Needs.Rows[0].ProgressText);
            Assert.AreEqual("Food  6/6", _ui.Needs.Rows[2].ProgressText);
            Assert.AreEqual("All served", _ui.Needs.Rows[2].StateText);

            guests.FastForward(180f);
            yield return null;
            yield return null;

            Assert.AreEqual(GamePhase.WaveComplete, Game.Phase);
            Assert.IsFalse(_ui.Needs.IsVisible);
        }

        [UnityTest]
        public IEnumerator OUR35_WhenEveryoneIsServedTheGuestsBoardAndTheWaveEnds()
        {
            GuestCrowdView guests = _island.Guests;

            Game.StartWave();
            Game.Tick(Game.Config.wave.incomingDuration + Game.Config.wave.waveTimer);
            yield return null;
            guests.FastForward(60f);

            // Some guests are still being served (12 s / 36 s per guest); finish them directly.
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

        [UnityTest]
        public IEnumerator OUR38_OUR39_AnOverloadedBuildingGlowsRedThenGreenWhenAWorkerHelps()
        {
            // Build doc numbers: 12 s per guest, Food 20 on the empty Restaurant. The asset is restored afterwards.
            Game.Config.wave.serviceTime = 12f;
            int savedFood = Game.Config.waves[0].food;
            Game.Config.waves[0].food = 20;
            try
            {
                BuildingView restaurant = _island.GetView(BuildingId.Restaurant);
                Game.StartWave();
                Game.Tick(Game.Config.wave.incomingDuration);
                for (int i = 0; i < 60; i++) Game.Tick(1);
                yield return null;

                Assert.AreEqual(NeedState.Bottleneck, Game.GetBuildingState(BuildingId.Restaurant));
                Assert.AreEqual(NeedState.Bottleneck, restaurant.ShownState);
                Assert.IsTrue(restaurant.ShowsStateGlow, "Red glow around the Restaurant.");
                Assert.IsFalse(_island.GetView(BuildingId.Reception).ShowsStateGlow, "The Reception keeps up.");
                Assert.IsTrue(_ui.StateToast.IsVisible);
                Assert.AreEqual("! Restaurant queue growing", _ui.StateToast.Title);
                Assert.AreEqual("\"Food takes a bit too long.\"", _ui.StateToast.Quote);

                Game.MoveWorker(BuildingId.Reception, BuildingId.Restaurant);
                yield return null;

                Assert.AreEqual(NeedState.Recovering, restaurant.ShownState, "Catching up in the same moment as the move.");
                Assert.IsTrue(restaurant.ShowsStateGlow, "Green glow: still busy, but getting better.");
                Assert.AreEqual("Restaurant is catching up", _ui.StateToast.Title);
            }
            finally
            {
                Game.Config.waves[0].food = savedFood;
            }
        }

        /// <summary>
        /// The whole of wave 1 with the shipped config and the starting Workers, in 0.05 s steps with the guests
        /// walking in lockstep: when the last guest arrives, when the last one is served, and when the last one is
        /// back on the boat (the wave end). Logs the timeline as "[Wave 1 timeline] ...".
        /// </summary>
        [UnityTest]
        public IEnumerator Wave1Timeline_ArrivalsServiceAndBoarding()
        {
            const float step = 0.05f;
            GuestCrowdView guests = _island.Guests;
            NeedId[] needs = { NeedId.Rooms, NeedId.Drinks, NeedId.Food };
            double staffed = Game.GetServiceSeconds(BuildingId.Reception);
            double empty = Game.GetServiceSeconds(BuildingId.Restaurant);

            Game.StartWave();
            Game.Tick(Game.Config.wave.incomingDuration);
            Assert.AreEqual(GamePhase.WaveActive, Game.Phase);

            double time = 0, lastArrival = 0, lastStepOff = 0, allServed = -1, lastBoarded = -1;
            int arrived = 0, steppedOff = 0;
            while (time < 900 && Game.Phase != GamePhase.WaveComplete)
            {
                Game.Tick(step);
                guests.FastForward(step);
                _island.Refresh();
                time += step;

                int nowArrived = needs.Sum(n => Game.GetNeedStatus(n).Arrived);
                if (nowArrived > arrived) { arrived = nowArrived; lastArrival = time; }
                if (guests.SpawnedCount > steppedOff) { steppedOff = guests.SpawnedCount; lastStepOff = time; }
                if (allServed < 0 && Game.Phase == GamePhase.WaveResolving) allServed = Game.LastWaveResult.Duration;
                if (Game.Phase == GamePhase.WaveComplete) lastBoarded = time;
            }

            Debug.Log($"[Wave 1 timeline] service with a Worker {staffed:0.##} s, without {empty:0.##} s; " +
                      $"last unit arrives {lastArrival:0.##} s ({arrived} units); last guest steps off the boat {lastStepOff:0.##} s ({steppedOff} guests); " +
                      $"everyone served {allServed:0.##} s; last guest back on the boat (wave end) {lastBoarded:0.##} s");
            yield return null;

            Assert.AreEqual(12, staffed, 1e-4, "Build doc: 12 s per guest with a Worker.");
            Assert.AreEqual(12 / 0.33, empty, 1e-4, "Build doc: 12 / 0.33 = 36.4 s without.");
            Assert.AreEqual(22, arrived);
            Assert.AreEqual(90, lastArrival, step + 1e-6, "Guests stop arriving at 90 s (wave_timer).");
            Assert.AreEqual(90 + empty, allServed, 0.1, "The last Food guest (90 s) on the empty Restaurant is done 36.4 s later.");
            Assert.AreEqual(GamePhase.WaveComplete, Game.Phase, "The wave ends once the last guest has boarded.");
            Assert.Greater(lastBoarded, allServed, "Guests still walk back to the boat after the last service.");
            Assert.Less(lastBoarded, allServed + 30, "The walk back takes seconds, not minutes.");
        }

        /// <summary>Plays the wave that was just started until every guest is back on the boat.</summary>
        private IEnumerator FinishStartedWave()
        {
            Game.Tick(Game.Config.wave.incomingDuration + Game.Config.wave.waveTimer + 120); // wave 2's Food queue on the empty Restaurant needs about 65 s after the arrivals
            yield return null;
            Assert.AreEqual(GamePhase.WaveResolving, Game.Phase, "Everyone is served.");

            _island.Guests.FastForward(300f);
            yield return null;
            yield return null;
            Assert.AreEqual(GamePhase.WaveComplete, Game.Phase, "Every guest is back on the boat.");
        }

        [UnityTest]
        public IEnumerator OUR40_TheOverlayAppearsOnlyOnceTheWaveIsComplete()
        {
            yield return null;
            Assert.IsFalse(_ui.WaveComplete.IsVisible, "Not before the wave.");

            Game.StartWave();
            Game.Tick(Game.Config.wave.incomingDuration + 1);
            yield return null;
            Assert.AreEqual(GamePhase.WaveActive, Game.Phase);
            Assert.IsFalse(_ui.WaveComplete.IsVisible, "Never as a pop-up during the wave.");

            yield return FinishStartedWave();

            Assert.IsTrue(_ui.WaveComplete.IsVisible);
            Assert.AreEqual("WAVE 1 COMPLETE", _ui.WaveComplete.TitleText);
            Assert.AreEqual("Wave 1 has ended. Well done!", _ui.WaveComplete.MessageText);
            Assert.AreEqual("12/12", _ui.WaveComplete.GuestsServedText);
            Assert.IsFalse(_ui.Hud.StartWaveButton.gameObject.activeInHierarchy, "Continue is the only main button.");
        }

        [UnityTest]
        public IEnumerator OUR40_ContinueReturnsToTheIslandWithTheNextForecast()
        {
            Game.StartWave();
            yield return FinishStartedWave();

            _ui.WaveComplete.ContinueButton.onClick.Invoke();
            yield return null;

            Assert.AreEqual(GamePhase.AwaitingWave, Game.Phase);
            Assert.IsFalse(_ui.WaveComplete.IsVisible);
            Assert.AreEqual("READY FOR GUESTS", _ui.Hud.StatusText);
            Assert.IsTrue(_ui.Hud.ForecastVisible);
            Assert.AreEqual(2, Game.GetNextWaveForecast().WaveNumber);
            Assert.IsTrue(_ui.Hud.StartWaveButton.gameObject.activeInHierarchy, "Welcome Guests is back.");
            StringAssert.Contains("Wave 2", _ui.Hud.StartWaveButton.GetComponentInChildren<TMPro.TMP_Text>().text);
        }

        [UnityTest]
        public IEnumerator OUR40_TheNextWaveEndsWithItsOwnNumber()
        {
            Game.StartWave();
            yield return FinishStartedWave();
            _ui.WaveComplete.ContinueButton.onClick.Invoke();
            yield return null;

            _ui.Hud.StartWaveButton.onClick.Invoke();
            yield return null;
            Assert.AreEqual(GamePhase.WaveIncoming, Game.Phase);
            Assert.AreEqual(2, Game.CurrentWave.WaveNumber);

            yield return FinishStartedWave();

            Assert.IsTrue(_ui.WaveComplete.IsVisible);
            Assert.AreEqual("WAVE 2 COMPLETE", _ui.WaveComplete.TitleText);
            Assert.AreEqual("18/18", _ui.WaveComplete.GuestsServedText);
        }
    }
}
