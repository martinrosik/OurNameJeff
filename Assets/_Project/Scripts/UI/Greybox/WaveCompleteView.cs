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
    /// Stars, rewards and "What went well" join the card in a later sprint.
    /// Wave evaluation: the card shows the rating as five stars in an arc (plus PERFECT! for a Perfect wave), then
    /// the Wave Satisfaction % and Guests served, one row each.
    /// </summary>
    internal sealed class WaveCompleteView
    {
        private const float CardWidth = 340f;
        private const float ContentWidth = 316f;
        private const float UnderTitle = 38f;
        private const float PerfectHeight = 22f;
        private const float MessageHeight = 20f;
        private const float RowHeight = 26f;
        private const float RowSpacing = 30f;

        // Below the last row: the gap, the Continue button and the bottom margin.
        private const float ButtonArea = 72f;

        private readonly GreyboxUI _owner;
        private readonly GameObject _root;
        private readonly RectTransform _card;
        private readonly TextMeshProUGUI _title;
        private readonly TextMeshProUGUI _message;
        private readonly RatingStarsView _stars;
        private readonly TextMeshProUGUI _perfect;
        private readonly StatRow _satisfactionRow;
        private readonly StatRow _statRow;
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

            _satisfactionRow = new StatRow("SatisfactionRow", card.transform);
            _statRow = new StatRow("StatRow", card.transform);

            _continue = UIFactory.Button("ContinueButton", card.transform, GameText.ContinueLabel, GreyboxPalette.Primary, Color.white, 17, OnButton);
            ((RectTransform)_continue.transform).At(bottom, bottom, new Vector2(0f, 12f), new Vector2(240f, 46f));

            _root.SetActive(false);
        }

        public bool IsVisible => _root.activeSelf;
        public string TitleText => _title.text;
        public string MessageText => _message.text;
        public string StatText => _statRow.Value;
        public bool StarsVisible => _stars.IsShown;
        public int EarnedStars => _stars.EarnedCount;
        public bool PerfectVisible => _perfect.gameObject.activeSelf;
        public string SatisfactionText => _satisfactionRow.IsShown ? _satisfactionRow.Value : string.Empty;
        public Button ContinueButton => _continue;

        public void Refresh(ResortGame game)
        {
            bool isWaveComplete = game.Phase == GamePhase.WaveComplete;
            _isAllWavesPlayed = game.Phase == GamePhase.AwaitingWave && !game.HasNextWave;
            _root.SetActive(isWaveComplete || _isAllWavesPlayed);

            if (isWaveComplete) ShowWaveComplete(game.LastWaveResult);
            else if (_isAllWavesPlayed) ShowAllWavesPlayed(game);
        }

        private void ShowWaveComplete(WaveResult result)
        {
            _title.text = GameText.WaveCompleteTitle(result.WaveNumber);
            _message.text = GameText.WaveCompleteMessage(result);
            _continue.SetLabel(GameText.ContinueLabel);

            // Perfect is a sixth tier above five stars: it shows all five plus the PERFECT! label.
            int earnedStars = Math.Min((int)result.Rating, RatingStarsView.StarCount);
            LayOut(
                true,
                earnedStars,
                result.Rating == WaveRating.Perfect,
                (_satisfactionRow, GameText.WaveSatisfactionLabel, GameText.WaveSatisfaction(result)),
                (_statRow, GameText.GuestsServedLabel, GameText.GuestsServed(result)));
        }

        private void ShowAllWavesPlayed(ResortGame game)
        {
            _title.text = GameText.AllWavesPlayedTitle;
            _message.text = GameText.AllWavesPlayedMessage;
            _continue.SetLabel(GameText.PlayAgainLabel);
            LayOut(false, 0, false, (_statRow, GameText.WavesCompletedLabel, GameText.WavesCompleted(game)));
        }

        /// <summary>
        /// Places, top to bottom under the title: the stars, PERFECT!, the message and the given rows. Hides the rest and
        /// fits the card's height to what is shown.
        /// </summary>
        private void LayOut(bool showStars, int earnedStars, bool showPerfect, params (StatRow Row, string Label, string Value)[] rows)
        {
            var top = new Vector2(0.5f, 1f);
            float y = UnderTitle;

            if (showStars)
            {
                _stars.Show(earnedStars, -y);
                y += RatingStarsView.Height;
            }
            else
            {
                _stars.Hide();
            }

            _perfect.gameObject.SetActive(showPerfect);
            if (showPerfect)
            {
                _perfect.rectTransform.At(top, top, new Vector2(0f, -y), new Vector2(ContentWidth, PerfectHeight));
                y += PerfectHeight;
            }

            _message.rectTransform.At(top, top, new Vector2(0f, -y), new Vector2(ContentWidth, MessageHeight));
            y += MessageHeight + 8f;

            for (int i = 0; i < rows.Length; i++)
            {
                rows[i].Row.Show(rows[i].Label, rows[i].Value, -(y + i * RowSpacing));
            }

            // Hide only the rows not listed: switching a shown row off and on every frame would rebuild the canvas.
            foreach (StatRow row in new[] { _satisfactionRow, _statRow })
            {
                bool isListed = false;
                foreach ((StatRow Row, string Label, string Value) shown in rows) isListed |= shown.Row == row;
                if (!isListed) row.Hide();
            }

            y += rows.Length * RowSpacing - (RowSpacing - RowHeight);
            _card.sizeDelta = new Vector2(CardWidth, y + ButtonArea);
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
                Image row = UIFactory.Panel(name, card, UIFactory.RowBackground);
                _rect = row.rectTransform;
                _label = UIFactory.Text("Label", row.transform, string.Empty, 13, UIFactory.Ink, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
                _label.rectTransform.Fill(8, 120, 0, 0);
                _value = UIFactory.Text("Value", row.transform, string.Empty, 13, UIFactory.Ink, TextAlignmentOptions.MidlineRight, FontStyles.Bold);
                _value.rectTransform.Fill(120, 8, 0, 0);
            }

            public string Value => _value.text;
            public bool IsShown => _rect.gameObject.activeSelf;

            public void Show(string label, string value, float y)
            {
                var top = new Vector2(0.5f, 1f);
                _rect.At(top, top, new Vector2(0f, y), new Vector2(ContentWidth, RowHeight));
                _label.text = label;
                _value.text = value;
                _rect.gameObject.SetActive(true);
            }

            public void Hide() => _rect.gameObject.SetActive(false);
        }
    }
}
