using MatchmakingService.DTOs;

namespace MatchmakingService.Services
{
    public interface ISafetyServiceClient
    {
        Task<List<int>> GetBlockedUserIdsAsync(int userId);
        Task<bool> IsBlockedAsync(int userId, int targetUserId);
    }

    public class SafetyServiceClient : ISafetyServiceClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<SafetyServiceClient> _logger;

        public SafetyServiceClient(HttpClient httpClient, ILogger<SafetyServiceClient> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<List<int>> GetBlockedUserIdsAsync(int userId)
        {
            try
            {
                // Safety service uses authenticated requests, gets current user from JWT token
                var response = await _httpClient.GetAsync("/api/safety/blocked");
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning($"Failed to fetch blocked users for user {userId}: {response.StatusCode}");
                    return new List<int>();
                }

                var result = await response.Content.ReadFromJsonAsync<SafetyApiResponse<List<BlockedUserDto>>>();
                
                if (result?.Success == true && result.Data != null)
                {
                    // Convert string UserIds to int (assuming they are numeric strings)
                    var blockedIds = new List<int>();
                    foreach (var blocked in result.Data)
                    {
                        if (int.TryParse(blocked.BlockedUserId, out int blockedId))
                        {
                            blockedIds.Add(blockedId);
                        }
                    }
                    return blockedIds;
                }
                
                return new List<int>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching blocked users for user {userId}");
                return new List<int>(); // Fail gracefully - don't break matchmaking if safety service is down
            }
        }

        public async Task<bool> IsBlockedAsync(int userId, int targetUserId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"/api/safety/is-blocked/{targetUserId}");
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning($"Failed to check block status between {userId} and {targetUserId}");
                    return false;
                }

                var result = await response.Content.ReadFromJsonAsync<SafetyApiResponse<bool>>();
                return result?.Data ?? false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking block status between {userId} and {targetUserId}");
                return false; // Fail open - allow matchmaking if safety service is down
            }
        }
    }

    // DTOs matching Safety Service responses
    public record SafetyApiResponse<T>(bool Success, T? Data, string? Error = null);
    public record BlockedUserDto(int Id, string BlockedUserId, DateTime BlockedAt, string? Reason);
}
