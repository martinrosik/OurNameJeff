using System.Collections;
using System.Linq;
using NUnit.Framework;
using ResortEmpire.Core;
using ResortEmpire.Game;
using ResortEmpire.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

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
        private bool _savedSuddenRush;
        private int _savedSuddenRushWave;

        private ResortGame Game => _controller.Game;

        /// <summary>
        /// Stops the Core from serving anyone, so guests stay in their buildings and queues build up. The config is
        /// the real asset, so <see cref="RestoreServiceTime"/> puts the value back.
        /// </summary>
        private void PauseService() => Game.Config.wave.serviceTime = 1_000_000f;

        /// <summary>The icon, the bar and the state label share the state's color (Figma S2).</summary>
        private static void AssertRowColor(NeedRowView row, Color expected)
        {
            Assert.AreEqual(expected, row.IconColor, "Icon color.");
            Assert.AreEqual(expected, row.BarColor, "Bar color.");
            Assert.AreEqual(expected, row.StateColor, "State label color.");
        }

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

            // A Sudden rush adds guests; tests that count arrivals need the plain wave. The Sudden rush tests turn it
            // back on. The config is the real asset, so TearDown restores the values.
            _savedSuddenRush = Game.Config.wave.suddenRushEnabled;
            _savedSuddenRushWave = Game.Config.wave.suddenRushWave;
            Game.Config.wave.suddenRushEnabled = false;
        }

        [TearDown]
        public void RestoreServiceTime()
        {
            if (_controller == null) return;
            _controller.Game.Config.wave.serviceTime = _savedServiceTime;
            _controller.Game.Config.wave.suddenRushEnabled = _savedSuddenRush;
            _controller.Game.Config.wave.suddenRushWave = _savedSuddenRushWave;
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
                Assert.AreEqual(Game.GetNeedStatus(need).Waiting, view.ShownWaiting, $"{id}: queue squares match the needs panel.");
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
            Game.Tick(Game.Config.wave.incomingDuration);
            PlayInLockstep(60); // the first Food guest arrives at 15 s and takes about 36 s without a Worker
            yield return null;

            var needs = new[] { NeedId.Rooms, NeedId.Drinks, NeedId.Food };
            for (int i = 0; i < needs.Length; i++)
            {
                NeedStatus status = Game.GetNeedStatus(needs[i]);
                int shown = _island.Guests.GetShownServed(needs[i]);
                NeedRowView row = _ui.Needs.Rows[i];
                Assert.Greater(shown, 0, $"{needs[i]}: someone was served and walked out by now.");
                Assert.LessOrEqual(shown, status.Served, "Never ahead of the Core.");
                StringAssert.EndsWith($"{shown}/{status.Total}", row.ProgressText);
                Assert.AreEqual((float)shown / status.Total, row.BarFraction, 0.001f);
            }
        }

        /// <summary>Plays the wave for <paramref name="seconds"/> with the guests walking in step (0.05 s steps).</summary>
        private void PlayInLockstep(double seconds)
        {
            const float step = 0.05f;
            for (double t = 0; t < seconds - 1e-9; t += step)
            {
                Game.Tick(step);
                _island.Guests.FastForward(step);
                _island.Refresh();
            }
        }

        [UnityTest]
        public IEnumerator TheNeedsPanelSaysFullOnlyWhenTheGuestsAreReallyInside()
        {
            GuestCrowdView guests = _island.Guests;
            PauseService(); // nobody leaves a seat, so the Core's seats fill up
            Game.StartWave();
            Game.Tick(Game.Config.wave.incomingDuration + 40); // Drinks at 11.25, 22.5 and 33.75 s: 3 seated in the Core
            yield return null;

            Assert.AreEqual(3, Game.GetNeedStatus(NeedId.Drinks).Seated, "The Core has the Beach Bar full ...");
            Assert.AreEqual(0, guests.GetShownSeated(NeedId.Drinks), "... but the guests are still on their way.");
            NeedRowView drinks = _ui.Needs.Rows[1];
            Assert.AreEqual("0 waiting", drinks.QueueText, "No \"Full: 3/3 seats\" while nobody is there.");

            // Once guests have walked in, the row follows the Beach Bar's seat squares, never the Core's seats.
            guests.FastForward(30f);
            yield return null;

            int inside = _island.GetView(BuildingId.BeachBar).ShownSeated;
            Assert.Greater(inside, 0, "Some guests sit in the Beach Bar now.");
            Assert.AreEqual(inside == 3 ? "Full: 3/3 seats, 0 waiting" : "0 waiting", drinks.QueueText);
        }

        [UnityTest]
        public IEnumerator TheNeedsPanelSaysAllServedOnlyOnceTheLastGuestHasWalkedOut()
        {
            GuestCrowdView guests = _island.Guests;
            Game.StartWave();
            Game.Tick(Game.Config.wave.incomingDuration);

            // The Core is done with Rooms (the last unit arrives at 90 s and takes 12 s) ...
            while (Game.GetNeedState(NeedId.Rooms) != NeedState.Satisfied && Game.WaveTime < 200) PlayInLockstep(0.05);
            Assume.That(guests.GetShownServed(NeedId.Rooms), Is.LessThan(8), "The last Rooms guest is still in the Reception.");
            yield return null;

            // ... but the panel waits for the last guest to walk out.
            NeedRowView rooms = _ui.Needs.Rows[0];
            StringAssert.EndsWith($"{guests.GetShownServed(NeedId.Rooms)}/8", rooms.ProgressText);
            Assert.AreEqual("OK", rooms.StateText, "Not \"All served\" while the count says 7/8.");
            AssertRowColor(rooms, GreyboxPalette.NeedInProgress);

            while (guests.GetShownServed(NeedId.Rooms) < 8 && Game.WaveTime < 300) PlayInLockstep(0.05);
            yield return null;

            Assert.AreEqual("Rooms  8/8", rooms.ProgressText);
            Assert.AreEqual("All served", rooms.StateText);
            AssertRowColor(rooms, GreyboxPalette.NeedGood);
        }

        [UnityTest]
        public IEnumerator TheNeedsPanelCountsAGuestServedOnlyOnceItHasWalkedOut()
        {
            GuestCrowdView guests = _island.Guests;
            BuildingView reception = _island.GetView(BuildingId.Reception);
            Game.StartWave();
            Game.Tick(Game.Config.wave.incomingDuration);

            // Rooms unit 0 arrives at 11.25 s and the Core serves it 12 s later, while its guest is still inside.
            while (Game.GetNeedStatus(NeedId.Rooms).Served == 0) PlayInLockstep(0.05);
            yield return null;

            Assert.AreEqual(1, Game.GetNeedStatus(NeedId.Rooms).Served, "The Core is done with the first Rooms guest ...");
            Assert.AreEqual(1, reception.ShownSeated, "... but on the island the guest still sits in the Reception ...");
            Assert.AreEqual("Rooms  0/8", _ui.Needs.Rows[0].ProgressText, "... so the panel doesn't count it yet.");
            Assert.AreEqual(0f, _ui.Needs.Rows[0].BarFraction);

            while (guests.GetShownServed(NeedId.Rooms) == 0 && Game.WaveTime < 60) PlayInLockstep(0.05);
            yield return null;

            Assert.AreEqual(0, reception.ShownSeated, "The guest walked out: its seat square is empty ...");
            Assert.AreEqual("Rooms  1/8", _ui.Needs.Rows[0].ProgressText, "... and only now the panel counts it.");
        }

        [UnityTest]
        public IEnumerator OUR37_RowsSayWhoHasAWorkerAndWhenSeatsAreFull()
        {
            PauseService();
            Game.StartWave();
            Game.Tick(Game.Config.wave.incomingDuration + Game.Config.wave.waveTimer);
            yield return null;
            _island.Guests.FastForward(60f); // the guests walk in; the panel counts seats the island shows taken
            yield return null;

            NeedRowView rooms = _ui.Needs.Rows[0];
            NeedRowView food = _ui.Needs.Rows[2];
            Assert.AreEqual(3, _island.GetView(BuildingId.Reception).ShownSeated, "Three guests sit in the Reception.");
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
                AssertRowColor(food, GreyboxPalette.NeedBottleneck);
                Assert.AreEqual("OK", _ui.Needs.Rows[0].StateText, "The Reception keeps up.");
                AssertRowColor(_ui.Needs.Rows[0], GreyboxPalette.NeedInProgress);

                Game.MoveWorker(BuildingId.Reception, BuildingId.Restaurant);
                yield return null;

                Assert.AreEqual("Catching up", food.StateText, "Catching up in the same moment as the move.");
                AssertRowColor(food, GreyboxPalette.NeedGood);
            }
            finally
            {
                Game.Config.waves[0].food = savedFood;
            }
        }

        [UnityTest]
        public IEnumerator NeedIcons_TheForecastShowsEachNeedsIconInOneCalmColor()
        {
            yield return null;

            var needs = new[] { NeedId.Rooms, NeedId.Drinks, NeedId.Food };
            for (int i = 0; i < needs.Length; i++)
            {
                Image icon = _ui.Hud.GetForecastIcon(i);
                Assert.IsTrue(icon.enabled, $"{needs[i]} has an icon.");
                Assert.AreEqual($"Icon_Need_{needs[i]}", icon.sprite.name);
                Assert.AreEqual(GreyboxPalette.Ink, icon.color, "Before the wave nothing is going well or badly yet.");
            }
        }

        [UnityTest]
        public IEnumerator NeedIcons_TheNeedsPanelShowsEachNeedsIcon()
        {
            Game.StartWave();
            Game.Tick(Game.Config.wave.incomingDuration + 1);
            yield return null;

            var needs = new[] { NeedId.Rooms, NeedId.Drinks, NeedId.Food };
            for (int i = 0; i < needs.Length; i++)
            {
                Assert.AreEqual($"Icon_Need_{needs[i]}", _ui.Needs.Rows[i].IconSprite.name);
                AssertRowColor(_ui.Needs.Rows[i], GreyboxPalette.NeedInProgress);
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

            // The panel counts a guest once it has walked out of its building: let them finish their visits.
            for (int i = 0; i < 4000 && (guests.GetShownServed(NeedId.Rooms) < 8 || guests.GetShownServed(NeedId.Food) < 6); i++)
            {
                guests.FastForward(0.05f);
            }

            yield return null;
            Assert.AreEqual(GamePhase.WaveResolving, Game.Phase, "The guests are still on their way to the boat.");
            Assert.IsTrue(_ui.Needs.IsVisible, "The final counts stay up while the guests leave.");
            Assert.AreEqual("Rooms  8/8", _ui.Needs.Rows[0].ProgressText);
            Assert.AreEqual("Food  6/6", _ui.Needs.Rows[2].ProgressText);
            Assert.AreEqual("All served", _ui.Needs.Rows[2].StateText);
            AssertRowColor(_ui.Needs.Rows[2], GreyboxPalette.NeedGood);

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
            Assert.Less(lastBoarded, allServed + 10, "Once everyone is served the guests only walk back: WAVE_RESOLVING is short (OUR-35).");
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
            Assert.AreEqual("12/12", _ui.WaveComplete.StatText);
            Assert.IsTrue(_ui.WaveComplete.StarsVisible);
            Assert.AreEqual(5, _ui.WaveComplete.EarnedStars, "Nobody waited in wave 1: Perfect shows all five stars.");
            Assert.IsTrue(_ui.WaveComplete.PerfectVisible, "PERFECT! tells Perfect apart from five stars.");
            Assert.AreEqual("100%", _ui.WaveComplete.SatisfactionText);
            Assert.IsFalse(_ui.Hud.StartWaveButton.gameObject.activeInHierarchy, "Continue is the only main button.");
        }

        [UnityTest]
        public IEnumerator WaveEvaluation_ALongQueueShowsALowerRatingAndSatisfaction()
        {
            int savedFood = Game.Config.waves[0].food;
            Game.Config.waves[0].food = 30; // a big Food demand on the empty Restaurant
            try
            {
                Game.StartWave();
                Game.Tick(Game.Config.wave.incomingDuration + Game.Config.wave.waveTimer + 600);
                yield return null;
                _island.Guests.FastForward(600f);
                yield return null;
                yield return null;
                Assert.AreEqual(GamePhase.WaveComplete, Game.Phase);

                WaveResult result = Game.LastWaveResult;
                Assert.Less(result.Rating, WaveRating.ThreeStars);
                Assert.IsTrue(_ui.WaveComplete.IsVisible);
                Assert.AreEqual($"{(int)System.Math.Floor(result.Satisfaction + 1e-6)}%", _ui.WaveComplete.SatisfactionText);
                Assert.AreEqual((int)result.Rating, _ui.WaveComplete.EarnedStars, "One gold star per tier reached, the rest grey.");
                Assert.IsFalse(_ui.WaveComplete.PerfectVisible);
                Assert.AreEqual("WAVE 1 COMPLETE", _ui.WaveComplete.TitleText, "Calm tone: the wave still completes.");
            }
            finally
            {
                Game.Config.waves[0].food = savedFood;
            }
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
            Assert.AreEqual("18/18", _ui.WaveComplete.StatText);
            Assert.AreEqual(2, Game.LastWaveResult.WaveNumber, "The card shows wave 2's own evaluation.");
            Assert.AreEqual(WaveRating.FiveStars, Game.LastWaveResult.Rating, "Wave 2 left alone: a short Food queue, just under Perfect.");
            Assert.AreEqual(5, _ui.WaveComplete.EarnedStars);
            Assert.IsFalse(_ui.WaveComplete.PerfectVisible, "Five stars without PERFECT! is not a Perfect wave.");
            Assert.AreEqual($"{(int)System.Math.Floor(Game.LastWaveResult.Satisfaction + 1e-6)}%", _ui.WaveComplete.SatisfactionText);
        }

        [UnityTest]
        public IEnumerator OUR45_ABuildingThatIsCatchingUpIsNotCalledTooSmall()
        {
            int savedFood = Game.Config.waves[0].food;
            Game.Config.waves[0].food = 20;
            try
            {
                Game.StartWave();
                Game.Tick(Game.Config.wave.incomingDuration);
                for (int i = 0; i < 60; i++) Game.Tick(1);
                Game.MoveWorker(BuildingId.Reception, BuildingId.Restaurant);
                Assert.AreEqual(NeedState.Recovering, Game.GetNeedState(NeedId.Food));
                Assert.Greater(Game.GetNeedStatus(NeedId.Food).Waiting, Game.Config.wave.bottleneckThreshold);

                _ui.OpenWorkerAllocation(BuildingId.Restaurant);
                yield return null;

                StringAssert.DoesNotContain("too small", _ui.Workers.SubtitleText);
            }
            finally
            {
                Game.Config.waves[0].food = savedFood;
            }
        }

        [UnityTest]
        public IEnumerator OUR45_ABuildingWhoseQueueStillGrowsWithAWorkerIsCalledTooSmall()
        {
            int savedFood = Game.Config.waves[0].food;
            Game.Config.waves[0].food = 40;
            try
            {
                Game.MoveWorker(BuildingId.Reception, BuildingId.Restaurant);
                Game.StartWave();
                Game.Tick(Game.Config.wave.incomingDuration);
                for (int i = 0; i < 60; i++) Game.Tick(1);
                Assert.AreEqual(NeedState.Bottleneck, Game.GetNeedState(NeedId.Food));

                _ui.OpenWorkerAllocation(BuildingId.Restaurant);
                yield return null;

                StringAssert.Contains("too small", _ui.Workers.SubtitleText);
            }
            finally
            {
                Game.Config.waves[0].food = savedFood;
            }
        }

        [UnityTest]
        public IEnumerator OUR45_TheSheetKeepsItsNumbersCurrentWhileOpen()
        {
            Game.StartWave();
            Game.Tick(Game.Config.wave.incomingDuration + 20);
            _ui.OpenWorkerAllocation(BuildingId.Restaurant);
            yield return null;
            string before = _ui.Workers.Options[0].GetComponentInChildren<TMPro.TMP_Text>().text;

            Game.Tick(30);
            yield return null;

            NeedStatus rooms = Game.GetNeedStatus(NeedId.Rooms);
            NeedStatus food = Game.GetNeedStatus(NeedId.Food);
            string after = _ui.Workers.Options[0].GetComponentInChildren<TMPro.TMP_Text>().text;
            Assert.AreNotEqual(before, after, "The numbers moved on while the sheet was open.");
            StringAssert.Contains($"Rooms: {rooms.Served}/{rooms.Total} served, {rooms.Waiting} waiting", after);
            StringAssert.Contains($"Food: {food.Served}/{food.Total} served, {food.Waiting} waiting", _ui.Workers.SubtitleText);
        }

        [UnityTest]
        public IEnumerator OUR35_InEveryWaveTheGuestsLeaveSoonAfterTheLastService()
        {
            const float step = 0.05f;
            GuestCrowdView guests = _island.Guests;
            for (int wave = 0; wave < Game.Config.waves.Count; wave++)
            {
                Game.StartWave();
                Game.Tick(Game.Config.wave.incomingDuration);
                double resolving = 0;
                while (Game.Phase != GamePhase.WaveComplete && resolving < 60)
                {
                    Game.Tick(step);
                    guests.FastForward(step);
                    _island.Refresh();
                    if (Game.Phase != GamePhase.WaveActive) resolving += step;
                }

                Assert.Less(resolving, 8, $"Wave {wave + 1}: once everyone is served the guests only walk back (OUR-35).");
                Game.ContinueAfterWave();
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator AfterTheLastWaveTheDimmedCardOffersPlayAgain()
        {
            ResortGame finishedGame = Game;
            int waves = Game.Config.waves.Count;
            for (int wave = 0; wave < waves; wave++)
            {
                Game.StartWave();
                Game.Tick(Game.Config.wave.incomingDuration + Game.Config.wave.waveTimer + 600);
                yield return null;
                _island.Guests.FastForward(300f);
                yield return null;
                yield return null;
                Assert.AreEqual(GamePhase.WaveComplete, Game.Phase, $"Wave {wave + 1} is complete.");
                _ui.WaveComplete.ContinueButton.onClick.Invoke();
                yield return null;
            }

            Assert.IsFalse(Game.HasNextWave);
            Assert.IsTrue(_ui.WaveComplete.IsVisible, "The dimmed card stays up after the last wave.");
            Assert.AreEqual("ALL WAVES PLAYED", _ui.WaveComplete.TitleText);
            Assert.AreEqual($"{waves}/{waves}", _ui.WaveComplete.StatText);
            Assert.IsFalse(_ui.WaveComplete.StarsVisible, "No single wave's rating after the last wave.");
            Assert.IsFalse(_ui.WaveComplete.PerfectVisible);
            Assert.AreEqual(string.Empty, _ui.WaveComplete.SatisfactionText);
            StringAssert.Contains("PLAY AGAIN", _ui.WaveComplete.ContinueButton.GetComponentInChildren<TMPro.TMP_Text>().text);

            _ui.WaveComplete.ContinueButton.onClick.Invoke();
            yield return null;

            Assert.AreNotSame(finishedGame, Game, "Play Again starts a new game.");
            Assert.AreEqual(GamePhase.AwaitingWave, Game.Phase);
            Assert.AreEqual(1, Game.GetNextWaveForecast().WaveNumber);
            Assert.IsFalse(_ui.WaveComplete.IsVisible);
            Assert.IsTrue(_ui.Hud.StartWaveButton.gameObject.activeInHierarchy, "Welcome Guests is back.");
            Assert.IsFalse(Game.GetBuilding(BuildingId.Restaurant).HasWorker, "Workers start where the config puts them.");
        }

        /// <summary>Starts wave 1 with a Sudden rush (as wave 2 would have) and plays it to just after 45 s.</summary>
        private IEnumerator StartWaveWithSuddenRush()
        {
            Game.Config.wave.suddenRushEnabled = true;
            Game.Config.wave.suddenRushWave = 1;
            Game.StartWave();
            Game.Tick(Game.Config.wave.incomingDuration);
            for (int i = 0; i < 46; i++) Game.Tick(1);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SuddenRush_ThePanelAsksToMoveAWorkerToTheBuildingWithoutOne()
        {
            yield return StartWaveWithSuddenRush();

            SuddenRush rush = Game.CurrentWave.SuddenRush;
            Assert.IsNotNull(rush);
            Assert.AreEqual(BuildingId.Restaurant, rush.Building, "The Restaurant has no Worker.");
            Assert.AreEqual(12, Game.GetNeedStatus(NeedId.Food).Total, "Food 6 + 6 extra guests.");
            Assert.AreEqual(GamePhase.WaveActive, Game.Phase, "The wave doesn't pause.");

            SuddenRushPromptView prompt = _ui.SuddenRush;
            Assert.IsTrue(prompt.IsVisible);
            Assert.AreEqual("Sudden rush at Restaurant!", prompt.Title);
            Assert.AreEqual("+6 guests want Food, and the Restaurant has no Worker.", prompt.Detail);

            prompt.MoveWorkerButton.onClick.Invoke();
            yield return null;
            Assert.IsTrue(_ui.Workers.IsOpen, "Move Worker here opens Worker Allocation for the Restaurant.");

            Assert.AreEqual(WorkerMoveResult.Moved, Game.MoveWorker(BuildingId.Reception, BuildingId.Restaurant));
            yield return null;
            Assert.IsFalse(prompt.IsVisible, "A Worker is there now: the prompt goes away.");
        }

        [UnityTest]
        public IEnumerator SuddenRush_IgnoreClosesThePromptAndTheGuestsStillCome()
        {
            yield return StartWaveWithSuddenRush();
            SuddenRushPromptView prompt = _ui.SuddenRush;
            Assert.IsTrue(prompt.IsVisible);

            prompt.IgnoreButton.onClick.Invoke();
            yield return null;
            Assert.IsFalse(prompt.IsVisible);

            for (int i = 0; i < 10; i++) Game.Tick(1);
            yield return null;
            Assert.IsFalse(prompt.IsVisible, "It doesn't come back.");
            Assert.AreEqual(12, Game.GetNeedStatus(NeedId.Food).Total, "The extra guests come anyway (Build doc §4.9).");
            Assert.GreaterOrEqual(Game.GetNeedStatus(NeedId.Food).Arrived, 3 + 6, "3 steady Food guests by 45 s, then all 6 extra ones by 55 s.");
        }
    }
}
