using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MatchmakingService.Common;
using MatchmakingService.Data;

namespace MatchmakingService.Controllers
{
    [ApiController]
    [Route("api/internal/matchmaking")]
    public class SyncController : ControllerBase
    {
        private readonly MatchmakingDbContext _context;
        private readonly ILogger<SyncController> _logger;

        public SyncController(MatchmakingDbContext context, ILogger<SyncController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// POST /api/internal/matchmaking/activity-ping
        /// Updates LastActiveAt for a single user. Protected by internal API key.
        /// </summary>
        [HttpPost("activity-ping")]
        [RequireInternalApiKey]
        public async Task<IActionResult> ActivityPing([FromBody] ActivityPingRequest request)
        {
            if (request.UserId <= 0)
                return BadRequest("UserId must be positive");

            var profile = await _context.UserProfiles
                .FirstOrDefaultAsync(p => p.UserId == request.UserId);

            if (profile == null)
            {
                _logger.LogDebug("Activity ping for unknown user {UserId}, ignoring", request.UserId);
                return NotFound();
            }

            profile.LastActiveAt = request.LastActiveAt ?? DateTime.UtcNow;
            profile.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogDebug("Updated LastActiveAt for user {UserId}", request.UserId);
            return Ok();
        }

        /// <summary>
        /// POST /api/internal/matchmaking/activity-ping/batch
        /// Updates LastActiveAt for multiple users in one call. Protected by internal API key.
        /// </summary>
        [HttpPost("activity-ping/batch")]
        [RequireInternalApiKey]
        public async Task<IActionResult> ActivityPingBatch([FromBody] List<ActivityPingRequest> requests)
        {
            if (requests == null || requests.Count == 0)
                return BadRequest("Empty request list");

            var userIds = requests.Where(r => r.UserId > 0).Select(r => r.UserId).ToList();
            var profiles = await _context.UserProfiles
                .Where(p => userIds.Contains(p.UserId))
                .ToDictionaryAsync(p => p.UserId);

            var updated = 0;
            foreach (var req in requests.Where(r => r.UserId > 0))
            {
                if (profiles.TryGetValue(req.UserId, out var profile))
                {
                    profile.LastActiveAt = req.LastActiveAt ?? DateTime.UtcNow;
                    profile.UpdatedAt = DateTime.UtcNow;
                    updated++;
                }
            }

            await _context.SaveChangesAsync();
            _logger.LogDebug("Batch activity ping: {Updated}/{Total} users updated", updated, requests.Count);
            return Ok(new { updated, total = requests.Count });
        }
    }

    public class ActivityPingRequest
    {
        public int UserId { get; set; }
        public DateTime? LastActiveAt { get; set; }
    }
}
