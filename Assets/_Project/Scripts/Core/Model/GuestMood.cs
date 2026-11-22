namespace ResortEmpire.Core
{
    /// <summary>
    /// How a guest felt about one visit, shown as a face in a bubble above it when it walks out of the building
    /// (<see cref="WaveSatisfaction.GetGuestMood"/>). Never a number or a countdown: the Build doc (§4.4) keeps the
    /// Wave Satisfaction score for Wave Complete.
    /// </summary>
    public enum GuestMood
    {
        /// <summary>Served with at most patienceGrace of delay: counts 100 %.</summary>
        Happy,

        /// <summary>Delayed longer than patienceGrace but less than patienceFloorAfter: counts less.</summary>
        Neutral,

        /// <summary>Delayed patienceFloorAfter or longer: counts only unitFloor.</summary>
        Unhappy,
    }
}
