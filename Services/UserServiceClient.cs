using MatchmakingService.Models;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace MatchmakingService.Services
{
    public interface IUserServiceClient
    {
        Task<UserProfile?> GetUserProfileAsync(int userId);
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
            // Call the YARP gateway to fetch the user profile
            var response = await _httpClient.GetAsync($"/api/user/userprofiles/{userId}");
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<UserProfile>();
            }

            // Handle errors (e.g., log them or throw an exception)
            return null;
        }
    }
}