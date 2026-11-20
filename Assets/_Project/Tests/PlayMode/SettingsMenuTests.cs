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
    /// The settings menu behind the gear in the top-right corner (<see cref="SettingsView"/>): sound on/off, the wave
    /// waiting while the menu is open, and PLAY TUTORIAL AGAIN at any time, after which the player is back in their own
    /// game (<see cref="ParkedGame"/>). The device's sound and tutorial flags are put back after each test.
    /// </summary>
    public class SettingsMenuTests
    {
        private bool _wasMuted;
        private bool _tutorialWasDone;
        private GreyboxUI _ui;
        private GameController _controller;

        private SettingsView Settings => _ui.Settings;

        [SetUp]
        public void RememberTheDeviceSettings()
        {
            _wasMuted = SoundSettings.IsMuted;
            _tutorialWasDone = TutorialProgress.IsDone;
            SoundSettings.SetMuted(false);
            ParkedGame.Clear();
        }

        [TearDown]
        public void PutTheDeviceSettingsBack()
        {
            Time.timeScale = 1f;
            ParkedGame.Clear(); // the next test starts a new game
            SoundSettings.SetMuted(_wasMuted);
            if (_tutorialWasDone) TutorialProgress.MarkDone();
            else TutorialProgress.Reset();
        }

        private IEnumerator LoadGame()
        {
            yield return SceneManager.LoadSceneAsync(GameScenes.Game, LoadSceneMode.Single);
            yield return null;
            yield return null;
            _ui = Object.FindAnyObjectByType<GreyboxUI>();
            _controller = Object.FindAnyObjectByType<GameController>();
            _controller.SimulationSpeed = 0f; // tests drive time themselves
        }

        [UnityTest]
        public IEnumerator TheGearSitsInTheTopRightCornerAndOpensTheMenu()
        {
            yield return LoadGame();

            Assert.IsTrue(Settings.GearButton.gameObject.activeInHierarchy);
            var corners = new Vector3[4];
            ((RectTransform)Settings.GearButton.transform).GetWorldCorners(corners);
            Assert.Greater(corners[2].x, Screen.width * 0.9f, "Right edge.");
            Assert.Greater(corners[2].y, Screen.height * 0.9f, "Top edge.");

            Assert.IsFalse(Settings.IsOpen);
            Settings.GearButton.onClick.Invoke();
            Assert.IsTrue(Settings.IsOpen);
            Assert.IsTrue(Settings.SoundButton.gameObject.activeInHierarchy);
            Assert.IsTrue(Settings.TutorialButton.gameObject.activeInHierarchy);

            Settings.GearButton.onClick.Invoke();
            Assert.IsFalse(Settings.IsOpen, "The gear closes it again.");
        }

        [UnityTest]
        public IEnumerator TheWaveWaitsWhileTheMenuIsOpen()
        {
            yield return LoadGame();
            Assert.IsTrue(_controller.Game.StartWave());

            Settings.Open();
            Assert.AreEqual(0f, Time.timeScale);
            yield return null;
            Assert.AreEqual(0f, Time.deltaTime, "GameController ticks the wave by deltaTime, so it stands still.");

            Settings.Close();
            Assert.AreEqual(1f, Time.timeScale, "Closing lets the wave run again.");
        }

        [UnityTest]
        public IEnumerator SoundTurnsOffAndTheDeviceRemembersIt()
        {
            yield return LoadGame();
            Settings.Open();
            yield return null;
            Assert.AreEqual(GameText.SoundOnLabel, Settings.SoundText);
            Assert.AreEqual(1f, AudioListener.volume);

            Settings.SoundButton.onClick.Invoke();
            Assert.IsTrue(SoundSettings.IsMuted, "Saved on the device.");
            Assert.AreEqual(0f, AudioListener.volume, "Every sound goes quiet.");
            Assert.AreEqual(GameText.SoundOffLabel, Settings.SoundText);

            // Loading a scene with the menu open keeps the sound off and lets time run again.
            yield return LoadGame();
            Assert.AreEqual(1f, Time.timeScale);
            Assert.AreEqual(0f, AudioListener.volume);

            Settings.Open();
            Settings.SoundButton.onClick.Invoke();
            Assert.IsFalse(SoundSettings.IsMuted);
            Assert.AreEqual(1f, AudioListener.volume);
            Assert.AreEqual(GameText.SoundOnLabel, Settings.SoundText);
        }

        [UnityTest]
        public IEnumerator PlayTutorialAgainWorksAtAnyTime()
        {
            yield return LoadGame();
            Settings.Open();
            yield return null;
            Assert.IsTrue(Settings.TutorialButton.interactable, "Before the first wave.");
            Assert.AreEqual(GameText.TutorialReplayNote, Settings.TutorialNoteText);
            Settings.Close();

            Assert.IsTrue(_controller.Game.StartWave());
            Settings.Open();
            yield return null;
            Assert.IsTrue(Settings.TutorialButton.interactable, "During a wave too.");
        }

        [UnityTest]
        public IEnumerator PlayTutorialAgainOpensTheTutorial()
        {
            TutorialProgress.MarkDone();
            yield return LoadGame();
            ResortGame game = _controller.Game;

            yield return OpenTutorialFromTheSettings();

            Assert.IsNotNull(Object.FindAnyObjectByType<TutorialController>());
            var ui = Object.FindAnyObjectByType<GreyboxUI>();
            Assert.IsFalse(ui.Settings.GearButton.gameObject.activeInHierarchy, "No gear inside the tutorial.");
            Assert.IsTrue(ParkedGame.HasGame, "The player's game waits.");
            Assert.AreNotSame(game, Object.FindAnyObjectByType<GameController>().Game, "The tutorial plays its own practice game.");
            Assert.AreEqual(GameText.TutorialBackLabel, ui.ContinueLabelOverride, "Its last button leads back to the resort.");
        }

        [UnityTest]
        public IEnumerator AfterAReplayDuringAWaveThePlayerIsBackInTheSameWave()
        {
            yield return LoadGame();
            ResortGame game = _controller.Game;
            Assert.IsTrue(game.StartWave());
            for (int i = 0; i < 800 && (game.Phase != GamePhase.WaveActive || game.WaveTime < 30.0); i++) game.Tick(0.25);
            Assert.AreEqual(GamePhase.WaveActive, game.Phase);
            Assert.AreEqual(WorkerMoveResult.Moved, game.MoveWorker(BuildingId.Reception, BuildingId.Restaurant));
            yield return null; // the island sends out the guests who have arrived
            double waveTime = game.WaveTime;
            int roomsServed = game.GetNeedStatus(NeedId.Rooms).Served;

            yield return OpenTutorialFromTheSettings();
            yield return new WaitForSecondsRealtime(2.5f); // time passes in the tutorial
            Object.FindAnyObjectByType<TutorialController>().View.SkipButton.onClick.Invoke();
            yield return WaitForScene(GameScenes.Game);

            var controller = Object.FindAnyObjectByType<GameController>();
            Assert.AreSame(game, controller.Game, "The same game, not a new one.");
            Assert.IsTrue(controller.IsResumed);
            Assert.IsFalse(ParkedGame.HasGame);
            Assert.AreEqual(GamePhase.WaveActive, game.Phase);
            // Only the few frames since coming back count (a scene-load frame is up to 1/3 s); not the 2.5 s away.
            Assert.AreEqual(waveTime, game.WaveTime, 1.0, "The wave stood still while the player was away (WAVE_PAUSED).");
            Assert.GreaterOrEqual(game.GetNeedStatus(NeedId.Rooms).Served, roomsServed);
            Assert.IsTrue(game.GetBuilding(BuildingId.Restaurant).HasWorker, "The Worker move is kept.");
            Assert.IsFalse(game.GetBuilding(BuildingId.Reception).HasWorker);
            Assert.AreEqual(1f, Time.timeScale);

            GuestCrowdView guests = Object.FindAnyObjectByType<IslandView>().Guests;
            Assert.Greater(guests.SpawnedCount, 0, "The guests who had arrived are on the island again.");
            Assert.Greater(guests.InsideCount, 0, "They went straight back into the buildings.");
        }

        [UnityTest]
        public IEnumerator AfterAReplayBetweenWavesTheWavesPlayedAreKept()
        {
            yield return LoadGame();
            ResortGame game = _controller.Game;
            Assert.IsTrue(game.StartWave());
            for (int i = 0; i < 4000 && game.Phase != GamePhase.WaveResolving; i++) game.Tick(0.25);
            Assert.IsTrue(game.FinishResolving()); // the island does this once the guests are back on the boat
            Assert.IsTrue(game.ContinueAfterWave());
            Assert.AreEqual(1, game.CompletedWaves);
            yield return null;

            yield return OpenTutorialFromTheSettings();
            Object.FindAnyObjectByType<TutorialController>().View.SkipButton.onClick.Invoke();
            yield return WaitForScene(GameScenes.Game);

            var controller = Object.FindAnyObjectByType<GameController>();
            Assert.AreSame(game, controller.Game);
            Assert.AreEqual(1, game.CompletedWaves, "Wave 2 is next, as before.");
            Assert.AreEqual(GamePhase.AwaitingWave, game.Phase);
        }

        private IEnumerator OpenTutorialFromTheSettings()
        {
            Settings.Open();
            Settings.TutorialButton.onClick.Invoke();
            yield return WaitForScene(GameScenes.Tutorial);
            yield return null;
        }

        private static IEnumerator WaitForScene(string name)
        {
            for (int i = 0; i < 120 && SceneManager.GetActiveScene().name != name; i++) yield return null;
            Assert.AreEqual(name, SceneManager.GetActiveScene().name);
            yield return null;
            yield return null;
        }
    }
}
