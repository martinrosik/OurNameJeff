namespace ResortEmpire.Core
{
    /// <summary>
    /// Sudden rush (Build doc §4.6): a short surge of extra guests at the building that has no Worker at that moment.
    /// <see cref="Units"/> extra units of <see cref="Need"/> arrive within <see cref="Duration"/> seconds from
    /// <see cref="StartTime"/>, on top of the wave's demand (the need's total grows, e.g. Drinks 9/16 → 9/22). The
    /// forecast never shows it, and the wave doesn't pause. Moving a Worker there always solves it.
    /// </summary>
    public sealed class SuddenRush
    {
        public SuddenRush(NeedId need, BuildingId building, double startTime, int units, double duration)
        {
            Need = need;
            Building = building;
            StartTime = startTime;
            Units = units;
            Duration = duration;
        }

        /// <summary>The need that gets the extra guests.</summary>
        public NeedId Need { get; }

        /// <summary>The building that serves it (it had no Worker when the rush started).</summary>
        public BuildingId Building { get; }

        /// <summary>Wave time the rush starts.</summary>
        public double StartTime { get; }

        /// <summary>Extra units, added to the need's total.</summary>
        public int Units { get; }

        /// <summary>Seconds over which they arrive.</summary>
        public double Duration { get; }

        public double EndTime => StartTime + Duration;
    }
}
