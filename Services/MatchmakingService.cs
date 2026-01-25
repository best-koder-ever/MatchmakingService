using MatchmakingService.Data;
using MatchmakingService.Models;

namespace MatchmakingService.Services
{
    public class MatchmakingService
    {
        private readonly MatchmakingDbContext _context;
        private readonly INotificationService _notificationService;

        public MatchmakingService(MatchmakingDbContext context, INotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
        }

        public async Task SaveMatchAsync(int user1Id, int user2Id)
        {
            var existingMatch = _context.Matches.FirstOrDefault(m =>
                (m.User1Id == user1Id && m.User2Id == user2Id) ||
                (m.User1Id == user2Id && m.User2Id == user1Id));

            if (existingMatch == null)
            {
                var match = new Match
                {
                    User1Id = user1Id,
                    User2Id = user2Id
                };

                _context.Matches.Add(match);
                _context.SaveChanges();

                // Notify both users about the match
                await _notificationService.NotifyMatchAsync(user1Id, user2Id, match.Id);
            }
        }
    }
}