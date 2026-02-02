using MatchmakingService.Models;
using MatchmakingService.Services;
using MatchmakingService.Data;
using MatchmakingService.DTOs;
using Microsoft.AspNetCore.Authorization;
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
        private readonly INotificationService _notificationService;
        private readonly IDailySuggestionTracker _suggestionTracker;
        private readonly MatchmakingDbContext _context;
        private readonly ILogger<MatchmakingController> _logger;

        public MatchmakingController(
            IUserServiceClient userServiceClient,
            MatchmakingService.Services.MatchmakingService matchmakingService,
            IAdvancedMatchingService advancedMatchingService,
            INotificationService notificationService,
            IDailySuggestionTracker suggestionTracker,
            MatchmakingDbContext context,
            ILogger<MatchmakingController> logger)
        {
            _userServiceClient = userServiceClient;
            _matchmakingService = matchmakingService;
            _advancedMatchingService = advancedMatchingService;
            _notificationService = notificationService;
            _suggestionTracker = suggestionTracker;
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

                // Send match notifications to both users
                await _notificationService.NotifyMatchAsync(request.User1Id, request.User2Id, match.Id);

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
        /// <summary>
        /// T033: Enhanced endpoint with daily suggestion limit tracking
        /// </summary>
        [HttpPost("find-matches")]
        public async Task<IActionResult> FindMatches([FromBody] FindMatchesRequest request)
        {
            try
            {
                if (request.UserId <= 0)
                {
                    return BadRequest("Invalid user ID");
                }

                var isPremium = request.IsPremium ?? false;
                
                // Get current status before making request
                var statusBefore = await _suggestionTracker.GetStatusAsync(request.UserId, isPremium);
                
                // Check if user has reached daily limit
                if (statusBefore.SuggestionsRemaining <= 0)
                {
                    _logger.LogInformation("User {UserId} has reached daily suggestion limit ({Count}/{Max})", 
                        request.UserId, statusBefore.SuggestionsShownToday, statusBefore.MaxDailySuggestions);
                    
                    return Ok(new FindMatchesResponse
                    {
                        Matches = new List<MatchSuggestionResponse>(),
                        Count = 0,
                        RequestId = Guid.NewGuid().ToString(),
                        SuggestionsRemaining = 0,
                        DailyLimitReached = true,
                        NextResetAt = statusBefore.NextResetDate,
                        QueueExhausted = false,
                        Message = isPremium 
                            ? $"You've viewed all {statusBefore.MaxDailySuggestions} daily suggestions. Check back tomorrow!"
                            : $"You've reached your daily limit of {statusBefore.MaxDailySuggestions} profiles. Upgrade to Premium for {50 - statusBefore.MaxDailySuggestions} more!"
                    });
                }

                var matches = await _advancedMatchingService.FindMatchesAsync(request);
                
                // Get updated status after matches found
                var statusAfter = await _suggestionTracker.GetStatusAsync(request.UserId, isPremium);
                
                // Determine if queue is exhausted (no more candidates available)
                var queueExhausted = matches.Count == 0 && statusAfter.SuggestionsRemaining > 0;
                
                var message = queueExhausted
                    ? "No more profiles available right now. Try broadening your preferences!"
                    : matches.Count > 0
                        ? $"Found {matches.Count} compatible profiles"
                        : statusAfter.SuggestionsRemaining > 0
                            ? "No matches found. Try adjusting your filters."
                            : "Daily limit reached. Check back tomorrow!";
                
                _logger.LogInformation("Found {Count} matches for user {UserId}. Remaining suggestions: {Remaining}/{Max}", 
                    matches.Count, request.UserId, statusAfter.SuggestionsRemaining, statusAfter.MaxDailySuggestions);
                
                return Ok(new FindMatchesResponse
                {
                    Matches = matches,
                    Count = matches.Count,
                    RequestId = Guid.NewGuid().ToString(),
                    SuggestionsRemaining = statusAfter.SuggestionsRemaining,
                    DailyLimitReached = statusAfter.SuggestionsRemaining <= 0,
                    NextResetAt = statusAfter.NextResetDate,
                    QueueExhausted = queueExhausted,
                    Message = message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error finding matches for user {request.UserId}");
                return StatusCode(500, "Error finding matches");
            }
        }

        // GET: Check daily suggestion status without consuming a suggestion
        /// <summary>
        /// T033: Get current daily suggestion limit status for a user
        /// </summary>
        [HttpGet("daily-suggestions/status/{userId}")]
        public async Task<IActionResult> GetDailySuggestionStatus(int userId, [FromQuery] bool isPremium = false)
        {
            try
            {
                if (userId <= 0)
                {
                    return BadRequest("Invalid user ID");
                }

                var status = await _suggestionTracker.GetStatusAsync(userId, isPremium);
                
                var response = new DailySuggestionStatusResponse
                {
                    SuggestionsShownToday = status.SuggestionsShownToday,
                    MaxDailySuggestions = status.MaxDailySuggestions,
                    SuggestionsRemaining = status.SuggestionsRemaining,
                    LastResetDate = status.LastResetDate,
                    NextResetDate = status.NextResetDate,
                    QueueExhausted = status.QueueExhausted,
                    IsPremium = isPremium,
                    Tier = isPremium ? "premium" : "free"
                };
                
                _logger.LogInformation("Daily suggestion status for user {UserId}: {Shown}/{Max} shown, {Remaining} remaining", 
                    userId, status.SuggestionsShownToday, status.MaxDailySuggestions, status.SuggestionsRemaining);
                
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting daily suggestion status for user {UserId}", userId);
                return StatusCode(500, "Error retrieving daily suggestion status");
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

        // GET: Retrieve matches for authenticated user (JWT-based, more RESTful)
        /// <summary>
        /// T001: New endpoint - Get matches for currently authenticated user
        /// Uses JWT claims to identify user, no userId parameter needed
        /// </summary>
        [HttpGet("matches")]
        public async Task<IActionResult> GetMyMatches([FromQuery] bool includeInactive = false, [FromQuery] int? page = 1, [FromQuery] int? pageSize = 20)
        {
            try
            {
                // Extract userId from JWT claims (when auth is enabled)
                // For now, expect userId in query string or header for backwards compatibility
                var userIdClaim = User.FindFirst("sub")?.Value ?? User.FindFirst("user_id")?.Value;
                
                if (string.IsNullOrEmpty(userIdClaim))
                {
                    // Fallback: check query parameter for demo/testing purposes
                    var userIdParam = Request.Query["userId"].FirstOrDefault();
                    if (string.IsNullOrEmpty(userIdParam))
                    {
                        return BadRequest(new { 
                            Error = "User ID not found in authentication token",
                            Message = "Please include userId query parameter or ensure valid JWT token"
                        });
                    }
                    userIdClaim = userIdParam;
                }

                if (!int.TryParse(userIdClaim, out int userId) || userId <= 0)
                {
                    return BadRequest("Invalid user ID in token");
                }

                // Use AsNoTracking() for read-only query optimization
                var query = _context.Matches
                    .AsNoTracking()
                    .Where(m => m.User1Id == userId || m.User2Id == userId);

                if (!includeInactive)
                {
                    query = query.Where(m => m.IsActive);
                }

                // Get total count before pagination
                var totalCount = await query.CountAsync();

                // Apply pagination
                var skip = ((page ?? 1) - 1) * (pageSize ?? 20);
                var matches = await query
                    .OrderByDescending(m => m.LastMessageAt ?? m.CreatedAt)
                    .Skip(skip)
                    .Take(pageSize ?? 20)
                    .Select(m => new
                    {
                        MatchId = m.Id,
                        MatchedUserId = m.User1Id == userId ? m.User2Id : m.User1Id,
                        MatchedAt = m.CreatedAt,
                        CompatibilityScore = Math.Round(m.CompatibilityScore, 1),
                        IsActive = m.IsActive,
                        MatchSource = m.MatchSource,
                        LastMessageAt = m.LastMessageAt,
                        LastMessageByUserId = m.LastMessageByUserId,
                        UnmatchedAt = m.UnmatchedAt,
                        UnmatchedByUserId = m.UnmatchedByUserId
                    })
                    .ToListAsync();

                _logger.LogInformation("Retrieved {Count} matches for authenticated user {UserId} (page {Page}/{PageSize})", 
                    matches.Count, userId, page, pageSize);

                return Ok(new { 
                    Matches = matches,
                    TotalCount = totalCount,
                    ActiveCount = matches.Count(m => m.IsActive),
                    Page = page ?? 1,
                    PageSize = pageSize ?? 20,
                    TotalPages = (int)Math.Ceiling(totalCount / (double)(pageSize ?? 20))
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving matches for authenticated user");
                return StatusCode(500, "Error retrieving matches");
            }
        }

        // GET: Retrieve matches for a user with enhanced details (admin/specific user lookup)
        [HttpGet("matches/{userId}")]
        public async Task<IActionResult> GetMatchesForUser(int userId, [FromQuery] bool includeInactive = false)
        {
            try
            {
                if (userId <= 0)
                {
                    return BadRequest("Invalid user ID");
                }

                // Use AsNoTracking() for read-only query optimization
                var query = _context.Matches
                    .AsNoTracking()
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

        // GET: Consolidated match list with user profiles and message previews
        [HttpGet("matches/{userId}/consolidated")]
        public async Task<IActionResult> GetConsolidatedMatches(int userId, [FromQuery] bool includeInactive = false)
        {
            try
            {
                if (userId <= 0)
                {
                    return BadRequest("Invalid user ID");
                }

                // Use AsNoTracking() for read-only query optimization
                var query = _context.Matches
                    .AsNoTracking()
                    .Where(m => m.User1Id == userId || m.User2Id == userId);

                if (!includeInactive)
                {
                    query = query.Where(m => m.IsActive);
                }

                var matches = await query
                    .OrderByDescending(m => m.LastMessageAt ?? m.CreatedAt)
                    .ToListAsync();

                var consolidatedMatches = new List<ConsolidatedMatchDto>();
                int unreadCount = 0;

                foreach (var match in matches)
                {
                    var matchedUserId = match.User1Id == userId ? match.User2Id : match.User1Id;
                    
                    // Fetch user profile from UserService
                    var userProfile = await _userServiceClient.GetUserProfileAsync(matchedUserId);
                    
                    if (userProfile == null)
                    {
                        _logger.LogWarning("Could not fetch profile for user {UserId}", matchedUserId);
                        continue; // Skip this match if profile unavailable
                    }

                    var consolidatedMatch = new ConsolidatedMatchDto
                    {
                        MatchId = match.Id,
                        MatchedUserId = matchedUserId,
                        MatchedAt = match.CreatedAt,
                        CompatibilityScore = Math.Round(match.CompatibilityScore, 1),
                        MatchSource = match.MatchSource,
                        
                        // User profile details (from matchmaking-local UserProfile)
                        Name = $"User {matchedUserId}", // TODO: Fetch from UserService API when available
                        Age = userProfile.Age,
                        Bio = null, // TODO: Fetch from UserService API
                        PrimaryPhotoUrl = $"/api/photos/{matchedUserId}/primary",
                        City = userProfile.City,
                        DistanceKm = null, // Could calculate from lat/long if needed
                        
                        // Message preview (stub - to be implemented when messaging API available)
                        LastMessagePreview = null,
                        LastMessageAt = match.LastMessageAt,
                        IsLastMessageFromMe = match.LastMessageByUserId == userId,
                        UnreadCount = null, // TODO: Call messaging service for unread count
                        
                        // Status
                        IsActive = match.IsActive,
                        IsOnline = false // TODO: Could integrate with presence service
                    };

                    consolidatedMatches.Add(consolidatedMatch);
                }

                var response = new ConsolidatedMatchListResponse
                {
                    Matches = consolidatedMatches,
                    TotalCount = consolidatedMatches.Count,
                    ActiveCount = consolidatedMatches.Count(m => m.IsActive),
                    UnreadMessagesCount = unreadCount
                };

                _logger.LogInformation("Retrieved {Count} consolidated matches for user {UserId}", 
                    consolidatedMatches.Count, userId);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving consolidated matches for user {UserId}", userId);
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

        // POST: Unmatch by match ID with reason tracking (preferred method)
        [HttpPost("matches/{matchId}/unmatch")]
        public async Task<IActionResult> UnmatchByMatchId(int matchId, [FromBody] UnmatchRequest request)
        {
            try
            {
                if (matchId <= 0)
                {
                    return BadRequest("Invalid match ID");
                }

                if (request.UserId <= 0)
                {
                    return BadRequest("Invalid user ID");
                }

                var match = await _context.Matches
                    .FirstOrDefaultAsync(m => m.Id == matchId && m.IsActive);

                if (match == null)
                {
                    return NotFound("Active match not found");
                }

                // Verify the requesting user is a participant in this match
                if (match.User1Id != request.UserId && match.User2Id != request.UserId)
                {
                    return Forbid(); // User is not part of this match
                }

                // Soft delete the match with reason tracking
                match.IsActive = false;
                match.UnmatchedAt = DateTime.UtcNow;
                match.UnmatchedByUserId = request.UserId;
                match.UnmatchReason = request.Reason ?? "not_specified";

                await _context.SaveChangesAsync();

                var otherUserId = match.User1Id == request.UserId ? match.User2Id : match.User1Id;

                _logger.LogInformation(
                    "Match {MatchId} unmatched by user {UserId} (other user: {OtherUserId}). Reason: {Reason}",
                    matchId, request.UserId, otherUserId, match.UnmatchReason);

                return Ok(new UnmatchResponse
                {
                    Success = true,
                    Message = "Match ended successfully",
                    MatchId = matchId,
                    UnmatchedAt = match.UnmatchedAt.Value
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unmatching match {MatchId} by user {UserId}", matchId, request.UserId);
                return StatusCode(500, "Error ending match");
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

                // Note: Not using AsNoTracking() here because we need to track changes for update
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

        /// <summary>
        /// Delete all matches for a specific user (used during account deletion)
        /// </summary>
        [HttpDelete("user/{userProfileId:int}/matches")]
        [AllowAnonymous]
        public async Task<IActionResult> DeleteUserMatches(int userProfileId)
        {
            try
            {
                _logger.LogInformation("Deleting all matches for user {UserProfileId}", userProfileId);

                // Delete matches where user is either user1 or user2
                var matches = await _context.Matches
                    .Where(m => m.User1Id == userProfileId || m.User2Id == userProfileId)
                    .ToListAsync();

                var count = matches.Count;
                _context.Matches.RemoveRange(matches);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Deleted {Count} matches for user {UserProfileId}", count, userProfileId);
                return Ok(count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting matches for user {UserProfileId}", userProfileId);
                return StatusCode(500, "An error occurred while deleting user matches");
            }
        }
    }
}
