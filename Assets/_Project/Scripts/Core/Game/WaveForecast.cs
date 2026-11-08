using System.Collections.Generic;

namespace ResortEmpire.Core
{
    /// <summary>
    /// "Next wave: Rooms 16 · Drinks 18 · Food 14" on S1, so the player can place Workers before Start Wave.
    /// Lists only needs the player owns (WR9) and never shows the Sudden rush.
    /// </summary>
    public sealed class WaveForecast
    {
        public WaveForecast(int waveIndex, int guests, IReadOnlyList<NeedDemand> demand)
        {
            WaveIndex = waveIndex;
            Guests = guests;
            Demand = demand;
        }

        public int WaveIndex { get; }
        public int WaveNumber => WaveIndex + 1;
        public int Guests { get; }
        public IReadOnlyList<NeedDemand> Demand { get; }

        public int GetDemand(NeedId need)
        {
            foreach (NeedDemand entry in Demand)
            {
                if (entry.Need == need) return entry.Units;
            }

            return 0;
        }
    }

    public readonly struct NeedDemand
    {
        public NeedDemand(NeedId need, int units)
        {
            Need = need;
            Units = units;
        }

        public NeedId Need { get; }
        public int Units { get; }
    }
}
