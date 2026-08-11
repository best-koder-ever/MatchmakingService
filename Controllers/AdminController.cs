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
}
