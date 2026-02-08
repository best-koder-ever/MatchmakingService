using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MatchmakingService.DTOs;

namespace MatchmakingService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CompatibilityController : ControllerBase
{
    [HttpGet("{userId1}/{userId2}")]
    public IActionResult GetCompatibility(string userId1, string userId2)
    {
        // Deterministic pseudo-score based on user ID hash for MVP
        var hash = Math.Abs((userId1 + userId2).GetHashCode());
        var interests = (hash % 40) + 30;   // 30-69
        var location = (hash % 30) + 40;    // 40-69
        var preference = (hash % 50) + 25;  // 25-74
        var overall = (int)((interests * 0.4) + (location * 0.3) + (preference * 0.3));

        var dto = new CompatibilityScoreDto
        {
            UserId1 = userId1,
            UserId2 = userId2,
            OverallScore = Math.Clamp(overall, 0, 100),
            InterestsScore = interests,
            LocationScore = location,
            PreferenceScore = preference
        };

        return Ok(dto);
    }
}
