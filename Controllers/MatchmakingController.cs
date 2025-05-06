using MatchmakingService.Models;
using MatchmakingService.Services;
using MatchmakingService.Data;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using System.Linq;

namespace MatchmakingService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MatchmakingController : ControllerBase
    {
        private readonly IUserServiceClient _userServiceClient;
        private readonly MatchmakingService.Services.MatchmakingService _matchmakingService;
        private readonly MatchmakingDbContext _context;

        public MatchmakingController(
            IUserServiceClient userServiceClient,
            MatchmakingService.Services.MatchmakingService matchmakingService,
            MatchmakingDbContext context)
        {
            _userServiceClient = userServiceClient;
            _matchmakingService = matchmakingService;
            _context = context;
        }

        // POST: Handle mutual match notifications
        [HttpPost("matches")]
        public IActionResult HandleMutualMatch([FromBody] MutualMatchRequest request)
        {
            _matchmakingService.SaveMatch(request.User1Id, request.User2Id);
            return Ok(new { Message = "Match saved successfully!" });
        }

        [HttpGet("health")]
        public IActionResult HealthCheck()
        {
            return Ok("Matchmaking Service is running!");
        }

        [HttpGet("userprofile/{userId}")]
        public async Task<IActionResult> GetUserProfile(int userId)
        {
            var userProfile = await _userServiceClient.GetUserProfileAsync(userId);
            if (userProfile == null)
            {
                return NotFound($"User profile with ID {userId} not found.");
            }

            return Ok(userProfile);
        }

        // GET: Retrieve matches for a user
        [HttpGet("matches/{userId}")]
        public IActionResult GetMatchesForUser(int userId)
        {
            var matches = _context.Matches
                .Where(m => m.User1Id == userId || m.User2Id == userId)
                .Select(m => new
                {
                    MatchId = m.Id,
                    MatchedUserId = m.User1Id == userId ? m.User2Id : m.User1Id,
                    MatchedAt = m.CreatedAt
                })
                .ToList();

            return Ok(matches);
        }
    }

    public class MutualMatchRequest
    {
        public int User1Id { get; set; }
        public int User2Id { get; set; }
    }
}