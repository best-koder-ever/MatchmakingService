using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using MatchmakingService.Hubs;

namespace MatchmakingService.Services
{
    public interface INotificationService
    {
        Task NotifyMatchAsync(int userId1, int userId2, int matchId);
        Task NotifyNewLikeAsync(int userId, int likedByUserId);
    }

    /// <summary>
    /// T036: Enhanced notification service with real-time SignalR support
    /// </summary>
    public class NotificationService : INotificationService
    {
        private readonly IHubContext<MatchmakingHub> _hubContext;
        private readonly HttpClient _httpClient;
        private readonly ILogger<NotificationService> _logger;
        private readonly string _messagingServiceUrl;

        public NotificationService(
            IHubContext<MatchmakingHub> hubContext,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<NotificationService> logger)
        {
            _hubContext = hubContext;
            _httpClient = httpClientFactory.CreateClient();
            _logger = logger;
            _messagingServiceUrl = configuration["Services:MessagingService"] ?? "http://messaging-service:8086";
        }

        public async Task NotifyMatchAsync(int userId1, int userId2, int matchId)
        {
            try
            {
                var timestamp = DateTime.UtcNow;

                // Build notification payloads for both users
                var notification1 = new
                {
                    Type = "Match",
                    MatchId = matchId,
                    UserId = userId1,
                    MatchedWithUserId = userId2,
                    Message = "You have a new match! 🎉",
                    Timestamp = timestamp
                };

                var notification2 = new
                {
                    Type = "Match",
                    MatchId = matchId,
                    UserId = userId2,
                    MatchedWithUserId = userId1,
                    Message = "You have a new match! 🎉",
                    Timestamp = timestamp
                };

                // Send real-time SignalR notifications (primary delivery method)
                await Task.WhenAll(
                    _hubContext.Clients.Group($"user_{userId1}").SendAsync("MatchCreated", notification1),
                    _hubContext.Clients.Group($"user_{userId2}").SendAsync("MatchCreated", notification2)
                );

                _logger.LogInformation("Real-time match notification sent to users {UserId1} and {UserId2} via SignalR",
                    userId1, userId2);

                // Fallback: Send HTTP notification to MessagingService for offline delivery
                await Task.WhenAll(
                    SendHttpNotificationAsync(userId1, notification1),
                    SendHttpNotificationAsync(userId2, notification2)
                );

                _logger.LogInformation("Match notification completed for users {UserId1} and {UserId2}",
                    userId1, userId2);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send match notification for users {UserId1} and {UserId2}",
                    userId1, userId2);
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
                    UserId = userId,
                    LikedByUserId = likedByUserId,
                    Message = "Someone liked you! ❤️",
                    Timestamp = DateTime.UtcNow
                };

                // Real-time SignalR notification
                await _hubContext.Clients.Group($"user_{userId}").SendAsync("NewLike", notification);

                _logger.LogInformation("Real-time like notification sent to user {UserId} from {LikedByUserId} via SignalR",
                    userId, likedByUserId);

                // Fallback HTTP notification
                await SendHttpNotificationAsync(userId, notification);

                _logger.LogInformation("Like notification completed for user {UserId} from {LikedByUserId}",
                    userId, likedByUserId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send like notification to user {UserId}", userId);
            }
        }

        private async Task SendHttpNotificationAsync(int userId, object notification)
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
                    _logger.LogWarning("HTTP notification to MessagingService returned {StatusCode} for user {UserId}",
                        response.StatusCode, userId);
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "Failed to reach MessagingService for user {UserId} - user may receive notification when they reconnect",
                    userId);
            }
        }
    }
}
