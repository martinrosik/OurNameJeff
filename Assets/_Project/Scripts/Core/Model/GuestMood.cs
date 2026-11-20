namespace ResortEmpire.Core
{
    /// <summary>
    /// How a guest felt about one visit, shown as a face in a bubble above it when it walks out of the building
    /// (<see cref="WaveSatisfaction.GetGuestMood"/>). Never a number or a countdown: the Build doc (§4.4) keeps the
    /// Wave Satisfaction score for Wave Complete.
    /// </summary>
    public enum GuestMood
    {
        /// <summary>Found a seat within patienceGrace: counts 100 %.</summary>
        Happy,

        /// <summary>Waited longer than patienceGrace but less than patienceFloorAfter: counts less.</summary>
        Neutral,

        /// <summary>Waited patienceFloorAfter or longer: counts only unitFloor.</summary>
        Unhappy,
    }
}
