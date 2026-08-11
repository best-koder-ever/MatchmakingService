using System.Security.Claims;
using MatchmakingService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace MatchmakingService.Controllers;

[Route("api/spark-notifications")]
[ApiController]
[Authorize]
public class SparkNotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<SparkNotificationsController> _logger;

    public SparkNotificationsController(
        INotificationService notificationService,
        ILogger<SparkNotificationsController> logger)
    {
        _notificationService = notificationService;
        _logger = logger;
    }

    /// <summary>
    /// Notify a user that they received a Spark.
    /// Called by UserService BillingController after a successful spark send.
    /// </summary>
    [HttpPost("notify")]
    public async Task<IActionResult> NotifySparkReceived([FromBody] SparkNotificationRequest request)
    {
        // Validate the caller via internal API key (service-to-service)
        var expectedKey = HttpContext.RequestServices
            .GetService<Microsoft.Extensions.Configuration.IConfiguration>()
            ?.GetSection("InternalAuth")?["SparkServiceApiKey"];
        var providedKey = Request.Headers["X-Internal-API-Key"].FirstOrDefault();

        if (!string.IsNullOrEmpty(expectedKey) && providedKey != expectedKey)
            return Unauthorized(new { error = "Invalid internal API key" });

        if (string.IsNullOrWhiteSpace(request.RecipientUserId))
            return BadRequest(new { error = "RecipientUserId is required" });

        await _notificationService.NotifySparkReceivedAsync(
            request.RecipientUserId,
            request.SenderUserId,
            request.Message);

        return Ok(new { success = true });
    }
}

public record SparkNotificationRequest(string RecipientUserId, string SenderUserId, string? Message);
