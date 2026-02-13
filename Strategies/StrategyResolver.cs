using MatchmakingService.Data;
using MatchmakingService.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MatchmakingService.Strategies;

/// <summary>
/// Resolves which ICandidateStrategy to use based on config, per-request
/// override, or auto-detection of user count. Falls back to Live on error.
/// Hot-reloadable via IOptionsMonitor.
/// </summary>
public class StrategyResolver
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IOptionsMonitor<CandidateOptions> _options;
    private readonly MatchmakingDbContext _context;
    private readonly ILogger<StrategyResolver> _logger;

    public StrategyResolver(
        IServiceProvider serviceProvider,
        IOptionsMonitor<CandidateOptions> options,
        MatchmakingDbContext context,
        ILogger<StrategyResolver> logger)
    {
        _serviceProvider = serviceProvider;
        _options = options;
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Resolve the strategy to use. Supports per-request override for A/B testing.
    /// </summary>
    public ICandidateStrategy Resolve(string? strategyOverride = null)
    {
        var name = strategyOverride ?? _options.CurrentValue.Strategy;

        try
        {
            return name.ToLowerInvariant() switch
            {
                "live" => _serviceProvider.GetRequiredService<LiveScoringStrategy>(),
                "precomputed" => _serviceProvider.GetRequiredService<PreComputedStrategy>(),
                "auto" => ResolveAuto(),
                _ => FallbackToLive($"Unknown strategy '{name}'")
            };
        }
        catch (Exception ex)
        {
            return FallbackToLive($"Error resolving strategy '{name}': {ex.Message}");
        }
    }

    private ICandidateStrategy ResolveAuto()
    {
        var thresholds = _options.CurrentValue.AutoStrategyThresholds;

        try
        {
            // Quick user count (cached by EF)
            var userCount = _context.UserProfiles.Count(u => u.IsActive);

            _logger.LogDebug("Auto strategy: {UserCount} active users", userCount);

            if (userCount <= thresholds.LiveMaxUsers)
            {
                _logger.LogInformation("Auto → Live (active users {Count} ≤ {Threshold})",
                    userCount, thresholds.LiveMaxUsers);
                return _serviceProvider.GetRequiredService<LiveScoringStrategy>();
            }

            _logger.LogInformation("Auto → PreComputed (active users {Count} > {Threshold})",
                userCount, thresholds.LiveMaxUsers);
            return _serviceProvider.GetRequiredService<PreComputedStrategy>();
        }
        catch (Exception ex)
        {
            return FallbackToLive($"Auto resolution failed: {ex.Message}");
        }
    }

    private ICandidateStrategy FallbackToLive(string reason)
    {
        _logger.LogWarning("Falling back to LiveScoringStrategy: {Reason}", reason);
        return _serviceProvider.GetRequiredService<LiveScoringStrategy>();
    }
}
