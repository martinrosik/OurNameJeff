using System.Collections.Generic;
using ResortEmpire.Core;
using ResortEmpire.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ResortEmpire.UI
{
    /// <summary>
    /// Live satisfaction bar at the top of the Wave Needs Panel (Figma S2, designer decision in docs/design-conflicts.md
    /// row 4): "SATISFACTION  86%" with the stars reached so far, a bar with marks at the 3, 4 and 5 star thresholds,
    /// and under it what the last guest said as it walked out of a building.
    /// <para>
    /// It counts only the guests that have walked out (<see cref="GuestCrowdView.TryGetShownSatisfaction"/>), with the
    /// Wave Complete formula and the config's thresholds, so it moves together with the mood bubbles and ends exactly
    /// on the rating Wave Complete shows.
    /// </para>
    /// </summary>
    internal sealed class SatisfactionMeterView
    {
        /// <summary>Height of the whole block, from its top to the bottom of the comment.</summary>
        public const float Height = 70f;

        private const float Left = 12f;
        private const float BarWidth = 276f;
        private const float StarSize = 12f;
        private const float StarStep = 13f;
        private const int StarCount = 5;
        private const float CommentPopSeconds = 0.25f;

        // Marks on the bar for these tiers: 3, 4 and 5 stars (Figma S2).
        private static readonly WaveRating[] MarkedTiers = { WaveRating.ThreeStars, WaveRating.FourStars, WaveRating.FiveStars };

        private readonly TextMeshProUGUI _value;
        private readonly List<Image> _stars = new List<Image>();
        private readonly RectTransform _barFill;
        private readonly Image _barFillImage;
        private readonly List<RectTransform> _marks = new List<RectTransform>();
        private readonly RectTransform _comment;
        private readonly Image _face;
        private readonly TextMeshProUGUI _commentText;
        private readonly float _marksTop;
        private int _shownFeedbackNumber;
        private float _popTime = CommentPopSeconds;

        public SatisfactionMeterView(Transform panel, float top)
        {
            TextMeshProUGUI label = UIFactory.Text("SatisfactionLabel", panel, GameText.SatisfactionLabel, 11, UIFactory.Ink, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            label.rectTransform.At(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(Left, -top), new Vector2(110f, 16f));

            float starsWidth = StarStep * (StarCount - 1) + StarSize;
            _value = UIFactory.Text("SatisfactionValue", panel, string.Empty, 11, UIFactory.Ink, TextAlignmentOptions.MidlineRight, FontStyles.Bold);
            _value.rectTransform.At(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-Left - starsWidth - 6f, -top), new Vector2(60f, 16f));
            for (int i = 0; i < StarCount; i++)
            {
                Image star = UIFactory.Star($"SatisfactionStar{i}", panel, GreyboxPalette.StarEmpty);
                float x = -Left - (StarCount - 1 - i) * StarStep;
                star.rectTransform.At(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(x, -top - 2f), new Vector2(StarSize, StarSize));
                _stars.Add(star);
            }

            _barFill = UIFactory.Bar("SatisfactionBar", panel, Color.white, GreyboxPalette.NeedGood);
            var track = (RectTransform)_barFill.parent;
            track.At(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(Left, -top - 19f), new Vector2(BarWidth, 10f));
            _barFillImage = _barFill.GetComponent<Image>();

            _marksTop = top + 17f; // the ticks stick out 2 px above the bar
            for (int i = 0; i < MarkedTiers.Length; i++)
            {
                RectTransform mark = UIFactory.Rect($"Mark{(int)MarkedTiers[i]}", panel);
                mark.anchorMin = mark.anchorMax = new Vector2(0f, 1f);
                mark.pivot = new Vector2(0.5f, 1f);
                mark.sizeDelta = new Vector2(30f, 26f);

                Image tick = UIFactory.Panel("Tick", mark, UIFactory.Ink.WithAlpha(0.55f), rounded: false);
                tick.rectTransform.At(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(1.5f, 14f));

                TextMeshProUGUI digit = UIFactory.Text("Tier", mark, ((int)MarkedTiers[i]).ToString(), 10, UIFactory.Ink.WithAlpha(0.75f), TextAlignmentOptions.MidlineRight, FontStyles.Bold);
                digit.rectTransform.At(new Vector2(0.5f, 1f), new Vector2(1f, 1f), new Vector2(0f, -14f), new Vector2(10f, 12f));
                Image star = UIFactory.Star("Star", mark, GreyboxPalette.StarEarnedOutline);
                star.rectTransform.At(new Vector2(0.5f, 1f), new Vector2(0f, 1f), new Vector2(1f, -15f), new Vector2(9f, 9f));
                _marks.Add(mark);
            }

            Image comment = UIFactory.Panel("GuestComment", panel, UIFactory.RowBackground);
            _comment = comment.rectTransform;
            _comment.At(new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(10f + 140f, -top - 57f), new Vector2(280f, 26f));
            _face = UIFactory.Icon("Face", comment.transform, null, Color.white);
            _face.rectTransform.At(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(5f, 0f), new Vector2(20f, 20f));
            _commentText = UIFactory.Text("Text", comment.transform, string.Empty, 11, UIFactory.Ink, TextAlignmentOptions.MidlineLeft, FontStyles.Italic);
            _commentText.rectTransform.Fill(30f, 6f, 2f, 2f);
            _commentText.enableAutoSizing = true;
            _commentText.fontSizeMin = 9f;
            _commentText.fontSizeMax = 11f;
        }

        /// <summary>Whether any guest has walked out yet, so the bar shows a value.</summary>
        public bool HasValue { get; private set; }

        /// <summary>"86%", or "-" before the first guest walks out.</summary>
        public string ValueText => _value.text;

        /// <summary>The filled part of the bar, from 0 to 1 (satisfaction ÷ 100).</summary>
        public float BarFraction => _barFill.anchorMax.x;

        public Color BarColor => _barFillImage.color;

        /// <summary>Gold stars: the rating the guests so far would give (0 before the first guest).</summary>
        public int EarnedStars { get; private set; }

        /// <summary>What the last guest said, with its quotes.</summary>
        public string CommentText => _commentText.text;

        /// <summary>The face next to the comment; null before the first guest.</summary>
        public Sprite CommentFace => _face.enabled ? _face.sprite : null;

        /// <summary>Where each 3/4/5-star mark sits along the bar, from 0 to 1.</summary>
        public float GetMarkFraction(int index) => (_marks[index].anchoredPosition.x - Left) / BarWidth;

        public void Refresh(ResortGame game, GuestCrowdView guests)
        {
            SatisfactionSettings settings = game.Config.satisfaction;
            PlaceMarks(settings);

            double satisfaction = 0;
            HasValue = guests != null && guests.TryGetShownSatisfaction(out satisfaction);
            if (HasValue)
            {
                WaveRating rating = WaveSatisfaction.GetRating(satisfaction, settings);
                EarnedStars = Mathf.Min((int)rating, StarCount);
                _value.text = GameText.LiveSatisfaction(satisfaction);
                _barFill.SetFill((float)(satisfaction / 100.0));
                _barFillImage.color = GetBarColor(rating);
            }
            else
            {
                EarnedStars = 0;
                _value.text = GameText.LiveSatisfaction(null);
                _barFill.SetFill(0f);
            }

            for (int i = 0; i < _stars.Count; i++)
            {
                _stars[i].color = i < EarnedStars ? GreyboxPalette.StarEarned : GreyboxPalette.StarEmpty;
            }

            RefreshComment(game, guests != null ? guests.LatestFeedback : null);
        }

        private void RefreshComment(ResortGame game, GuestCrowdView.GuestFeedback feedback)
        {
            if (feedback == null)
            {
                _shownFeedbackNumber = 0;
                _face.enabled = false;
                _commentText.text = GameText.NoGuestFeedbackYet;
                _comment.localScale = Vector3.one;
                return;
            }

            if (feedback.Number != _shownFeedbackNumber)
            {
                // A new guest has spoken: its comment pops in.
                _shownFeedbackNumber = feedback.Number;
                _face.enabled = true;
                _face.sprite = MoodBubbleArt.Get(feedback.Mood);
                _commentText.text = GameText.GuestFeedback(game, feedback.Need, feedback.Mood, feedback.Number);
                _popTime = 0f;
            }

            _popTime = Mathf.Min(CommentPopSeconds, _popTime + Time.unscaledDeltaTime);
            float pop = Mathf.Lerp(1.06f, 1f, _popTime / CommentPopSeconds);
            _comment.localScale = new Vector3(pop, pop, 1f);
        }

        private void PlaceMarks(SatisfactionSettings settings)
        {
            for (int i = 0; i < _marks.Count; i++)
            {
                int tier = (int)MarkedTiers[i] - 1;
                bool hasThreshold = settings.ratingThresholds != null && tier < settings.ratingThresholds.Length;
                _marks[i].gameObject.SetActive(hasThreshold);
                if (!hasThreshold) continue;

                float fraction = Mathf.Clamp01(settings.ratingThresholds[tier] / 100f);
                _marks[i].anchoredPosition = new Vector2(Left + fraction * BarWidth, -_marksTop);
            }
        }

        /// <summary>Red below 3 stars, amber for 3 stars, green from 4 stars.</summary>
        private static Color GetBarColor(WaveRating rating)
        {
            if (rating < WaveRating.ThreeStars) return GreyboxPalette.NeedBottleneck;
            if (rating < WaveRating.FourStars) return GreyboxPalette.NeedInProgress;
            return GreyboxPalette.NeedGood;
        }
    }
}
