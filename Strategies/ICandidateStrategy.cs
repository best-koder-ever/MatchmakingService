namespace MatchmakingService.Strategies;

/// <summary>
/// Core strategy abstraction for candidate delivery.
/// All strategies take a user ID + request, run the filter pipeline,
/// apply their scoring approach, and return ranked candidates.
/// </summary>
public interface ICandidateStrategy
{
    /// <summary>Strategy name for observability and logging.</summary>
    string Name { get; }

    /// <summary>
    /// Get scored, filtered, ranked candidates for a user.
    /// </summary>
    Task<CandidateResult> GetCandidatesAsync(
        int userId, CandidateRequest request, CancellationToken ct = default);
}
