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
    /// <item><b>OUR-50 capacity</b>: one seat square per seat (3 at level 1); a seat fills when a guest sits there.</item>
    /// <item><b>OUR-48 Worker</b>: a green "W" badge when a Worker stands here, a grey badge and "No Worker" when not.
    /// The empty building must look unstaffed at a glance (Build doc §4.5).</item>
    /// </list>
    /// </summary>
    public sealed class BuildingView : MonoBehaviour
    {
        private const float SeatSize = 0.22f;
        private const float SeatSpacing = 0.29f;
        private const int SeatSortingOrder = 7;

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

        private readonly List<SpriteRenderer> _seats = new List<SpriteRenderer>();

        public BuildingId Building => building;

        /// <summary>The building's art sprites (for later per-level art or highlights).</summary>
        public IReadOnlyList<SpriteRenderer> Art => art;

        /// <summary>Seat squares currently shown (OUR-50).</summary>
        public int ShownSeats { get; private set; }

        /// <summary>Whether the "W" badge is shown (OUR-48).</summary>
        public bool ShowsWorker { get; private set; }

        public string StatusText => statusLabel != null ? statusLabel.text : string.Empty;

        public void Refresh(ResortGame game)
        {
            BuildingRuntime runtime = game.GetBuilding(building);
            if (runtime == null)
            {
                gameObject.SetActive(false);
                return;
            }

            body.color = GreyboxPalette.ForBuilding(building);
            nameLabel.text = runtime.Definition.displayName;

            ShowSeats(runtime.Seats, game.GetSeatedCount(building));
            ShowWorker(runtime.HasWorker);
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
