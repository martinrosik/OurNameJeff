using ResortEmpire.Core;
using ResortEmpire.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ResortEmpire.UI
{
    /// <summary>
    /// OUR-37: one need on the Wave Needs Panel: "Food  2/6", a progress bar, the state, the queue and whether the
    /// building has a Worker. Tapping the row opens Worker Allocation (OUR-45) for that building.
    /// </summary>
    internal sealed class NeedRowView
    {
        private static readonly Color RowColor = Color.white.WithAlpha(0.85f);
        private static readonly Color StaffedColor = UIFactory.Ink.WithAlpha(0.75f);
        private static readonly Color BottleneckRowColor = GreyboxPalette.Warning.Lighten(0.75f);
        private static readonly Color RecoveringRowColor = GreyboxPalette.WorkerOn.Lighten(0.75f);

        private readonly GreyboxUI _owner;
        private readonly Image _background;
        private readonly TextMeshProUGUI _progress;
        private readonly TextMeshProUGUI _staffing;
        private readonly RectTransform _barFill;
        private readonly TextMeshProUGUI _state;
        private readonly TextMeshProUGUI _queue;
        private BuildingId _building;

        public NeedRowView(GreyboxUI owner, Transform parent, int index, Vector2 position, Vector2 size)
        {
            _owner = owner;

            _background = UIFactory.Panel($"NeedRow{index}", parent, RowColor);
            _background.rectTransform.At(new Vector2(0f, 1f), new Vector2(0f, 1f), position, size);
            Button = _background.gameObject.AddComponent<Button>();
            Button.targetGraphic = _background;
            Button.onClick.AddListener(OnTapped);

            Transform row = _background.transform;
            _progress = UIFactory.Text("Progress", row, string.Empty, 13, UIFactory.Ink, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            _progress.rectTransform.Fill(8, 158, 4, 26);
            _progress.enableAutoSizing = true; // the longest name, "Ocean View  16/16", must shrink to leave room for the building
            _progress.fontSizeMin = 11f;
            _progress.fontSizeMax = 13f;
            _staffing = UIFactory.Text("Staffing", row, string.Empty, 11, StaffedColor, TextAlignmentOptions.MidlineRight);
            _staffing.rectTransform.Fill(124, 8, 4, 26);

            _barFill = UIFactory.Bar("ProgressBar", row, GreyboxPalette.PanelBlue, GreyboxPalette.Primary);
            ((RectTransform)_barFill.parent).Fill(8, 8, 23, 19);

            _state = UIFactory.Text("State", row, string.Empty, 11, UIFactory.Ink, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            _state.rectTransform.Fill(8, 147, 30, 3);
            _queue = UIFactory.Text("Queue", row, string.Empty, 11, StaffedColor, TextAlignmentOptions.MidlineRight);
            _queue.rectTransform.Fill(141, 8, 30, 3);
        }

        public Button Button { get; }
        public bool IsVisible => _background.gameObject.activeSelf;
        public Vector2 Position => _background.rectTransform.anchoredPosition;
        public string ProgressText => _progress.text;
        public string StaffingText => _staffing.text;
        public string StateText => _state.text;
        public Color StateColor => _state.color;
        public string QueueText => _queue.text;

        /// <summary>The filled part of the progress bar, from 0 to 1.</summary>
        public float BarFraction => _barFill.anchorMax.x;

        public void Show(ResortGame game, NeedId need)
        {
            NeedStatus status = game.GetNeedStatus(need);
            BuildingRuntime building = game.GetBuilding(game.Config.GetBuildingForNeed(need).building);
            bool isPremium = need == NeedId.OceanView;
            NeedState state = game.GetNeedState(need);

            _building = building.Id;
            _background.gameObject.SetActive(true);
            _background.color = GetRowColor(state, isPremium);
            _progress.text = GameText.NeedProgress(game, status);
            _staffing.text = GameText.BuildingStaffing(building);
            _staffing.color = building.HasWorker ? StaffedColor : GreyboxPalette.Warning;
            _barFill.SetFill(status.Total > 0 ? (float)status.Served / status.Total : 0f);
            _state.text = GameText.NeedStateLabel(state, isPremium);
            _state.color = GetStateColor(state);
            _queue.text = GameText.NeedQueue(status, building);
        }

        public void Hide() => _background.gameObject.SetActive(false);

        private static Color GetRowColor(NeedState state, bool isPremium)
        {
            switch (state)
            {
                case NeedState.Bottleneck: return BottleneckRowColor;
                case NeedState.Recovering: return RecoveringRowColor;
                default: return isPremium ? GreyboxPalette.Premium : RowColor;
            }
        }

        private static Color GetStateColor(NeedState state)
        {
            switch (state)
            {
                case NeedState.Bottleneck: return GreyboxPalette.Warning;
                case NeedState.Recovering: return GreyboxPalette.WorkerOn;
                default: return UIFactory.Ink;
            }
        }

        private void OnTapped() => _owner.OpenWorkerAllocation(_building);
    }
}
