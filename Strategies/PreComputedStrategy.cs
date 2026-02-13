using System.Diagnostics;
using MatchmakingService.Data;
using MatchmakingService.Filters;
using MatchmakingService.Models;
using MatchmakingService.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MatchmakingService.Strategies;

/// <summary>
/// Reads from pre-computed MatchScores table instead of scoring on-the-fly.
/// Best for 10K-500K users. Falls back to LiveScoringStrategy when no
/// pre-computed scores exist (new user or expired scores).
/// </summary>
public class PreComputedStrategy : ICandidateStrategy
{
    public string Name => "PreComputed";

    private readonly MatchmakingDbContext _context;
    private readonly CandidateFilterPipeline _filterPipeline;
    private readonly LiveScoringStrategy _liveFallback;
    private readonly ISwipeServiceClient _swipeServiceClient;
    private readonly ISafetyServiceClient _safetyServiceClient;
    private readonly IOptionsMonitor<CandidateOptions> _options;
    private readonly IOptionsMonitor<ScoringConfiguration> _scoringConfig;
    private readonly ILogger<PreComputedStrategy> _logger;

    public PreComputedStrategy(
        MatchmakingDbContext context,
        CandidateFilterPipeline filterPipeline,
        LiveScoringStrategy liveFallback,
        ISwipeServiceClient swipeServiceClient,
        ISafetyServiceClient safetyServiceClient,
        IOptionsMonitor<CandidateOptions> options,
        IOptionsMonitor<ScoringConfiguration> scoringConfig,
        ILogger<PreComputedStrategy> logger)
    {
        _context = context;
        _filterPipeline = filterPipeline;
        _liveFallback = liveFallback;
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
        var scoringConfig = _scoringConfig.CurrentValue;
        var staleThresholdHours = scoringConfig.ScoreCacheHours > 0
            ? scoringConfig.ScoreCacheHours : 24;

        // 1. Load requesting user
        var user = await _context.UserProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserId == userId && u.IsActive, ct);

        if (user == null)
        {
            _logger.LogWarning("PreComputed: user {UserId} not found or inactive", userId);
            return new CandidateResult(
                new List<ScoredCandidate>(), 0, 0, Name, sw.Elapsed, true, 0);
        }

        // 2. Check for fresh pre-computed scores
        var staleThreshold = DateTime.UtcNow.AddHours(-staleThresholdHours);
        var preComputedScores = await _context.MatchScores
            .AsNoTracking()
            .Where(ms => ms.UserId == userId
                      && ms.IsValid
                      && ms.CalculatedAt > staleThreshold)
            .OrderByDescending(ms => ms.OverallScore)
            .Take(request.Limit * 3)
            .ToListAsync(ct);

        // 3. If no fresh scores, fall back to live scoring
        if (preComputedScores.Count == 0)
        {
            _logger.LogInformation(
                "PreComputed: no fresh scores for user {UserId}, falling back to LiveScoring",
                userId);
            return await _liveFallback.GetCandidatesAsync(userId, request, ct);
        }

        // 4. Get candidate profiles for pre-scored users
        var candidateIds = preComputedScores.Select(s => s.TargetUserId).ToList();
        var candidateProfiles = await _context.UserProfiles
            .AsNoTracking()
            .Where(u => candidateIds.Contains(u.UserId))
            .ToDictionaryAsync(u => u.UserId, ct);

        // 5. Build filter context and apply filters on the pre-scored candidates
        var swipedIds = await _swipeServiceClient.GetSwipedUserIdsAsync(userId);
        var blockedIds = await _safetyServiceClient.GetBlockedUserIdsAsync(userId);

        var filterContext = new FilterContext(
            RequestingUser: user,
            SwipedUserIds: swipedIds,
            BlockedUserIds: new HashSet<int>(blockedIds),
            Options: config);

        // Apply filters on the pre-scored candidate set using IQueryable
        var candidateQuery = _context.UserProfiles
            .AsNoTracking()
            .Where(u => candidateIds.Contains(u.UserId));

        var filterResult = await _filterPipeline.ExecuteAsync(
            candidateQuery, filterContext, request.Limit * 3, ct);

        var filteredIds = filterResult.Candidates.Select(c => c.UserId).ToHashSet();

        // 6. Map scores to ScoredCandidates, applying MinScore threshold
        var minScore = request.MinScore > 0 ? request.MinScore : scoringConfig.MinimumCompatibilityThreshold;
        var scored = new List<ScoredCandidate>();

        foreach (var score in preComputedScores)
        {
            if (!filteredIds.Contains(score.TargetUserId)) continue;
            if (score.OverallScore < minScore) continue;
            if (!candidateProfiles.TryGetValue(score.TargetUserId, out var profile)) continue;

            scored.Add(new ScoredCandidate(
                Profile: profile,
                CompatibilityScore: Math.Round(score.OverallScore, 1),
                ActivityScore: Math.Round(score.ActivityScore, 1),
                DesirabilityScore: Math.Round(profile.DesirabilityScore, 1),
                FinalScore: Math.Round(score.OverallScore, 1),
                StrategyUsed: Name));
        }

        var limitedCandidates = scored.Take(request.Limit).ToList();

        // 7. If pre-computed produced fewer than requested, supplement with live
        if (limitedCandidates.Count < request.Limit)
        {
            _logger.LogInformation(
                "PreComputed: only {Count}/{Limit} from cache, supplementing with live for user {UserId}",
                limitedCandidates.Count, request.Limit, userId);

            var supplementRequest = request with { Limit = request.Limit - limitedCandidates.Count };
            var supplement = await _liveFallback.GetCandidatesAsync(userId, supplementRequest, ct);

            // Avoid duplicates
            var existingIds = limitedCandidates.Select(c => c.Profile.UserId).ToHashSet();
            var uniqueSupplement = supplement.Candidates
                .Where(c => !existingIds.Contains(c.Profile.UserId))
                .Take(supplementRequest.Limit)
                .ToList();

            limitedCandidates.AddRange(uniqueSupplement);
        }

        sw.Stop();
        _logger.LogInformation(
            "PreComputed for user {UserId}: {PreComputed} pre-scored → {Final} returned in {Ms}ms",
            userId, preComputedScores.Count, limitedCandidates.Count, sw.ElapsedMilliseconds);

        return new CandidateResult(
            Candidates: limitedCandidates,
            TotalFiltered: filterResult.Candidates.Count,
            TotalScored: scored.Count,
            StrategyUsed: Name,
            ExecutionTime: sw.Elapsed,
            QueueExhausted: limitedCandidates.Count < request.Limit,
            SuggestionsRemaining: Math.Max(0, scored.Count - limitedCandidates.Count));
    }
}
