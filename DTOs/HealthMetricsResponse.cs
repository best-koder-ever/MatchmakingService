namespace MatchmakingService.DTOs;

/// <summary>
/// Health metrics for the matchmaking service
/// </summary>
public class HealthMetricsResponse
{
    /// <summary>
    /// Overall health status: "healthy", "degraded", or "unhealthy"
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Number of swipes processed in the last hour
    /// </summary>
    public int QueueSize { get; set; }

    /// <summary>
    /// Average milliseconds from swipe to match creation
    /// </summary>
    public double AverageProcessingTimeMs { get; set; }

    /// <summary>
    /// Percentage of failed match calculations (0-100)
    /// </summary>
    public double ErrorRate { get; set; }

    /// <summary>
    /// Daily limit usage metrics
    /// </summary>
    public DailyLimitMetrics DailyLimits { get; set; } = new();

    /// <summary>
    /// Percentage of requests served from cache
    /// </summary>
    public double CacheHitRate { get; set; }

    /// <summary>
    /// Timestamp when metrics were last collected
    /// </summary>
    public DateTime LastUpdated { get; set; }
}

/// <summary>
/// Metrics about daily swipe limit usage
/// </summary>
public class DailyLimitMetrics
{
    /// <summary>
    /// Number of users who have hit their daily swipe limit
    /// </summary>
    public int UsersAtLimit { get; set; }

    /// <summary>
    /// Percentage of active users who are at their limit
    /// </summary>
    public double PercentageExhausted { get; set; }

    /// <summary>
    /// Daily swipe limit for free users
    /// </summary>
    public int FreeUserLimit { get; set; } = 100;

    /// <summary>
    /// Daily swipe limit for premium users
    /// </summary>
    public int PremiumUserLimit { get; set; } = 500;
}
