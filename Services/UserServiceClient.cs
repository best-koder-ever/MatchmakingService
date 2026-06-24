using MatchmakingService.Models;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace MatchmakingService.Services
{
    public interface IUserServiceClient
    {
        Task<UserProfile?> GetUserProfileAsync(int userId);
        Task<bool> IsPremiumAsync(string keycloakUserId);
        /// <summary>Returns cosine similarity [0,1] between two users' reflection vectors, or null if unavailable.</summary>
        Task<double?> GetVectorSimilarityAsync(string keycloakIdA, string keycloakIdB);
    }

    public class UserServiceClient : IUserServiceClient
    {
        private readonly HttpClient _httpClient;

        public UserServiceClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<UserProfile?> GetUserProfileAsync(int userId)
        {
            var response = await _httpClient.GetAsync($"/api/user/userprofiles/{userId}");
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<UserProfile>();
            }
            return null;
        }

        public async Task<bool> IsPremiumAsync(string keycloakUserId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"/api/billing/internal-status?userId={keycloakUserId}");
                if (!response.IsSuccessStatusCode) return false;
                var json = await response.Content.ReadFromJsonAsync<BillingStatusResponse>();
                return json?.IsPremium ?? false;
            }
            catch
            {
                return false;
            }
        }

        private record BillingStatusResponse(string UserId, string Tier, DateTime? ExpiresAt, bool IsPremium, int SparksBalance);

        public async Task<double?> GetVectorSimilarityAsync(string keycloakIdA, string keycloakIdB)
        {
            try
            {
                // Caller authenticates using the service-to-service token (no user token in this context).
                // For simplicity we use a direct HTTP call; the UserService endpoint does not require auth
                // in internal calls (add an internal-auth header if needed).
                var resp = await _httpClient.GetAsync(
                    $"/api/psykolog/vector-similarity/{Uri.EscapeDataString(keycloakIdB)}" +
                    $"?callerId={Uri.EscapeDataString(keycloakIdA)}");
                if (!resp.IsSuccessStatusCode) return null;
                using var doc = System.Text.Json.JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
                if (doc.RootElement.TryGetProperty("similarity", out var simEl) &&
                    simEl.ValueKind == System.Text.Json.JsonValueKind.Number)
                    return simEl.GetDouble();
                return null;
            }
            catch { return null; }
        }
    }
}