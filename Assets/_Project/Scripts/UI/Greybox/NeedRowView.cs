using System;
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
        private static readonly Color RowColor = UIFactory.RowBackground;
        private static readonly Color StaffedColor = UIFactory.Ink.WithAlpha(0.75f);
        private static readonly Color BottleneckRowColor = GreyboxPalette.Warning.Lighten(0.75f);
        private static readonly Color RecoveringRowColor = GreyboxPalette.WorkerOn.Lighten(0.75f);

        private const float IconSize = 22f;
        private const float ContentLeft = 36f;

        private readonly GreyboxUI _owner;
        private readonly Image _background;
        private readonly Image _icon;
        private readonly TextMeshProUGUI _progress;
        private readonly TextMeshProUGUI _staffing;
        private readonly RectTransform _barFill;
        private readonly Image _barFillImage;
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
            _icon = UIFactory.Icon("Icon", row, null, Color.white);
            _icon.rectTransform.At(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(8f, 0f), new Vector2(IconSize, IconSize));

            _progress = UIFactory.Text("Progress", row, string.Empty, 13, UIFactory.Ink, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            _progress.rectTransform.Fill(ContentLeft, 158, 4, 26);
            _progress.enableAutoSizing = true; // the longest name, "Ocean View  16/16", must shrink to leave room for the building
            _progress.fontSizeMin = 11f;
            _progress.fontSizeMax = 13f;
            _staffing = UIFactory.Text("Staffing", row, string.Empty, 11, StaffedColor, TextAlignmentOptions.MidlineRight);
            _staffing.rectTransform.Fill(124, 8, 4, 26);

            _barFill = UIFactory.Bar("ProgressBar", row, GreyboxPalette.PanelBlue, GreyboxPalette.Primary);
            ((RectTransform)_barFill.parent).Fill(ContentLeft, 8, 23, 19);
            _barFillImage = _barFill.GetComponent<Image>();

            _state = UIFactory.Text("State", row, string.Empty, 11, UIFactory.Ink, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            _state.rectTransform.Fill(ContentLeft, 147, 30, 3);
            _queue = UIFactory.Text("Queue", row, string.Empty, 11, StaffedColor, TextAlignmentOptions.MidlineRight);
            _queue.rectTransform.Fill(141, 8, 30, 3);
        }

        public Button Button { get; }
        public bool IsVisible => _background.gameObject.activeSelf;
        /// <summary>"Food  2/6" as the player reads it, without the count's color tag.</summary>
        public string ProgressText => _progress.GetParsedText();
        public string StaffingText => _staffing.text;
        public string StateText => _state.text;
        public Color StateColor => _state.color;
        public string QueueText => _queue.text;
        public Sprite IconSprite => _icon.sprite;
        public Color IconColor => _icon.color;
        public Color BarColor => _barFillImage.color;

        /// <summary>The filled part of the progress bar, from 0 to 1.</summary>
        public float BarFraction => _barFill.anchorMax.x;

        /// <param name="shownStatus">
        /// The need's numbers as the island shows them: seated = guests inside the building, served = guests that have
        /// walked out. Null uses the Core's numbers, which run ahead of the walking guests (a unit sits down the moment
        /// it arrives, while its guest is still on the way, so "Full: 3/3 seats" could flash with nobody there).
        /// </param>
        public void Show(ResortGame game, NeedId need, Func<NeedId, NeedStatus, NeedStatus> shownStatus = null)
        {
            NeedStatus status = game.GetNeedStatus(need);
            if (shownStatus != null) status = shownStatus(need, status);
            BuildingRuntime building = game.GetBuilding(game.Config.GetBuildingForNeed(need).building);
            bool isPremium = need == NeedId.OceanView;
            NeedState state = game.GetNeedState(need);

            // "All served" (and its green) only once the island shows it too: the last guests must have walked out,
            // not just been served by the Core. Until then the row stays "OK" with the count still filling up.
            if (state == NeedState.Satisfied && status.Served < status.Total) state = NeedState.Ok;
            Color stateColor = GetStateColor(state);
            Sprite icon = UIFactory.GetNeedIcon(need);

            _building = building.Id;
            _background.gameObject.SetActive(true);
            _background.color = GetRowColor(state, isPremium);
            _icon.sprite = icon;
            _icon.enabled = icon != null;
            _icon.color = stateColor;
            _progress.text = GameText.NeedProgress(game, status, stateColor);
            _staffing.text = GameText.BuildingStaffing(building);
            _staffing.color = building.HasWorker ? StaffedColor : GreyboxPalette.Warning;
            _barFill.SetFill(status.Total > 0 ? (float)status.Served / status.Total : 0f);
            _barFillImage.color = stateColor;
            _state.text = GameText.NeedStateLabel(state, isPremium);
            _state.color = stateColor;
            _queue.text = GameText.NeedQueue(status, building);
        }

        public void Hide() => _background.gameObject.SetActive(false);

        /// <summary>Moves and sizes the row on the panel (its contents stretch with it).</summary>
        public void SetLayout(Vector2 position, Vector2 size) =>
            _background.rectTransform.At(new Vector2(0f, 1f), new Vector2(0f, 1f), position, size);

        private static Color GetRowColor(NeedState state, bool isPremium)
        {
            switch (state)
            {
                case NeedState.Bottleneck: return BottleneckRowColor;
                case NeedState.Recovering: return RecoveringRowColor;
                default: return isPremium ? GreyboxPalette.Premium : RowColor;
            }
        }

        /// <summary>One color per state for the icon, the served count, the bar and the state label.</summary>
        private static Color GetStateColor(NeedState state)
        {
            switch (state)
            {
                case NeedState.Bottleneck: return GreyboxPalette.NeedBottleneck;
                case NeedState.Recovering:
                case NeedState.Satisfied: return GreyboxPalette.NeedGood;
                default: return GreyboxPalette.NeedInProgress;
            }
        }

        private void OnTapped() => _owner.OpenWorkerAllocation(_building);
    }
}
