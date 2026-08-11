using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;

namespace MatchmakingService.Hubs;

/// <summary>
/// SignalR hub for real-time match notifications.
/// T036: Real-time match creation notifications.
///
/// Joins TWO groups per connection so that <see cref="Services.NotificationService"/>
/// (which broadcasts to <c>user_{profileId}</c>) reaches the right client even though
/// the JWT only carries the Keycloak GUID:
///   1. <c>user_{keycloakId}</c> — direct from JWT sub claim
///   2. <c>user_{profileId}</c> — looked up via UserService /api/profiles/me
/// </summary>
public class MatchmakingHub : Hub
{
    private const string ProfileIdItemKey = "_profileId";

    private readonly ILogger<MatchmakingHub> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public MatchmakingHub(
        ILogger<MatchmakingHub> logger,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? Context.User?.FindFirst("sub")?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("MatchmakingHub connection attempt without userId claim");
            Context.Abort();
            return;
        }

        // Group 1: keycloak-keyed group (for any future keycloak-keyed broadcasts).
        await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");

        // Group 2: profile-id-keyed group (matches NotificationService broadcast key).
        var profileId = await ResolveProfileIdAsync();
        if (profileId.HasValue)
        {
            Context.Items[ProfileIdItemKey] = profileId.Value;
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{profileId.Value}");
            _logger.LogInformation(
                "MatchmakingHub connected: keycloak={KeycloakId} profile={ProfileId} conn={ConnectionId}",
                userId, profileId.Value, Context.ConnectionId);
        }
        else
        {
            _logger.LogWarning(
                "MatchmakingHub connected without profileId (notifications may be delayed): keycloak={KeycloakId} conn={ConnectionId}",
                userId, Context.ConnectionId);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? Context.User?.FindFirst("sub")?.Value;

        if (!string.IsNullOrEmpty(userId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user_{userId}");
        }

        if (Context.Items.TryGetValue(ProfileIdItemKey, out var pid) && pid is int profileId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user_{profileId}");
        }

        if (exception != null)
        {
            _logger.LogError(exception, "User {UserId} disconnected with error", userId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Client can call this to confirm they're ready to receive notifications.
    /// </summary>
    public async Task Subscribe()
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? Context.User?.FindFirst("sub")?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            await Clients.Caller.SendAsync("Error", "Authentication required");
            return;
        }

        var profileId = Context.Items.TryGetValue(ProfileIdItemKey, out var pid) && pid is int p ? (int?)p : null;
        await Clients.Caller.SendAsync("Subscribed", new
        {
            UserId = userId,
            ProfileId = profileId,
            Timestamp = DateTime.UtcNow
        });
    }

    private async Task<int?> ResolveProfileIdAsync()
    {
        try
        {
            var bearer = ExtractBearerToken();
            if (string.IsNullOrEmpty(bearer))
            {
                return null;
            }

            var userServiceUrl = _configuration["Services:UserService:BaseUrl"]
                ?? "http://localhost:8082";

            using var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
            using var resp = await client.GetAsync($"{userServiceUrl.TrimEnd('/')}/api/profiles/me");
            if (!resp.IsSuccessStatusCode)
            {
                return null;
            }

            var body = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("data", out var data) &&
                data.TryGetProperty("id", out var id) &&
                id.TryGetInt32(out var profileId))
            {
                return profileId;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve profileId from UserService for hub connection");
        }
        return null;
    }

    private string? ExtractBearerToken()
    {
        var http = Context.GetHttpContext();
        if (http == null) return null;

        // Prefer Authorization header (HTTP transport / fetch).
        var authHeader = http.Request.Headers["Authorization"].FirstOrDefault();
        if (!string.IsNullOrEmpty(authHeader) &&
            authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return authHeader.Substring("Bearer ".Length).Trim();
        }

        // Fallback: SignalR WebSocket transport puts token in query string.
        var qsToken = http.Request.Query["access_token"].FirstOrDefault();
        return string.IsNullOrEmpty(qsToken) ? null : qsToken;
    }
}

