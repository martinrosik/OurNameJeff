using ResortEmpire.Core;
using ResortEmpire.Game;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ResortEmpire.UI
{
    /// <summary>
    /// Root of the greybox UI. Builds the canvas at runtime (landscape, 852×393 reference like the Figma frames)
    /// with S1: the resort status, the next-wave forecast (OUR-33) and Welcome Guests (OUR-27).
    /// OUR-45: tapping a building opens the Worker allocation sheet (<see cref="WorkerAllocationView"/>).
    /// OUR-37: the Wave Needs Panel (<see cref="NeedsPanelView"/>) uses the side area to the right during the wave.
    /// OUR-38 / OUR-39: building state changes show a short message over the island (<see cref="StateToastView"/>).
    /// OUR-40: <see cref="WaveCompleteView"/> tells the player the wave is over.
    /// Settings: a gear in the top-right corner opens <see cref="SettingsView"/> (sound, PLAY TUTORIAL AGAIN).
    /// Tutorial: in the Tutorial scene, TutorialController hides the gear, draws its bubbles on a canvas of its own over
    /// this one, limits which buildings open and when Welcome Guests works, and swaps Wave Complete's Continue for
    /// START MY RESORT.
    /// UI reads game state and sends player intents; it never changes rules.
    /// </summary>
    public sealed class GreyboxUI : MonoBehaviour
    {
        internal const float SidePanelWidth = 300f;
        internal const float Margin = 6f;
        internal const float TopBarHeight = 42f;

        [SerializeField] private GameController controller;
        [SerializeField] private Sprite roundedSprite;
        [SerializeField] private Sprite starSprite;

        // Need icons on the forecast and the Wave Needs Panel (Figma S2), white so the panels can tint them.
        [SerializeField] private Sprite roomsIcon;
        [SerializeField] private Sprite drinksIcon;
        [SerializeField] private Sprite foodIcon;

        [Tooltip("The gear on the settings button (white, tinted at runtime).")]
        [SerializeField] private Sprite settingsIcon;

        private ResortGame _game;
        private HudView _hud;
        private SettingsView _settings;
        private NeedsPanelView _needs;
        private WorkerAllocationView _workers;
        private StateToastView _stateToast;
        private SuddenRushPromptView _suddenRush;
        private WaveCompleteView _waveComplete;
        private IslandView _island;

        internal HudView Hud => _hud;
        internal SettingsView Settings => _settings;
        internal NeedsPanelView Needs => _needs;
        internal WorkerAllocationView Workers => _workers;
        internal StateToastView StateToast => _stateToast;
        internal SuddenRushPromptView SuddenRush => _suddenRush;
        internal WaveCompleteView WaveComplete => _waveComplete;
        internal ResortGame Game => _game;

        /// <summary>The area right of the island, below the top bar (forecast now; needs panel OUR-37 during the wave).</summary>
        internal RectTransform SideArea { get; private set; }

        /// <summary>The area over the island, for prompts and toasts.</summary>
        internal RectTransform IslandArea { get; private set; }

        internal IslandView Island => _island;

        /// <summary>Whether the settings gear shows. The Tutorial scene turns it off: SKIP TUTORIAL sits in that corner there.</summary>
        internal bool SettingsAvailable { get; set; } = true;

        /// <summary>Tutorial: the label and action that replace Continue on Wave Complete (null: the normal Continue).</summary>
        internal string ContinueLabelOverride { get; private set; }

        /// <summary>
        /// Tutorial: whether a tap (on the island or a needs-panel row) may open this building's Worker sheet. Null lets
        /// every building open. The Tutorial scene lets only the building its bubble points at open.
        /// </summary>
        internal System.Func<BuildingId, bool> CanOpenWorkerSheet { get; set; }

        /// <summary>Tutorial: raised when a tap asked for a Worker sheet that <see cref="CanOpenWorkerSheet"/> refused.</summary>
        internal event System.Action<BuildingId> WorkerSheetRefused;

        /// <summary>Tutorial: whether Welcome Guests may start the wave now. Null: whenever the game allows it.</summary>
        internal System.Func<bool> CanStartWave { get; set; }

        internal bool IsWaveStartAllowed => CanStartWave == null || CanStartWave();

        private System.Action _continueOverride;

        private void Start()
        {
            if (controller == null || controller.Game == null)
            {
                Debug.LogError("GreyboxUI needs a GameController with a running game.", this);
                enabled = false;
                return;
            }

            UIFactory.Init(roundedSprite, starSprite, roomsIcon, drinksIcon, foodIcon);
            Build();
            _game = controller.Game;
            _island = FindAnyObjectByType<IslandView>();
            controller.GameCreated += OnGameCreated;
        }

        private void OnDestroy()
        {
            if (controller != null) controller.GameCreated -= OnGameCreated;
            _settings?.Close(); // lets the wave run again if the scene goes away with the menu open
        }

        private void Update()
        {
            // OUR-45: a tap (released press) on a building, not on the UI, opens its Worker allocation sheet.
            if (_game == null || _island == null || _workers.IsOpen || _settings.IsOpen) return;
            Pointer pointer = Pointer.current;
            if (pointer == null || !pointer.press.wasReleasedThisFrame) return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            Camera camera = Camera.main;
            if (camera == null) return;
            Vector2 screen = pointer.position.ReadValue();
            Vector3 world = camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -camera.transform.position.z));
            BuildingView building = _island.GetBuildingAt(world);
            if (building != null) OpenWorkerAllocation(building.Building);
        }

        private void LateUpdate()
        {
            if (_game == null) return;
            _hud.Refresh(_game);
            _settings.Refresh(_game);
            _needs.Refresh(_game);
            _suddenRush.Refresh(_game);
            _stateToast.Refresh(_game);
            _workers.Refresh(_game);
            _waveComplete.Refresh(_game);
        }

        /// <summary>OUR-45: opens the Worker allocation sheet for a building (a tap on the island or a needs-panel row).</summary>
        internal void OpenWorkerAllocation(BuildingId building)
        {
            if (CanOpenWorkerSheet != null && !CanOpenWorkerSheet(building))
            {
                WorkerSheetRefused?.Invoke(building);
                return;
            }

            _workers.Open(_game, building);
        }

        /// <summary>OUR-27: Welcome Guests pressed.</summary>
        internal void StartWave()
        {
            if (IsWaveStartAllowed) _game.StartWave();
        }

        /// <summary>OUR-40: Continue pressed on Wave Complete.</summary>
        internal void ContinueAfterWave()
        {
            if (_continueOverride != null) _continueOverride();
            else _game.ContinueAfterWave();
        }

        /// <summary>Tutorial: Wave Complete's button shows <paramref name="label"/> and runs <paramref name="onContinue"/> instead.</summary>
        internal void OverrideContinue(string label, System.Action onContinue)
        {
            ContinueLabelOverride = label;
            _continueOverride = onContinue;
        }

        /// <summary>
        /// PLAY TUTORIAL AGAIN pressed in the settings, at any time. This game waits frozen (<see cref="ParkedGame"/>) while
        /// the Tutorial scene plays, and the Game scene picks it up again afterwards.
        /// </summary>
        internal void OpenTutorial()
        {
            ParkedGame.Park(_game);
            GameScenes.LoadTutorial();
        }

        /// <summary>Play Again pressed after the last wave: a new game from wave 1 (OnGameCreated takes it over).</summary>
        internal void PlayAgain() => controller.NewGame();

        private void OnGameCreated(ResortGame game)
        {
            _game = game;
            _workers.Close();
        }

        private void Build()
        {
            var canvasObject = new GameObject("GreyboxCanvas", typeof(RectTransform));
            canvasObject.layer = 5;
            canvasObject.transform.SetParent(transform, false);

            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(852f, 393f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;

            canvasObject.AddComponent<GraphicRaycaster>();

            RectTransform safe = UIFactory.Rect("SafeArea", canvasObject.transform).Fill();
            safe.gameObject.AddComponent<SafeAreaFitter>();

            SideArea = UIFactory.Rect("SideArea", safe);
            SideArea.anchorMin = new Vector2(1f, 0f);
            SideArea.anchorMax = new Vector2(1f, 1f);
            SideArea.pivot = new Vector2(1f, 0.5f);
            SideArea.offsetMin = new Vector2(-SidePanelWidth - Margin, Margin);
            SideArea.offsetMax = new Vector2(-Margin, -TopBarHeight);

            IslandArea = UIFactory.Rect("IslandArea", safe);
            IslandArea.Fill(Margin, SidePanelWidth + Margin * 2, TopBarHeight, Margin);

            _hud = new HudView(this, safe, SideArea, IslandArea);
            _needs = new NeedsPanelView(this, SideArea);
            _suddenRush = new SuddenRushPromptView(this, SideArea); // over the needs panel
            _stateToast = new StateToastView(IslandArea); // OUR-38 / OUR-39
            _settings = new SettingsView(this, safe, settingsIcon); // its menu moves to the front when it opens
            _workers = new WorkerAllocationView(this, safe); // last, so it draws over the HUD
            _waveComplete = new WaveCompleteView(this, canvasObject.transform); // on the canvas, so the dim also covers the notch area
        }
    }
}
