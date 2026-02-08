namespace MatchmakingService.DTOs;

/// <summary>
/// Match statistics for a user
/// </summary>
public record MatchStatsDto(
    int TotalMatches,
    int PendingMatches,
    double MatchRate,
    string? MostActiveDay,
    DateTime? LastMatchAt
);
