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
    /// The Tutorial scene: its practice wave, its bubbles, the pauses, and the way into the Game scene (START MY RESORT,
    /// SKIP TUTORIAL). PLAY TUTORIAL AGAIN is tested with the settings (SettingsMenuTests). Time is driven with Tick() so tests run fast. The device's
    /// "tutorial done" flag is put back after each test.
    /// </summary>
    public class TutorialSceneTests
    {
        private bool _wasDone;
        private TutorialController _tutorial;
        private GameController _controller;
        private GreyboxUI _ui;
        private IslandView _island;

        private ResortGame Game => _controller.Game;

        [SetUp]
        public void ForgetTheTutorial()
        {
            _wasDone = TutorialProgress.IsDone;
            TutorialProgress.Reset();
            ParkedGame.Clear(); // a first-time tutorial: no game of the player's waits
        }

        [TearDown]
        public void PutTheFlagBack()
        {
            Time.timeScale = 1f;
            ParkedGame.Clear();
            if (_wasDone) TutorialProgress.MarkDone();
            else TutorialProgress.Reset();
        }

        private IEnumerator LoadTutorial()
        {
            Assert.IsTrue(Application.CanStreamedLevelBeLoaded(GameScenes.Tutorial), "Tutorial is not in the Build Profile scene list.");
            yield return SceneManager.LoadSceneAsync(GameScenes.Tutorial, LoadSceneMode.Single);
            yield return null; // Start() builds the UI and the tutorial overlay
            yield return null; // first LateUpdate shows the first bubble

            _tutorial = Object.FindAnyObjectByType<TutorialController>();
            _controller = Object.FindAnyObjectByType<GameController>();
            _ui = Object.FindAnyObjectByType<GreyboxUI>();
            _island = Object.FindAnyObjectByType<IslandView>();
            Assert.IsNotNull(_tutorial, "Tutorial scene has no TutorialController.");
            Assert.IsNotNull(_controller);
            _controller.SimulationSpeed = 0f; // tests drive time themselves
        }

        private IEnumerator WaitForScene(string name)
        {
            for (int i = 0; i < 120 && SceneManager.GetActiveScene().name != name; i++) yield return null;
            Assert.AreEqual(name, SceneManager.GetActiveScene().name);
            yield return null;
        }

        /// <summary>Plays the wave in steps of 0.25 s until <paramref name="done"/> holds, one frame per step.</summary>
        private IEnumerator PlayUntil(System.Func<bool> done, double maxSeconds = 600)
        {
            for (double played = 0; !done(); played += 0.25)
            {
                Assert.Less(played, maxSeconds, $"Waited too long on {_tutorial.Script.Step}.");
                Game.Tick(0.25);
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator TheTutorialStartsWithWelcomeOnThePracticeWave()
        {
            yield return LoadTutorial();

            Assert.AreEqual(1, Game.Config.waves.Count, "The tutorial plays only the practice wave.");
            Assert.AreEqual(TutorialStep.Welcome, _tutorial.Script.Step);
            Assert.IsTrue(_tutorial.View.IsBubbleVisible);
            Assert.AreEqual(GameText.TutorialWelcomeTitle, _tutorial.View.TitleText);
            Assert.IsTrue(_tutorial.View.NextButton.gameObject.activeInHierarchy);
            Assert.IsTrue(_tutorial.View.SkipButton.gameObject.activeInHierarchy);
            Assert.IsTrue(_tutorial.View.BlocksGame, "Taps on the island wait until NEXT.");
            Assert.IsFalse(_ui.Settings.GearButton.gameObject.activeInHierarchy, "No settings gear inside the tutorial: SKIP TUTORIAL takes that corner.");
        }

        [UnityTest]
        public IEnumerator ResortValueStaysClearOfSkipTutorial()
        {
            yield return LoadTutorial();

            var pill = new Vector3[4];
            var skip = new Vector3[4];
            _ui.Hud.ResortValuePill.GetWorldCorners(pill);
            ((RectTransform)_tutorial.View.SkipButton.transform).GetWorldCorners(skip);
            Assert.LessOrEqual(pill[2].x, skip[0].x, "Resort Value sits left of SKIP TUTORIAL, which takes the gear's corner here.");
        }

        [UnityTest]
        public IEnumerator BubblesPointAtWhatTheyExplain()
        {
            yield return LoadTutorial();

            _tutorial.Next(); // Buildings
            yield return null;
            Assert.IsTrue(_tutorial.View.IsFrameVisible);
            Assert.IsTrue(_tutorial.View.IsArrowVisible, "An arrow points at the building.");

            _tutorial.Next(); // Workers
            _tutorial.Next(); // Forecast
            _tutorial.Next(); // Move a Worker
            yield return null;
            Assert.AreEqual(TutorialStep.MoveWorker, _tutorial.Script.Step);
            Assert.IsFalse(_tutorial.View.NextButton.gameObject.activeInHierarchy, "The player does it instead of tapping NEXT.");
            Assert.IsFalse(_tutorial.View.BlocksGame, "The island takes taps again.");
            StringAssert.Contains("Restaurant", _tutorial.View.BodyText, "It names the building without a Worker.");

            _ui.OpenWorkerAllocation(BuildingId.Restaurant);
            yield return null;
            Assert.IsFalse(_tutorial.View.IsBubbleVisible, "The bubble waits behind the Worker sheet.");
        }

        [UnityTest]
        public IEnumerator MovingAWorkerOpensOnlyTheHighlightedBuilding()
        {
            yield return LoadTutorial();

            for (int i = 0; i < 4; i++) _tutorial.Next();
            yield return null;
            Assert.AreEqual(TutorialStep.MoveWorker, _tutorial.Script.Step);
            Assert.IsFalse(_ui.Hud.StartWaveButton.interactable, "Welcome Guests waits for the move.");

            _ui.OpenWorkerAllocation(BuildingId.Reception);
            Assert.IsFalse(_ui.Workers.IsOpen, "A building with its Worker doesn't open.");
            Assert.AreEqual(1, _tutorial.View.Nudges, "The bubble shakes instead.");
            _ui.StartWave();
            Assert.AreEqual(GamePhase.AwaitingWave, Game.Phase, "Welcome Guests does nothing yet.");

            _ui.OpenWorkerAllocation(BuildingId.Restaurant);
            Assert.IsTrue(_ui.Workers.IsOpen, "The empty building opens.");
            Assert.AreEqual(BuildingId.Restaurant, _ui.Workers.Building);
            _ui.Workers.Options[0].onClick.Invoke(); // take a Worker over
            yield return null;
            Assert.IsFalse(_ui.Workers.IsOpen);
            Assert.AreEqual(TutorialStep.WelcomeGuests, _tutorial.Script.Step);
            yield return null; // the HUD refreshes in LateUpdate, after the test resumes
            Assert.IsTrue(_ui.Hud.StartWaveButton.interactable);
        }

        [UnityTest]
        public IEnumerator PlayingItThroughOpensTheGameAtWaveOne()
        {
            yield return LoadTutorial();

            for (int i = 0; i < 4; i++) _tutorial.Next();
            Assert.AreEqual(WorkerMoveResult.Moved, Game.MoveWorker(BuildingId.Reception, BuildingId.Restaurant));
            yield return null;
            Assert.AreEqual(TutorialStep.WelcomeGuests, _tutorial.Script.Step);

            _ui.Hud.StartWaveButton.onClick.Invoke();
            yield return PlayUntil(() => _tutorial.Script.Step == TutorialStep.NeedsPanel);
            yield return null;
            Assert.AreEqual(0f, Time.timeScale, "The wave waits while the needs panel is explained.");

            _tutorial.Next();
            yield return null;
            Assert.AreEqual(1f, Time.timeScale);

            yield return PlayUntil(() => _tutorial.Script.Step == TutorialStep.Overload);
            yield return null;
            Assert.AreEqual(BuildingId.Reception, _tutorial.Script.OverloadedBuilding);
            Assert.AreEqual(0f, Time.timeScale, "The wave waits for the fix.");
            StringAssert.Contains("Reception", _tutorial.View.TitleText);
            _ui.OpenWorkerAllocation(BuildingId.BeachBar);
            Assert.IsFalse(_ui.Workers.IsOpen, "Only the building falling behind opens.");

            Assert.AreEqual(WorkerMoveResult.Moved, Game.MoveWorker(BuildingId.BeachBar, BuildingId.Reception));
            yield return null;
            Assert.AreEqual(TutorialStep.CatchingUp, _tutorial.Script.Step);
            _tutorial.Next();

            yield return PlayUntil(() => Game.Phase == GamePhase.WaveResolving);
            _island.Guests.FastForward(600f); // the guests walk back to the boat
            for (int i = 0; i < 10 && Game.Phase != GamePhase.WaveComplete; i++) yield return null;
            yield return null;
            Assert.AreEqual(TutorialStep.Result, _tutorial.Script.Step);
            Assert.IsTrue(_ui.WaveComplete.IsVisible);
            Assert.AreEqual(GameText.TutorialFinishLabel, _ui.WaveComplete.ContinueButton.GetComponentInChildren<TMPro.TMP_Text>().text);
            Assert.IsFalse(_tutorial.View.SkipButton.gameObject.activeInHierarchy, "Only START MY RESORT at the end.");

            _ui.WaveComplete.ContinueButton.onClick.Invoke();
            yield return WaitForScene(GameScenes.Game);

            Assert.IsTrue(TutorialProgress.IsDone, "The device remembers the tutorial.");
            Assert.AreEqual(1f, Time.timeScale);
            var controller = Object.FindAnyObjectByType<GameController>();
            Assert.AreEqual(GamePhase.AwaitingWave, controller.Game.Phase);
            Assert.AreEqual(0, controller.Game.CompletedWaves, "The real game starts at wave 1.");
            Assert.Greater(controller.Game.Config.waves.Count, 1, "The real waves, not the practice wave.");
        }

        [UnityTest]
        public IEnumerator SkipOpensTheGameAndRemembersIt()
        {
            yield return LoadTutorial();

            _tutorial.View.SkipButton.onClick.Invoke();
            yield return WaitForScene(GameScenes.Game);

            Assert.IsTrue(TutorialProgress.IsDone);
            Assert.AreEqual(0, Object.FindAnyObjectByType<GameController>().Game.CompletedWaves);
        }

        [UnityTest]
        public IEnumerator OutsideTheTutorialEveryBuildingOpens()
        {
            yield return SceneManager.LoadSceneAsync(GameScenes.Game, LoadSceneMode.Single);
            yield return null;
            yield return null;
            var ui = Object.FindAnyObjectByType<GreyboxUI>();
            Object.FindAnyObjectByType<GameController>().SimulationSpeed = 0f;

            ui.OpenWorkerAllocation(BuildingId.Reception);
            Assert.IsTrue(ui.Workers.IsOpen, "No tutorial limits in the Game scene.");
        }
    }
}
