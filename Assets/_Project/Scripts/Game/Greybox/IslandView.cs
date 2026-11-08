using System;
using ResortEmpire.Core;
using UnityEngine;

namespace ResortEmpire.Game
{
    /// <summary>
    /// The island (OUR-31): refreshes every <see cref="BuildingView"/> and the guest boat from the game state.
    /// Tapping buildings to move Workers is OUR-45 (see docs/tasks/OUR-45.md).
    /// </summary>
    public sealed class IslandView : MonoBehaviour
    {
        [SerializeField] private GameController controller;
        [SerializeField] private BoatView boat;

        private BuildingView[] _buildings = Array.Empty<BuildingView>();

        private void Awake()
        {
            _buildings = GetComponentsInChildren<BuildingView>(true);
        }

        private void LateUpdate()
        {
            ResortGame game = controller != null ? controller.Game : null;
            if (game == null) return;

            foreach (BuildingView view in _buildings)
            {
                view.Refresh(game);
            }

            if (boat != null) boat.Refresh(game);
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
