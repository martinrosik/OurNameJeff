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
    /// The wave-end info (OUR-40) plugs in here later.
    /// UI reads game state and sends player intents; it never changes rules.
    /// </summary>
    public sealed class GreyboxUI : MonoBehaviour
    {
        internal const float SidePanelWidth = 300f;
        internal const float Margin = 6f;
        internal const float TopBarHeight = 42f;

        [SerializeField] private GameController controller;
        [SerializeField] private Sprite roundedSprite;

        private ResortGame _game;
        private HudView _hud;
        private NeedsPanelView _needs;
        private WorkerAllocationView _workers;
        private IslandView _island;

        internal HudView Hud => _hud;
        internal NeedsPanelView Needs => _needs;
        internal WorkerAllocationView Workers => _workers;
        internal ResortGame Game => _game;

        /// <summary>The area right of the island, below the top bar (forecast now; needs panel OUR-37 during the wave).</summary>
        internal RectTransform SideArea { get; private set; }

        /// <summary>The area over the island, for prompts and toasts.</summary>
        internal RectTransform IslandArea { get; private set; }

        private void Start()
        {
            if (controller == null || controller.Game == null)
            {
                Debug.LogError("GreyboxUI needs a GameController with a running game.", this);
                enabled = false;
                return;
            }

            UIFactory.Init(roundedSprite);
            Build();
            _game = controller.Game;
            _island = FindAnyObjectByType<IslandView>();
            controller.GameCreated += OnGameCreated;
        }

        private void OnDestroy()
        {
            if (controller != null) controller.GameCreated -= OnGameCreated;
        }

        private void Update()
        {
            // OUR-45: a tap (released press) on a building, not on the UI, opens its Worker allocation sheet.
            if (_game == null || _island == null || _workers.IsOpen) return;
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
            _needs.Refresh(_game);
            _workers.Refresh(_game);
        }

        /// <summary>OUR-45: opens the Worker allocation sheet for a building (a tap on the island).</summary>
        internal void OpenWorkerAllocation(BuildingId building) => _workers.Open(_game, building);

        /// <summary>OUR-27: Welcome Guests pressed.</summary>
        internal void StartWave() => _game.StartWave();

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
            _workers = new WorkerAllocationView(this, safe); // last, so it draws over the HUD
        }
    }
}
