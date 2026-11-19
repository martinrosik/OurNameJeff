using System.Collections.Generic;
using ResortEmpire.Core;
using ResortEmpire.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ResortEmpire.UI
{
    /// <summary>
    /// OUR-45 / UI doc S3 Worker Allocation: a bottom sheet that opens when the player taps a building, before or
    /// during the wave.
    /// <list type="bullet">
    /// <item>Building without a Worker: "Take from Reception" / "Take from Beach Bar".</item>
    /// <item>Building with its Worker: "Move to Restaurant" (the empty building).</item>
    /// </list>
    /// Every option says what the other building is doing and which building <b>will be left without a Worker</b>:
    /// the cost is shown, not just the gain (AC2b). Moving is free and instant (WR5), so a tap on an option moves the
    /// Worker at once and closes the sheet. Tapping the dimmed backdrop or Close closes it without a move.
    /// </summary>
    internal sealed class WorkerAllocationView
    {
        private const float SheetWidth = 440f;
        private const float OptionHeight = 44f;
        private const float OptionSpacing = 6f;
        private const float HeaderHeight = 58f;
        private const float FooterHeight = 10f;

        private readonly GreyboxUI _owner;
        private readonly GameObject _root;
        private readonly RectTransform _sheet;
        private readonly TextMeshProUGUI _title;
        private readonly TextMeshProUGUI _subtitle;
        private readonly List<Button> _options = new List<Button>();
        private readonly List<BuildingId> _optionTargets = new List<BuildingId>();

        public WorkerAllocationView(GreyboxUI owner, RectTransform root)
        {
            _owner = owner;

            Image backdrop = UIFactory.Backdrop("WorkerAllocation", root);
            var backdropButton = backdrop.gameObject.AddComponent<Button>();
            backdropButton.transition = Selectable.Transition.None;
            backdropButton.onClick.AddListener(Close);
            _root = backdrop.gameObject;

            Image sheet = UIFactory.Panel("Sheet", backdrop.transform, UIFactory.Paper);
            _sheet = sheet.rectTransform;
            _sheet.At(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 6f), new Vector2(SheetWidth, 120f));
            sheet.gameObject.AddComponent<Button>().transition = Selectable.Transition.None; // taps on the sheet don't close it

            _title = UIFactory.Text("Title", _sheet, string.Empty, 15, UIFactory.Ink, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            _title.rectTransform.At(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12f, -8f), new Vector2(SheetWidth - 100f, 22f));

            _subtitle = UIFactory.Text("Subtitle", _sheet, string.Empty, 11, UIFactory.Ink.WithAlpha(0.8f), TextAlignmentOptions.TopLeft);
            _subtitle.textWrappingMode = TextWrappingModes.Normal;
            _subtitle.rectTransform.At(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12f, -30f), new Vector2(SheetWidth - 24f, 26f));

            Button close = UIFactory.Button("CloseButton", _sheet, GameText.CloseLabel, Color.white, UIFactory.Ink, 11, Close);
            ((RectTransform)close.transform).At(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-8f, -8f), new Vector2(70f, 24f));

            _root.SetActive(false);
        }

        public bool IsOpen => _root.activeSelf;

        /// <summary>The building whose sheet is open.</summary>
        public BuildingId Building { get; private set; }

        public string SubtitleText => _subtitle.text;

        /// <summary>The option buttons shown, in order (tests click them).</summary>
        public IReadOnlyList<Button> Options
        {
            get
            {
                var shown = new List<Button>();
                foreach (Button option in _options)
                {
                    if (option.gameObject.activeSelf) shown.Add(option);
                }

                return shown;
            }
        }

        public void Open(ResortGame game, BuildingId building)
        {
            if (game == null || !game.CanMoveWorkers || game.GetBuilding(building) == null) return;

            Building = building;
            _root.SetActive(true);
            _root.transform.SetAsLastSibling();
            Show(game);
        }

        public void Close() => _root.SetActive(false);

        /// <summary>
        /// Called every frame while open: keeps the numbers current, and closes the sheet once moving is no longer
        /// allowed (the wave ended).
        /// </summary>
        public void Refresh(ResortGame game)
        {
            if (!IsOpen) return;
            if (game == null || !game.CanMoveWorkers)
            {
                Close();
                return;
            }

            Show(game);
        }

        private void Show(ResortGame game)
        {
            BuildingRuntime here = game.GetBuilding(Building);
            _title.text = GameText.WorkerSheetTitle(here.Definition.displayName);

            _optionTargets.Clear();
            var labels = new List<string>();
            foreach (BuildingRuntime other in game.Buildings)
            {
                if (other.Id == Building) continue;

                if (!here.HasWorker && other.HasWorker)
                {
                    // Take the Worker from the other building: that one will be left empty.
                    _optionTargets.Add(other.Id);
                    labels.Add(GameText.TakeWorkerOption(game, other));
                }
                else if (here.HasWorker && !other.HasWorker)
                {
                    // Send this building's Worker over there: this one will be left empty.
                    _optionTargets.Add(other.Id);
                    labels.Add(GameText.MoveWorkerOption(game, here, other));
                }
            }

            _subtitle.text = GameText.WorkerSheetSubtitle(game, here);

            while (_options.Count < labels.Count)
            {
                int index = _options.Count;
                Button option = UIFactory.Button($"Option{index}", _sheet, string.Empty, GreyboxPalette.PanelBlue, UIFactory.Ink, 13, () => Choose(index));
                _options.Add(option);
            }

            for (int i = 0; i < _options.Count; i++)
            {
                bool visible = i < labels.Count;
                _options[i].gameObject.SetActive(visible);
                if (!visible) continue;

                _options[i].SetLabel(labels[i]);
                ((RectTransform)_options[i].transform).At(
                    new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0f, -HeaderHeight - i * (OptionHeight + OptionSpacing)),
                    new Vector2(SheetWidth - 24f, OptionHeight));
            }

            float height = HeaderHeight + labels.Count * (OptionHeight + OptionSpacing) + FooterHeight;
            _sheet.sizeDelta = new Vector2(SheetWidth, height);
        }

        private void Choose(int index)
        {
            ResortGame game = _owner.Game;
            if (game == null || index >= _optionTargets.Count) return;

            BuildingRuntime here = game.GetBuilding(Building);
            BuildingId other = _optionTargets[index];
            if (here.HasWorker) game.MoveWorker(Building, other);
            else game.MoveWorker(other, Building);

            Close();
        }
    }
}
