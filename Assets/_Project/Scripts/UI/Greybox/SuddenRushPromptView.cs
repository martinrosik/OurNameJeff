using ResortEmpire.Core;
using ResortEmpire.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ResortEmpire.UI
{
    /// <summary>
    /// Sudden rush (Build doc §4.6): the challenge on the Wave Needs Panel (S2) when the building without a Worker gets
    /// a surge of extra guests. "Sudden rush at Beach Bar!" with two choices:
    /// <list type="bullet">
    /// <item><b>Move Worker here</b> (the main one): opens Worker Allocation for that building, so the player picks
    /// which building to leave empty. Once a Worker is there, the prompt goes away.</item>
    /// <item><b>Ignore</b>: the prompt goes away; the guests come anyway and are served slowly (§4.9).</item>
    /// </list>
    /// The Build doc's third choice, "Use Energy", is left out: Energy isn't in the game. The wave never pauses.
    /// </summary>
    internal sealed class SuddenRushPromptView
    {
        private const float Width = 280f;
        private const float Height = 78f;

        private readonly GreyboxUI _owner;
        private readonly GameObject _card;
        private readonly TextMeshProUGUI _title;
        private readonly TextMeshProUGUI _detail;
        private ResortGame _game;
        private SuddenRush _rush;
        private bool _ignored;

        public SuddenRushPromptView(GreyboxUI owner, RectTransform sideArea)
        {
            _owner = owner;

            // At the bottom of the side panel, under the need rows and above the panel's hint line.
            Image card = UIFactory.Panel("SuddenRushPrompt", sideArea, GreyboxPalette.Primary);
            card.rectTransform.At(new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(10f, 26f), new Vector2(Width, Height));
            _card = card.gameObject;

            _title = UIFactory.Text("Title", card.transform, string.Empty, 14, Color.white, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            _title.rectTransform.At(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -4f), new Vector2(Width - 20f, 20f));

            _detail = UIFactory.Text("Detail", card.transform, string.Empty, 10, Color.white);
            _detail.textWrappingMode = TextWrappingModes.Normal;
            _detail.rectTransform.At(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -23f), new Vector2(Width - 20f, 24f));

            Button move = UIFactory.Button("MoveWorkerHere", card.transform, GameText.SuddenRushMoveWorker, Color.white, UIFactory.Ink, 12, PressMoveWorker);
            ((RectTransform)move.transform).At(new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(10f, 6f), new Vector2(170f, 24f));
            MoveWorkerButton = move;

            Button ignore = UIFactory.Button("Ignore", card.transform, GameText.SuddenRushIgnore, GreyboxPalette.Primary.Lighten(0.25f), Color.white, 12, PressIgnore);
            ((RectTransform)ignore.transform).At(new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-10f, 6f), new Vector2(80f, 24f));
            IgnoreButton = ignore;

            _card.SetActive(false);
        }

        public bool IsVisible => _card.activeSelf;
        public string Title => _title.text;
        public string Detail => _detail.text;
        public Button MoveWorkerButton { get; }
        public Button IgnoreButton { get; }

        public void Refresh(ResortGame game)
        {
            SuddenRush rush = game.CurrentWave?.SuddenRush;
            if (game != _game || rush != _rush)
            {
                _game = game;
                _rush = rush;
                _ignored = false;
            }

            bool show = rush != null
                && game.Phase == GamePhase.WaveActive
                && !_ignored
                && game.GetBuilding(rush.Building) is BuildingRuntime building
                && !building.HasWorker;
            _card.SetActive(show);
            if (!show) return;

            _title.text = GameText.SuddenRushTitle(game, rush.Building);
            _detail.text = GameText.SuddenRushDetail(game, rush);
        }

        private void PressMoveWorker()
        {
            if (_rush != null) _owner.OpenWorkerAllocation(_rush.Building);
        }

        private void PressIgnore()
        {
            _ignored = true;
            _card.SetActive(false);
        }
    }
}
