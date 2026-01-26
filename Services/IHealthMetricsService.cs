using MatchmakingService.DTOs;

namespace MatchmakingService.Services;

/// <summary>
/// Service for collecting and caching matchmaking health metrics
/// </summary>
public interface IHealthMetricsService
{
    /// <summary>
    /// Get current health metrics (cached for 60 seconds)
    /// </summary>
    Task<HealthMetricsResponse> GetHealthMetricsAsync();
}
