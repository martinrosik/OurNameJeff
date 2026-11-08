using System;
using ResortEmpire.Core;
using UnityEngine;

namespace ResortEmpire.Game
{
    /// <summary>
    /// The island (OUR-31): refreshes every <see cref="BuildingView"/>, the guest boat and the walking guests
    /// (<see cref="GuestCrowdView"/>) from the game state.
    /// Tapping buildings to move Workers is OUR-45 (see docs/tasks/OUR-45.md).
    /// </summary>
    public sealed class IslandView : MonoBehaviour
    {
        [SerializeField] private GameController controller;
        [SerializeField] private BoatView boat;
        [SerializeField] private GuestCrowdView guests;

        private BuildingView[] _buildings = Array.Empty<BuildingView>();

        private void Awake()
        {
            _buildings = GetComponentsInChildren<BuildingView>(true);
        }

        private void LateUpdate()
        {
            ResortGame game = controller != null ? controller.Game : null;
            if (game == null) return;

            // Guests first: a building's seats and queue squares fill when its guests have walked in.
            if (guests != null) guests.Refresh(game, this);

            foreach (BuildingView view in _buildings)
            {
                BuildingRuntime building = game.GetBuilding(view.Building);
                if (guests != null && building != null)
                {
                    view.Refresh(game, guests.GetShownSeated(building.Need), guests.GetShownWaiting(building.Need));
                }
                else
                {
                    view.Refresh(game);
                }
            }

            if (boat != null) boat.Refresh(game);
        }

        /// <summary>The guests walking on the island.</summary>
        public GuestCrowdView Guests => guests;

        public BuildingView GetView(BuildingId building)
        {
            foreach (BuildingView view in _buildings)
            {
                if (view.Building == building) return view;
            }

            return null;
        }
    }
}
