using Xunit;
using Moq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using MatchmakingService.Controllers;
using MatchmakingService.Data;
using MatchmakingService.Models;
using ModelMatch = MatchmakingService.Models.Match;

namespace MatchmakingService.Tests.Controllers;

/// <summary>
/// Tests for UserMatchDeletionController — cascade delete of user matches during account deletion.
/// Called by UserService as service-to-service during account cleanup.
/// </summary>
public class UserMatchDeletionControllerTests : IDisposable
{
    private readonly MatchmakingDbContext _context;
    private readonly Mock<ILogger<UserMatchDeletionController>> _mockLogger;
    private readonly UserMatchDeletionController _controller;

    public UserMatchDeletionControllerTests()
    {
        var options = new DbContextOptionsBuilder<MatchmakingDbContext>()
            .UseInMemoryDatabase(databaseName: $"MatchDeletion_{Guid.NewGuid()}")
            .Options;

        _context = new MatchmakingDbContext(options);
        _mockLogger = new Mock<ILogger<UserMatchDeletionController>>();
        _controller = new UserMatchDeletionController(_context, _mockLogger.Object);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    private async Task SeedMatch(int user1Id, int user2Id)
    {
        _context.Matches.Add(new ModelMatch
        {
            User1Id = user1Id,
            User2Id = user2Id,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();
    }

    [Fact]
    public async Task DeleteUserMatches_WithMatches_RemovesAllAndReturnsCount()
    {
        // Arrange — user 42 has 3 matches
        await SeedMatch(42, 10);
        await SeedMatch(42, 11);
        await SeedMatch(99, 42); // user 42 is User2

        // Act
        var result = await _controller.DeleteUserMatches(42);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("3", okResult.Value);
        Assert.Equal(0, await _context.Matches.CountAsync(m => m.User1Id == 42 || m.User2Id == 42));
    }

    [Fact]
    public async Task DeleteUserMatches_NoMatches_ReturnsZero()
    {
        var result = await _controller.DeleteUserMatches(999);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("0", okResult.Value);
    }

    [Fact]
    public async Task DeleteUserMatches_OnlyDeletesTargetUser()
    {
        // Arrange
        await SeedMatch(1, 10);
        await SeedMatch(2, 20);

        // Act — delete user 1's matches only
        await _controller.DeleteUserMatches(1);

        // Assert — user 2's match remains
        Assert.Equal(1, await _context.Matches.CountAsync());
        Assert.True(await _context.Matches.AnyAsync(m => m.User1Id == 2));
    }

    [Fact]
    public async Task DeleteUserMatches_DeletesBothDirections()
    {
        // user 5 as User1 and as User2
        await SeedMatch(5, 10);
        await SeedMatch(20, 5);

        var result = await _controller.DeleteUserMatches(5);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("2", okResult.Value);
        Assert.Equal(0, await _context.Matches.CountAsync());
    }
}
