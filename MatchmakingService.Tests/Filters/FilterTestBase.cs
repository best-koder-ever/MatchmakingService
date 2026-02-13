using MatchmakingService.Filters;
using MatchmakingService.Models;

namespace MatchmakingService.Tests.Filters;

/// <summary>Shared test helpers for filter tests.</summary>
public abstract class FilterTestBase
{
    protected static UserProfile CreateUser(int userId = 1, string gender = "Male",
        string preferredGender = "Female", int age = 28, int minAge = 22, int maxAge = 35,
        double lat = 59.33, double lon = 18.07, double maxDistance = 50,
        bool isActive = true, string lookingFor = "Relationship",
        bool isVerified = false, double desirabilityScore = 50.0)
    {
        return new UserProfile
        {
            Id = userId,
            UserId = userId,
            Gender = gender,
            PreferredGender = preferredGender,
            Age = age,
            MinAge = minAge,
            MaxAge = maxAge,
            Latitude = lat,
            Longitude = lon,
            MaxDistance = maxDistance,
            IsActive = isActive,
            LookingFor = lookingFor,
            IsVerified = isVerified,
            DesirabilityScore = desirabilityScore,
            LastActiveAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    protected static FilterContext CreateContext(UserProfile user,
        HashSet<int>? swipedIds = null,
        HashSet<int>? blockedIds = null)
    {
        return new FilterContext(
            user,
            swipedIds ?? new HashSet<int>(),
            blockedIds ?? new HashSet<int>(),
            new CandidateOptions()
        );
    }

    protected static IQueryable<UserProfile> CreateCandidates(params UserProfile[] profiles)
    {
        return profiles.AsQueryable();
    }
}
