using MediatR;
using MatchmakingService.Commands;
using MatchmakingService.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace MatchmakingService.Controllers;

[ApiController]
[Route("api/matchmaking")]
[Authorize]
public class FeedbackController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ILogger<FeedbackController> _logger;

    public FeedbackController(ISender sender, ILogger<FeedbackController> logger)
    {
        _sender = sender;
        _logger = logger;
    }

    /// <summary>
    /// POST /api/matchmaking/matches/{matchId}/feedback
    /// Submit post-date reflection feedback for a match.
    /// </summary>
    [HttpPost("matches/{matchId}/feedback")]
    public async Task<IActionResult> SubmitFeedback(
        string matchId,
        [FromBody] SubmitFeedbackRequest request,
        CancellationToken cancellationToken)
    {
        var keycloakId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                         ?? User.FindFirst("sub")?.Value;

        if (string.IsNullOrEmpty(keycloakId))
            return Unauthorized("Could not determine user identity.");

        if (string.IsNullOrEmpty(matchId))
            return BadRequest("matchId is required.");

        var command = new SubmitPostDateFeedbackCommand(
            keycloakId,
            matchId,
            request.OverallRating,
            request.ChemistryRating,
            request.ConversationRating,
            request.WouldMeetAgain,
            request.FreeformReflection);

        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "DUPLICATE_FEEDBACK" => Conflict(new { error = result.Error }),
                "INVALID_RATING" => BadRequest(new { error = result.Error }),
                _ => StatusCode(500, new { error = result.Error })
            };
        }

        return Ok(new { feedbackId = result.Data!.FeedbackId });
    }

    /// <summary>
    /// GET /api/matchmaking/feedback/trends
    /// Return post-date feedback trends for the current user.
    /// </summary>
    [HttpGet("feedback/trends")]
    public async Task<IActionResult> GetFeedbackTrends()
    {
        var keycloakId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                         ?? User.FindFirst("sub")?.Value;

        if (string.IsNullOrEmpty(keycloakId))
            return Unauthorized("Could not determine user identity.");

        var query = new GetFeedbackTrendsQuery(keycloakId);
        var result = await _sender.Send(query);

        if (!result.IsSuccess)
            return NotFound(new { error = result.Error });

        return Ok(result.Data);
    }
}
