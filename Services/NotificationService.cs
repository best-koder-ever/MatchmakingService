using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MatchmakingService.Services
{
    public interface INotificationService
    {
        Task NotifyMatchAsync(int userId1, int userId2, int matchId);
        Task NotifyNewLikeAsync(int userId, int likedByUserId);
    }

    public class NotificationService : INotificationService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<NotificationService> _logger;
        private readonly string _messagingServiceUrl;

        public NotificationService(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<NotificationService> logger)
        {
            _httpClient = httpClientFactory.CreateClient();
            _logger = logger;
            _messagingServiceUrl = configuration["Services:MessagingService"] ?? "http://messaging-service:8086";
        }

        public async Task NotifyMatchAsync(int userId1, int userId2, int matchId)
        {
            try
            {
                var notification = new
                {
                    Type = "Match",
                    MatchId = matchId,
                    UserId1 = userId1,
                    UserId2 = userId2,
                    Message = "You have a new match! 🎉",
                    Timestamp = DateTime.UtcNow
                };

                // Send notification to both users via MessagingService
                await SendNotificationAsync(userId1, notification);
                await SendNotificationAsync(userId2, notification);

                _logger.LogInformation("Match notification sent for users {UserId1} and {UserId2}", userId1, userId2);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send match notification for users {UserId1} and {UserId2}", userId1, userId2);
                // Don't throw - notifications are best-effort
            }
        }

        public async Task NotifyNewLikeAsync(int userId, int likedByUserId)
        {
            try
            {
                var notification = new
                {
                    Type = "NewLike",
                    LikedByUserId = likedByUserId,
                    Message = "Someone liked you! ❤️",
                    Timestamp = DateTime.UtcNow
                };

                await SendNotificationAsync(userId, notification);

                _logger.LogInformation("Like notification sent to user {UserId} from {LikedByUserId}", userId, likedByUserId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send like notification to user {UserId}", userId);
            }
        }

        private async Task SendNotificationAsync(int userId, object notification)
        {
            try
            {
                var content = new StringContent(
                    JsonSerializer.Serialize(notification),
                    Encoding.UTF8,
                    "application/json");

                var response = await _httpClient.PostAsync(
                    $"{_messagingServiceUrl}/api/notifications/{userId}",
                    content);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Notification request to MessagingService returned {StatusCode}", response.StatusCode);
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "Failed to reach MessagingService for user {UserId}", userId);
            }
        }

        // Legacy method for backwards compatibility
        public void NotifyUser(int userId, string message)
        {
            _logger.LogInformation("Notifying User {UserId}: {Message}", userId, message);
        }
    }
}
