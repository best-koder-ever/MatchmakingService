using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Text.Json;

namespace MatchmakingService.Controllers
{
    [ApiController]
    [Route("api/matchmaking")]
    public class ProfilesController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ProfilesController> _logger;

        public ProfilesController(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<ProfilesController> logger)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// GET /api/matchmaking/profiles/{userId}
        /// Returns candidate profiles for the Discover screen.
        /// userId can be a Keycloak UUID or integer profile ID.
        /// </summary>
        [HttpGet("profiles/{userId}")]
        public async Task<IActionResult> GetProfiles(string userId)
        {
            try
            {
                var userServiceUrl = _configuration["Services:UserService:BaseUrl"]
                    ?? "http://localhost:8082";

                var client = _httpClientFactory.CreateClient();
                var authHeader = Request.Headers["Authorization"].FirstOrDefault();
                if (!string.IsNullOrEmpty(authHeader))
                    client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", authHeader);

                // Get the requesting user's integer profile ID via /api/profiles/me
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
                catch { }

                // Fetch all profiles via UserService demo search (returns rich data with photos, prompts, voice)
                var searchResponse = await client.PostAsync(
                    $"{userServiceUrl}/api/demo/search",
                    new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));

                if (!searchResponse.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Demo search returned {StatusCode}", searchResponse.StatusCode);
                    return Ok(new List<object>());
                }

                var content = await searchResponse.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(content);
                var results = new List<object>();

                // DemoController returns SearchResultDto directly (results at top level)
                // Real controller wraps in ApiResponse (data.results)
                JsonElement profileArray;
                bool found = false;

                // Try ApiResponse wrapper first: { data: { results: [...] } }
                if (doc.RootElement.TryGetProperty("data", out var data) &&
                    data.TryGetProperty("results", out profileArray))
                {
                    found = true;
                }
                // Try direct SearchResultDto: { results: [...] }
                else if (doc.RootElement.TryGetProperty("results", out profileArray))
                {
                    found = true;
                }

                if (found)
                {
                    foreach (var profile in profileArray.EnumerateArray())
                    {
                        if (myProfileId.HasValue &&
                            profile.TryGetProperty("id", out var idProp) &&
                            idProp.GetInt32() == myProfileId.Value)
                        {
                            continue;
                        }

                        var obj = JsonSerializer.Deserialize<object>(profile.GetRawText());
                        if (obj != null) results.Add(obj);
                    }
                }

                doc.Dispose();

                _logger.LogInformation("Returning {Count} candidate profiles for user {UserId}",
                    results.Count, userId);

                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching profiles for user {UserId}", userId);
                return StatusCode(500, "Error fetching profiles");
            }
        }
    }
}
