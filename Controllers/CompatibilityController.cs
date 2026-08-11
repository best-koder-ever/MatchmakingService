using System.Security.Claims;
using MatchmakingService.Commands;
using MatchmakingService.Common;
using MatchmakingService.Data;
using MatchmakingService.DTOs;
using MatchmakingService.Models;
using MatchmakingService.Queries;
using MatchmakingService.Services;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MatchmakingService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CompatibilityController : ControllerBase
{
    private readonly MatchmakingDbContext _context;
    private readonly IUserServiceClient _userServiceClient;
    private readonly ILogger<CompatibilityController> _logger;
    private readonly RadarProfileCalculator _radar;
    private readonly ISender _sender;

    public CompatibilityController(
        MatchmakingDbContext context,
        IUserServiceClient userServiceClient,
        ILogger<CompatibilityController> logger,
        RadarProfileCalculator radar,
        ISender sender)
    {
        _context = context;
        _userServiceClient = userServiceClient;
        _logger = logger;
        _radar = radar;
        _sender = sender;
    }

    [HttpGet("questions")]
    [Authorize]
    public async Task<IActionResult> GetQuestions()
    {
        var result = await _sender.Send(new GetCompatibilityQuestionsQuery());
        return Ok(new { questions = result.Data!.Questions, grouped = result.Data.Grouped });
    }

    [HttpPost("answers")]
    [Authorize]
    public async Task<IActionResult> SubmitAnswers([FromBody] SubmitAnswersRequest req)
    {
        var keycloakId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (string.IsNullOrEmpty(keycloakId)) return Unauthorized();
        if (req.Answers == null || req.Answers.Count == 0)
            return BadRequest(new { error = "At least one answer is required." });

        var command = new SubmitAnswersCommand(
            keycloakId,
            req.Answers.Select(a => new AnswerItemDto(a.QuestionId, a.Value, a.AnswerType)).ToList());

        var result = await _sender.Send(command);
        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "INVALID_QUESTIONS" => BadRequest(new { error = result.Error }),
                "OUT_OF_RANGE" => BadRequest(new { error = result.Error }),
                _ => BadRequest(new { error = result.Error })
            };
        }
        return Ok(new { saved = result.Data!.Saved, totalAnswered = result.Data.TotalAnswered });
    }

    [HttpGet("answers/{keycloakId}")]
    [Authorize]
    public async Task<IActionResult> GetAnswers(string keycloakId)
    {
        var caller = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (caller != keycloakId) return Forbid();

        var result = await _sender.Send(new GetUserAnswersQuery(keycloakId));
        return Ok(new { keycloakId = result.Data!.KeycloakId, answers = result.Data.Answers, count = result.Data.Count });
    }

    [HttpGet("preview/{candidateUserId:int}")]
    [Authorize]
    public async Task<IActionResult> GetPreMatchInsight(int candidateUserId)
    {
        var keycloakId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (string.IsNullOrEmpty(keycloakId)) return Unauthorized();
        var myProfile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.KeycloakId == keycloakId);
        if (myProfile == null) return NotFound("Your profile not found in matchmaking service");
        var theirProfile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == candidateUserId);
        if (theirProfile == null) return NotFound("Candidate profile not found");
        var myInterests = ParseJsonList(myProfile.Interests);
        var theirInterests = ParseJsonList(theirProfile.Interests);
        var shared = myInterests.Intersect(theirInterests, StringComparer.OrdinalIgnoreCase).ToList();
        var commonalities = new List<string>();
        if (!string.IsNullOrEmpty(myProfile.City) && string.Equals(myProfile.City, theirProfile.City, StringComparison.OrdinalIgnoreCase))
            commonalities.Add($"Both in {myProfile.City}");
        if (!string.IsNullOrEmpty(myProfile.Occupation) && string.Equals(myProfile.Occupation, theirProfile.Occupation, StringComparison.OrdinalIgnoreCase))
            commonalities.Add($"Both {myProfile.Occupation}s");
        var headline = shared.Count switch
        {
            >= 2 => $"You both enjoy {shared[0]} and {shared[1]}",
            1 => $"You both enjoy {shared[0]}",
            _ when commonalities.Count > 0 => commonalities[0],
            _ => "See what you might have in common"
        };
        var chips = new List<string>();
        chips.AddRange(shared.Take(3).Select(s => char.ToUpper(s[0]) + s[1..]));
        chips.AddRange(commonalities.Take(3 - chips.Count));
        return Ok(new
        {
            headline, body = "", evidenceChips = chips,
            suggestedPrompt = shared.Count > 0 ? $"Ask what got them into {shared[0].ToLowerInvariant()}" : "Ask what they're passionate about",
            confidenceLabel = shared.Count > 0 ? "Common ground" : "Worth exploring",
            tone = shared.Count > 0 ? "warm" : "curious"
        });
    }

    private static List<string> ParseJsonList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return System.Text.Json.JsonSerializer.Deserialize<List<string>>(json) ?? []; }
        catch { return []; }
    }

    // ── GET /api/compatibility/radar/{keycloakId} ─────────────────────────

    [HttpGet("radar/{keycloakId}")]
    [Authorize]
    public async Task<IActionResult> GetRadarProfile(string keycloakId)
    {
        var caller = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (caller != keycloakId) return Forbid();

        var profile = await _context.RadarProfiles.FirstOrDefaultAsync(r => r.KeycloakId == keycloakId);
        if (profile == null)
        {
            // Compute on-demand if not yet cached
            profile = await _radar.ComputeAndSaveAsync(keycloakId);
        }
        return Ok(MapRadarDto(profile));
    }

    // ── GET /api/compatibility/radar/me ──────────────────────────────────

    [HttpGet("radar/me")]
    [Authorize]
    public async Task<IActionResult> GetMyRadarProfile()
    {
        var keycloakId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (string.IsNullOrEmpty(keycloakId)) return Unauthorized();

        var profile = await _context.RadarProfiles.FirstOrDefaultAsync(r => r.KeycloakId == keycloakId);
        if (profile == null)
        {
            profile = await _radar.ComputeAndSaveAsync(keycloakId);
        }
        return Ok(MapRadarDto(profile));
    }

    // ── GET /api/compatibility/radar/compare/{otherKeycloakId} ───────────

    [HttpGet("radar/compare/{otherKeycloakId}")]
    [Authorize]
    public async Task<IActionResult> CompareRadarProfiles(string otherKeycloakId)
    {
        var myId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (string.IsNullOrEmpty(myId)) return Unauthorized();

        var myProfile = await _context.RadarProfiles.FirstOrDefaultAsync(r => r.KeycloakId == myId)
                        ?? await _radar.ComputeAndSaveAsync(myId);
        var theirProfile = await _context.RadarProfiles.FirstOrDefaultAsync(r => r.KeycloakId == otherKeycloakId);

        return Ok(new
        {
            mine = MapRadarDto(myProfile),
            theirs = theirProfile != null ? MapRadarDto(theirProfile) : null
        });
    }

    private static object MapRadarDto(RadarProfile p) => new
    {
        p.KeycloakId,
        axes = new
        {
            emotionalStability = p.EmotionalStability,
            socialEnergy       = p.SocialEnergy,
            openness           = p.Openness,
            warmth             = p.Warmth,
            lifeStructure      = p.LifeStructure,
            intimacyComfort    = p.IntimacyComfort,
            conflictStyle      = p.ConflictStyle
        },
        previousAxes = p.PreviousValuesJson != null
            ? (System.Text.Json.JsonElement?)System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(p.PreviousValuesJson)
            : null,
        p.Confidence,
        p.UpdatedAt
    };

    // ── POST /api/compatibility/radar/refresh/{keycloakId} ───────────────
    // Service-to-service: UserService triggers this after psykolog session ends.

    [HttpPost("radar/refresh/{keycloakId}")]
    [ServiceFilter(typeof(InternalApiKeyAuthFilter))]
    public async Task<IActionResult> RefreshRadarProfile(string keycloakId)
    {
        var profile = await _radar.ComputeAndSaveAsync(keycloakId);
        return Ok(MapRadarDto(profile));
    }
}

public record AnswerItem(int QuestionId, int Value, string? AnswerType = "tap");
public record SubmitAnswersRequest(List<AnswerItem> Answers);
