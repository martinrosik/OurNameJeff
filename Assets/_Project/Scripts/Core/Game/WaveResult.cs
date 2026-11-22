using System;

namespace ResortEmpire.Core
{
    /// <summary>
    /// OUR-35: what happened in a finished wave, computed in WAVE_RESOLVING. OUR-40 shows it on Wave Complete
    /// ("Guests served 12/12").
    /// Wave evaluation: the Wave Satisfaction % and its star rating, shown on Wave Complete. Rewards come later.
    /// </summary>
    public sealed class WaveResult
    {
        public WaveResult(
            int waveIndex, int guests, int unitsServed, int totalUnits, double duration, double satisfaction, WaveRating rating,
            int resortValueGain = 0, WaveBreakdown breakdown = null)
        {
            ResortValueGain = Math.Max(0, resortValueGain);
            Breakdown = breakdown ?? WaveBreakdown.Empty;
            WaveIndex = waveIndex;
            Guests = guests;
            UnitsServed = unitsServed;
            TotalUnits = totalUnits;
            Duration = duration;
            Satisfaction = satisfaction;
            Rating = rating;
        }

        public int WaveIndex { get; }
        public int WaveNumber => WaveIndex + 1;

        /// <summary>Guests of the wave (visual count). Everyone is always served (WR4).</summary>
        public int Guests { get; }

        /// <summary>Need units served; always equals <see cref="TotalUnits"/>, since nobody leaves (WR4).</summary>
        public int UnitsServed { get; }

        public int TotalUnits { get; }

        /// <summary>Wave time from WAVE_ACTIVE until the last unit was served, in seconds.</summary>
        public double Duration { get; }

        /// <summary>Wave Satisfaction in % (wave floor–100), from how long every unit waited (Build doc §4.4).</summary>
        public double Satisfaction { get; }

        /// <summary>Rating tier reached by <see cref="Satisfaction"/>: one star at the least, never zero.</summary>
        public WaveRating Rating { get; }

        /// <summary>Resort Value this wave added, from its rating. 0 or more: Resort Value never drops.</summary>
        public int ResortValueGain { get; }

        /// <summary>Wave Complete breakdown: which needs went great and what the others need (UI doc S4, AC6).</summary>
        public WaveBreakdown Breakdown { get; }
    }
}
