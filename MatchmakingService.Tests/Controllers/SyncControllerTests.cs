using Xunit;
using Moq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using MatchmakingService.Controllers;
using MatchmakingService.Data;
using MatchmakingService.Models;

namespace MatchmakingService.Tests.Controllers;

/// <summary>
/// Tests for SyncController — internal activity-ping endpoint for updating user LastActiveAt.
/// Called by YARP gateway to keep matchmaking recency data fresh.
/// </summary>
public class SyncControllerTests : IDisposable
{
    private readonly MatchmakingDbContext _context;
    private readonly Mock<ILogger<SyncController>> _mockLogger;
    private readonly SyncController _controller;

    public SyncControllerTests()
    {
        var options = new DbContextOptionsBuilder<MatchmakingDbContext>()
            .UseInMemoryDatabase(databaseName: $"Sync_{Guid.NewGuid()}")
            .Options;

        _context = new MatchmakingDbContext(options);
        _mockLogger = new Mock<ILogger<SyncController>>();
        _controller = new SyncController(_context, _mockLogger.Object);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    private async Task<UserProfile> SeedProfile(int userId)
    {
        var profile = new UserProfile
        {
            UserId = userId,
            Gender = "Male",
            Age = 28,
            LastActiveAt = DateTime.UtcNow.AddDays(-7),
            CreatedAt = DateTime.UtcNow
        };
        _context.UserProfiles.Add(profile);
        await _context.SaveChangesAsync();
        return profile;
    }

    [Fact]
    public async Task ActivityPing_ExistingUser_UpdatesLastActiveAt()
    {
        var profile = await SeedProfile(42);
        var oldTimestamp = profile.LastActiveAt;

        var request = new ActivityPingRequest { UserId = 42 };
        var result = await _controller.ActivityPing(request);

        Assert.IsType<OkResult>(result);
        var updated = await _context.UserProfiles.FirstAsync(p => p.UserId == 42);
        Assert.True(updated.LastActiveAt > oldTimestamp);
    }

    [Fact]
    public async Task ActivityPing_WithExplicitTimestamp_UsesProvidedTime()
    {
        await SeedProfile(42);
        var specificTime = new DateTime(2025, 6, 15, 12, 0, 0, DateTimeKind.Utc);

        var request = new ActivityPingRequest { UserId = 42, LastActiveAt = specificTime };
        await _controller.ActivityPing(request);

        var updated = await _context.UserProfiles.FirstAsync(p => p.UserId == 42);
        Assert.Equal(specificTime, updated.LastActiveAt);
    }

    [Fact]
    public async Task ActivityPing_UnknownUser_ReturnsNotFound()
    {
        var request = new ActivityPingRequest { UserId = 999 };
        var result = await _controller.ActivityPing(request);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task ActivityPing_InvalidUserId_ReturnsBadRequest()
    {
        var request = new ActivityPingRequest { UserId = 0 };
        var result = await _controller.ActivityPing(request);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task ActivityPingBatch_UpdatesMultipleUsers()
    {
        await SeedProfile(1);
        await SeedProfile(2);
        await SeedProfile(3);

        var requests = new List<ActivityPingRequest>
        {
            new() { UserId = 1 },
            new() { UserId = 2 },
            new() { UserId = 3 }
        };

        var result = await _controller.ActivityPingBatch(requests);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var updated = GetProperty<int>(okResult.Value!, "updated");
        Assert.Equal(3, updated);
    }

    [Fact]
    public async Task ActivityPingBatch_SkipsMissingUsers()
    {
        await SeedProfile(1);
        // userId 999 doesn't exist

        var requests = new List<ActivityPingRequest>
        {
            new() { UserId = 1 },
            new() { UserId = 999 }
        };

        var result = await _controller.ActivityPingBatch(requests);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var updated = GetProperty<int>(okResult.Value!, "updated");
        Assert.Equal(1, updated);
    }

    [Fact]
    public async Task ActivityPingBatch_EmptyList_ReturnsBadRequest()
    {
        var result = await _controller.ActivityPingBatch(new List<ActivityPingRequest>());

        Assert.IsType<BadRequestObjectResult>(result);
    }

    private static T GetProperty<T>(object obj, string name)
    {
        var prop = obj.GetType().GetProperty(name);
        Assert.NotNull(prop);
        return (T)prop.GetValue(obj)!;
    }
}
