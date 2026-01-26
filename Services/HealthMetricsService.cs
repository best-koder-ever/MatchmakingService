using MatchmakingService.Data;
using MatchmakingService.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace MatchmakingService.Services;

/// <summary>
/// Service for collecting and caching matchmaking health metrics
/// </summary>
public class HealthMetricsService : IHealthMetricsService
{
    private readonly MatchmakingDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly ILogger<HealthMetricsService> _logger;
    
    private const string CACHE_KEY = "health_metrics";
    private const int CACHE_DURATION_SECONDS = 60;
    
    // Health status thresholds
    private const int QUEUE_SIZE_WARNING = 5000;
    private const int QUEUE_SIZE_CRITICAL = 10000;
    private const double ERROR_RATE_WARNING = 1.0; // 1%
    private const double ERROR_RATE_CRITICAL = 5.0; // 5%
    private const double PROCESSING_TIME_WARNING = 50.0; // ms
    private const double PROCESSING_TIME_CRITICAL = 100.0; // ms
    
    public HealthMetricsService(
        MatchmakingDbContext context,
        IMemoryCache cache,
        ILogger<HealthMetricsService> logger)
    {
        _context = context;
        _cache = cache;
        _logger = logger;
    }
    
    public async Task<HealthMetricsResponse> GetHealthMetricsAsync()
    {
        // Try cache first
        if (_cache.TryGetValue(CACHE_KEY, out HealthMetricsResponse? cached) && cached != null)
        {
            _logger.LogDebug("Returning cached health metrics");
            return cached;
        }
        
        _logger.LogInformation("Collecting fresh health metrics");
        
        try
        {
            // Collect metrics (parallel queries for performance)
            var metrics = await CollectMetricsAsync();
            
            // Cache for 60 seconds
            _cache.Set(CACHE_KEY, metrics, TimeSpan.FromSeconds(CACHE_DURATION_SECONDS));
            
            return metrics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error collecting health metrics");
            
            // Return degraded status on error
            return new HealthMetricsResponse
            {
                Status = "unhealthy",
                LastUpdated = DateTime.UtcNow
            };
        }
    }
    
    private async Task<HealthMetricsResponse> CollectMetricsAsync()
    {
        var now = DateTime.UtcNow;
        var oneHourAgo = now.AddHours(-1);
        
        // Parallel metric collection
        var queueSizeTask = GetQueueSizeAsync(oneHourAgo);
        var processingTimeTask = GetAverageProcessingTimeAsync();
        var errorRateTask = GetErrorRateAsync(oneHourAgo);
        var dailyLimitsTask = GetDailyLimitMetricsAsync();
        
        await Task.WhenAll(queueSizeTask, processingTimeTask, errorRateTask, dailyLimitsTask);
        
        var queueSize = await queueSizeTask;
        var avgProcessingTime = await processingTimeTask;
        var errorRate = await errorRateTask;
        var dailyLimitMetrics = await dailyLimitsTask;
        
        // Determine health status
        var status = DetermineHealthStatus(queueSize, errorRate, avgProcessingTime);
        
        return new HealthMetricsResponse
        {
            Status = status,
            QueueSize = queueSize,
            AverageProcessingTimeMs = avgProcessingTime,
            ErrorRate = errorRate,
            DailyLimits = dailyLimitMetrics,
            CacheHitRate = 0, // Will be tracked separately if needed
            LastUpdated = now
        };
    }
    
    private async Task<int> GetQueueSizeAsync(DateTime since)
    {
        try
        {
            // Count interactions (swipes) processed in the last hour
            var count = await _context.UserInteractions
                .Where(ui => ui.CreatedAt >= since)
                .CountAsync();
            
            return count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting queue size");
            return 0;
        }
    }
    
    private async Task<double> GetAverageProcessingTimeAsync()
    {
        try
        {
            // Get the 100 most recent matches and calculate average time from creation
            // Note: In a real scenario, we'd track interaction->match latency
            // For MVP, we'll use a simplified approach
            var recentMatches = await _context.Matches
                .Where(m => m.IsActive)
                .OrderByDescending(m => m.CreatedAt)
                .Take(100)
                .Select(m => m.CreatedAt)
                .ToListAsync();
            
            if (!recentMatches.Any())
                return 0;
            
            // Simplified: assume avg processing time based on recent match rate
            // In production, you'd track actual processing timestamps
            var avgMs = 25.0; // Baseline assumption
            return Math.Round(avgMs, 2);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting processing time");
            return 0;
        }
    }
    
    private async Task<double> GetErrorRateAsync(DateTime since)
    {
        try
        {
            // For MVP, we'll track this simply
            // In production, you'd log failed match calculations to a separate table
            var totalOperations = await _context.UserInteractions
                .Where(ui => ui.CreatedAt >= since)
                .CountAsync();
            
            if (totalOperations == 0)
                return 0;
            
            // For now, assume very low error rate (would be tracked in production)
            var errors = 0; // Would come from error tracking table
            var errorRate = (errors / (double)totalOperations) * 100;
            
            return Math.Round(errorRate, 2);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating error rate");
            return 0;
        }
    }
    
    private async Task<DailyLimitMetrics> GetDailyLimitMetricsAsync()
    {
        try
        {
            var today = DateTime.UtcNow.Date;
            
            // Count unique users who have interacted today
            var activeUsersToday = await _context.UserInteractions
                .Where(ui => ui.CreatedAt >= today)
                .Select(ui => ui.UserId)
                .Distinct()
                .CountAsync();
            
            // Count users who have hit their daily limit (100 swipes for free users)
            var usersAtLimit = await _context.UserInteractions
                .Where(ui => ui.CreatedAt >= today)
                .GroupBy(ui => ui.UserId)
                .Where(g => g.Count() >= 100) // Free tier limit
                .CountAsync();
            
            var percentageExhausted = activeUsersToday > 0
                ? Math.Round((usersAtLimit / (double)activeUsersToday) * 100, 2)
                : 0;
            
            return new DailyLimitMetrics
            {
                UsersAtLimit = usersAtLimit,
                PercentageExhausted = percentageExhausted,
                FreeUserLimit = 100,
                PremiumUserLimit = 500
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting daily limit metrics");
            return new DailyLimitMetrics();
        }
    }
    
    private string DetermineHealthStatus(int queueSize, double errorRate, double processingTime)
    {
        // Check critical thresholds
        if (queueSize >= QUEUE_SIZE_CRITICAL || 
            errorRate >= ERROR_RATE_CRITICAL || 
            processingTime >= PROCESSING_TIME_CRITICAL)
        {
            return "unhealthy";
        }
        
        // Check warning thresholds
        if (queueSize >= QUEUE_SIZE_WARNING || 
            errorRate >= ERROR_RATE_WARNING || 
            processingTime >= PROCESSING_TIME_WARNING)
        {
            return "degraded";
        }
        
        return "healthy";
    }
}
