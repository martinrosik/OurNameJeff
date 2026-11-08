using System;
using System.Collections.Generic;
using ResortEmpire.Core;
using TMPro;
using UnityEngine;

namespace ResortEmpire.Game
{
    /// <summary>
    /// A building on the island (OUR-31) with its two indicators:
    /// <list type="bullet">
    /// <item><b>OUR-50 capacity</b>: one seat square per seat (3 at level 1); a seat fills when a guest has walked
    /// in (<see cref="GuestCrowdView"/>).</item>
    /// <item><b>OUR-48 Worker</b>: a green "W" badge when a Worker stands here, a grey badge and "No Worker" when not.
    /// The empty building must look unstaffed at a glance (Build doc §4.5).</item>
    /// <item><b>OUR-34 queue</b>: one small dot per guest waiting for a seat, 2 rows of 6, then "+N".</item>
    /// <item><b>OUR-38 / OUR-39 state</b>: a red glow around the building (and red queue dots) while it is in
    /// BOTTLENECK, a green glow while it is RECOVERING ("catching up"); nothing when it is calm.</item>
    /// </list>
    /// </summary>
    public sealed class BuildingView : MonoBehaviour
    {
        private const float SeatSize = 0.22f;
        private const float SeatSpacing = 0.29f;
        private const int SeatSortingOrder = 7;
        private const float QueueDotSize = 0.13f;
        private const float QueueDotSpacing = 0.17f;
        private const int QueueDotsPerRow = 6;
        private const int QueueRows = 2;
        private const int QueueMaxDots = QueueDotsPerRow * QueueRows;
        private const int StateGlowSortingOrder = 3;
        private static readonly Vector2 StateGlowSize = new Vector2(2.75f, 2.5f);
        private const float StateGlowY = 0.35f;

        [SerializeField] private BuildingId building;

        [Tooltip("Building art (placeholder Kenney renders).")]
        [SerializeField] private SpriteRenderer[] art = Array.Empty<SpriteRenderer>();

        [Tooltip("The patio plate the seats sit on.")]
        [SerializeField] private SpriteRenderer body;

        [SerializeField] private float seatY = -0.5f;
        [SerializeField] private SpriteRenderer workerBadge;
        [SerializeField] private TMP_Text workerLabel;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text statusLabel;
        [SerializeField] private SpriteRenderer statusBacking;
        [SerializeField] private Transform markersRoot;
        [SerializeField] private Sprite seatSprite;

        [Tooltip("OUR-34: local y of the first row of waiting-guest dots, under the plate.")]
        [SerializeField] private float queueY = -1.1f;

        private readonly List<SpriteRenderer> _seats = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> _queueDots = new List<SpriteRenderer>();
        private TextMeshPro _queueOverflow;
        private SpriteRenderer _stateGlow;

        public BuildingId Building => building;

        /// <summary>The building's art sprites (for later per-level art or highlights).</summary>
        public IReadOnlyList<SpriteRenderer> Art => art;

        /// <summary>Seat squares currently shown (OUR-50).</summary>
        public int ShownSeats { get; private set; }

        /// <summary>Seat squares shown as taken (OUR-50).</summary>
        public int ShownSeated { get; private set; }

        /// <summary>Guests shown waiting in front of the building (OUR-34).</summary>
        public int ShownWaiting { get; private set; }

        /// <summary>OUR-38 / OUR-39: the state the building shows (glow colour).</summary>
        public NeedState ShownState { get; private set; } = NeedState.Ok;

        /// <summary>Whether the red or green state glow is visible.</summary>
        public bool ShowsStateGlow => _stateGlow != null && _stateGlow.enabled;

        /// <summary>Whether the "W" badge is shown (OUR-48).</summary>
        public bool ShowsWorker { get; private set; }

        public string StatusText => statusLabel != null ? statusLabel.text : string.Empty;

        /// <summary>OUR-45: whether a world point is on the building (its art or patio plate), for taps.</summary>
        public bool Contains(Vector2 worldPoint)
        {
            if (!gameObject.activeInHierarchy) return false;
            if (body != null && Hits(body, worldPoint)) return true;
            foreach (SpriteRenderer sprite in art)
            {
                if (sprite != null && Hits(sprite, worldPoint)) return true;
            }

            return false;
        }

        private static bool Hits(SpriteRenderer sprite, Vector2 point)
        {
            if (!sprite.enabled || !sprite.gameObject.activeInHierarchy) return false;
            Bounds bounds = sprite.bounds;
            return point.x >= bounds.min.x && point.x <= bounds.max.x && point.y >= bounds.min.y && point.y <= bounds.max.y;
        }

        /// <param name="seated">Seats to show as taken; null shows the Core's count.</param>
        /// <param name="waiting">Guests to show waiting; null shows the Core's count.</param>
        public void Refresh(ResortGame game, int? seated = null, int? waiting = null)
        {
            BuildingRuntime runtime = game.GetBuilding(building);
            if (runtime == null)
            {
                gameObject.SetActive(false);
                return;
            }

            body.color = GreyboxPalette.ForBuilding(building);
            nameLabel.text = runtime.Definition.displayName;

            ShowState(game.GetBuildingState(building));
            ShowSeats(runtime.Seats, seated ?? game.GetSeatedCount(building));
            ShowQueue(waiting ?? game.GetNeedStatus(runtime.Need).Waiting);
            ShowWorker(runtime.HasWorker);
        }

        /// <summary>OUR-38 / OUR-39: red glow for BOTTLENECK, green for RECOVERING, none otherwise.</summary>
        private void ShowState(NeedState state)
        {
            bool changed = state != ShownState;
            ShownState = state;

            bool glow = state == NeedState.Bottleneck || state == NeedState.Recovering;
            if (glow && _stateGlow == null) _stateGlow = CreateStateGlow();
            if (_stateGlow != null)
            {
                _stateGlow.enabled = glow;
                if (glow) _stateGlow.color = state == NeedState.Bottleneck ? GreyboxPalette.BottleneckGlow : GreyboxPalette.RecoveringGlow;
            }

            if (changed)
            {
                Color dots = state == NeedState.Bottleneck ? GreyboxPalette.Warning : GreyboxPalette.Guest;
                foreach (SpriteRenderer dot in _queueDots) dot.color = dots;
                if (_queueOverflow != null) _queueOverflow.color = dots;
            }
        }

        private SpriteRenderer CreateStateGlow()
        {
            var go = new GameObject("StateGlow");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, StateGlowY, 0f);
            var glow = go.AddComponent<SpriteRenderer>();
            glow.sprite = body.sprite; // the rounded plate sprite, sliced to a soft rounded rectangle
            glow.drawMode = SpriteDrawMode.Sliced;
            glow.size = StateGlowSize;
            glow.sortingOrder = StateGlowSortingOrder;
            return glow;
        }

        private void ShowQueue(int waiting)
        {
            if (waiting == ShownWaiting) return;
            ShownWaiting = waiting;

            int dots = Mathf.Min(waiting, QueueMaxDots);
            while (_queueDots.Count < dots)
            {
                var go = new GameObject("QueueDot");
                go.transform.SetParent(markersRoot, false);
                go.transform.localScale = new Vector3(QueueDotSize, QueueDotSize, 1f);
                var dot = go.AddComponent<SpriteRenderer>();
                dot.sprite = seatSprite;
                dot.color = ShownState == NeedState.Bottleneck ? GreyboxPalette.Warning : GreyboxPalette.Guest;
                dot.sortingOrder = SeatSortingOrder;
                _queueDots.Add(dot);
            }

            for (int i = 0; i < _queueDots.Count; i++)
            {
                bool visible = i < dots;
                _queueDots[i].gameObject.SetActive(visible);
                if (!visible) continue;

                int row = i / QueueDotsPerRow;
                int column = i % QueueDotsPerRow;
                _queueDots[i].transform.localPosition = new Vector3(
                    (column - (QueueDotsPerRow - 1) * 0.5f) * QueueDotSpacing,
                    queueY - row * QueueDotSpacing,
                    0f);
            }

            int overflow = waiting - dots;
            if (overflow > 0 && _queueOverflow == null) _queueOverflow = CreateQueueOverflowLabel();
            if (_queueOverflow != null)
            {
                _queueOverflow.gameObject.SetActive(overflow > 0);
                _queueOverflow.text = overflow > 0 ? $"+{overflow}" : string.Empty;
            }
        }

        private TextMeshPro CreateQueueOverflowLabel()
        {
            // TextMeshPro resets its own RectTransform when added, so position a plain parent instead.
            var anchor = new GameObject("QueueOverflow");
            anchor.transform.SetParent(markersRoot, false);
            float rowsWidth = QueueDotsPerRow * QueueDotSpacing * 0.5f;
            anchor.transform.localPosition = new Vector3(rowsWidth + 0.2f, queueY - (QueueRows - 1) * QueueDotSpacing * 0.5f, 0f);

            var go = new GameObject("Text");
            go.transform.SetParent(anchor.transform, false);
            var label = go.AddComponent<TextMeshPro>();
            label.rectTransform.sizeDelta = new Vector2(0.6f, 0.3f);
            label.fontSize = 2.2f;
            label.color = GreyboxPalette.Guest;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Left;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.sortingOrder = 10;
            return label;
        }

        private void ShowWorker(bool hasWorker)
        {
            ShowsWorker = hasWorker;
            workerBadge.color = hasWorker ? GreyboxPalette.WorkerOn : GreyboxPalette.WorkerOff;
            workerLabel.text = hasWorker ? "W" : "-";

            statusLabel.text = hasWorker ? string.Empty : "No Worker";
            statusLabel.color = GreyboxPalette.Warning;
            if (statusBacking != null) statusBacking.enabled = !hasWorker;
        }

        private void ShowSeats(int seats, int seated)
        {
            ShownSeats = seats;
            ShownSeated = Mathf.Min(seated, seats);
            while (_seats.Count < seats)
            {
                var go = new GameObject("Seat");
                go.transform.SetParent(markersRoot, false);
                go.transform.localScale = new Vector3(SeatSize, SeatSize, 1f);
                var seat = go.AddComponent<SpriteRenderer>();
                seat.sprite = seatSprite;
                seat.sortingOrder = SeatSortingOrder;
                _seats.Add(seat);
            }

            for (int i = 0; i < _seats.Count; i++)
            {
                bool visible = i < seats;
                _seats[i].gameObject.SetActive(visible);
                if (!visible) continue;

                _seats[i].transform.localPosition = new Vector3((i - (seats - 1) * 0.5f) * SeatSpacing, seatY, 0f);
                _seats[i].color = i < seated ? GreyboxPalette.Guest : GreyboxPalette.SeatFree;
            }
        }
    }
}
