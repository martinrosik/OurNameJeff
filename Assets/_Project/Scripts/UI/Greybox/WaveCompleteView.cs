using System;
using ResortEmpire.Core;
using ResortEmpire.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ResortEmpire.UI
{
    /// <summary>
    /// OUR-40 / UI doc S4 Wave Complete: a card in the middle of the dimmed screen once the wave is over.
    /// One main button, Continue, returns to the island with the next wave's forecast.
    /// After the last wave the same card says ALL WAVES PLAYED, and its button, Play Again, starts a new game.
    /// Rewards and "What went well" join the card in a later sprint.
    /// Wave evaluation: the card shows the rating as five stars in an arc (plus PERFECT! for a Perfect wave), then
    /// the Wave Satisfaction % and Guests served, one row each. Resort Value: a last row says what the wave added ("+100",
    /// "+0" after a weak wave).
    /// Wave evaluation details: under the stars a PERFORMANCE part with colored Guests served and Wave Satisfaction, then
    /// icon lines "What went well / Needs improvement" from <see cref="WaveBreakdown"/>, only the ones that apply.
    /// </summary>
    internal sealed class WaveCompleteView
    {
        private const float CardWidth = 340f;
        private const float ContentWidth = 316f;
        private const float UnderTitle = 38f;
        private const float PerfectHeight = 22f;
        private const float MessageHeight = 20f;
        private const float HeadingHeight = 20f;
        private const float RowHeight = 22f;
        private const float RowSpacing = 22f;
        private const float LineHeight = 20f;
        private const float LineSpacing = 22f;
        private const float IconSize = 16f;

        // Below the last row: the gap, the Continue button and the bottom margin.
        private const float ButtonArea = 72f;

        private readonly GreyboxUI _owner;
        private readonly GameObject _root;
        private readonly RectTransform _card;
        private readonly TextMeshProUGUI _title;
        private readonly TextMeshProUGUI _message;
        private readonly RatingStarsView _stars;
        private readonly TextMeshProUGUI _perfect;
        private readonly TextMeshProUGUI _performance;
        private readonly StatRow _satisfactionRow;
        private readonly StatRow _statRow;
        private readonly StatRow _resortValueRow;
        private readonly BreakdownLine _wentGreatLine;
        private readonly BreakdownLine _needsWorkerLine;
        private readonly BreakdownLine _needsCapacityLine;
        private readonly Button _continue;
        private bool _isAllWavesPlayed;

        public WaveCompleteView(GreyboxUI owner, Transform canvas)
        {
            _owner = owner;

            Image backdrop = UIFactory.Backdrop("WaveComplete", canvas);
            _root = backdrop.gameObject;

            var center = new Vector2(0.5f, 0.5f);
            var top = new Vector2(0.5f, 1f);
            var bottom = new Vector2(0.5f, 0f);

            Image card = UIFactory.Panel("Card", backdrop.transform, UIFactory.PanelBackground);
            _card = card.rectTransform;
            _card.At(center, center, Vector2.zero, new Vector2(CardWidth, UnderTitle + ButtonArea));

            _title = UIFactory.Text("Title", card.transform, string.Empty, 17, UIFactory.Ink, TextAlignmentOptions.Center, FontStyles.Bold);
            _title.rectTransform.At(top, top, new Vector2(0f, -12f), new Vector2(ContentWidth, 24f));

            _stars = new RatingStarsView(card.transform);
            _perfect = UIFactory.Text("Perfect", card.transform, GameText.PerfectLabel, 16, GreyboxPalette.Primary, TextAlignmentOptions.Center, FontStyles.Bold);
            _message = UIFactory.Text("Message", card.transform, string.Empty, 13, UIFactory.Ink, TextAlignmentOptions.Center);
            _performance = UIFactory.Text("Performance", card.transform, GameText.PerformanceLabel, 14, UIFactory.Ink, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);

            _satisfactionRow = new StatRow("SatisfactionRow", card.transform);
            _statRow = new StatRow("StatRow", card.transform);
            _resortValueRow = new StatRow("ResortValueRow", card.transform);

            _wentGreatLine = new BreakdownLine("WentGreatLine", card.transform, ResultIconArt.Check, GreyboxPalette.NeedGood);
            _needsWorkerLine = new BreakdownLine("NeedsWorkerLine", card.transform, ResultIconArt.Warning, GreyboxPalette.NeedInProgress);
            _needsCapacityLine = new BreakdownLine("NeedsCapacityLine", card.transform, ResultIconArt.Warning, GreyboxPalette.NeedInProgress);

            _continue = UIFactory.Button("ContinueButton", card.transform, GameText.ContinueLabel, GreyboxPalette.Primary, Color.white, 17, OnButton);
            ((RectTransform)_continue.transform).At(bottom, bottom, new Vector2(0f, 12f), new Vector2(240f, 46f));

            _root.SetActive(false);
        }

        public bool IsVisible => _root.activeSelf;
        public string TitleText => _title.text;
        public string MessageText => _message.gameObject.activeSelf ? _message.text : string.Empty;
        public string StatText => _statRow.Value;
        public bool StarsVisible => _stars.IsShown;
        public int EarnedStars => _stars.EarnedCount;
        public bool PerfectVisible => _perfect.gameObject.activeSelf;
        public bool PerformanceVisible => _performance.gameObject.activeSelf;
        public string SatisfactionText => _satisfactionRow.IsShown ? _satisfactionRow.Value : string.Empty;
        public Color SatisfactionColor => _satisfactionRow.ValueColor;
        public Color GuestsServedColor => _statRow.ValueColor;
        public string ResortValueGainText => _resortValueRow.IsShown ? _resortValueRow.Value : string.Empty;
        public string WentGreatText => _wentGreatLine.Text;
        public string NeedsWorkerText => _needsWorkerLine.Text;
        public string NeedsCapacityText => _needsCapacityLine.Text;
        public Button ContinueButton => _continue;

        /// <summary>Tutorial: the card, and the rating stars on it, for the bubble that explains them.</summary>
        public RectTransform Card => _card;
        public RectTransform Stars => _stars.Root;

        public void Refresh(ResortGame game)
        {
            bool isWaveComplete = game.Phase == GamePhase.WaveComplete;
            _isAllWavesPlayed = game.Phase == GamePhase.AwaitingWave && !game.HasNextWave;
            _root.SetActive(isWaveComplete || _isAllWavesPlayed);

            if (isWaveComplete) ShowWaveComplete(game, game.LastWaveResult);
            else if (_isAllWavesPlayed) ShowAllWavesPlayed(game);
        }

        /// <summary>
        /// Places, top to bottom under the title: the stars, PERFECT!, the message and the given rows. Hides the rest and
        /// fits the card's height to what is shown.
        /// </summary>
        private void ShowWaveComplete(ResortGame game, WaveResult result)
        {
            _title.text = GameText.WaveCompleteTitle(result.WaveNumber);
            _continue.SetLabel(_owner.ContinueLabelOverride ?? GameText.ContinueLabel);

            // Perfect is a sixth tier above five stars: it shows all five plus the PERFECT! label.
            int earnedStars = Math.Min((int)result.Rating, RatingStarsView.StarCount);
            float y = UnderTitle;
            _stars.Show(earnedStars, -y);
            y += RatingStarsView.Height;
            y = Place(_perfect, result.Rating == WaveRating.Perfect, y, PerfectHeight);
            y = Place(_message, false, y, MessageHeight);
            y = Place(_performance, true, y, HeadingHeight);
            y = ShowRows(
                y,
                (_statRow, GameText.GuestsServedLabel, GameText.GuestsServed(result), GreyboxPalette.NeedGood),
                (_satisfactionRow, GameText.WaveSatisfactionLabel, GameText.WaveSatisfaction(result), GreyboxPalette.ForRating(result.Rating)),
                (_resortValueRow, GameText.ResortValueLabel, GameText.ResortValueGain(result), UIFactory.Ink));

            WaveBreakdown breakdown = result.Breakdown;
            y += 4f;
            y = _wentGreatLine.Place(breakdown.WentGreat.Count > 0 ? GameText.WentGreat(game, breakdown.WentGreat) : null, y);
            y = _needsWorkerLine.Place(breakdown.NeedsWorker.Count > 0 ? GameText.NeedsWorker(game, breakdown.NeedsWorker) : null, y);
            y = _needsCapacityLine.Place(breakdown.NeedsCapacity.Count > 0 ? GameText.NeedsCapacity(game, breakdown.NeedsCapacity) : null, y);
            _card.sizeDelta = new Vector2(CardWidth, y + ButtonArea);
        }

        private void ShowAllWavesPlayed(ResortGame game)
        {
            _title.text = GameText.AllWavesPlayedTitle;
            _message.text = GameText.AllWavesPlayedMessage;
            _continue.SetLabel(GameText.PlayAgainLabel);

            float y = UnderTitle;
            _stars.Hide();
            y = Place(_perfect, false, y, PerfectHeight);
            y = Place(_message, true, y, MessageHeight) + 8f;
            y = Place(_performance, false, y, HeadingHeight);
            y = ShowRows(y, (_statRow, GameText.WavesCompletedLabel, GameText.WavesCompleted(game), UIFactory.Ink));
            _wentGreatLine.Place(null, y);
            _needsWorkerLine.Place(null, y);
            _needsCapacityLine.Place(null, y);
            _card.sizeDelta = new Vector2(CardWidth, y + ButtonArea);
        }

        /// <summary>Shows a one-line text at <paramref name="y"/> under the card's top, or hides it. Returns the y below it.</summary>
        private static float Place(TextMeshProUGUI text, bool show, float y, float height)
        {
            text.gameObject.SetActive(show);
            if (!show) return y;

            var top = new Vector2(0.5f, 1f);
            text.rectTransform.At(top, top, new Vector2(0f, -y), new Vector2(ContentWidth, height));
            return y + height;
        }

        /// <summary>Places the given rows top to bottom from <paramref name="y"/>, hides the rest and returns the y below them.</summary>
        private float ShowRows(float y, params (StatRow Row, string Label, string Value, Color ValueColor)[] rows)
        {
            for (int i = 0; i < rows.Length; i++)
            {
                rows[i].Row.Show(rows[i].Label, rows[i].Value, rows[i].ValueColor, -(y + i * RowSpacing));
            }

            // Hide only the rows not listed: switching a shown row off and on every frame would rebuild the canvas.
            foreach (StatRow row in new[] { _satisfactionRow, _statRow, _resortValueRow })
            {
                bool isListed = false;
                foreach ((StatRow Row, string Label, string Value, Color ValueColor) shown in rows) isListed |= shown.Row == row;
                if (!isListed) row.Hide();
            }

            return y + rows.Length * RowSpacing - (RowSpacing - RowHeight);
        }

        private void OnButton()
        {
            if (_isAllWavesPlayed) _owner.PlayAgain();
            else _owner.ContinueAfterWave();
        }

        /// <summary>One "label ... value" row of the card, e.g. "Guests served   12/12".</summary>
        private sealed class StatRow
        {
            private readonly RectTransform _rect;
            private readonly TextMeshProUGUI _label;
            private readonly TextMeshProUGUI _value;

            public StatRow(string name, Transform card)
            {
                _rect = UIFactory.Rect(name, card);
                _label = UIFactory.Text("Label", _rect, string.Empty, 13, UIFactory.Ink, TextAlignmentOptions.MidlineLeft);
                _label.rectTransform.Fill(0, 120, 0, 0);
                _value = UIFactory.Text("Value", _rect, string.Empty, 14, UIFactory.Ink, TextAlignmentOptions.MidlineRight, FontStyles.Bold);
                _value.rectTransform.Fill(120, 0, 0, 0);
            }

            public string Value => _value.text;
            public Color ValueColor => _value.color;
            public bool IsShown => _rect.gameObject.activeSelf;

            public void Show(string label, string value, Color valueColor, float y)
            {
                var top = new Vector2(0.5f, 1f);
                _rect.At(top, top, new Vector2(0f, y), new Vector2(ContentWidth, RowHeight));
                _label.text = label;
                _value.text = value;
                _value.color = valueColor;
                _rect.gameObject.SetActive(true);
            }

            public void Hide() => _rect.gameObject.SetActive(false);
        }

        /// <summary>One breakdown line: a tinted icon and a short text, e.g. "(check) Rooms & Drinks went great!".</summary>
        private sealed class BreakdownLine
        {
            private readonly RectTransform _rect;
            private readonly TextMeshProUGUI _text;

            public BreakdownLine(string name, Transform card, Sprite icon, Color iconColor)
            {
                _rect = UIFactory.Rect(name, card);
                Image image = UIFactory.Icon("Icon", _rect, icon, iconColor);
                image.rectTransform.At(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(IconSize, IconSize));
                _text = UIFactory.Text("Text", _rect, string.Empty, 13, UIFactory.Ink);
                _text.rectTransform.Fill(IconSize + 6f, 0, 0, 0);

                // A long list of needs shrinks the font a little instead of being cut off.
                _text.enableAutoSizing = true;
                _text.fontSizeMin = 10;
                _text.fontSizeMax = 13;
            }

            public string Text => _rect.gameObject.activeSelf ? _text.text : string.Empty;

            /// <summary>Shows <paramref name="text"/> at <paramref name="y"/>, or hides the line when it is null. Returns the y below it.</summary>
            public float Place(string text, float y)
            {
                _rect.gameObject.SetActive(text != null);
                if (text == null) return y;

                var top = new Vector2(0.5f, 1f);
                _rect.At(top, top, new Vector2(0f, -y), new Vector2(ContentWidth, LineHeight));
                _text.text = text;
                return y + LineSpacing;
            }
        }
    }
}
