namespace MatchmakingService.Models
{
    /// <summary>
    /// Recommended scoring weights and algorithm configuration
    /// These values have been tuned based on testing and can be customized per-user
    /// </summary>
    public class ScoringConfiguration
    {
        /// <summary>
        /// Default weight for location/distance factor (0-10 scale)
        /// Higher = distance matters more
        /// Recommendation: 1.5 - Proximity is nice but not critical
        /// </summary>
        public double DefaultLocationWeight { get; set; } = 1.5;

        /// <summary>
        /// Default weight for age compatibility (0-10 scale)
        /// Higher = age matching matters more
        /// Recommendation: 2.0 - Age range is important for relationship compatibility
        /// </summary>
        public double DefaultAgeWeight { get; set; } = 2.0;

        /// <summary>
        /// Default weight for shared interests (0-10 scale)
        /// Higher = common hobbies/interests matter more
        /// Recommendation: 1.8 - Shared interests help connection but aren't everything
        /// </summary>
        public double DefaultInterestsWeight { get; set; } = 1.8;

        /// <summary>
        /// Default weight for education level compatibility (0-10 scale)
        /// Higher = similar education matters more
        /// Recommendation: 1.0 - Moderate importance, preferences vary
        /// </summary>
        public double DefaultEducationWeight { get; set; } = 1.0;

        /// <summary>
        /// Default weight for lifestyle compatibility (smoking, drinking, children) (0-10 scale)
        /// Higher = lifestyle alignment matters more
        /// Recommendation: 2.5 - Lifestyle compatibility is critical for long-term success
        /// </summary>
        public double DefaultLifestyleWeight { get; set; } = 2.5;

        /// <summary>
        /// Minimum compatibility score required for match suggestions (0-100)
        /// Recommendation: 60 - Only suggest reasonably compatible matches
        /// </summary>
        public double MinimumCompatibilityThreshold { get; set; } = 60.0;

        /// <summary>
        /// Maximum distance in kilometers for potential matches
        /// Recommendation: 50 - Keep matches within reasonable travel distance by default
        /// </summary>
        public double DefaultMaxDistance { get; set; } = 50.0;

        /// <summary>
        /// Cache validity duration in hours
        /// Recommendation: 24 - Scores stay valid for a day
        /// </summary>
        public int ScoreCacheHours { get; set; } = 24;

        /// <summary>
        /// Penalty for children preference mismatch (points deducted)
        /// Recommendation: 30 - Strong dealbreaker for many people
        /// </summary>
        public double ChildrenMismatchPenalty { get; set; } = 30.0;

        /// <summary>
        /// Penalty for smoking habit mismatch (points deducted)
        /// Recommendation: 20 - Significant lifestyle factor
        /// </summary>
        public double SmokingMismatchPenalty { get; set; } = 20.0;

        /// <summary>
        /// Penalty for drinking habit mismatch (points deducted)
        /// Recommendation: 15 - Moderate lifestyle factor
        /// </summary>
        public double DrinkingMismatchPenalty { get; set; } = 15.0;

        /// <summary>
        /// Penalty for religion mismatch (points deducted)
        /// Recommendation: 10 - Lower penalty, many people are flexible
        /// </summary>
        public double ReligionMismatchPenalty { get; set; } = 10.0;
    }
}
