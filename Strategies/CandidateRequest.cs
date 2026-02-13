namespace MatchmakingService.Strategies;

/// <summary>
/// Request parameters for candidate retrieval.
/// Maps cleanly to query string parameters on the API.
/// </summary>
public record CandidateRequest(
    int Limit = 20,
    double MinScore = 0,
    int? ActiveWithinDays = null,
    bool OnlyVerified = false
);
