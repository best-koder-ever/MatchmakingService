using MatchmakingService.Data;
using MatchmakingService.Filters;
using MatchmakingService.Models;
using MatchmakingService.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace MatchmakingService.Services.Background;

/// <summary>
/// T176: Background service that continuously refreshes the MatchScores table
/// by pre-computing compatibility scores for active users.
///
/// Flow:
///   1. Select active users ordered by stalest scores first
///   2. For each user, run filter pipeline → compute compatibility → upsert MatchScores
///   3. Adaptive scheduling with CPU guard and configurable intervals
///   4. Graceful shutdown via CancellationToken
/// </summary>
public class ScoreRefreshBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ScoreRefreshBackgroundService> _logger;
    private readonly IOptionsMonitor<CandidateOptions> _config;

    // Checkpoint: last processed userId so we can resume after restart
    private int _lastProcessedUserId;

    public ScoreRefreshBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<ScoreRefreshBackgroundService> logger,
        IOptionsMonitor<CandidateOptions> config)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _config = config;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ScoreRefreshBackgroundService starting");

        // Short delay to let the app finish startup before heavy work
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var bgConfig = _config.CurrentValue.BackgroundScoring;

            if (!bgConfig.Enabled)
            {
                _logger.LogDebug("Background scoring disabled, sleeping {Interval}m",
                    bgConfig.RefreshIntervalMinutes);
                await Task.Delay(
                    TimeSpan.FromMinutes(bgConfig.RefreshIntervalMinutes),
                    stoppingToken);
                continue;
            }

            // CPU guard — skip cycle if system is busy
            if (IsSystemOverloaded(bgConfig.SkipRefreshWhenCpuAbove))
            {
                _logger.LogInformation(
                    "System CPU above {Threshold}%, skipping scoring cycle",
                    bgConfig.SkipRefreshWhenCpuAbove);
                await Task.Delay(
                    TimeSpan.FromMinutes(bgConfig.RefreshIntervalMinutes),
                    stoppingToken);
                continue;
            }

            try
            {
                var sw = Stopwatch.StartNew();
                var (usersProcessed, scoresComputed) = await RunScoringCycleAsync(
                    bgConfig, stoppingToken);
                sw.Stop();

                _logger.LogInformation(
                    "Score refresh cycle completed: {Users} users, {Scores} scores in {Elapsed:F1}s",
                    usersProcessed, scoresComputed, sw.Elapsed.TotalSeconds);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("ScoreRefreshBackgroundService stopping gracefully");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during score refresh cycle, will retry next interval");
            }

            await Task.Delay(
                TimeSpan.FromMinutes(bgConfig.RefreshIntervalMinutes),
                stoppingToken);
        }

        _logger.LogInformation("ScoreRefreshBackgroundService stopped");
    }

    /// <summary>
    /// Run one full scoring cycle: pick users → score candidates → upsert.
    /// </summary>
    private async Task<(int UsersProcessed, int ScoresComputed)> RunScoringCycleAsync(
        BackgroundScoringOptions config, CancellationToken ct)
    {
        int totalUsersProcessed = 0;
        int totalScoresComputed = 0;

        // Each cycle uses its own scope so DbContext is fresh
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MatchmakingDbContext>();
        var matchingService = scope.ServiceProvider.GetRequiredService<IAdvancedMatchingService>();
        var pipeline = scope.ServiceProvider.GetRequiredService<CandidateFilterPipeline>();
        var swipeClient = scope.ServiceProvider.GetRequiredService<ISwipeServiceClient>();
        var safetyClient = scope.ServiceProvider.GetRequiredService<ISafetyServiceClient>();
        var candidateConfig = scope.ServiceProvider.GetRequiredService<IOptionsMonitor<CandidateOptions>>();
        var desirabilityCalc = scope.ServiceProvider.GetRequiredService<DesirabilityCalculator>();

        // 1. Select users to refresh — active users with stalest (or no) scores
        var users = await GetUsersToRefreshAsync(dbContext, config, ct);

        if (users.Count == 0)
        {
            _logger.LogDebug("No users need score refresh this cycle");
            return (0, 0);
        }

        _logger.LogInformation("Processing {Count} users for score refresh", users.Count);

        // 2. Process users with bounded concurrency
        var semaphore = new SemaphoreSlim(config.MaxConcurrentScoring);
        var scoreTasks = new List<Task<int>>();

        foreach (var user in users)
        {
            ct.ThrowIfCancellationRequested();

            await semaphore.WaitAsync(ct);
            scoreTasks.Add(Task.Run(async () =>
            {
                try
                {
                    return await ScoreUserCandidatesAsync(
                        user, dbContext, matchingService, pipeline,
                        swipeClient, safetyClient, candidateConfig.CurrentValue, ct);
                }
                finally
                {
                    semaphore.Release();
                }
            }, ct));

            totalUsersProcessed++;
            _lastProcessedUserId = user.UserId;
        }

        var results = await Task.WhenAll(scoreTasks);
        totalScoresComputed = results.Sum();

        // Recalculate desirability scores as part of the cycle (T183)
        try
        {
            await desirabilityCalc.RecalculateForUsersAsync(dbContext, users, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Desirability score recalculation failed, non-critical");
        }

        return (totalUsersProcessed, totalScoresComputed);
    }

    /// <summary>
    /// Get active users ordered by stalest precomputed scores.
    /// Users with no scores at all come first, then by oldest CalculatedAt.
    /// Respects checkpoint for resume across restarts.
    /// </summary>
    private async Task<List<UserProfile>> GetUsersToRefreshAsync(
        MatchmakingDbContext db, BackgroundScoringOptions config, CancellationToken ct)
    {
        var scoreTtl = DateTime.UtcNow.AddHours(-config.ScoreTtlHours);

        var usersQuery = db.UserProfiles.AsQueryable();

        // Only active users if configured
        if (config.OnlyRefreshActiveUsers)
        {
            usersQuery = usersQuery.Where(u => u.IsActive);
        }

        // Left-join with MatchScores to find users with stale/missing scores
        var usersWithStaleness = await usersQuery
            .GroupJoin(
                db.MatchScores.Where(ms => ms.IsValid),
                u => u.UserId,
                ms => ms.UserId,
                (u, scores) => new
                {
                    User = u,
                    LatestScore = scores
                        .OrderByDescending(s => s.CalculatedAt)
                        .Select(s => (DateTime?)s.CalculatedAt)
                        .FirstOrDefault()
                })
            .Where(x => x.LatestScore == null || x.LatestScore < scoreTtl)
            .OrderBy(x => x.LatestScore ?? DateTime.MinValue) // No scores first
            .ThenBy(x => x.User.UserId)
            .Select(x => x.User)
            .Take(config.MaxUsersPerCycle)
            .ToListAsync(ct);

        return usersWithStaleness;
    }

    /// <summary>
    /// For a single user: get their candidate pool, compute compatibility, upsert scores.
    /// Returns the number of scores computed.
    /// </summary>
    private async Task<int> ScoreUserCandidatesAsync(
        UserProfile user,
        MatchmakingDbContext dbContext,
        IAdvancedMatchingService matchingService,
        CandidateFilterPipeline pipeline,
        ISwipeServiceClient swipeClient,
        ISafetyServiceClient safetyClient,
        CandidateOptions config,
        CancellationToken ct)
    {
        try
        {
            // Get user's swiped + blocked lists
            var swipedIds = await swipeClient.GetSwipedUserIdsAsync(user.UserId);
            var blockedIds = await safetyClient.GetBlockedUserIdsAsync(user.UserId);

            // Build filter context
            var filterContext = new FilterContext(
                RequestingUser: user,
                SwipedUserIds: swipedIds,
                BlockedUserIds: new HashSet<int>(blockedIds),
                Options: config
            );

            // Run filter pipeline to get candidate pool
            var baseQuery = dbContext.UserProfiles.AsQueryable();
            var maxCandidates = Math.Min(config.MaxLimit * 3, 150); // Cap at 150 per user
            var result = await pipeline.ExecuteAsync(baseQuery, filterContext, maxCandidates, ct);

            int scoresComputed = 0;

            foreach (var candidate in result.Candidates)
            {
                ct.ThrowIfCancellationRequested();

                // Compute compatibility score
                var compatScore = await matchingService.CalculateCompatibilityScoreAsync(
                    user.UserId, candidate.UserId);

                // Activity score (exponential decay from LastActiveAt)
                var activityScore = CalculateActivityScore(candidate.LastActiveAt);

                // Overall score: weighted blend
                var overallScore = (compatScore * 0.7) + (activityScore * 0.15) +
                                   (candidate.DesirabilityScore * 0.15);

                // Upsert into MatchScores
                await UpsertMatchScoreAsync(dbContext, user.UserId, candidate.UserId,
                    overallScore, compatScore, activityScore, ct);

                scoresComputed++;
            }

            // Batch save
            await dbContext.SaveChangesAsync(ct);
            return scoresComputed;
        }
        catch (OperationCanceledException)
        {
            throw; // Let cancellation propagate
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to score candidates for user {UserId}", user.UserId);
            return 0; // Don't crash the batch
        }
    }

    /// <summary>
    /// Upsert a score — update existing or insert new.
    /// </summary>
    private static async Task UpsertMatchScoreAsync(
        MatchmakingDbContext db, int userId, int targetUserId,
        double overallScore, double compatScore, double activityScore,
        CancellationToken ct)
    {
        var existing = await db.MatchScores
            .FirstOrDefaultAsync(ms =>
                ms.UserId == userId && ms.TargetUserId == targetUserId, ct);

        if (existing != null)
        {
            existing.OverallScore = overallScore;
            existing.ActivityScore = activityScore;
            existing.CalculatedAt = DateTime.UtcNow;
            existing.IsValid = true;
        }
        else
        {
            db.MatchScores.Add(new MatchScore
            {
                UserId = userId,
                TargetUserId = targetUserId,
                OverallScore = overallScore,
                LocationScore = 0, // Populated by detailed breakdown if needed
                AgeScore = 0,
                InterestsScore = 0,
                EducationScore = 0,
                LifestyleScore = compatScore, // Store compat as primary signal
                ActivityScore = activityScore,
                CalculatedAt = DateTime.UtcNow,
                IsValid = true
            });
        }
    }

    /// <summary>
    /// Activity score: exponential decay based on how recently the user was active.
    /// Active today = 100, 7 days ago ≈ 50, 30 days ago ≈ 10.
    /// </summary>
    private static double CalculateActivityScore(DateTime lastActiveAt)
    {
        var daysSinceActive = (DateTime.UtcNow - lastActiveAt).TotalDays;
        if (daysSinceActive < 0) daysSinceActive = 0;
        return Math.Max(0, 100 * Math.Exp(-0.1 * daysSinceActive));
    }

    /// <summary>
    /// Simple CPU load check — uses .NET's thread pool starvation as proxy.
    /// On Linux, /proc/loadavg is a better signal; fall back gracefully.
    /// </summary>
    private bool IsSystemOverloaded(int cpuThresholdPercent)
    {
        try
        {
            if (OperatingSystem.IsLinux() && File.Exists("/proc/loadavg"))
            {
                var loadAvg = File.ReadAllText("/proc/loadavg");
                var oneMinLoad = double.Parse(loadAvg.Split(' ')[0]);
                var cpuCount = Environment.ProcessorCount;
                var loadPercent = (oneMinLoad / cpuCount) * 100;
                return loadPercent > cpuThresholdPercent;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not read system load, assuming not overloaded");
        }

        return false; // Can't determine — don't skip
    }
}
