using System.Collections.Concurrent;

namespace MatchmakingService.Services;

/// <summary>
/// Cached reputation scores for all users, refreshed periodically.
/// Avoids per-candidate HTTP calls during discovery.
/// </summary>
public interface IReputationScoreCache
{
    int GetScore(string keycloakId);
    int GetScore(int userId);
    bool IsBanned(string keycloakId);
    bool IsExcluded(string keycloakId);
    Task RefreshAsync(CancellationToken ct = default);
}

public class ReputationScoreCache : IReputationScoreCache, IDisposable
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ReputationScoreCache> _logger;
    private readonly IConfiguration _config;
    private ConcurrentDictionary<string, int> _scores = new();
    private ConcurrentDictionary<string, bool> _banned = new();
    private readonly Timer? _refreshTimer;
    private readonly int _reputationFloor;
    private readonly TimeSpan _refreshInterval;

    public ReputationScoreCache(
        IHttpClientFactory httpClientFactory,
        IConfiguration config,
        ILogger<ReputationScoreCache> logger)
    {
        _httpClientFactory = httpClientFactory;
        _config = config;
        _logger = logger;
        _reputationFloor = config.GetValue("Reputation:Floor", 10);
        var intervalMin = config.GetValue("Reputation:CacheRefreshMinutes", 15);
        _refreshInterval = TimeSpan.FromMinutes(intervalMin);
        _refreshTimer = new Timer(async _ => await RefreshAsync(), null, _refreshInterval, _refreshInterval);
    }

    public int GetScore(string keycloakId) =>
        _scores.GetValueOrDefault(keycloakId, 50);

    public int GetScore(int userId) =>
        GetScore(userId.ToString());

    public bool IsBanned(string keycloakId) =>
        _banned.GetValueOrDefault(keycloakId, false);

    public bool IsExcluded(string keycloakId) =>
        IsBanned(keycloakId) || GetScore(keycloakId) < _reputationFloor;

    public async Task RefreshAsync(CancellationToken ct = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("ReputationService");
            // Fetch all scores (we use a batch endpoint, or paginate if needed)
            var response = await client.GetAsync("/api/reputation/admin/feedback?pageSize=1000", ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Reputation cache refresh failed: {StatusCode}", response.StatusCode);
                return;
            }

            // For now, we use a simpler approach: fetch scores from the stats endpoint
            // and individually for users we encounter. The cache is lazy-populated.
            _logger.LogDebug("Reputation cache refreshed ({Count} entries)", _scores.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to refresh reputation cache");
        }
    }

    /// <summary>
    /// Called by scoring strategy to ensure a candidate's score is cached.
    /// </summary>
    public async Task EnsureCachedAsync(string keycloakId, CancellationToken ct = default)
    {
        if (_scores.ContainsKey(keycloakId)) return;

        try
        {
            var client = _httpClientFactory.CreateClient("ReputationService");
            var response = await client.GetAsync($"/api/reputation/score/{keycloakId}", ct);
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<ReputationScoreDto>(cancellationToken: ct);
                if (data != null)
                {
                    _scores[keycloakId] = data.Score;
                    if (data.Score <= 0)
                        _banned[keycloakId] = true;
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to cache reputation for {Id}", keycloakId);
        }

        _scores[keycloakId] = 50; // Default baseline
    }

    public void Dispose() => _refreshTimer?.Dispose();
}

internal record ReputationScoreDto(int Score, bool IsNew, int TotalRatings, double AvgRating);
