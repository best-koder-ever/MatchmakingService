using MatchmakingService.Data;
using MatchmakingService.DTOs;
using MatchmakingService.Models;
using MatchmakingService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MatchmakingService.Controllers;

[ApiController]
[Route("api/compatibility")]
public class CompatibilityController : ControllerBase
{
    private readonly MatchmakingDbContext _context;
    private readonly ICompatibilityScorer _scorer;
    private readonly ILogger<CompatibilityController> _logger;

    public CompatibilityController(
        MatchmakingDbContext context,
        ICompatibilityScorer scorer,
        ILogger<CompatibilityController> logger)
    {
        _context = context;
        _scorer = scorer;
        _logger = logger;
    }

    /// <summary>
    /// GET /api/compatibility/questions
    /// Returns all active compatibility questions ordered by SortOrder.
    /// Requires authentication.
    /// </summary>
    [HttpGet("questions")]
    public async Task<IActionResult> GetQuestions()
    {
        var callerKeycloakId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(callerKeycloakId))
            return Unauthorized();

        var questions = await _context.CompatibilityQuestions
            .Where(q => q.IsActive)
            .OrderBy(q => q.SortOrder)
            .ToListAsync();

        return Ok(questions);
    }

    /// <summary>
    /// POST /api/compatibility/answers
    /// Upserts a compatibility answer for the authenticated user.
    /// First call inserts; subsequent calls update the same row.
    /// Returns 404 if the question does not exist.
    /// </summary>
    [HttpPost("answers")]
    public async Task<IActionResult> UpsertAnswer([FromBody] UpsertAnswerRequest request)
    {
        var callerKeycloakId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(callerKeycloakId))
            return Unauthorized();

        var question = await _context.CompatibilityQuestions.FindAsync(request.QuestionId);
        if (question == null)
            return NotFound(new { Error = "Question not found" });

        var existing = await _context.CompatibilityAnswers
            .FirstOrDefaultAsync(a => a.KeycloakId == callerKeycloakId && a.QuestionId == request.QuestionId);

        if (existing == null)
        {
            _context.CompatibilityAnswers.Add(new CompatibilityAnswer
            {
                QuestionId = request.QuestionId,
                KeycloakId = callerKeycloakId,
                AnswerValue = request.AnswerValue
            });
        }
        else
        {
            existing.AnswerValue = request.AnswerValue;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
        return Ok();
    }

    /// <summary>
    /// GET /api/compatibility/answers/{keycloakId}
    /// Returns the compatibility answers for the specified user.
    /// Only the authenticated user may view their own answers; returns 403 for others.
    /// </summary>
    [HttpGet("answers/{keycloakId}")]
    public async Task<IActionResult> GetAnswers(string keycloakId)
    {
        var callerKeycloakId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(callerKeycloakId))
            return Unauthorized();

        if (callerKeycloakId != keycloakId)
            return Forbid();

        var answers = await _context.CompatibilityAnswers
            .Where(a => a.KeycloakId == keycloakId)
            .ToListAsync();

        return Ok(answers);
    }

    /// <summary>
    /// GET /api/compatibility/score/{otherKeycloakId}
    /// Returns the compatibility score DTO between the caller and another user.
    /// When one or both users have no answers the scorer returns a neutral result.
    /// </summary>
    [HttpGet("score/{otherKeycloakId}")]
    public async Task<IActionResult> GetScore(string otherKeycloakId)
    {
        var callerKeycloakId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(callerKeycloakId))
            return Unauthorized();

        var myAnswers = await _context.CompatibilityAnswers
            .Where(a => a.KeycloakId == callerKeycloakId)
            .ToListAsync();

        var theirAnswers = await _context.CompatibilityAnswers
            .Where(a => a.KeycloakId == otherKeycloakId)
            .ToListAsync();

        var score = _scorer.Score(callerKeycloakId, otherKeycloakId, myAnswers, theirAnswers);
        return Ok(score);
    }
}

/// <summary>Request body for POST /api/compatibility/answers</summary>
public record UpsertAnswerRequest(int QuestionId, int AnswerValue);
