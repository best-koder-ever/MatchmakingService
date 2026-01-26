using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MatchmakingService.Data;

namespace MatchmakingService.Controllers
{
    [Route("api/matchmaking")]
    [ApiController]
    public class UserMatchDeletionController : ControllerBase
    {
        private readonly MatchmakingDbContext _context;
        private readonly ILogger<UserMatchDeletionController> _logger;

        public UserMatchDeletionController(MatchmakingDbContext context, ILogger<UserMatchDeletionController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Cascade delete all matches involving a user (for account deletion).
        /// </summary>
        /// <param name="userProfileId">The UserProfile.Id (int)</param>
        /// <returns>Count of matches deleted as plain text</returns>
        [HttpDelete("user/{userProfileId:int}/matches")]
        [AllowAnonymous] // Service-to-service call from UserService
        public async Task<IActionResult> DeleteUserMatches(int userProfileId)
        {
            try
            {
                var matches = await _context.Matches
                    .Where(m => m.User1Id == userProfileId || m.User2Id == userProfileId)
                    .ToListAsync();

                var count = matches.Count;

                if (count > 0)
                {
                    _context.Matches.RemoveRange(matches);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Deleted {Count} matches for user {UserProfileId}", count, userProfileId);
                }

                return Ok(count.ToString());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting matches for user {UserProfileId}", userProfileId);
                return StatusCode(500, "0");
            }
        }
    }
}
