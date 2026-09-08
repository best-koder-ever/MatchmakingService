using System.Text.Json;

namespace MatchmakingService.Services;

/// <summary>
/// HTTP client that asks photo-service (routed via the gateway) whether candidate
/// profiles have a voice prompt, mirroring <see cref="VideoServiceClient"/>.
///
/// Flutter renders a voice-prompt card only when a candidate's voicePromptUrl is
/// non-null, so the Discover deck asks photo-service for each candidate's prompt
/// metadata and returns the audio URL (photo-service serves audio by the same int
/// profile id used everywhere in the ecosystem).
/// </summary>
public class VoicePromptServiceClient
{
    private readonly HttpClient _http;
    private readonly ILogger<VoicePromptServiceClient> _logger;

    public VoicePromptServiceClient(HttpClient http, ILogger<VoicePromptServiceClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    /// <summary>
    /// Get the voice prompt audio URL for a user, if any.
    /// Calls GET /api/voice-prompts/meta/{profileId} on photo-service (via gateway).
    /// Forwards the caller's auth token. Returns null if no prompt or service down.
    /// </summary>
    public async Task<string?> GetVoicePromptUrlAsync(int profileId, string? authToken = null)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"/api/voice-prompts/meta/{profileId}");
            if (!string.IsNullOrEmpty(authToken))
                request.Headers.TryAddWithoutValidation("Authorization", authToken);

            var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("audioUrl", out var urlProp) &&
                urlProp.ValueKind == JsonValueKind.String)
            {
                var url = urlProp.GetString();
                return string.IsNullOrEmpty(url) ? null : url;
            }
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to fetch voice prompt URL for profile {ProfileId}", profileId);
            return null;
        }
    }

    /// <summary>Batch-fetch voice prompt URLs for multiple profile ids (parallel).</summary>
    public async Task<Dictionary<int, string?>> GetVoicePromptUrlsAsync(
        IEnumerable<int> profileIds, string? authToken = null)
    {
        var results = new Dictionary<int, string?>();
        var tasks = profileIds.Select(async id =>
        {
            var url = await GetVoicePromptUrlAsync(id, authToken);
            lock (results) { results[id] = url; }
        });
        await Task.WhenAll(tasks);
        return results;
    }
}
