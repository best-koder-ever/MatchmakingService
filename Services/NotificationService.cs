using System;

namespace MatchmakingService.Services
{
    public class NotificationService
    {
        public void NotifyUser(int userId, string message)
        {
            // TODO: Implement notification logic (e.g., push notifications, email)
            Console.WriteLine($"Notifying User {userId}: {message}");
        }
    }
}