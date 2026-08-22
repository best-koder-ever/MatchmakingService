using MatchmakingService.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MatchmakingService.Controllers;

/// <summary>
/// Dev/staging-only administrative reset endpoints.
/// Wipes match-related interaction data so a clean MVP demo can begin.
/// </summary>
[ApiController]
[Route("api/admin")]
[Authorize]
public class AdminController : ControllerBase
{
    private readonly MatchmakingDbContext _context;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<AdminController> _logger;

    public AdminController(MatchmakingDbContext context, IWebHostEnvironment env, ILogger<AdminController> logger)
    {
        _context = context;
        _env = env;
        _logger = logger;
    }

    private bool IsResetAllowed() =>
        _env.IsDevelopment() || _env.IsStaging() || _env.EnvironmentName == "Demo";

    /// <summary>
    /// Wipe matches, scores and user interactions. Dev/Staging/Demo only.
    /// Preserves UserProfiles and CompatibilityQuestions.
    /// </summary>
    [HttpDelete("matches")]
    public async Task<IActionResult> ResetAllMatches()
    {
        if (!IsResetAllowed())
        {
            _logger.LogWarning("Admin reset rejected: environment={Env} is not dev/staging/demo", _env.EnvironmentName);
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Admin reset disabled in this environment." });
        }

        var matches = await _context.Matches.ToListAsync();
        var interactions = await _context.UserInteractions.ToListAsync();
        var scores = await _context.MatchScores.ToListAsync();

        var matchCount = matches.Count;
        var interactionCount = interactions.Count;
        var scoreCount = scores.Count;

        _context.Matches.RemoveRange(matches);
        _context.MatchScores.RemoveRange(scores);
        _context.UserInteractions.RemoveRange(interactions);
        await _context.SaveChangesAsync();

        _logger.LogWarning(
            "[FINDING] High AdminReset: cleared {MatchCount} matches, {InteractionCount} interactions, {ScoreCount} scores by {User}",
            matchCount, interactionCount, scoreCount, User.Identity?.Name ?? "unknown");

        return Ok(new
        {
            message = "Matches, scores, and interactions cleared. UserProfiles preserved.",
            deletedMatches = matchCount,
            deletedInteractions = interactionCount,
            deletedScores = scoreCount,
            environment = _env.EnvironmentName,
        });
    }

    /// <summary>
    /// Targeted purge: deletes ONLY bot-related match data (matches, interactions, scores
    /// involving a profile flagged IsBot=true). Bot profiles themselves are preserved so the
    /// synthetic users stay provisioned. Real-user data is never touched. Dev/Staging/Demo only.
    /// </summary>
    [HttpDelete("bot-match-data")]
    public async Task<IActionResult> ResetBotMatchData([FromQuery] int olderThanHours = 0)
    {
        if (!IsResetAllowed())
        {
            _logger.LogWarning("Admin reset rejected: environment={Env} is not dev/staging/demo", _env.EnvironmentName);
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Admin reset disabled in this environment." });
        }

        var botIds = await _context.UserProfiles
            .Where(u => u.IsBot)
            .Select(u => u.UserId)
            .ToListAsync();
        var botSet = botIds.ToHashSet();
        if (botSet.Count == 0)
        {
            return Ok(new { message = "No bot profiles found — nothing to purge.", deletedMatches = 0, deletedInteractions = 0, deletedScores = 0 });
        }

        // Optional TTL filter: only purge bot matches older than N hours.
        var cutoff = olderThanHours > 0 ? DateTime.UtcNow.AddHours(-olderThanHours) : (DateTime?)null;
        var botMatches = await _context.Matches
            .Where(m => (botSet.Contains(m.User1Id) || botSet.Contains(m.User2Id)) &&
                        (cutoff == null || m.CreatedAt < cutoff))
            .ToListAsync();
        var botInteractions = await _context.UserInteractions
            .Where(i => botSet.Contains(i.UserId) || botSet.Contains(i.TargetUserId))
            .ToListAsync();
        var botScores = await _context.MatchScores
            .Where(s => botSet.Contains(s.UserId))
            .ToListAsync();

        _context.Matches.RemoveRange(botMatches);
        _context.UserInteractions.RemoveRange(botInteractions);
        _context.MatchScores.RemoveRange(botScores);
        await _context.SaveChangesAsync();

        _logger.LogWarning(
            "[FINDING] Medium AdminReset: cleared {MatchCount} bot matches, {InteractionCount} bot interactions, {ScoreCount} bot scores by {User}",
            botMatches.Count, botInteractions.Count, botScores.Count, User.Identity?.Name ?? "unknown");

        return Ok(new
        {
            message = "Bot-related matches, interactions, and scores cleared. Bot profiles preserved.",
            deletedMatches = botMatches.Count,
            deletedInteractions = botInteractions.Count,
            deletedScores = botScores.Count,
            environment = _env.EnvironmentName,
        });
    }
}
