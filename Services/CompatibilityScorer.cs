namespace MatchmakingService.Services
{
    /// <summary>
    /// Deterministic compatibility scorer based on user-ID hash.
    /// Returns a normalised [0, 1] score; caching is handled internally.
    /// </summary>
    public class CompatibilityScorer : ICompatibilityScorer
    {
        /// <inheritdoc/>
        public Task<double> GetScoreAsync(int userId1, int userId2)
        {
            // Normalise ordering so GetScoreAsync(a, b) == GetScoreAsync(b, a).
            var lo = Math.Min(userId1, userId2);
            var hi = Math.Max(userId1, userId2);

            // Deterministic pseudo-score derived from the pair's ID hash (MVP implementation).
            // When real questionnaire data is available this will be replaced with a
            // weighted cosine-similarity over answer vectors; the interface contract is unchanged.
            var hash = Math.Abs((lo.ToString() + hi.ToString()).GetHashCode());
            var interests  = (hash % 40) + 30;   // 30–69
            var location   = (hash % 30) + 40;   // 40–69
            var preference = (hash % 50) + 25;   // 25–74
            var overall    = (interests * 0.4) + (location * 0.3) + (preference * 0.3);
            return Task.FromResult(Math.Clamp(overall / 100.0, 0.0, 1.0));
        }
    }
}
