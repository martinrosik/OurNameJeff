namespace ResortEmpire.Core
{
    /// <summary>
    /// A mini-event during a wave: the least busy building gets a rush of its own guests. The next
    /// <see cref="Units"/> guests that were going to come to it later arrive within <see cref="Duration"/> seconds
    /// from <see cref="StartTime"/> instead. Nobody new comes: the wave's demand stays the same.
    /// </summary>
    public sealed class MiniEvent
    {
        public MiniEvent(NeedId need, double startTime, int units, double duration)
        {
            Need = need;
            StartTime = startTime;
            Units = units;
            Duration = duration;
        }

        /// <summary>The need (and so the building) that gets the rush.</summary>
        public NeedId Need { get; }

        /// <summary>Wave time the rush starts.</summary>
        public double StartTime { get; }

        /// <summary>Guests that come early (their own units, moved forward).</summary>
        public int Units { get; }

        /// <summary>Seconds over which they arrive.</summary>
        public double Duration { get; }

        public double EndTime => StartTime + Duration;
    }
}
