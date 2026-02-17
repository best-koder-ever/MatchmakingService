using MatchmakingService.Data;
using MatchmakingService.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace MatchmakingService.Strategies;

/// <summary>
/// T174: DailyPickStrategy — serves pre-generated daily curated picks.
/// Falls back to LiveScoringStrategy if no picks are available.
///
/// Flow:
///   1. Query DailyPicks for today (not expired, not acted upon)
///   2. Return ranked picks as ScoredCandidates
///   3. If no picks exist, fall back to Live scoring
/// </summary>
public class DailyPickStrategy : ICandidateStrategy
{
    private readonly MatchmakingDbContext _context;
    private readonly LiveScoringStrategy _liveFallback;
    private readonly IOptionsMonitor<CandidateOptions> _config;
    private readonly ILogger<DailyPickStrategy> _logger;

    public string Name => "DailyPick";

    public DailyPickStrategy(
        MatchmakingDbContext context,
        LiveScoringStrategy liveFallback,
        IOptionsMonitor<CandidateOptions> config,
        ILogger<DailyPickStrategy> logger)
    {
        _context = context;
        _liveFallback = liveFallback;
        _config = config;
        _logger = logger;
    }

    public async Task<CandidateResult> GetCandidatesAsync(
        int userId, CandidateRequest request, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var now = DateTime.UtcNow;

        // 1. Get today's unexpired, un-acted picks for this user
        var picks = await _context.DailyPicks
            .Where(dp => dp.UserId == userId
                      && dp.ExpiresAt > now
                      && !dp.Acted)
            .OrderBy(dp => dp.Rank)
            .Take(request.Limit)
            .Include(dp => dp.Candidate)
            .ToListAsync(ct);

        if (picks.Count == 0)
        {
            _logger.LogInformation(
                "No daily picks for user {UserId}, falling back to Live", userId);
            return await _liveFallback.GetCandidatesAsync(userId, request, ct);
        }

        // 2. Mark as seen
        foreach (var pick in picks)
        {
            pick.Seen = true;
        }
        await _context.SaveChangesAsync(ct);

        // 3. Convert to ScoredCandidates
        var candidates = picks
            .Where(p => p.Candidate != null)
            .Select(p => new ScoredCandidate(
                Profile: p.Candidate!,
                CompatibilityScore: p.Score,
                ActivityScore: CalculateActivityScore(p.Candidate!.LastActiveAt),
                DesirabilityScore: p.Candidate!.DesirabilityScore,
                FinalScore: p.Score,
                StrategyUsed: Name
            ))
            .ToList();

        sw.Stop();

        var totalUnseen = await _context.DailyPicks
            .CountAsync(dp => dp.UserId == userId && dp.ExpiresAt > now && !dp.Acted, ct);

        _logger.LogInformation(
            "DailyPickStrategy served {Count} picks for user {UserId} in {Ms}ms",
            candidates.Count, userId, sw.ElapsedMilliseconds);

        return new CandidateResult(
            Candidates: candidates,
            TotalFiltered: picks.Count,
            TotalScored: picks.Count,
            StrategyUsed: Name,
            ExecutionTime: sw.Elapsed,
            QueueExhausted: totalUnseen <= candidates.Count,
            SuggestionsRemaining: Math.Max(0, totalUnseen - candidates.Count)
        );
    }

    private static double CalculateActivityScore(DateTime lastActiveAt)
    {
        var daysSinceActive = (DateTime.UtcNow - lastActiveAt).TotalDays;
        if (daysSinceActive < 0) daysSinceActive = 0;
        return Math.Max(0, 100 * Math.Exp(-0.1 * daysSinceActive));
    }
}
