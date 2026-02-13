namespace MatchmakingService.Strategies;

/// <summary>
/// Result of a candidate delivery operation.
/// QueueExhausted and SuggestionsRemaining map to existing Flutter DTOs.
/// StrategyUsed included for observability.
/// </summary>
public record CandidateResult(
    List<ScoredCandidate> Candidates,
    int TotalFiltered,
    int TotalScored,
    string StrategyUsed,
    TimeSpan ExecutionTime,
    bool QueueExhausted,
    int SuggestionsRemaining
);
