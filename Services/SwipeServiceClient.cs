using System.Net.Http.Json;

namespace MatchmakingService.Services
{
    /// <summary>
    /// Client for querying SwipeService to get swiped user IDs and trust scores.
    /// Used by AdvancedMatchingService to exclude already-swiped profiles from candidate lists,
    /// and by LiveScoringStrategy for shadow-restricting low-trust users (T188).
    /// </summary>
    public interface ISwipeServiceClient
    {
        /// <summary>
        /// Gets all user IDs that the given user has swiped on (both likes and passes).
        /// </summary>
        Task<HashSet<int>> GetSwipedUserIdsAsync(int userId);

        /// <summary>
        /// T188: Gets the swipe trust score for a user (0-100).
        /// Returns 100 (max trust) if the service is unavailable.
        /// </summary>
        Task<decimal> GetSwipeTrustScoreAsync(int userId);

        /// <summary>
        /// T188: Gets trust scores for multiple users in a single batch call.
        /// Returns a dictionary of userId => trustScore. Missing users default to 100.
        /// </summary>
        Task<Dictionary<int, decimal>> GetBatchTrustScoresAsync(IEnumerable<int> userIds);
    }

    public class SwipeServiceClient : ISwipeServiceClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<SwipeServiceClient> _logger;

        public SwipeServiceClient(HttpClient httpClient, ILogger<SwipeServiceClient> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<HashSet<int>> GetSwipedUserIdsAsync(int userId)
        {
            try
            {
                var allSwipedIds = new HashSet<int>();
                int page = 1;
                const int pageSize = 200;
                bool hasMore = true;

                while (hasMore)
                {
                    var response = await _httpClient.GetAsync(
                        $"/api/swipes/user/{userId}?page={page}&pageSize={pageSize}");

                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogWarning(
                            "Failed to fetch swiped users from SwipeService for user {UserId}: {StatusCode}",
                            userId, response.StatusCode);
                        break;
                    }

                    var result = await response.Content.ReadFromJsonAsync<SwipeServiceApiResponse>();

                    if (result?.Success == true && result.Data?.Swipes != null)
                    {
                        foreach (var swipe in result.Data.Swipes)
                        {
                            allSwipedIds.Add(swipe.TargetUserId);
                        }

                        hasMore = result.Data.Swipes.Count >= pageSize;
                        page++;
                    }
                    else
                    {
                        hasMore = false;
                    }
                }

                _logger.LogDebug(
                    "Fetched {Count} swiped user IDs from SwipeService for user {UserId}",
                    allSwipedIds.Count, userId);

                return allSwipedIds;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching swiped users from SwipeService for user {UserId}", userId);
                return new HashSet<int>(); // Fail gracefully — don't break matchmaking
            }
        }

        /// <summary>
        /// T188: Fetch trust score for a single candidate from SwipeService's internal endpoint.
        /// Gracefully returns 100 (default/max) if the service is unavailable.
        /// </summary>
        public async Task<decimal> GetSwipeTrustScoreAsync(int userId)
        {
            try
            {
                var response = await _httpClient.GetAsync(
                    $"/api/internal/swipe-behavior/{userId}/trust-score");

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogDebug(
                        "Trust score unavailable for user {UserId}: {StatusCode}",
                        userId, response.StatusCode);
                    return 100m; // Default: full trust
                }

                var result = await response.Content.ReadFromJsonAsync<TrustScoreResponse>();
                return result?.TrustScore ?? 100m;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error fetching trust score for user {UserId}, defaulting to 100", userId);
                return 100m; // Graceful degradation
            }
        }

        /// <summary>
        /// T188: Batch fetch trust scores for multiple candidates.
        /// </summary>
        public async Task<Dictionary<int, decimal>> GetBatchTrustScoresAsync(IEnumerable<int> userIds)
        {
            var userIdList = userIds.ToList();
            var defaults = userIdList.ToDictionary(id => id, _ => 100m);

            if (userIdList.Count == 0)
                return defaults;

            try
            {
                var response = await _httpClient.PostAsJsonAsync(
                    "/api/internal/swipe-behavior/batch-trust-scores",
                    new { UserIds = userIdList });

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Batch trust score fetch failed: {StatusCode}", response.StatusCode);
                    return defaults;
                }

                var results = await response.Content.ReadFromJsonAsync<List<TrustScoreResponse>>();
                if (results == null) return defaults;

                var scoreMap = new Dictionary<int, decimal>();
                foreach (var r in results)
                {
                    scoreMap[r.UserId] = r.TrustScore;
                }

                // Fill in any missing with defaults
                foreach (var id in userIdList)
                {
                    if (!scoreMap.ContainsKey(id))
                        scoreMap[id] = 100m;
                }

                return scoreMap;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error batch-fetching trust scores, defaulting all to 100");
                return defaults;
            }
        }
    }

    // DTOs matching SwipeService ApiResponse<UserSwipeHistory> format exactly
    internal class SwipeServiceApiResponse
    {
        public bool Success { get; set; }
        public SwipeHistoryData? Data { get; set; }
        public string? Message { get; set; }
        public List<string>? Errors { get; set; }
    }

    internal class SwipeHistoryData
    {
        public int UserId { get; set; }
        public List<SwipeRecordDto> Swipes { get; set; } = new();
        public int TotalSwipes { get; set; }
        public int TotalLikes { get; set; }
        public int TotalPasses { get; set; }
    }

    internal class SwipeRecordDto
    {
        public int Id { get; set; }
        public int TargetUserId { get; set; }
        public bool IsLike { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>DTO for trust score responses from SwipeService.</summary>
    internal class TrustScoreResponse
    {
        public int UserId { get; set; }
        public decimal TrustScore { get; set; }
    }
}
