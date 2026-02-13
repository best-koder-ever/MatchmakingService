using System.Diagnostics;
using MatchmakingService.Data;
using MatchmakingService.Filters;
using MatchmakingService.Models;
using MatchmakingService.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MatchmakingService.Strategies;

/// <summary>
/// Real-time scoring strategy. Runs filter pipeline at DB level, then scores
/// each candidate on-the-fly. Best for &lt;10K users where per-request
/// scoring completes in &lt;200ms.
/// </summary>
public class LiveScoringStrategy : ICandidateStrategy
{
    public string Name => "Live";

    private readonly MatchmakingDbContext _context;
    private readonly CandidateFilterPipeline _filterPipeline;
    private readonly IAdvancedMatchingService _matchingService;
    private readonly ISwipeServiceClient _swipeServiceClient;
    private readonly ISafetyServiceClient _safetyServiceClient;
    private readonly IOptionsMonitor<CandidateOptions> _options;
    private readonly IOptionsMonitor<ScoringConfiguration> _scoringConfig;
    private readonly ILogger<LiveScoringStrategy> _logger;

    public LiveScoringStrategy(
        MatchmakingDbContext context,
        CandidateFilterPipeline filterPipeline,
        IAdvancedMatchingService matchingService,
        ISwipeServiceClient swipeServiceClient,
        ISafetyServiceClient safetyServiceClient,
        IOptionsMonitor<CandidateOptions> options,
        IOptionsMonitor<ScoringConfiguration> scoringConfig,
        ILogger<LiveScoringStrategy> logger)
    {
        _context = context;
        _filterPipeline = filterPipeline;
        _matchingService = matchingService;
        _swipeServiceClient = swipeServiceClient;
        _safetyServiceClient = safetyServiceClient;
        _options = options;
        _scoringConfig = scoringConfig;
        _logger = logger;
    }

    public async Task<CandidateResult> GetCandidatesAsync(
        int userId, CandidateRequest request, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var config = _options.CurrentValue;

        // 1. Load requesting user
        var user = await _context.UserProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserId == userId && u.IsActive, ct);

        if (user == null)
        {
            _logger.LogWarning("LiveScoring: user {UserId} not found or inactive", userId);
            return new CandidateResult(
                new List<ScoredCandidate>(), 0, 0, Name, sw.Elapsed, true, 0);
        }

        // 2. Build filter context
        var swipedIds = await _swipeServiceClient.GetSwipedUserIdsAsync(userId);
        var blockedIds = await _safetyServiceClient.GetBlockedUserIdsAsync(userId);

        var filterContext = new FilterContext(
            RequestingUser: user,
            SwipedUserIds: swipedIds,
            BlockedUserIds: new HashSet<int>(blockedIds),
            Options: config);

        // 3. Run filter pipeline (DB-level, paginated)
        // Request more than needed so we can apply MinScore threshold after scoring
        var filterLimit = Math.Min(request.Limit * 3, config.MaxLimit * 3);
        var baseQuery = _context.UserProfiles.AsNoTracking();

        var filterResult = await _filterPipeline.ExecuteAsync(
            baseQuery, filterContext, filterLimit, ct);

        var totalFiltered = filterResult.Candidates.Count;

        // 4. Score each candidate
        var scoringConfig = _scoringConfig.CurrentValue;
        var minScore = request.MinScore > 0 ? request.MinScore : scoringConfig.MinimumCompatibilityThreshold;
        var scored = new List<ScoredCandidate>();

        foreach (var candidate in filterResult.Candidates)
        {
            if (scored.Count >= request.Limit) break;

            var compatScore = await _matchingService.CalculateCompatibilityScoreAsync(
                userId, candidate.UserId);

            if (compatScore < minScore) continue;

            // Activity score: use LastActiveAt with exponential decay
            var activityScore = CalculateActivityFromLastActive(candidate.LastActiveAt, scoringConfig);
            var desirabilityScore = candidate.DesirabilityScore;

            // Final score = weighted blend of compatibility + activity + desirability
            var finalScore = (compatScore * 0.7) + (activityScore * 0.15) + (desirabilityScore * 0.15);

            scored.Add(new ScoredCandidate(
                Profile: candidate,
                CompatibilityScore: Math.Round(compatScore, 1),
                ActivityScore: Math.Round(activityScore, 1),
                DesirabilityScore: Math.Round(desirabilityScore, 1),
                FinalScore: Math.Round(finalScore, 1),
                StrategyUsed: Name));
        }

        // 5. Sort by FinalScore descending
        scored = scored.OrderByDescending(s => s.FinalScore).ToList();
        var limitedCandidates = scored.Take(request.Limit).ToList();

        sw.Stop();
        _logger.LogInformation(
            "LiveScoring for user {UserId}: {Filtered} filtered → {Scored} scored → {Returned} returned in {Ms}ms",
            userId, totalFiltered, scored.Count, limitedCandidates.Count, sw.ElapsedMilliseconds);

        return new CandidateResult(
            Candidates: limitedCandidates,
            TotalFiltered: totalFiltered,
            TotalScored: scored.Count,
            StrategyUsed: Name,
            ExecutionTime: sw.Elapsed,
            QueueExhausted: limitedCandidates.Count < request.Limit,
            SuggestionsRemaining: Math.Max(0, scored.Count - limitedCandidates.Count));
    }

    private static double CalculateActivityFromLastActive(DateTime lastActive, ScoringConfiguration config)
    {
        var daysSinceActive = (DateTime.UtcNow - lastActive).TotalDays;
        if (daysSinceActive <= 0) return 100.0;

        var halfLife = config.ActivityScoreHalfLifeDays;
        var decayRate = Math.Log(2) / halfLife;
        return Math.Max(0, 100.0 * Math.Exp(-decayRate * daysSinceActive));
    }
}
