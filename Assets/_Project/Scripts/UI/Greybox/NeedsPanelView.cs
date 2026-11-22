using System;
using System.Collections.Generic;
using ResortEmpire.Core;
using ResortEmpire.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ResortEmpire.UI
{
    /// <summary>
    /// OUR-37 / UI doc S2 Wave Needs Panel: how the resort is coping, need by need, on the right of the island.
    /// It takes over the side area from the forecast (OUR-33) once the player presses Welcome Guests.
    /// At the top, the live satisfaction bar with the last guest's comment (<see cref="SatisfactionMeterView"/>,
    /// designer decision in docs/design-conflicts.md row 4). Under it one row per need, in the order of the wave's
    /// needs; the rows get shorter when more needs than fit at full height take part (later unlocks).
    /// </summary>
    internal sealed class NeedsPanelView
    {
        private const float SatisfactionTop = 54f;
        private const float RowsTop = SatisfactionTop + SatisfactionMeterView.Height + 6f;
        private const float RowHeight = 48f;
        private const float MinRowHeight = 36f;
        private const float RowSpacing = 4f;
        private const float RowWidth = 280f;
        private const float RowsBottom = 24f; // room for the hint
        private static readonly int RowCount = Enum.GetValues(typeof(NeedId)).Length;

        private readonly GameObject _panel;
        private readonly TextMeshProUGUI _title;
        private readonly TextMeshProUGUI _arrivals;
        private readonly TextMeshProUGUI _guests;
        private readonly SatisfactionMeterView _satisfaction;
        private readonly List<NeedRowView> _rows = new List<NeedRowView>();

        public NeedsPanelView(GreyboxUI owner, RectTransform sideArea)
        {
            Image panel = UIFactory.SidePanel("NeedsPanel", sideArea);
            _panel = panel.gameObject;

            _title = UIFactory.Text("Title", panel.transform, string.Empty, 17, UIFactory.Ink, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            _title.rectTransform.At(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12f, -8f), new Vector2(120f, 24f));
            _arrivals = UIFactory.Text("Arrivals", panel.transform, string.Empty, 13, UIFactory.Ink, TextAlignmentOptions.MidlineRight);
            _arrivals.rectTransform.At(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-12f, -8f), new Vector2(150f, 24f));
            _guests = UIFactory.Text("Guests", panel.transform, string.Empty, 13, UIFactory.Ink);
            _guests.rectTransform.At(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12f, -32f), new Vector2(276f, 20f));

            _satisfaction = new SatisfactionMeterView(panel.transform, SatisfactionTop);

            for (int i = 0; i < RowCount; i++)
            {
                var row = new NeedRowView(owner, panel.transform, i, Vector2.zero, new Vector2(RowWidth, RowHeight));
                row.SetLayout(GetRowPosition(i, RowHeight), new Vector2(RowWidth, RowHeight));
                _rows.Add(row);
            }

            TextMeshProUGUI hint = UIFactory.Text("Hint", panel.transform, GameText.NeedsPanelHint, 11, UIFactory.Ink.WithAlpha(0.75f));
            hint.rectTransform.At(new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(12f, 6f), new Vector2(276f, 16f));
        }

        public bool IsVisible => _panel.activeSelf;
        public RectTransform Panel => (RectTransform)_panel.transform;
        public string TitleText => _title.text;
        public string ArrivalsText => _arrivals.text;
        public SatisfactionMeterView Satisfaction => _satisfaction;
        public IReadOnlyList<NeedRowView> Rows => _rows;

        /// <param name="guests">
        /// The walking guests: the rows show each need's numbers as the island shows them
        /// (<see cref="GuestCrowdView.GetShownStatus"/>: seated = guests inside, served = guests that walked out), and the
        /// satisfaction bar counts the guests that walked out. Null uses the Core's numbers and leaves the bar empty.
        /// </param>
        public void Refresh(ResortGame game, GuestCrowdView guests = null)
        {
            bool isVisible = ShouldShow(game.Phase);
            _panel.SetActive(isVisible);
            if (!isVisible) return;

            WaveSimulation wave = game.CurrentWave;
            _title.text = GameText.WaveTitle(wave);
            _arrivals.text = GameText.WaveArrivals(wave);
            _guests.text = GameText.WaveGuests(wave);
            _satisfaction.Refresh(game, guests);

            Func<NeedId, NeedStatus, NeedStatus> shownStatus = guests != null ? guests.GetShownStatus : (Func<NeedId, NeedStatus, NeedStatus>)null;
            var needs = new List<NeedId>(wave.Needs);
            float rowHeight = GetRowHeight(needs.Count);
            for (int i = 0; i < needs.Count && i < _rows.Count; i++)
            {
                _rows[i].SetLayout(GetRowPosition(i, rowHeight), new Vector2(RowWidth, rowHeight));
                _rows[i].Show(game, needs[i], shownStatus);
            }

            for (int i = needs.Count; i < _rows.Count; i++)
            {
                _rows[i].Hide();
            }
        }

        /// <summary>Full height while the rows fit under the satisfaction bar; shorter when more needs take part.</summary>
        private float GetRowHeight(int rows)
        {
            float panelHeight = Panel.rect.height;
            if (rows <= 0 || panelHeight <= 0f) return RowHeight;
            float fit = (panelHeight - RowsTop - RowsBottom) / rows - RowSpacing;
            return Mathf.Clamp(fit, MinRowHeight, RowHeight);
        }

        private static Vector2 GetRowPosition(int index, float rowHeight) =>
            new Vector2(10f, -RowsTop - index * (rowHeight + RowSpacing));

        /// <summary>Stays up while the guests leave, so the player can see the final counts.</summary>
        private static bool ShouldShow(GamePhase phase) =>
            phase == GamePhase.WaveIncoming || phase == GamePhase.WaveActive || phase == GamePhase.WaveResolving;
    }
}
