namespace ResortEmpire.Core
{
    /// <summary>
    /// Rating tier of a finished wave (Build doc §4.4, UI doc S4), from its Wave Satisfaction and
    /// <see cref="SatisfactionSettings.ratingThresholds"/>. The worst result is one star, never zero.
    /// </summary>
    public enum WaveRating
    {
        OneStar = 1,
        TwoStars = 2,
        ThreeStars = 3,
        FourStars = 4,
        FiveStars = 5,
        Perfect = 6,
    }
}
