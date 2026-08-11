using System.Text.Json;

namespace MatchmakingService.Services;

/// <summary>
/// HTTP client that calls the video-service for public profile video data.
/// </summary>
public class VideoServiceClient
{
    private readonly HttpClient _http;
    private readonly ILogger<VideoServiceClient> _logger;

    public VideoServiceClient(HttpClient http, ILogger<VideoServiceClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    /// <summary>
    /// Get the profile video URL for a user, if any.
    /// Calls GET /api/videos/public/{keycloakId} on the video-service.
    /// Forwards the caller's auth token for authentication.
    /// Returns null if no video exists or service is unavailable.
    /// </summary>
    public async Task<string?> GetProfileVideoUrlAsync(string keycloakId, string? authToken = null)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"/api/videos/public/{keycloakId}");
            if (!string.IsNullOrEmpty(authToken))
                request.Headers.TryAddWithoutValidation("Authorization", authToken);

            var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("profileVideoUrl", out var urlProp) &&
                urlProp.ValueKind == JsonValueKind.String)
            {
                var url = urlProp.GetString();
                return string.IsNullOrEmpty(url) ? null : url;
            }
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to fetch video URL for user {KeycloakId}", keycloakId);
            return null;
        }
    }

    /// <summary>
    /// Batch-fetch profile video URLs for multiple users.
    /// Forwards the caller's auth token.
    /// </summary>
    public async Task<Dictionary<string, string?>> GetProfileVideoUrlsAsync(IEnumerable<string> keycloakIds, string? authToken = null)
    {
        var results = new Dictionary<string, string?>();
        var tasks = keycloakIds.Select(async id =>
        {
            var url = await GetProfileVideoUrlAsync(id, authToken);
            lock (results) { results[id] = url; }
        });
        await Task.WhenAll(tasks);
        return results;
    }
}
