using System;
using ResortEmpire.Core;
using UnityEngine;

namespace ResortEmpire.Game
{
    /// <summary>
    /// The island (OUR-31): refreshes every <see cref="BuildingView"/>, the guest boat and the walking guests
    /// (<see cref="GuestCrowdView"/>) from the game state, and completes a resolving wave when the guests have
    /// boarded (OUR-35).
    /// OUR-45: <see cref="GetBuildingAt"/> finds the tapped building; the UI opens the Worker allocation sheet.
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

        private void LateUpdate() => Refresh();

        /// <summary>Updates every view from the game; once per frame, or directly from tests.</summary>
        internal void Refresh()
        {
            ResortGame game = controller != null ? controller.Game : null;
            if (game == null) return;

            // Guests first: a building's seats fill when its guests have walked in.
            if (guests != null) guests.Refresh(game, this);

            foreach (BuildingView view in _buildings)
            {
                BuildingRuntime building = game.GetBuilding(view.Building);
                if (guests != null && building != null)
                {
                    view.Refresh(game, guests.GetShownSeated(building.Need));
                }
                else
                {
                    view.Refresh(game);
                }
            }

            if (boat != null) boat.Refresh(game);

            // OUR-35: everyone is served; the wave is over once the last guest is back on the boat.
            if (game.Phase == GamePhase.WaveResolving && (guests == null || guests.AllBoarded))
            {
                game.FinishResolving();
            }
        }

        /// <summary>The guests walking on the island.</summary>
        public GuestCrowdView Guests => guests;

        /// <summary>OUR-45: the building under a world point (a tap), or null.</summary>
        public BuildingView GetBuildingAt(Vector2 worldPoint)
        {
            foreach (BuildingView view in _buildings)
            {
                if (view.Contains(worldPoint)) return view;
            }

            return null;
        }

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
