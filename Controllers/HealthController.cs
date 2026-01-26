using MatchmakingService.DTOs;
using MatchmakingService.Services;
using Microsoft.AspNetCore.Mvc;

namespace MatchmakingService.Controllers;

/// <summary>
/// Health and metrics endpoint for monitoring
/// </summary>
[ApiController]
[Route("api/matchmaking")]
public class HealthController : ControllerBase
{
    private readonly IHealthMetricsService _healthService;
    private readonly ILogger<HealthController> _logger;
    
    public HealthController(
        IHealthMetricsService healthService,
        ILogger<HealthController> logger)
    {
        _healthService = healthService;
        _logger = logger;
    }
    
    /// <summary>
    /// Get matchmaking service health metrics
    /// </summary>
    /// <returns>Health status and performance metrics</returns>
    /// <response code="200">Returns health metrics</response>
    [HttpGet("health")]
    [ProducesResponseType(typeof(HealthMetricsResponse), 200)]
    public async Task<ActionResult<HealthMetricsResponse>> GetHealth()
    {
        _logger.LogInformation("Health endpoint called");
        
        var metrics = await _healthService.GetHealthMetricsAsync();
        
        return Ok(metrics);
    }
}
