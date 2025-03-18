using Microsoft.AspNetCore.Mvc;

namespace MatchmakingService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MatchmakingController : ControllerBase
    {
        [HttpGet("health")]
        public IActionResult HealthCheck()
        {
            return Ok("Matchmaking Service is running!");
        }
    }
}