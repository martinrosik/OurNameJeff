using System.Collections.Generic;
using System.Text;

namespace ResortEmpire.Core
{
    /// <summary>Per-wave outcome of <see cref="WaveBalanceSimulator.BuildReport"/>, with WR7/WR8 checks.</summary>
    public sealed class WaveBalanceReport
    {
        public WaveBalanceReport(IReadOnlyList<WaveBalanceRow> rows)
        {
            Rows = rows;
        }

        public IReadOnlyList<WaveBalanceRow> Rows { get; }

        public bool HasViolations
        {
            get
            {
                foreach (WaveBalanceRow row in Rows)
                {
                    if (row.ViolatesWr7 || row.ViolatesWr8) return true;
                }

                return false;
            }
        }

        /// <summary>Coins a passive player earns over waves [first, last] (1-based, inclusive).</summary>
        public int PassiveCoins(int firstWave, int lastWave)
        {
            int total = 0;
            foreach (WaveBalanceRow row in Rows)
            {
                if (row.WaveNumber >= firstWave && row.WaveNumber <= lastWave)
                {
                    total += row.Passive.Result.Coins;
                }
            }

            return total;
        }

        public string ToTable()
        {
            var text = new StringBuilder();
            text.AppendLine("Wave | Guests | Debut | Passive Lv1 (empty bldg)        | Coins | Fully upgraded | Checks");
            text.AppendLine("-----|--------|-------|---------------------------------|-------|----------------|-------");
            foreach (WaveBalanceRow row in Rows)
            {
                string passive = $"{row.Passive.Result.Rating.ToLabel(),-7} {row.Passive.Result.Satisfaction,6:P1} ({row.Passive.EmptyBuilding})";
                string prepared = $"{row.Prepared.Result.Rating.ToLabel(),-7} {row.Prepared.Result.Satisfaction,6:P1}";
                string checks = row.ViolatesWr7 ? "WR7 VIOLATED " : string.Empty;
                checks += row.ViolatesWr8 ? "WR8 VIOLATED" : string.Empty;
                if (checks.Length == 0) checks = "ok";

                text.AppendLine($"{row.WaveNumber,4} | {row.Guests,6} | {(row.IsDebut ? "yes" : ""),-5} | {passive,-31} | {row.Passive.Result.Coins,5} | {prepared,-14} | {checks}");
            }

            return text.ToString();
        }
    }

    public sealed class WaveBalanceRow
    {
        public WaveBalanceRow(int waveIndex, int guests, bool isDebut, PlacementResult passive, PlacementResult prepared)
        {
            WaveIndex = waveIndex;
            Guests = guests;
            IsDebut = isDebut;
            Passive = passive;
            Prepared = prepared;
        }

        public int WaveIndex { get; }
        public int WaveNumber => WaveIndex + 1;
        public int Guests { get; }
        public bool IsDebut { get; }

        /// <summary>Best one-time placement, all buildings at level 1, no moves, no upgrades, no Energy.</summary>
        public PlacementResult Passive { get; }

        /// <summary>Best one-time placement with every building at its top level, no Energy.</summary>
        public PlacementResult Prepared { get; }

        /// <summary>WR7 applies from Wave 3 on, except debut waves.</summary>
        public bool Wr7Applies => WaveNumber >= 3 && !IsDebut;

        public bool ViolatesWr7 => Wr7Applies && Passive.Result.Rating >= StarRating.FiveStars;
        public bool ViolatesWr8 => Prepared.Result.Rating < StarRating.FiveStars;
    }
}
