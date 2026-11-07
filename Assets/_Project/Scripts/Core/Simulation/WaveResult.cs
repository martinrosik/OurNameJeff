using System.Collections.Generic;

namespace ResortEmpire.Core
{
    /// <summary>
    /// Everything Wave Complete (S4) and the Upgrade Suggestion (S5) need to explain the result.
    /// </summary>
    public sealed class WaveResult
    {
        public WaveResult(
            int waveIndex,
            int guests,
            int unitsServed,
            double satisfaction,
            StarRating rating,
            int coins,
            int energy,
            int resortValue,
            double durationSeconds,
            IReadOnlyList<NeedWaveStats> needs,
            BuildingId? recommendedUpgrade,
            BuildingId? rushTarget)
        {
            WaveIndex = waveIndex;
            Guests = guests;
            UnitsServed = unitsServed;
            Satisfaction = satisfaction;
            Rating = rating;
            Coins = coins;
            Energy = energy;
            ResortValue = resortValue;
            DurationSeconds = durationSeconds;
            Needs = needs;
            RecommendedUpgrade = recommendedUpgrade;
            RushTarget = rushTarget;
        }

        public int WaveIndex { get; }
        public int WaveNumber => WaveIndex + 1;

        /// <summary>Visual guest count ("Guests served 12/12"). Every guest is always served.</summary>
        public int Guests { get; }

        /// <summary>Need units served, including any Sudden rush. Coins are based on this.</summary>
        public int UnitsServed { get; }

        /// <summary>Wave Satisfaction 0–1. First shown to the player on S4, never during the wave.</summary>
        public double Satisfaction { get; }

        public StarRating Rating { get; }

        /// <summary>Rewards, paid out on Claim Rewards.</summary>
        public int Coins { get; }

        public int Energy { get; }
        public int ResortValue { get; }

        /// <summary>Arrival phase plus drain, in seconds of wave time.</summary>
        public double DurationSeconds { get; }

        /// <summary>Per-need breakdown for "What went well" / "Needs improvement".</summary>
        public IReadOnlyList<NeedWaveStats> Needs { get; }

        /// <summary>Building whose need spent longest in BOTTLENECK and can still be upgraded (AC6). Null when nothing needs it.</summary>
        public BuildingId? RecommendedUpgrade { get; }

        /// <summary>Building hit by the Sudden rush in this wave, if any.</summary>
        public BuildingId? RushTarget { get; }
    }

    public sealed class NeedWaveStats
    {
        public NeedWaveStats(
            NeedId need,
            BuildingId building,
            int units,
            double averageContribution,
            double averageWait,
            double maxWait,
            int unitsWithinGrace,
            double bottleneckSeconds)
        {
            Need = need;
            Building = building;
            Units = units;
            AverageContribution = averageContribution;
            AverageWait = averageWait;
            MaxWait = maxWait;
            UnitsWithinGrace = unitsWithinGrace;
            BottleneckSeconds = bottleneckSeconds;
        }

        public NeedId Need { get; }
        public BuildingId Building { get; }
        public int Units { get; }

        /// <summary>Average unit contribution 0–1 (unweighted).</summary>
        public double AverageContribution { get; }

        public double AverageWait { get; }
        public double MaxWait { get; }
        public int UnitsWithinGrace { get; }
        public double BottleneckSeconds { get; }
    }
}
