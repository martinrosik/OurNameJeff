using ResortEmpire.Core;
using ResortEmpire.Game;
using UnityEngine;
using UnityEngine.UI;

namespace ResortEmpire.UI
{
    /// <summary>
    /// Root of the greybox UI. Builds the canvas at runtime (landscape, 852×393 reference like the Figma frames)
    /// with S1: the resort status, the next-wave forecast (OUR-33) and Welcome Guests (OUR-27).
    /// The Wave Needs Panel (OUR-37), Worker moving (OUR-45) and the wave-end info (OUR-40) plug in here later;
    /// the side area to the right is reserved for the needs panel during the wave.
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

        internal HudView Hud => _hud;
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
            controller.GameCreated += OnGameCreated;
        }

        private void OnDestroy()
        {
            if (controller != null) controller.GameCreated -= OnGameCreated;
        }

        private void LateUpdate()
        {
            if (_game != null) _hud.Refresh(_game);
        }

        /// <summary>OUR-27: Welcome Guests pressed.</summary>
        internal void StartWave() => _game.StartWave();

        private void OnGameCreated(ResortGame game) => _game = game;

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
        }
    }
}
