using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace MatchmakingService.Hubs;

/// <summary>
/// SignalR hub for real-time match notifications
/// T036: Real-time match creation notifications
/// </summary>
public class MatchmakingHub : Hub
{
    private readonly ILogger<MatchmakingHub> _logger;

    public MatchmakingHub(ILogger<MatchmakingHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("Connection attempt without userId claim");
            Context.Abort();
            return;
        }

        // Add connection to user-specific group for targeted notifications
        await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
        _logger.LogInformation("User {UserId} connected to MatchmakingHub with connection {ConnectionId}",
            userId, Context.ConnectionId);

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!string.IsNullOrEmpty(userId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user_{userId}");
            _logger.LogInformation("User {UserId} disconnected from MatchmakingHub", userId);
        }

        if (exception != null)
        {
            _logger.LogError(exception, "User {UserId} disconnected with error", userId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Client can call this to confirm they're ready to receive notifications
    /// </summary>
    public async Task Subscribe()
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            await Clients.Caller.SendAsync("Error", "Authentication required");
            return;
        }

        _logger.LogInformation("User {UserId} subscribed to match notifications", userId);
        await Clients.Caller.SendAsync("Subscribed", new { UserId = userId, Timestamp = DateTime.UtcNow });
    }
}
