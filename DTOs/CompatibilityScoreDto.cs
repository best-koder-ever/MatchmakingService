namespace MatchmakingService.DTOs;

public record CompatibilityScoreDto
{
    public string UserId1 { get; init; } = string.Empty;
    public string UserId2 { get; init; } = string.Empty;
    public int OverallScore { get; init; }
    public int InterestsScore { get; init; }
    public int LocationScore { get; init; }
    public int PreferenceScore { get; init; }
    public DateTime CalculatedAt { get; init; } = DateTime.UtcNow;
}
