using MatchmakingService.Models;
using MatchmakingService.Services;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace MatchmakingService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MatchmakingController : ControllerBase
    {
        private readonly IUserServiceClient _userServiceClient;

        public MatchmakingController(IUserServiceClient userServiceClient)
        {
            _userServiceClient = userServiceClient;
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
    }
}