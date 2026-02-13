using MatchmakingService.Models;
using MatchmakingService.Strategies;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace MatchmakingService.Controllers
{
    [ApiController]
    [Route("api/matchmaking")]
    public class ProfilesController : ControllerBase
    {
        private readonly StrategyResolver _strategyResolver;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ProfilesController> _logger;

        public ProfilesController(
            StrategyResolver strategyResolver,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<ProfilesController> logger)
        {
            _strategyResolver = strategyResolver;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// GET /api/matchmaking/profiles/{userId}
        /// Returns scored, filtered, ranked candidate profiles for the Discover screen.
        /// T179: Strategy-backed. T180: Optional query params.
        /// </summary>
        [HttpGet("profiles/{userId}")]
        public async Task<IActionResult> GetProfiles(
            string userId,
            [FromQuery] int? limit = null,
            [FromQuery] double? minScore = null,
            [FromQuery] int? activeWithin = null,
            [FromQuery] bool? onlyVerified = null,
            [FromQuery] string? strategy = null)
        {
            try
            {
                // Resolve integer user ID from path (supports Keycloak UUID or integer)
                if (!int.TryParse(userId, out var userIdInt))
                {
                    _logger.LogWarning("Non-integer userId '{UserId}' — strategy requires integer ID", userId);
                    return Ok(new List<object>());
                }

                // Clamp query params (T180 — invalid values → defaults, never error)
                var clampedLimit = Math.Clamp(limit ?? 20, 1, 50);
                var clampedMinScore = Math.Clamp(minScore ?? 0, 0, 100);
                var clampedActiveWithin = activeWithin.HasValue
                    ? Math.Clamp(activeWithin.Value, 1, 365)
                    : (int?)null;

                var request = new CandidateRequest(
                    Limit: clampedLimit,
                    MinScore: clampedMinScore,
                    ActiveWithinDays: clampedActiveWithin,
                    OnlyVerified: onlyVerified ?? false);

                var resolvedStrategy = _strategyResolver.Resolve(strategy);
                var result = await resolvedStrategy.GetCandidatesAsync(userIdInt, request);

                // Map ScoredCandidate → JSON shape matching Flutter MatchCandidate.fromJson
                var response = result.Candidates.Select(c => MapToFlutterShape(c)).ToList();

                _logger.LogInformation(
                    "Returning {Count} candidates for user {UserId} via {Strategy} in {Ms}ms",
                    response.Count, userId, result.StrategyUsed,
                    result.ExecutionTime.TotalMilliseconds);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Strategy pipeline failed for user {UserId}, falling back to legacy", userId);
                return await GetProfilesLegacy(userId);
            }
        }

        /// <summary>
        /// Maps a ScoredCandidate to the JSON shape Flutter expects.
        /// Flutter MatchCandidate.fromJson reads: userId, displayName, age, bio,
        /// city, photoUrl, photoUrls, compatibility/compatibilityScore, interests, etc.
        /// </summary>
        private static object MapToFlutterShape(ScoredCandidate scored)
        {
            var p = scored.Profile;
            return new
            {
                userId = p.UserId,
                id = p.UserId,
                displayName = (string?)null ?? $"User {p.UserId}",
                name = (string?)null ?? $"User {p.UserId}",
                age = p.Age,
                bio = "",
                city = p.City ?? "",
                gender = p.Gender ?? "",
                compatibility = scored.FinalScore,
                compatibilityScore = scored.CompatibilityScore,
                activityScore = scored.ActivityScore,
                desirabilityScore = scored.DesirabilityScore,
                finalScore = scored.FinalScore,
                strategyUsed = scored.StrategyUsed,
                interests = ParseInterests(p.Interests),
                isVerified = p.IsVerified,
                // Empty defaults for fields we don't have at matchmaking level
                photoUrl = (string?)null,
                photoUrls = Array.Empty<string>(),
                prompts = Array.Empty<object>(),
                voicePromptUrl = (string?)null,
                occupation = (string?)null,
                education = (string?)null,
                height = (int?)null,
                distanceKm = (double?)null,
            };
        }

        private static List<string> ParseInterests(string? interests)
        {
            if (string.IsNullOrWhiteSpace(interests)) return new List<string>();
            try
            {
                return JsonSerializer.Deserialize<List<string>>(interests) ?? new List<string>();
            }
            catch
            {
                return interests.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            }
        }

        /// <summary>
        /// Legacy fallback: dumb-proxy to UserService. Only used if strategy pipeline throws.
        /// </summary>
        private async Task<IActionResult> GetProfilesLegacy(string userId)
        {
            try
            {
                var userServiceUrl = _configuration["Services:UserService:BaseUrl"]
                    ?? "http://localhost:8082";

                var client = _httpClientFactory.CreateClient();
                var authHeader = Request.Headers["Authorization"].FirstOrDefault();
                if (!string.IsNullOrEmpty(authHeader))
                    client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", authHeader);

                int? myProfileId = null;
                try
                {
                    var meResponse = await client.GetAsync($"{userServiceUrl}/api/profiles/me");
                    if (meResponse.IsSuccessStatusCode)
                    {
                        var meContent = await meResponse.Content.ReadAsStringAsync();
                        var meDoc = JsonDocument.Parse(meContent);
                        if (meDoc.RootElement.TryGetProperty("data", out var meData) &&
                            meData.TryGetProperty("id", out var meId))
                        {
                            myProfileId = meId.GetInt32();
                        }
                        meDoc.Dispose();
                    }
                }
                catch { /* best effort */ }

                var searchResponse = await client.PostAsync(
                    $"{userServiceUrl}/api/demo/search",
                    new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));

                if (!searchResponse.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Legacy demo search returned {StatusCode}", searchResponse.StatusCode);
                    return Ok(new List<object>());
                }

                var content = await searchResponse.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(content);
                var results = new List<object>();

                JsonElement profileArray;
                bool found = false;

                if (doc.RootElement.TryGetProperty("data", out var data) &&
                    data.TryGetProperty("results", out profileArray))
                    found = true;
                else if (doc.RootElement.TryGetProperty("results", out profileArray))
                    found = true;

                if (found)
                {
                    foreach (var profile in profileArray.EnumerateArray())
                    {
                        if (myProfileId.HasValue &&
                            profile.TryGetProperty("id", out var idProp) &&
                            idProp.GetInt32() == myProfileId.Value)
                            continue;

                        var obj = JsonSerializer.Deserialize<object>(profile.GetRawText());
                        if (obj != null) results.Add(obj);
                    }
                }

                doc.Dispose();

                _logger.LogWarning("Returning {Count} UNSCORED profiles via legacy fallback for user {UserId}",
                    results.Count, userId);

                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Legacy fallback also failed for user {UserId}", userId);
                return StatusCode(500, "Error fetching profiles");
            }
        }
    }
}
