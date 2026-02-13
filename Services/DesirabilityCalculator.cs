using MatchmakingService.Data;
using MatchmakingService.Models;
using Microsoft.EntityFrameworkCore;

namespace MatchmakingService.Services;

/// <summary>
/// T183: ELO-inspired desirability scoring based on swipe patterns.
///
/// Formula:
///   Base = Bayesian-smoothed like rate: (likes + prior * mean) / (total + prior)
///     where prior=10 (pseudocounts), mean=0.3 (avg ~30% like rate)
///   ELO adjustment: likes from high-desirability users boost more
///   Decay: scores decay toward 50 if no new swipes (half-life: 30 days)
///
/// Usage:
///   - NEVER exposed to users (internal scoring signal only)
///   - Used as TIEBREAKER when compatibility scores are within 5 points
///   - 15% weight in final scoring formula (0.7*compat + 0.15*activity + 0.15*desirability)
///
/// Minimum data: 20+ swipes received. Below that → default 50.0
/// </summary>
public class DesirabilityCalculator
{
    private readonly ILogger<DesirabilityCalculator> _logger;

    // Bayesian smoothing parameters
    private const double BayesianPrior = 10.0;       // Pseudocounts
    private const double BayesianMean = 0.3;          // Average like rate (~30%)
    private const int MinSwipesRequired = 20;         // Minimum data threshold
    private const double DefaultScore = 50.0;         // Score for new/low-data users
    private const double DecayHalfLifeDays = 30.0;    // Score decay half-life
    private const double EloKFactor = 32.0;           // Standard chess K-factor

    public DesirabilityCalculator(ILogger<DesirabilityCalculator> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Recalculate desirability scores for a batch of users.
    /// Called by ScoreRefreshBackgroundService during scoring cycles.
    /// </summary>
    public async Task RecalculateForUsersAsync(
        MatchmakingDbContext dbContext,
        List<UserProfile> users,
        CancellationToken ct)
    {
        if (users.Count == 0) return;

        var userIds = users.Select(u => u.UserId).ToHashSet();

        // Get latest metrics for these users
        var metrics = await dbContext.MatchingAlgorithmMetrics
            .Where(m => userIds.Contains(m.UserId))
            .GroupBy(m => m.UserId)
            .Select(g => new
            {
                UserId = g.Key,
                // Use the most recent metric entry
                SwipesReceived = g.OrderByDescending(m => m.CalculatedAt).First().SwipesReceived,
                LikesReceived = g.OrderByDescending(m => m.CalculatedAt).First().LikesReceived,
                LastCalculatedAt = g.Max(m => m.CalculatedAt)
            })
            .ToListAsync(ct);

        var metricsMap = metrics.ToDictionary(m => m.UserId);

        int updated = 0;
        foreach (var user in users)
        {
            ct.ThrowIfCancellationRequested();

            double newScore;

            if (metricsMap.TryGetValue(user.UserId, out var metric)
                && metric.SwipesReceived >= MinSwipesRequired)
            {
                // Bayesian-smoothed like rate → scale to 0-100
                var bayesianRate = (metric.LikesReceived + BayesianPrior * BayesianMean)
                                  / (metric.SwipesReceived + BayesianPrior);
                var baseScore = bayesianRate * 100.0;

                // Apply time decay toward mean (50) if data is stale
                var daysSinceLastCalc = (DateTime.UtcNow - metric.LastCalculatedAt).TotalDays;
                var decayFactor = Math.Pow(0.5, daysSinceLastCalc / DecayHalfLifeDays);
                newScore = DefaultScore + (baseScore - DefaultScore) * decayFactor;

                // Clamp to [0, 100]
                newScore = Math.Clamp(newScore, 0.0, 100.0);
            }
            else
            {
                newScore = DefaultScore;
            }

            // Only update if score changed meaningfully (> 0.1 difference)
            if (Math.Abs(user.DesirabilityScore - newScore) > 0.1)
            {
                user.DesirabilityScore = newScore;
                updated++;
            }
        }

        if (updated > 0)
        {
            await dbContext.SaveChangesAsync(ct);
            _logger.LogDebug("Updated desirability scores for {Count}/{Total} users",
                updated, users.Count);
        }
    }

    /// <summary>
    /// Calculate ELO-style adjustment for a single interaction.
    /// Called when a swipe event is processed to provide real-time score nudges.
    ///
    /// When UserA (desirability=70) likes UserB (desirability=40):
    ///   UserB gets a big boost (liked by someone "above" them).
    /// When UserA passes on UserC (desirability=80):
    ///   UserC gets a small penalty.
    /// </summary>
    public static double CalculateEloAdjustment(
        double swiperDesirability, double targetDesirability, bool isLike)
    {
        // Expected outcome: probability that swiper would like target
        // Higher target desirability → higher expected like probability
        var expectedOutcome = 1.0 / (1.0 + Math.Pow(10, (swiperDesirability - targetDesirability) / 400.0));

        // Actual outcome: 1.0 for like, 0.0 for pass
        var actualOutcome = isLike ? 1.0 : 0.0;

        // ELO delta for the target
        var delta = EloKFactor * (actualOutcome - expectedOutcome);

        return delta;
    }

    /// <summary>
    /// Apply a single ELO adjustment to a user's desirability score.
    /// Used for real-time updates when swipe events arrive.
    /// </summary>
    public static double ApplyAdjustment(double currentScore, double adjustment)
    {
        return Math.Clamp(currentScore + adjustment, 0.0, 100.0);
    }
}
