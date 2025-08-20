using MatchmakingService.Models;
using MatchmakingService.Services;
using MatchmakingService.Data;
using MatchmakingService.DTOs;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace MatchmakingService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MatchmakingController : ControllerBase
    {
        private readonly IUserServiceClient _userServiceClient;
        private readonly MatchmakingService.Services.MatchmakingService _matchmakingService;
        private readonly IAdvancedMatchingService _advancedMatchingService;
        private readonly MatchmakingDbContext _context;
        private readonly ILogger<MatchmakingController> _logger;

        public MatchmakingController(
            IUserServiceClient userServiceClient,
            MatchmakingService.Services.MatchmakingService matchmakingService,
            IAdvancedMatchingService advancedMatchingService,
            MatchmakingDbContext context,
            ILogger<MatchmakingController> logger)
        {
            _userServiceClient = userServiceClient;
            _matchmakingService = matchmakingService;
            _advancedMatchingService = advancedMatchingService;
            _context = context;
            _logger = logger;
        }

        // POST: Handle mutual match notifications from SwipeService
        [HttpPost("matches")]
        public async Task<IActionResult> HandleMutualMatch([FromBody] MutualMatchRequest request)
        {
            try
            {
                // Calculate compatibility score if not provided
                var compatibilityScore = request.CompatibilityScore ?? 
                    await _advancedMatchingService.CalculateCompatibilityScoreAsync(request.User1Id, request.User2Id);

                // Save the match with enhanced details
                var match = new Match
                {
                    User1Id = Math.Min(request.User1Id, request.User2Id),
                    User2Id = Math.Max(request.User1Id, request.User2Id),
                    CreatedAt = DateTime.UtcNow,
                    CompatibilityScore = compatibilityScore,
                    MatchSource = request.Source,
                    IsActive = true
                };

                _context.Matches.Add(match);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Match created between users {request.User1Id} and {request.User2Id} with score {compatibilityScore}");

                return Ok(new { 
                    Message = "Match saved successfully!", 
                    MatchId = match.Id,
                    CompatibilityScore = compatibilityScore 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error handling mutual match between users {request.User1Id} and {request.User2Id}");
                return StatusCode(500, "Error processing match");
            }
        }

        // GET: Find potential matches for a user using advanced algorithm
        [HttpPost("find-matches")]
        public async Task<IActionResult> FindMatches([FromBody] FindMatchesRequest request)
        {
            try
            {
                if (request.UserId <= 0)
                {
                    return BadRequest("Invalid user ID");
                }

                var matches = await _advancedMatchingService.FindMatchesAsync(request);
                
                return Ok(new { 
                    Matches = matches,
                    Count = matches.Count,
                    RequestId = Guid.NewGuid().ToString()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error finding matches for user {request.UserId}");
                return StatusCode(500, "Error finding matches");
            }
        }

        // GET: Get compatibility score between two users
        [HttpGet("compatibility/{userId}/{targetUserId}")]
        public async Task<IActionResult> GetCompatibilityScore(int userId, int targetUserId)
        {
            try
            {
                if (userId <= 0 || targetUserId <= 0 || userId == targetUserId)
                {
                    return BadRequest("Invalid user IDs");
                }

                var score = await _advancedMatchingService.CalculateCompatibilityScoreAsync(userId, targetUserId);
                
                return Ok(new { 
                    UserId = userId,
                    TargetUserId = targetUserId,
                    CompatibilityScore = Math.Round(score, 1),
                    CalculatedAt = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error calculating compatibility between users {userId} and {targetUserId}");
                return StatusCode(500, "Error calculating compatibility");
            }
        }

        // GET: Retrieve matches for a user with enhanced details
        [HttpGet("matches/{userId}")]
        public async Task<IActionResult> GetMatchesForUser(int userId, [FromQuery] bool includeInactive = false)
        {
            try
            {
                if (userId <= 0)
                {
                    return BadRequest("Invalid user ID");
                }

                var query = _context.Matches
                    .Where(m => m.User1Id == userId || m.User2Id == userId);

                if (!includeInactive)
                {
                    query = query.Where(m => m.IsActive);
                }

                var matches = await query
                    .OrderByDescending(m => m.CreatedAt)
                    .Select(m => new
                    {
                        MatchId = m.Id,
                        MatchedUserId = m.User1Id == userId ? m.User2Id : m.User1Id,
                        MatchedAt = m.CreatedAt,
                        CompatibilityScore = m.CompatibilityScore,
                        IsActive = m.IsActive,
                        MatchSource = m.MatchSource,
                        LastMessageAt = m.LastMessageAt,
                        LastMessageByUserId = m.LastMessageByUserId,
                        UnmatchedAt = m.UnmatchedAt,
                        UnmatchedByUserId = m.UnmatchedByUserId
                    })
                    .ToListAsync();

                return Ok(new { 
                    Matches = matches,
                    TotalCount = matches.Count,
                    ActiveCount = matches.Count(m => m.IsActive)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving matches for user {userId}");
                return StatusCode(500, "Error retrieving matches");
            }
        }

        // GET: Get match statistics for a user
        [HttpGet("stats/{userId}")]
        public async Task<IActionResult> GetMatchStats(int userId)
        {
            try
            {
                if (userId <= 0)
                {
                    return BadRequest("Invalid user ID");
                }

                var stats = await _advancedMatchingService.GetMatchStatsAsync(userId);
                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving match stats for user {userId}");
                return StatusCode(500, "Error retrieving match stats");
            }
        }

        // DELETE: Unmatch users
        [HttpDelete("matches/{userId}/{targetUserId}")]
        public async Task<IActionResult> UnmatchUsers(int userId, int targetUserId)
        {
            try
            {
                if (userId <= 0 || targetUserId <= 0)
                {
                    return BadRequest("Invalid user IDs");
                }

                var match = await _context.Matches
                    .FirstOrDefaultAsync(m => 
                        (m.User1Id == Math.Min(userId, targetUserId) && m.User2Id == Math.Max(userId, targetUserId)) &&
                        m.IsActive);

                if (match == null)
                {
                    return NotFound("Active match not found");
                }

                match.IsActive = false;
                match.UnmatchedAt = DateTime.UtcNow;
                match.UnmatchedByUserId = userId;

                await _context.SaveChangesAsync();

                _logger.LogInformation($"Users {userId} and {targetUserId} unmatched");

                return Ok(new { Message = "Users unmatched successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error unmatching users {userId} and {targetUserId}");
                return StatusCode(500, "Error unmatching users");
            }
        }

        // POST: Update user's matching preferences
        [HttpPost("preferences")]
        public async Task<IActionResult> UpdatePreferences([FromBody] UpdatePreferencesRequest request)
        {
            try
            {
                if (request.UserId <= 0)
                {
                    return BadRequest("Invalid user ID");
                }

                var userProfile = await _context.UserProfiles
                    .FirstOrDefaultAsync(up => up.UserId == request.UserId);

                if (userProfile == null)
                {
                    return NotFound("User profile not found");
                }

                // Update preferences
                userProfile.PreferredGender = request.PreferredGender;
                userProfile.MinAge = request.MinAge;
                userProfile.MaxAge = request.MaxAge;
                userProfile.MaxDistance = request.MaxDistance;
                userProfile.Interests = System.Text.Json.JsonSerializer.Serialize(request.Interests);

                // Update algorithm weights if provided
                if (request.AlgorithmWeights.ContainsKey("location"))
                    userProfile.LocationWeight = request.AlgorithmWeights["location"];
                if (request.AlgorithmWeights.ContainsKey("age"))
                    userProfile.AgeWeight = request.AlgorithmWeights["age"];
                if (request.AlgorithmWeights.ContainsKey("interests"))
                    userProfile.InterestsWeight = request.AlgorithmWeights["interests"];
                if (request.AlgorithmWeights.ContainsKey("education"))
                    userProfile.EducationWeight = request.AlgorithmWeights["education"];
                if (request.AlgorithmWeights.ContainsKey("lifestyle"))
                    userProfile.LifestyleWeight = request.AlgorithmWeights["lifestyle"];

                await _advancedMatchingService.UpdateUserProfileAsync(userProfile);

                return Ok(new { Message = "Preferences updated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating preferences for user {request.UserId}");
                return StatusCode(500, "Error updating preferences");
            }
        }

        // POST: Record swipe history to improve recommendations
        [HttpPost("swipe-history")]
        public async Task<IActionResult> RecordSwipeHistory([FromBody] SwipeHistoryRequest request)
        {
            try
            {
                if (request.UserId <= 0)
                {
                    return BadRequest("Invalid user ID");
                }

                await _advancedMatchingService.RecordSwipeHistoryAsync(request);
                
                return Ok(new { Message = "Swipe history recorded successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error recording swipe history for user {request.UserId}");
                return StatusCode(500, "Error recording swipe history");
            }
        }

        [HttpGet("health")]
        public IActionResult HealthCheck()
        {
            try
            {
                var dbConnected = _context.Database.CanConnect();
                return Ok(new { 
                    Status = "Healthy",
                    Timestamp = DateTime.UtcNow,
                    DatabaseConnected = dbConnected,
                    Service = "MatchmakingService v1.0"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Health check failed");
                return StatusCode(500, new { 
                    Status = "Unhealthy",
                    Error = ex.Message 
                });
            }
        }

        [HttpGet("userprofile/{userId}")]
        public async Task<IActionResult> GetUserProfile(int userId)
        {
            try
            {
                var userProfile = await _userServiceClient.GetUserProfileAsync(userId);
                if (userProfile == null)
                {
                    return NotFound($"User profile with ID {userId} not found.");
                }

                return Ok(userProfile);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving user profile for user {userId}");
                return StatusCode(500, "Error retrieving user profile");
            }
        }
    }
}