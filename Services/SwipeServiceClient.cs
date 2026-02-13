using System.Net.Http.Json;

namespace MatchmakingService.Services
{
    /// <summary>
    /// Client for querying SwipeService to get swiped user IDs.
    /// Used by AdvancedMatchingService to exclude already-swiped profiles from candidate lists.
    /// </summary>
    public interface ISwipeServiceClient
    {
        /// <summary>
        /// Gets all user IDs that the given user has swiped on (both likes and passes).
        /// </summary>
        Task<HashSet<int>> GetSwipedUserIdsAsync(int userId);
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
}
