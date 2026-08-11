using MatchmakingService.Data;
using MatchmakingService.Models;
using Microsoft.EntityFrameworkCore;

namespace MatchmakingService.Services;

/// <summary>
/// Syncs user profiles from UserService into the local MatchmakingDbContext.
/// Solves the "LiveScoring: user X not found or inactive" warning that
/// happens when a user has been provisioned in UserService (e.g. by the bot
/// provisioner) but never yet replicated to the matchmaking DB.
///
/// Strategy: pull-on-demand. When a candidate strategy can't find a user,
/// it calls <see cref="EnsureUserAsync"/>. We GET from UserService, upsert
/// into <c>MatchmakingDbContext.UserProfiles</c>, and return the local row.
/// </summary>
public interface IUserProfileSyncService
{
    /// <summary>
    /// Ensures the given userId exists locally. If missing, fetches from
    /// UserService and upserts. Returns null if UserService also has no record.
    /// </summary>
    Task<UserProfile?> EnsureUserAsync(int userId, CancellationToken ct = default);
}

public class UserProfileSyncService : IUserProfileSyncService
{
    private readonly MatchmakingDbContext _context;
    private readonly IUserServiceClient _userServiceClient;
    private readonly ILogger<UserProfileSyncService> _logger;

    public UserProfileSyncService(
        MatchmakingDbContext context,
        IUserServiceClient userServiceClient,
        ILogger<UserProfileSyncService> logger)
    {
        _context = context;
        _userServiceClient = userServiceClient;
        _logger = logger;
    }

    public async Task<UserProfile?> EnsureUserAsync(int userId, CancellationToken ct = default)
    {
        var local = await _context.UserProfiles
            .FirstOrDefaultAsync(u => u.UserId == userId, ct);

        if (local != null && local.IsActive)
        {
            return local;
        }

        UserProfile? remote;
        try
        {
            remote = await _userServiceClient.GetUserProfileAsync(userId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "UserProfileSync: UserService GET failed for userId={UserId}", userId);
            return local;
        }

        if (remote == null)
        {
            _logger.LogInformation("UserProfileSync: UserService has no profile for userId={UserId}", userId);
            return local;
        }

        if (local == null)
        {
            // Insert
            remote.Id = 0; // let EF allocate
            remote.UserId = userId;
            remote.IsActive = true;
            _context.UserProfiles.Add(remote);
            _logger.LogInformation("UserProfileSync: inserted userId={UserId}", userId);
        }
        else
        {
            // Update key fields. Don't overwrite Id/UserId.
            local.Gender = remote.Gender;
            local.Age = remote.Age;
            local.Latitude = remote.Latitude;
            local.Longitude = remote.Longitude;
            local.City = remote.City;
            local.State = remote.State;
            local.Country = remote.Country;
            local.PreferredGender = remote.PreferredGender;
            local.MinAge = remote.MinAge;
            local.MaxAge = remote.MaxAge;
            local.MaxDistance = remote.MaxDistance;
            local.Interests = remote.Interests;
            local.IsActive = true;
            _logger.LogInformation("UserProfileSync: refreshed userId={UserId}", userId);
        }

        await _context.SaveChangesAsync(ct);
        return await _context.UserProfiles.FirstOrDefaultAsync(u => u.UserId == userId, ct);
    }
}
