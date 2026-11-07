namespace ResortEmpire.Core
{
    /// <summary>
    /// Wave rating (Build doc §4.4). The worst possible result is <see cref="OneStar"/>, never zero.
    /// </summary>
    public enum StarRating
    {
        OneStar = 1,
        TwoStars = 2,
        ThreeStars = 3,
        FourStars = 4,
        FiveStars = 5,
        Perfect = 6,
    }

    public static class StarRatingExtensions
    {
        /// <summary>Index into per-rating config arrays (1★ = 0 … Perfect = 5).</summary>
        public static int ToIndex(this StarRating rating) => (int)rating - 1;

        /// <summary>Short label for logs and tables: "1★" … "5★", "Perfect".</summary>
        public static string ToLabel(this StarRating rating) =>
            rating == StarRating.Perfect ? "Perfect" : $"{(int)rating}★";
    }
}
