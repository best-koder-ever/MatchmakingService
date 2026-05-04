namespace MatchmakingService.Services
{
    /// <summary>
    /// Computes a compatibility score for a pair of users based on their questionnaire answers.
    /// </summary>
    public interface ICompatibilityScorer
    {
        /// <summary>
        /// Returns a compatibility score in the [0, 1] range for the given user pair.
        /// Returns 0.5 (neutral) when either user has no questionnaire answers.
        /// </summary>
        Task<double> GetScoreAsync(int userId1, int userId2);
    }
}
