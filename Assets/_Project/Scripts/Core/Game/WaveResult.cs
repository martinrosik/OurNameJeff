namespace ResortEmpire.Core
{
    /// <summary>
    /// OUR-35: what happened in a finished wave, computed in WAVE_RESOLVING. OUR-40 shows it on Wave Complete
    /// ("Guests served 12/12"). Satisfaction, stars and rewards are added in a later sprint.
    /// </summary>
    public sealed class WaveResult
    {
        public WaveResult(int waveIndex, int guests, int unitsServed, int totalUnits, double duration)
        {
            WaveIndex = waveIndex;
            Guests = guests;
            UnitsServed = unitsServed;
            TotalUnits = totalUnits;
            Duration = duration;
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
    }
}
