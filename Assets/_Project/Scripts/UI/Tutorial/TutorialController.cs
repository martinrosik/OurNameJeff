using System.Collections.Generic;
using ResortEmpire.Core;
using ResortEmpire.Game;
using UnityEngine;

namespace ResortEmpire.UI
{
    /// <summary>
    /// Runs the tutorial in the Tutorial scene (the same island as Game.unity, built by the scene builder):
    /// <list type="bullet">
    /// <item>swaps the game for the practice wave (<see cref="TutorialConfig"/>) before the UI starts;</item>
    /// <item>follows the steps of <see cref="TutorialScript"/> and shows each as a bubble with an arrow (<see cref="TutorialView"/>);</item>
    /// <item>pauses the wave (timeScale 0) while a step explains something during it;</item>
    /// <item>while a step asks for one thing, lets only that work: the building it points at opens its Worker sheet
    /// (others shake the bubble), and Welcome Guests waits until the Worker move is done;</item>
    /// <item>at the end, START MY RESORT (on Wave Complete) or SKIP TUTORIAL remembers the tutorial as done
    /// (<see cref="TutorialProgress"/>) and opens the Game scene: at the real wave 1, or, when the player replayed the
    /// tutorial from the settings, back in their own game exactly as they left it (BACK TO MY RESORT, <see cref="ParkedGame"/>).</item>
    /// </list>
    /// Runs after GameController (Awake) and GreyboxUI (Start, LateUpdate), so the views it points at exist and are
    /// up to date.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class TutorialController : MonoBehaviour
    {
        [SerializeField] private GameController controller;
        [SerializeField] private GreyboxUI ui;
        [SerializeField] private TutorialConfig tutorial;
        [SerializeField] private Sprite arrowSprite;
        [SerializeField] private Sprite frameSprite;

        [Tooltip("The resort host in the bubbles (a guest character for now).")]
        [SerializeField] private Sprite hostSprite;

        private GameConfig _practiceConfig;
        private TutorialScript _script;
        private TutorialView _view;
        private bool _leaving;
        private bool _isReplay;

        internal TutorialScript Script => _script;
        internal TutorialView View => _view;

        private void Awake()
        {
            if (controller == null || controller.Game == null || ui == null || tutorial == null)
            {
                Debug.LogError("TutorialController needs a GameController with a running game, the GreyboxUI and the tutorial config.", this);
                enabled = false;
                return;
            }

            // Before GreyboxUI.Start reads the game, so every view starts on the practice wave.
            _practiceConfig = tutorial.CreatePracticeConfig(controller.Game.Config);
            if (!controller.NewGame(_practiceConfig))
            {
                enabled = false;
            }
        }

        private void Start()
        {
            ui.SettingsAvailable = false; // SKIP TUTORIAL takes that corner here

            // Replayed from the settings: the player's own game waits in ParkedGame, and the way out leads back to it.
            _isReplay = ParkedGame.HasGame;
            ui.OverrideContinue(_isReplay ? GameText.TutorialBackLabel : GameText.TutorialFinishLabel, Finish);
            _script = new TutorialScript(controller.Game);
            _view = new TutorialView(transform, arrowSprite, frameSprite, hostSprite, Next, Skip);

            // While a step asks for one thing, only that works: the building the bubble points at, or Welcome Guests.
            ui.CanOpenWorkerSheet = _script.AllowsWorkerSheet;
            ui.CanStartWave = () => _script.AllowsWaveStart;
            ui.WorkerSheetRefused += OnWorkerSheetRefused;
        }

        private void OnDestroy()
        {
            Time.timeScale = 1f;
            if (ui != null)
            {
                ui.CanOpenWorkerSheet = null;
                ui.CanStartWave = null;
                ui.WorkerSheetRefused -= OnWorkerSheetRefused;
            }

            _script?.Dispose();
            if (_practiceConfig != null) Destroy(_practiceConfig);
        }

        private void Update()
        {
            if (_script == null) return;
            _script.Update();
            Time.timeScale = _script.PausesGame && !_leaving ? 0f : 1f;
        }

        private void LateUpdate()
        {
            if (_script == null || _view == null) return;

            // The Worker sheet is the action itself; the bubble waits behind it instead of covering its buttons.
            if (_script.IsFinished || ui.Workers.IsOpen)
            {
                _view.Hide();
            }
            else
            {
                _view.Show(BuildBubble());
            }

            _view.SetSkipVisible(!_script.IsFinished && _script.Step != TutorialStep.Result);
            _view.Animate(Time.unscaledTime);
        }

        /// <summary>A tap on a building the step doesn't ask for: the bubble shakes to show where to look.</summary>
        private void OnWorkerSheetRefused(BuildingId building) => _view?.Nudge();

        /// <summary>NEXT on the bubble.</summary>
        internal void Next() => _script.Next();

        /// <summary>SKIP TUTORIAL: straight to the real game.</summary>
        internal void Skip() => Leave();

        /// <summary>START MY RESORT on Wave Complete.</summary>
        internal void Finish()
        {
            _script.Finish();
            Leave();
        }

        private void Leave()
        {
            if (_leaving) return;
            _leaving = true;
            TutorialProgress.MarkDone();
            GameScenes.LoadGame();
        }

        private TutorialBubble BuildBubble()
        {
            ResortGame game = controller.Game;
            var bubble = new TutorialBubble
            {
                WaitsForNext = _script.Kind == TutorialStepKind.Info,
                IsHint = _script.Kind == TutorialStepKind.Hint,
                Fallback = _script.Kind == TutorialStepKind.Hint ? HintSpot() : IslandCentre(),
                Avoid = new List<Rect>(),
            };

            switch (_script.Step)
            {
                case TutorialStep.Welcome:
                    bubble.Title = GameText.TutorialWelcomeTitle;
                    bubble.Body = GameText.TutorialWelcome;
                    break;
                case TutorialStep.Buildings:
                    bubble.Title = GameText.TutorialBuildingsTitle;
                    bubble.Body = GameText.TutorialBuildings(game);
                    bubble.Target = BuildingRect(FirstBuilding(game, staffed: null));
                    AvoidBuildings(bubble.Avoid, FirstBuilding(game, staffed: null));
                    break;
                case TutorialStep.Workers:
                    bubble.Title = GameText.TutorialWorkersTitle;
                    bubble.Body = GameText.TutorialWorkers(game);
                    bubble.Target = WorkerBadgeRect(FirstBuilding(game, staffed: true));
                    AvoidBuildings(bubble.Avoid, except: null);
                    break;
                case TutorialStep.Forecast:
                    bubble.Title = GameText.TutorialForecastTitle;
                    bubble.Body = GameText.TutorialForecast;
                    bubble.Target = UIRect(ui.Hud.ForecastPanel);
                    break;
                case TutorialStep.MoveWorker:
                {
                    BuildingId? empty = _script.FocusBuilding; // the only building that opens now
                    bubble.Title = GameText.TutorialMoveWorkerTitle;
                    bubble.Body = GameText.TutorialMoveWorker(game, empty ?? BuildingId.Restaurant);
                    bubble.Target = BuildingRect(empty);
                    AvoidBuildings(bubble.Avoid, empty);
                    AddIfShown(bubble.Avoid, ui.Hud.StartWaveButton.transform as RectTransform);
                    break;
                }

                case TutorialStep.WelcomeGuests:
                    bubble.Title = GameText.TutorialWelcomeGuestsTitle;
                    bubble.Body = GameText.TutorialWelcomeGuests;
                    bubble.Target = UIRect(ui.Hud.StartWaveButton.transform as RectTransform);
                    break;
                case TutorialStep.BoatArriving:
                    bubble.Body = GameText.TutorialBoatArriving;
                    break;
                case TutorialStep.NeedsPanel:
                    bubble.Title = GameText.TutorialNeedsPanelTitle;
                    bubble.Body = GameText.TutorialNeedsPanel;
                    bubble.Target = UIRect(ui.Needs.Panel);
                    break;
                case TutorialStep.WatchQueues:
                    bubble.Body = GameText.TutorialWatchQueues;
                    break;
                case TutorialStep.Overload:
                {
                    BuildingId building = _script.FocusBuilding ?? BuildingId.Restaurant; // the only building that opens now
                    bubble.Title = GameText.TutorialOverloadTitle(game, building);
                    bubble.Body = GameText.TutorialOverload(game, building);
                    bubble.Target = BuildingRect(building);
                    AvoidBuildings(bubble.Avoid, building);
                    AddIfShown(bubble.Avoid, ui.Needs.Panel); // its rows open the Worker sheet too
                    break;
                }

                case TutorialStep.CatchingUp:
                {
                    BuildingId building = _script.FocusBuilding ?? BuildingId.Restaurant;
                    bubble.Title = GameText.TutorialCatchingUpTitle;
                    bubble.Body = GameText.TutorialCatchingUp(game, building);
                    bubble.Target = BuildingRect(building);
                    AvoidBuildings(bubble.Avoid, building);
                    break;
                }

                case TutorialStep.FinishWave:
                    bubble.Body = GameText.TutorialFinishWave;
                    break;
                case TutorialStep.Result:
                    bubble.Title = GameText.TutorialResultTitle;
                    bubble.Body = _isReplay ? GameText.TutorialResultReplay : GameText.TutorialResult;
                    bubble.Target = ui.WaveComplete.IsVisible ? UIRect(ui.WaveComplete.Stars) : null;
                    AddIfShown(bubble.Avoid, ui.WaveComplete.Card);
                    break;
            }

            return bubble;
        }

        /// <summary>The first building in config order: any (null), with a Worker (true) or without one (false).</summary>
        private static BuildingId? FirstBuilding(ResortGame game, bool? staffed)
        {
            foreach (BuildingRuntime building in game.Buildings)
            {
                if (staffed == null || building.HasWorker == staffed.Value) return building.Definition.building;
            }

            return null;
        }

        private Rect? BuildingRect(BuildingId? building)
        {
            BuildingView view = building != null && ui.Island != null ? ui.Island.GetView(building.Value) : null;
            return view != null ? WorldRect(view.GetBounds()) : null;
        }

        private Rect? WorkerBadgeRect(BuildingId? building)
        {
            BuildingView view = building != null && ui.Island != null ? ui.Island.GetView(building.Value) : null;
            Bounds? badge = view != null ? view.WorkerBadgeBounds : null;
            return badge != null ? WorldRect(badge.Value) : null;
        }

        /// <summary>World bounds (island) in the tutorial canvas's safe-area space.</summary>
        private Rect? WorldRect(Bounds bounds)
        {
            Camera camera = Camera.main;
            if (camera == null) return null;
            Vector2 min = ToSafeArea(camera.WorldToScreenPoint(bounds.min));
            Vector2 max = ToSafeArea(camera.WorldToScreenPoint(bounds.max));
            return Rect.MinMaxRect(Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y), Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y));
        }

        /// <summary>A rect of the greybox UI (overlay canvas, so world corners are screen pixels) in safe-area space.</summary>
        private Rect? UIRect(RectTransform rect)
        {
            if (rect == null || !rect.gameObject.activeInHierarchy) return null;
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector2 min = ToSafeArea(corners[0]);
            Vector2 max = ToSafeArea(corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        /// <summary>Every building on the island but <paramref name="except"/>: a bubble about one building stays off the others.</summary>
        private void AvoidBuildings(List<Rect> avoid, BuildingId? except)
        {
            foreach (BuildingRuntime building in controller.Game.Buildings)
            {
                BuildingId id = building.Definition.building;
                if (id == except) continue;
                Rect? rect = BuildingRect(id);
                if (rect != null) avoid.Add(rect.Value);
            }
        }

        private void AddIfShown(List<Rect> list, RectTransform rect)
        {
            Rect? shown = UIRect(rect);
            if (shown != null) list.Add(shown.Value);
        }

        private Vector2 ToSafeArea(Vector3 screenPoint)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_view.SafeArea, screenPoint, null, out Vector2 local);
            return local;
        }

        /// <summary>The middle of the island area (left of the side panel, below the top bar).</summary>
        private Vector2 IslandCentre()
        {
            Rect? island = UIRect(ui.IslandArea);
            return island?.center ?? Vector2.zero;
        }

        /// <summary>Hints sit low on the left of the island, over the palms, clear of the buildings and the boat.</summary>
        private Vector2 HintSpot()
        {
            Rect? island = UIRect(ui.IslandArea);
            if (island == null) return Vector2.zero;
            return new Vector2(island.Value.xMin + 130f, island.Value.yMin + 34f);
        }
    }
}
