using System;
using System.Collections.Generic;
using ResortEmpire.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ResortEmpire.UI
{
    /// <summary>
    /// OUR-37 / UI doc S2 Wave Needs Panel: how the resort is coping, need by need, on the right of the island.
    /// It takes over the side area from the forecast (OUR-33) once the player presses Welcome Guests.
    /// Rows have fixed positions, so needs added by later unlocks never move the existing ones.
    /// It never shows Wave Satisfaction (UI doc S2).
    /// </summary>
    internal sealed class NeedsPanelView
    {
        private const float RowsTop = 58f;
        private const float RowHeight = 48f;
        private const float RowSpacing = 4f;
        private const float RowWidth = 280f;
        private static readonly int RowCount = Enum.GetValues(typeof(NeedId)).Length;

        private readonly GameObject _panel;
        private readonly TextMeshProUGUI _title;
        private readonly TextMeshProUGUI _arrivals;
        private readonly TextMeshProUGUI _guests;
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

            for (int i = 0; i < RowCount; i++)
            {
                var position = new Vector2(10f, -RowsTop - i * (RowHeight + RowSpacing));
                _rows.Add(new NeedRowView(owner, panel.transform, i, position, new Vector2(RowWidth, RowHeight)));
            }

            TextMeshProUGUI hint = UIFactory.Text("Hint", panel.transform, GameText.NeedsPanelHint, 11, UIFactory.Ink.WithAlpha(0.75f));
            hint.rectTransform.At(new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(12f, 6f), new Vector2(276f, 16f));
        }

        public bool IsVisible => _panel.activeSelf;
        public string TitleText => _title.text;
        public string ArrivalsText => _arrivals.text;
        public IReadOnlyList<NeedRowView> Rows => _rows;

        public void Refresh(ResortGame game)
        {
            bool isVisible = ShouldShow(game.Phase);
            _panel.SetActive(isVisible);
            if (!isVisible) return;

            WaveSimulation wave = game.CurrentWave;
            _title.text = GameText.WaveTitle(wave);
            _arrivals.text = GameText.WaveArrivals(wave);
            _guests.text = GameText.WaveGuests(wave);

            int index = 0;
            foreach (NeedId need in wave.Needs)
            {
                _rows[index].Show(game, need);
                index++;
            }

            for (int i = index; i < _rows.Count; i++)
            {
                _rows[i].Hide();
            }
        }

        /// <summary>Stays up while the guests leave, so the player can see the final counts.</summary>
        private static bool ShouldShow(GamePhase phase) =>
            phase == GamePhase.WaveIncoming || phase == GamePhase.WaveActive || phase == GamePhase.WaveResolving;
    }
}
