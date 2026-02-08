using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MatchmakingService.DTOs;

namespace MatchmakingService.Controllers;

/// <summary>
/// Provides read-only match statistics for users.
/// </summary>
[ApiController]
[Route("api/matchstats")]
[Authorize]
public class MatchStatsController : ControllerBase
{
    private readonly ILogger<MatchStatsController> _logger;

    public MatchStatsController(ILogger<MatchStatsController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Get match statistics for a specific user.
    /// </summary>
    [HttpGet("{userId}")]
    public IActionResult GetMatchStats(string userId)
    {
        var requestingUserId = User.FindFirst("sub")?.Value ?? "unknown";

        _logger.LogInformation(
            "[MatchStats] User {RequesterId} requesting stats for {UserId}",
            requestingUserId, userId);

        // MVP: Return placeholder stats structure
        // Phase 2: Wire up to actual MatchmakingDbContext queries
        var stats = new MatchStatsDto(
            TotalMatches: 0,
            PendingMatches: 0,
            MatchRate: 0.0,
            MostActiveDay: null,
            LastMatchAt: null
        );

        return Ok(stats);
    }

    /// <summary>
    /// Get match statistics for the authenticated user.
    /// </summary>
    [HttpGet("me")]
    public IActionResult GetMyMatchStats()
    {
        var userId = User.FindFirst("sub")?.Value ?? "unknown";
        return GetMatchStats(userId);
    }
}
