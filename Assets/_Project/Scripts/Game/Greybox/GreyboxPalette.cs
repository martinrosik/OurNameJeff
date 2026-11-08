using ResortEmpire.Core;
using UnityEngine;

namespace ResortEmpire.Game
{
    /// <summary>Placeholder colors shared by the greybox island and UI. Replaced in the art pass.</summary>
    public static class GreyboxPalette
    {
        public static readonly Color Sand = new Color(0.93f, 0.85f, 0.62f);
        public static readonly Color Grass = new Color(0.47f, 0.72f, 0.38f);

        public static readonly Color Ink = new Color(0.22f, 0.14f, 0.09f);
        public static readonly Color Paper = new Color(1f, 0.98f, 0.94f);
        public static readonly Color PanelBlue = new Color(0.86f, 0.95f, 1f);
        public static readonly Color Primary = new Color(0.98f, 0.47f, 0.17f);
        public static readonly Color Warning = new Color(0.90f, 0.25f, 0.22f);
        public static readonly Color Premium = new Color(1f, 0.90f, 0.62f);

        public static readonly Color WorkerOn = new Color(0.18f, 0.70f, 0.38f);
        public static readonly Color WorkerOff = new Color(0.62f, 0.62f, 0.62f);
        public static readonly Color Guest = new Color(0.25f, 0.20f, 0.45f);
        public static readonly Color SeatFree = new Color(1f, 1f, 1f, 0.85f);

        public static Color ForBuilding(BuildingId building)
        {
            switch (building)
            {
                case BuildingId.Reception: return new Color(0.82f, 0.66f, 0.48f);
                case BuildingId.Restaurant: return new Color(0.95f, 0.62f, 0.36f);
                case BuildingId.BeachBar: return new Color(0.36f, 0.76f, 0.74f);
                case BuildingId.Pool: return new Color(0.42f, 0.66f, 0.95f);
                case BuildingId.Bungalows: return new Color(0.66f, 0.50f, 0.36f);
                default: return Color.white;
            }
        }
    }
}
