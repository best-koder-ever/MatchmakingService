using Xunit;
using Moq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using MatchmakingService.Controllers;
using MatchmakingService.Services;
using MatchmakingService.Data;
using MatchmakingService.Models;
using MatchmakingService.DTOs;
using CoreMatchmakingService = MatchmakingService.Services.MatchmakingService;

namespace MatchmakingService.Tests.Controllers;

/// <summary>
/// Comprehensive tests for MatchmakingController endpoints
/// Tests cover match creation, candidate finding, daily limits, and premium features
/// </summary>
public class MatchmakingControllerTests_Implementation : IDisposable
{
    private readonly MatchmakingDbContext _context;
    private readonly Mock<IUserServiceClient> _mockUserClient;
    private readonly Mock<IAdvancedMatchingService> _mockAdvancedMatching;
    private readonly Mock<INotificationService> _mockNotification;
    private readonly Mock<IDailySuggestionTracker> _mockSuggestionTracker;
    private readonly Mock<ILogger<MatchmakingController>> _mockLogger;
    private readonly MatchmakingController _controller;

    public MatchmakingControllerTests_Implementation()
    {
        var options = new DbContextOptionsBuilder<MatchmakingDbContext>()
            .UseInMemoryDatabase(databaseName: $"MatchmakingControllerTests_{Guid.NewGuid()}")
            .Options;

        _context = new MatchmakingDbContext(options);
        _mockUserClient = new Mock<IUserServiceClient>();
        _mockAdvancedMatching = new Mock<IAdvancedMatchingService>();
        _mockNotification = new Mock<INotificationService>();
        _mockSuggestionTracker = new Mock<IDailySuggestionTracker>();
        _mockLogger = new Mock<ILogger<MatchmakingController>>();

        _controller = new MatchmakingController(
            _mockUserClient.Object,
            null!,
            _mockAdvancedMatching.Object,
            _mockNotification.Object,
            _mockSuggestionTracker.Object,
            _context,
            _mockLogger.Object
        );
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    // === MUTUAL MATCH TESTS ===

    [Fact]
    public async Task HandleMutualMatch_ValidRequest_CreatesMatch()
    {
        // Arrange
        var request = new MutualMatchRequest
        {
            User1Id = 1,
            User2Id = 2,
            CompatibilityScore = 85.5,
            Source = "swipe"
        };

        _mockAdvancedMatching
            .Setup(x => x.CalculateCompatibilityScoreAsync(It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(85.5);

        _mockNotification
            .Setup(x => x.NotifyMatchAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.HandleMutualMatch(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = okResult.Value;
        
        Assert.NotNull(response);
        
        // Verify match was saved to database
        var match = await _context.Matches.FirstOrDefaultAsync();
        Assert.NotNull(match);
        Assert.Equal(1, match.User1Id); // Should be min(1,2)
        Assert.Equal(2, match.User2Id); // Should be max(1,2)
        Assert.Equal(85.5, match.CompatibilityScore);
        Assert.Equal("swipe", match.MatchSource);
        Assert.True(match.IsActive);
        
        // Verify notification was sent
        _mockNotification.Verify(
            x => x.NotifyMatchAsync(1, 2, It.IsAny<int>()), 
            Times.Once
        );
    }

    [Fact]
    public async Task HandleMutualMatch_ReversedUserIds_NormalizesOrder()
    {
        // Arrange - User2Id < User1Id
        var request = new MutualMatchRequest
        {
            User1Id = 5,
            User2Id = 3,
            Source = "suggestion"
        };

        _mockAdvancedMatching
            .Setup(x => x.CalculateCompatibilityScoreAsync(5, 3))
            .ReturnsAsync(75.0);

        // Act
        await _controller.HandleMutualMatch(request);

        // Assert - Should store with smaller ID first
        var match = await _context.Matches.FirstOrDefaultAsync();
        Assert.Equal(3, match!.User1Id);
        Assert.Equal(5, match.User2Id);
    }

    [Fact]
    public async Task HandleMutualMatch_NoProvidedScore_CalculatesCompatibility()
    {
        // Arrange - No score provided
        var request = new MutualMatchRequest
        {
            User1Id = 10,
            User2Id = 20,
            CompatibilityScore = null,
            Source = "swipe"
        };

        _mockAdvancedMatching
            .Setup(x => x.CalculateCompatibilityScoreAsync(10, 20))
            .ReturnsAsync(92.3);

        // Act
        await _controller.HandleMutualMatch(request);

        // Assert
        var match = await _context.Matches.FirstOrDefaultAsync();
        Assert.Equal(92.3, match!.CompatibilityScore);
        
        // Verify calculation was called
        _mockAdvancedMatching.Verify(
            x => x.CalculateCompatibilityScoreAsync(10, 20),
            Times.Once
        );
    }

    // === FIND MATCHES TESTS ===

    [Fact]
    public async Task FindMatches_ValidRequest_ReturnsMatches()
    {
        // Arrange
        var request = new FindMatchesRequest
        {
            UserId = 1,
            Limit = 10,
            MinScore = 60.0,
            IsPremium = false
        };

        var mockMatches = new List<MatchSuggestionResponse>
        {
            new() { UserId = 2, CompatibilityScore = 85.5 },
            new() { UserId = 3, CompatibilityScore = 78.2 }
        };

        _mockSuggestionTracker
            .Setup(x => x.GetStatusAsync(1, false))
            .ReturnsAsync(new DailySuggestionStatus
            {
                SuggestionsShownToday = 5,
                MaxDailySuggestions = 10,
                SuggestionsRemaining = 5,
                NextResetDate = DateTime.UtcNow.AddHours(12)
            });

        _mockAdvancedMatching
            .Setup(x => x.FindMatchesAsync(request))
            .ReturnsAsync(mockMatches);

        // Act
        var result = await _controller.FindMatches(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<FindMatchesResponse>(okResult.Value);

        Assert.Equal(2, response.Count);
        Assert.Equal(2, response.Matches.Count);
        Assert.False(response.DailyLimitReached);
        Assert.False(response.QueueExhausted);
        Assert.Contains("Found 2 compatible profiles", response.Message);
    }

    [Fact]
    public async Task FindMatches_DailyLimitReached_ReturnsEmptyWithMessage()
    {
        // Arrange
        var request = new FindMatchesRequest
        {
            UserId = 1,
            IsPremium = false
        };

        _mockSuggestionTracker
            .Setup(x => x.GetStatusAsync(1, false))
            .ReturnsAsync(new DailySuggestionStatus
            {
                SuggestionsShownToday = 10,
                MaxDailySuggestions = 10,
                SuggestionsRemaining = 0,
                NextResetDate = DateTime.UtcNow.AddHours(8)
            });

        // Act
        var result = await _controller.FindMatches(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<FindMatchesResponse>(okResult.Value);

        Assert.Empty(response.Matches);
        Assert.Equal(0, response.Count);
        Assert.True(response.DailyLimitReached);
        Assert.Equal(0, response.SuggestionsRemaining);
        Assert.Contains("daily limit", response.Message);
        Assert.Contains("10", response.Message); // Shows limit number
        
        // Verify advanced matching was NOT called (short-circuited)
        _mockAdvancedMatching.Verify(
            x => x.FindMatchesAsync(It.IsAny<FindMatchesRequest>()),
            Times.Never
        );
    }

    [Fact]
    public async Task FindMatches_PremiumUser_HigherDailyLimit()
    {
        // Arrange
        var request = new FindMatchesRequest
        {
            UserId = 1,
            IsPremium = true
        };

        _mockSuggestionTracker
            .Setup(x => x.GetStatusAsync(1, true))
            .ReturnsAsync(new DailySuggestionStatus
            {
                SuggestionsShownToday = 40,
                MaxDailySuggestions = 50, // Premium gets 50
                SuggestionsRemaining = 10
            });

        _mockAdvancedMatching
            .Setup(x => x.FindMatchesAsync(request))
            .ReturnsAsync(new List<MatchSuggestionResponse>());

        // Act
        var result = await _controller.FindMatches(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<FindMatchesResponse>(okResult.Value);

        Assert.Equal(10, response.SuggestionsRemaining);
        Assert.False(response.DailyLimitReached);
    }

    [Fact]
    public async Task FindMatches_QueueExhausted_ReturnsAppropriateMessage()
    {
        // Arrange
        var request = new FindMatchesRequest { UserId = 1, IsPremium = false };

        _mockSuggestionTracker
            .Setup(x => x.GetStatusAsync(1, false))
            .ReturnsAsync(new DailySuggestionStatus
            {
                SuggestionsRemaining = 5,
                MaxDailySuggestions = 10
            });

        _mockAdvancedMatching
            .Setup(x => x.FindMatchesAsync(request))
            .ReturnsAsync(new List<MatchSuggestionResponse>()); // Empty queue

        // Act
        var result = await _controller.FindMatches(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<FindMatchesResponse>(okResult.Value);

        Assert.True(response.QueueExhausted);
        Assert.Contains("No more profiles available", response.Message);
        Assert.Contains("broadening your preferences", response.Message);
    }

    [Fact]
    public async Task FindMatches_InvalidUserId_ReturnsBadRequest()
    {
        // Arrange
        var request = new FindMatchesRequest { UserId = 0 };

        // Act
        var result = await _controller.FindMatches(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Invalid user ID", badRequestResult.Value);
        
        // Verify no service calls were made
        _mockSuggestionTracker.Verify(
            x => x.GetStatusAsync(It.IsAny<int>(), It.IsAny<bool>()),
            Times.Never
        );
    }

    [Fact]
    public async Task FindMatches_ExceptionThrown_Returns500()
    {
        // Arrange
        var request = new FindMatchesRequest { UserId = 1 };

        _mockSuggestionTracker
            .Setup(x => x.GetStatusAsync(1, false))
            .ThrowsAsync(new Exception("Database connection failed"));

        // Act
        var result = await _controller.FindMatches(request);

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
        Assert.Equal("Error finding matches", statusResult.Value);
    }

    // === DAILY SUGGESTION STATUS TESTS ===

    [Fact]
    public async Task GetDailySuggestionStatus_ValidRequest_ReturnsStatus()
    {
        // Arrange
        int userId = 1;
        bool isPremium = false;

        _mockSuggestionTracker
            .Setup(x => x.GetStatusAsync(1, false))
            .ReturnsAsync(new DailySuggestionStatus
            {
                SuggestionsShownToday = 7,
                MaxDailySuggestions = 10,
                SuggestionsRemaining = 3,
                LastResetDate = DateTime.UtcNow.Date,
                NextResetDate = DateTime.UtcNow.Date.AddDays(1),
                QueueExhausted = false
            });

        // Act
        var result = await _controller.GetDailySuggestionStatus(userId, isPremium);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<DailySuggestionStatusResponse>(okResult.Value);

        Assert.Equal(7, response.SuggestionsShownToday);
        Assert.Equal(10, response.MaxDailySuggestions);
        Assert.Equal(3, response.SuggestionsRemaining);
        Assert.False(response.IsPremium);
        Assert.Equal("free", response.Tier);
    }

    [Fact]
    public async Task GetDailySuggestionStatus_PremiumUser_ShowsPremiumTier()
    {
        // Arrange
        _mockSuggestionTracker
            .Setup(x => x.GetStatusAsync(1, true))
            .ReturnsAsync(new DailySuggestionStatus
            {
                SuggestionsShownToday = 25,
                MaxDailySuggestions = 50,
                SuggestionsRemaining = 25
            });

        // Act
        var result = await _controller.GetDailySuggestionStatus(1, isPremium: true);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<DailySuggestionStatusResponse>(okResult.Value);

        Assert.True(response.IsPremium);
        Assert.Equal("premium", response.Tier);
        Assert.Equal(50, response.MaxDailySuggestions);
    }

    [Fact]
    public async Task GetDailySuggestionStatus_InvalidUserId_ReturnsBadRequest()
    {
        // Act
        var result = await _controller.GetDailySuggestionStatus(-5);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Invalid user ID", badRequestResult.Value);
    }

    // === GET MY MATCHES TESTS (T001 - New endpoint) ===

    [Fact]
    public async Task GetMyMatches_WithQueryParameter_ReturnsMatches()
    {
        // Arrange - Create test matches in database
        var matches = new List<Match>
        {
            new() { Id = 1, User1Id = 1, User2Id = 2, CompatibilityScore = 85.5, IsActive = true, CreatedAt = DateTime.UtcNow.AddDays(-2) },
            new() { Id = 2, User1Id = 1, User2Id = 3, CompatibilityScore = 78.0, IsActive = true, CreatedAt = DateTime.UtcNow.AddDays(-1) },
            new() { Id = 3, User1Id = 4, User2Id = 1, CompatibilityScore = 92.3, IsActive = true, CreatedAt = DateTime.UtcNow, LastMessageAt = DateTime.UtcNow.AddHours(-2) }
        };

        await _context.Matches.AddRangeAsync(matches);
        await _context.SaveChangesAsync();

        // Simulate userId in query string
        var queryString = new Microsoft.AspNetCore.Http.QueryCollection(
            new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
            {
                { "userId", "1" }
            });
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext()
        };
        _controller.Request.Query = queryString;

        // Act
        var result = await _controller.GetMyMatches();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        dynamic? response = okResult.Value;
        Assert.NotNull(response);
        
        var matchesProperty = response.GetType().GetProperty("Matches");
        var matchesValue = matchesProperty?.GetValue(response) as System.Collections.IEnumerable;
        var matchesList = matchesValue?.Cast<object>().ToList();
        
        Assert.NotNull(matchesList);
        Assert.Equal(3, matchesList.Count); // User 1 has 3 matches
    }

    [Fact]
    public async Task GetMyMatches_OrdersByLastMessageFirst()
    {
        // Arrange - Create matches with different last message times
        var matches = new List<Match>
        {
            new() { Id = 1, User1Id = 1, User2Id = 2, CompatibilityScore = 85.5, IsActive = true, CreatedAt = DateTime.UtcNow.AddDays(-10) },
            new() { Id = 2, User1Id = 1, User2Id = 3, CompatibilityScore = 78.0, IsActive = true, CreatedAt = DateTime.UtcNow.AddDays(-5), LastMessageAt = DateTime.UtcNow.AddHours(-1) }
        };

        await _context.Matches.AddRangeAsync(matches);
        await _context.SaveChangesAsync();

        var queryString = new Microsoft.AspNetCore.Http.QueryCollection(
            new Dictionary<string, Microsoft.Extensions.Primitives.StringValues> { { "userId", "1" } });
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext()
        };
        _controller.Request.Query = queryString;

        // Act
        var result = await _controller.GetMyMatches();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        dynamic? response = okResult.Value;
        Assert.NotNull(response);
        
        var matchesProperty = response.GetType().GetProperty("Matches");
        var matchesValue = matchesProperty?.GetValue(response) as System.Collections.IEnumerable;
        var matchesList = matchesValue?.Cast<object>().ToList();
        
        Assert.NotNull(matchesList);
        Assert.Equal(2, matchesList.Count);
        
        // First match should be the one with recent message (Id = 2)
        var firstMatch = matchesList[0];
        var matchIdProperty = firstMatch.GetType().GetProperty("MatchId");
        var matchId = (int)(matchIdProperty?.GetValue(firstMatch) ?? 0);
        Assert.Equal(2, matchId);
    }

    [Fact]
    public async Task GetMyMatches_ExcludesInactiveByDefault()
    {
        // Arrange
        var matches = new List<Match>
        {
            new() { Id = 1, User1Id = 1, User2Id = 2, CompatibilityScore = 85.5, IsActive = true, CreatedAt = DateTime.UtcNow },
            new() { Id = 2, User1Id = 1, User2Id = 3, CompatibilityScore = 78.0, IsActive = false, CreatedAt = DateTime.UtcNow, UnmatchedAt = DateTime.UtcNow }
        };

        await _context.Matches.AddRangeAsync(matches);
        await _context.SaveChangesAsync();

        var queryString = new Microsoft.AspNetCore.Http.QueryCollection(
            new Dictionary<string, Microsoft.Extensions.Primitives.StringValues> { { "userId", "1" } });
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext()
        };
        _controller.Request.Query = queryString;

        // Act
        var result = await _controller.GetMyMatches(includeInactive: false);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        dynamic? response = okResult.Value;
        Assert.NotNull(response);
        
        var matchesProperty = response.GetType().GetProperty("Matches");
        var matchesValue = matchesProperty?.GetValue(response) as System.Collections.IEnumerable;
        var matchesList = matchesValue?.Cast<object>().ToList();
        
        Assert.NotNull(matchesList);
        Assert.Single(matchesList); // Only active match
    }

    [Fact]
    public async Task GetMyMatches_WithPagination_ReturnsCorrectPage()
    {
        // Arrange - Create 25 matches
        var matches = Enumerable.Range(1, 25).Select(i => new Match
        {
            Id = i,
            User1Id = 1,
            User2Id = i + 100,
            CompatibilityScore = 80.0,
            IsActive = true,
            CreatedAt = DateTime.UtcNow.AddMinutes(-i)
        }).ToList();

        await _context.Matches.AddRangeAsync(matches);
        await _context.SaveChangesAsync();

        var queryString = new Microsoft.AspNetCore.Http.QueryCollection(
            new Dictionary<string, Microsoft.Extensions.Primitives.StringValues> { { "userId", "1" } });
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext()
        };
        _controller.Request.Query = queryString;

        // Act - Request page 2 with 10 items per page
        var result = await _controller.GetMyMatches(page: 2, pageSize: 10);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        dynamic? response = okResult.Value;
        Assert.NotNull(response);
        
        var totalCountProperty = response.GetType().GetProperty("TotalCount");
        var totalCount = (int)(totalCountProperty?.GetValue(response) ?? 0);
        Assert.Equal(25, totalCount);
        
        var pageProperty = response.GetType().GetProperty("Page");
        var page = (int)(pageProperty?.GetValue(response) ?? 0);
        Assert.Equal(2, page);
        
        var matchesProperty = response.GetType().GetProperty("Matches");
        var matchesValue = matchesProperty?.GetValue(response) as System.Collections.IEnumerable;
        var matchesList = matchesValue?.Cast<object>().ToList();
        
        Assert.NotNull(matchesList);
        Assert.Equal(10, matchesList.Count); // Second page should have 10 items
    }

    [Fact]
    public async Task GetMyMatches_NoUserIdInToken_ReturnsBadRequest()
    {
        // Arrange - No userId in query or claims
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext()
        };

        // Act
        var result = await _controller.GetMyMatches();

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequestResult.Value);
    }
}
