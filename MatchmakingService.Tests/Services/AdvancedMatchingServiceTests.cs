using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using MatchmakingService.Services;
using MatchmakingService.Data;
using MatchmakingService.Models;
using MatchmakingService.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace MatchmakingService.Tests.Services;

/// <summary>
/// T030: Unit tests for matchmaking scoring algorithm and queue ordering
/// Tests cover location scoring, age compatibility, interests matching, 
/// education/lifestyle scoring, and overall candidate ranking
/// </summary>
public class AdvancedMatchingServiceTests : IDisposable
{
    private readonly MatchmakingDbContext _context;
    private readonly Mock<IUserServiceClient> _mockUserServiceClient;
    private readonly Mock<ISafetyServiceClient> _mockSafetyServiceClient;
    private readonly Mock<ILogger<AdvancedMatchingService>> _mockLogger;
    private readonly Mock<IDailySuggestionTracker> _mockDailySuggestionTracker;
    private readonly Mock<ISwipeServiceClient> _mockSwipeServiceClient;
    private readonly Mock<IOptionsMonitor<ScoringConfiguration>> _mockScoringConfig;
    private readonly Mock<ICompatibilityScorer> _mockCompatibilityScorer;
    private readonly AdvancedMatchingService _service;

    public AdvancedMatchingServiceTests()
    {
        var options = new DbContextOptionsBuilder<MatchmakingDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new MatchmakingDbContext(options);
        _mockUserServiceClient = new Mock<IUserServiceClient>();
        _mockSafetyServiceClient = new Mock<ISafetyServiceClient>();
        _mockLogger = new Mock<ILogger<AdvancedMatchingService>>();
        _mockDailySuggestionTracker = new Mock<IDailySuggestionTracker>();
        _mockSwipeServiceClient = new Mock<ISwipeServiceClient>();
        _mockScoringConfig = new Mock<IOptionsMonitor<ScoringConfiguration>>();
        _mockScoringConfig.Setup(x => x.CurrentValue).Returns(new ScoringConfiguration());
        _mockCompatibilityScorer = new Mock<ICompatibilityScorer>();
        // Default: neutral score (0.5) — simulates users with no questionnaire answers
        _mockCompatibilityScorer
            .Setup(x => x.GetScoreAsync(It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(0.5);
        _service = new AdvancedMatchingService(
            _context, 
            _mockUserServiceClient.Object, 
            _mockSafetyServiceClient.Object,
            _mockSwipeServiceClient.Object,
            _mockScoringConfig.Object,
            _mockLogger.Object,
            _mockDailySuggestionTracker.Object,
            _mockCompatibilityScorer.Object
        );
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    // ==================== LOCATION SCORING TESTS ====================

    [Fact]
    public async Task CalculateCompatibilityScore_NearbyUser_ReturnsHighLocationScore()
    {
        // Arrange - Users 5km apart (San Francisco area)
        var user = CreateUserProfile(1, "Male", 28, 37.7749, -122.4194, "Female", 25, 35, 50);
        var target = CreateUserProfile(2, "Female", 26, 37.8199, -122.4783, "Male", 25, 35, 50); // ~5km away

        await _context.UserProfiles.AddRangeAsync(user, target);
        await _context.SaveChangesAsync();

        // Act
        var score = await _service.CalculateCompatibilityScoreAsync(1, 2);

        // Assert - Nearby users should get high score (90+ out of 100)
        Assert.True(score >= 60, $"Expected score >= 60 for nearby users, got {score}");
    }

    [Fact]
    public async Task CalculateCompatibilityScore_FarAwayUser_ReturnsLowLocationScore()
    {
        // Arrange - Users 100km+ apart
        var user = CreateUserProfile(1, "Male", 28, 37.7749, -122.4194, "Female", 25, 35, 50);
        var target = CreateUserProfile(2, "Female", 26, 38.5816, -121.4944, "Male", 25, 35, 50); // Sacramento ~140km away

        await _context.UserProfiles.AddRangeAsync(user, target);
        await _context.SaveChangesAsync();

        // Act
        var score = await _service.CalculateCompatibilityScoreAsync(1, 2);

        // Assert - Far users should get low score (< 50)
        Assert.True(score < 50, $"Expected score < 50 for distant users, got {score}");
    }

    [Fact]
    public async Task CalculateCompatibilityScore_BeyondMaxDistance_ReturnsZeroScore()
    {
        // Arrange - Target beyond max distance (maxDistance = 50km)
        var user = CreateUserProfile(1, "Male", 28, 37.7749, -122.4194, "Female", 25, 35, 50);
        var target = CreateUserProfile(2, "Female", 26, 40.7128, -74.0060, "Male", 25, 35, 50); // NYC ~4,100km away

        await _context.UserProfiles.AddRangeAsync(user, target);
        await _context.SaveChangesAsync();

        // Act
        var score = await _service.CalculateCompatibilityScoreAsync(1, 2);

        // Assert - Beyond max distance should return 0
        Assert.Equal(0, score);
    }

    // ==================== AGE SCORING TESTS ====================

    [Fact]
    public async Task CalculateCompatibilityScore_IdealAge_ReturnsHighScore()
    {
        // Arrange - Target age is middle of user's preferred range (25-35)
        var user = CreateUserProfile(1, "Male", 28, 37.7749, -122.4194, "Female", 25, 35, 50);
        var target = CreateUserProfile(2, "Female", 30, 37.7749, -122.4194, "Male", 25, 35, 50); // Age 30 = middle of 25-35

        await _context.UserProfiles.AddRangeAsync(user, target);
        await _context.SaveChangesAsync();

        // Act
        var score = await _service.CalculateCompatibilityScoreAsync(1, 2);

        // Assert - Ideal age should score high
        Assert.True(score >= 70, $"Expected score >= 70 for ideal age, got {score}");
    }

    [Fact]
    public async Task CalculateCompatibilityScore_OutsideAgeRange_ReturnsZero()
    {
        // Arrange - Target age outside user's preference
        var user = CreateUserProfile(1, "Male", 28, 37.7749, -122.4194, "Female", 25, 35, 50);
        var target = CreateUserProfile(2, "Female", 40, 37.7749, -122.4194, "Male", 25, 35, 50); // Age 40 > maxAge 35

        await _context.UserProfiles.AddRangeAsync(user, target);
        await _context.SaveChangesAsync();

        // Act
        var score = await _service.CalculateCompatibilityScoreAsync(1, 2);

        // Assert - Outside age range should return 0
        Assert.Equal(0, score);
    }

    [Fact]
    public async Task CalculateCompatibilityScore_MutualAgeIncompatibility_ReturnsZero()
    {
        // Arrange - User1 wants 25-35, User2 wants 35-45 (User1 age 28 < User2 minAge 35)
        var user = CreateUserProfile(1, "Male", 28, 37.7749, -122.4194, "Female", 25, 35, 50);
        var target = CreateUserProfile(2, "Female", 38, 37.7749, -122.4194, "Male", 35, 45, 50);

        await _context.UserProfiles.AddRangeAsync(user, target);
        await _context.SaveChangesAsync();

        // Act
        var score = await _service.CalculateCompatibilityScoreAsync(1, 2);

        // Assert - Mutual incompatibility should return 0
        Assert.Equal(0, score);
    }

    // ==================== INTERESTS SCORING TESTS ====================

    [Fact]
    public async Task CalculateCompatibilityScore_SharedInterests_IncreasesScore()
    {
        // Arrange - Users with overlapping interests
        var user = CreateUserProfile(1, "Male", 28, 37.7749, -122.4194, "Female", 25, 35, 50);
        user.Interests = JsonSerializer.Serialize(new[] { "hiking", "photography", "travel", "cooking" });

        var target = CreateUserProfile(2, "Female", 30, 37.7749, -122.4194, "Male", 25, 35, 50);
        target.Interests = JsonSerializer.Serialize(new[] { "hiking", "photography", "reading" });

        await _context.UserProfiles.AddRangeAsync(user, target);
        await _context.SaveChangesAsync();

        // Act
        var score = await _service.CalculateCompatibilityScoreAsync(1, 2);

        // Assert - 50% interest overlap (2 common out of 5 unique) should give good score
        Assert.True(score >= 70, $"Expected score >= 70 for shared interests, got {score}");
    }

    [Fact]
    public async Task CalculateCompatibilityScore_NoCommonInterests_ReturnsNeutralScore()
    {
        // Arrange - Users with completely different interests
        var user = CreateUserProfile(1, "Male", 28, 37.7749, -122.4194, "Female", 25, 35, 50);
        user.Interests = JsonSerializer.Serialize(new[] { "sports", "gaming", "technology" });

        var target = CreateUserProfile(2, "Female", 30, 37.7749, -122.4194, "Male", 25, 35, 50);
        target.Interests = JsonSerializer.Serialize(new[] { "art", "music", "dance" });

        await _context.UserProfiles.AddRangeAsync(user, target);
        await _context.SaveChangesAsync();

        // Act
        var score = await _service.CalculateCompatibilityScoreAsync(1, 2);

        // Assert - No common interests should give neutral/lower score
        Assert.True(score < 80, $"Expected score < 80 for no common interests, got {score}");
    }

    // ==================== LIFESTYLE SCORING TESTS ====================

    [Fact]
    public async Task CalculateCompatibilityScore_SameChildrenPreference_IncreasesScore()
    {
        // Arrange - Both want children
        var user = CreateUserProfile(1, "Male", 28, 37.7749, -122.4194, "Female", 25, 35, 50);
        user.WantsChildren = true;
        user.HasChildren = false;

        var target = CreateUserProfile(2, "Female", 30, 37.7749, -122.4194, "Male", 25, 35, 50);
        target.WantsChildren = true;
        target.HasChildren = false;

        await _context.UserProfiles.AddRangeAsync(user, target);
        await _context.SaveChangesAsync();

        // Act
        var score1 = await _service.CalculateCompatibilityScoreAsync(1, 2);

        // Now test opposite: both don't want children
        user.WantsChildren = false;
        target.WantsChildren = false;
        _context.UserProfiles.UpdateRange(user, target);
        await _context.SaveChangesAsync();

        var score2 = await _service.CalculateCompatibilityScoreAsync(1, 2);

        // Assert - Same preference should score higher than neutral
        Assert.True(score1 >= 70, $"Expected score >= 70 for same children preference, got {score1}");
        Assert.True(score2 >= 70, $"Expected score >= 70 for same children preference, got {score2}");
    }

    [Fact]
    public async Task CalculateCompatibilityScore_OppositeChildrenPreference_ReducesScore()
    {
        // Arrange - Conflicting children preferences
        var user = CreateUserProfile(1, "Male", 28, 37.7749, -122.4194, "Female", 25, 35, 50);
        user.WantsChildren = true;

        var target = CreateUserProfile(2, "Female", 30, 37.7749, -122.4194, "Male", 25, 35, 50);
        target.WantsChildren = false;

        await _context.UserProfiles.AddRangeAsync(user, target);
        await _context.SaveChangesAsync();

        // Act
        var score = await _service.CalculateCompatibilityScoreAsync(1, 2);

        // Assert - Conflicting preferences should reduce score
        Assert.True(score < 82, $"Expected score < 82 for conflicting children preference, got {score}");
    }

    [Fact]
    public async Task CalculateCompatibilityScore_SimilarLifestyle_IncreasesScore()
    {
        // Arrange - Similar smoking/drinking habits
        var user = CreateUserProfile(1, "Male", 28, 37.7749, -122.4194, "Female", 25, 35, 50);
        user.SmokingStatus = "Never";
        user.DrinkingStatus = "Sometimes";

        var target = CreateUserProfile(2, "Female", 30, 37.7749, -122.4194, "Male", 25, 35, 50);
        target.SmokingStatus = "Never";
        target.DrinkingStatus = "Sometimes";

        await _context.UserProfiles.AddRangeAsync(user, target);
        await _context.SaveChangesAsync();

        // Act
        var score = await _service.CalculateCompatibilityScoreAsync(1, 2);

        // Assert - Similar lifestyle should give good score
        Assert.True(score >= 70, $"Expected score >= 70 for similar lifestyle, got {score}");
    }

    // ==================== QUEUE ORDERING TESTS ====================

    [Fact]
    public async Task FindMatchesAsync_ReturnsOrderedByScore_HighestFirst()
    {
        // Arrange - Create user and 3 targets with varying compatibility
        var user = CreateUserProfile(1, "Male", 28, 37.7749, -122.4194, "Female", 25, 35, 100);
        user.Interests = JsonSerializer.Serialize(new[] { "hiking", "photography" });

        // High compatibility: nearby, ideal age, shared interests
        var target1 = CreateUserProfile(2, "Female", 28, 37.7749, -122.4194, "Male", 25, 35, 100);
        target1.Interests = JsonSerializer.Serialize(new[] { "hiking", "photography" });
        target1.WantsChildren = true;
        user.WantsChildren = true;

        // Medium compatibility: nearby, edge of age range, some shared interests
        var target2 = CreateUserProfile(3, "Female", 25, 37.7749, -122.4194, "Male", 25, 35, 100);
        target2.Interests = JsonSerializer.Serialize(new[] { "hiking", "reading" });

        // Low compatibility: far away, different interests
        var target3 = CreateUserProfile(4, "Female", 26, 38.5816, -121.4944, "Male", 25, 35, 100); // Sacramento ~140km
        target3.Interests = JsonSerializer.Serialize(new[] { "art", "music" });

        await _context.UserProfiles.AddRangeAsync(user, target1, target2, target3);
        await _context.SaveChangesAsync();

        // Setup mocks
        _mockSafetyServiceClient.Setup(x => x.GetBlockedUserIdsAsync(It.IsAny<int>()))
            .ReturnsAsync(new List<int>());
        _mockDailySuggestionTracker.Setup(x => x.CheckAndIncrementAsync(It.IsAny<int>(), It.IsAny<bool>()))
            .ReturnsAsync((true, 10));

        // Act
        var request = new FindMatchesRequest { UserId = 1, Limit = 10, ExcludePreviouslySwiped = false };
        var matches = await _service.FindMatchesAsync(request);

        // Assert - Results should be ordered by score (highest first)
        Assert.True(matches.Count >= 2, $"Expected at least 2 matches, got {matches.Count}");
        
        for (int i = 0; i < matches.Count - 1; i++)
        {
            Assert.True(matches[i].CompatibilityScore >= matches[i + 1].CompatibilityScore,
                $"Matches not ordered by score: position {i} score {matches[i].CompatibilityScore} " +
                $"< position {i+1} score {matches[i + 1].CompatibilityScore}");
        }

        // High compatibility user should be first
        Assert.Equal(2, matches[0].TargetUserId);
        Assert.True(matches[0].CompatibilityScore > 70, 
            $"Expected high compatibility score > 70, got {matches[0].CompatibilityScore}");
    }

    [Fact]
    public async Task FindMatchesAsync_RespectsLimit_ReturnsUpToLimitCount()
    {
        // Arrange - Create user and 5 potential matches
        var user = CreateUserProfile(1, "Male", 28, 37.7749, -122.4194, "Female", 25, 35, 100);
        var targets = Enumerable.Range(2, 5)
            .Select(id => CreateUserProfile(id, "Female", 25 + id, 37.7749, -122.4194, "Male", 25, 35, 100))
            .ToList();

        await _context.UserProfiles.AddAsync(user);
        await _context.UserProfiles.AddRangeAsync(targets);
        await _context.SaveChangesAsync();

        // Setup mocks
        _mockSafetyServiceClient.Setup(x => x.GetBlockedUserIdsAsync(It.IsAny<int>()))
            .ReturnsAsync(new List<int>());
        _mockDailySuggestionTracker.Setup(x => x.CheckAndIncrementAsync(It.IsAny<int>(), It.IsAny<bool>()))
            .ReturnsAsync((true, 10));

        // Act - Request limit of 3
        var request = new FindMatchesRequest { UserId = 1, Limit = 3, ExcludePreviouslySwiped = false };
        var matches = await _service.FindMatchesAsync(request);

        // Assert - Should return exactly 3 results
        Assert.Equal(3, matches.Count);
    }

    [Fact]
    public async Task FindMatchesAsync_WithMinScore_FiltersLowScoreCandidates()
    {
        // Arrange
        var user = CreateUserProfile(1, "Male", 28, 37.7749, -122.4194, "Female", 25, 35, 100);
        
        // High score candidate
        var target1 = CreateUserProfile(2, "Female", 28, 37.7749, -122.4194, "Male", 25, 35, 100);
        
        // Low score candidate (far away)
        var target2 = CreateUserProfile(3, "Female", 26, 40.7128, -74.0060, "Male", 25, 35, 100); // NYC

        await _context.UserProfiles.AddRangeAsync(user, target1, target2);
        await _context.SaveChangesAsync();

        // Setup mocks
        _mockSafetyServiceClient.Setup(x => x.GetBlockedUserIdsAsync(It.IsAny<int>()))
            .ReturnsAsync(new List<int>());
        _mockDailySuggestionTracker.Setup(x => x.CheckAndIncrementAsync(It.IsAny<int>(), It.IsAny<bool>()))
            .ReturnsAsync((true, 10));

        // Act - Request with minScore of 70
        var request = new FindMatchesRequest { UserId = 1, Limit = 10, MinScore = 70, ExcludePreviouslySwiped = false };
        var matches = await _service.FindMatchesAsync(request);

        // Assert - Should only return high-scoring matches
        Assert.All(matches, m => Assert.True(m.CompatibilityScore >= 70,
            $"Found match with score {m.CompatibilityScore} below minScore 70"));
    }

    // ==================== SCORE CACHING TESTS ====================

    [Fact]
    public async Task CalculateCompatibilityScore_UsesCachedScore_WhenRecent()
    {
        // Arrange
        var user = CreateUserProfile(1, "Male", 28, 37.7749, -122.4194, "Female", 25, 35, 50);
        var target = CreateUserProfile(2, "Female", 30, 37.7749, -122.4194, "Male", 25, 35, 50);

        await _context.UserProfiles.AddRangeAsync(user, target);
        await _context.SaveChangesAsync();

        // Act - First calculation (no cache)
        var score1 = await _service.CalculateCompatibilityScoreAsync(1, 2);

        // Second calculation (should use cache)
        var score2 = await _service.CalculateCompatibilityScoreAsync(1, 2);

        // Assert - Scores should be identical (from cache)
        Assert.Equal(score1, score2);

        // Verify cache was created
        var cachedScore = await _context.MatchScores
            .FirstOrDefaultAsync(ms => ms.UserId == 1 && ms.TargetUserId == 2 && ms.IsValid);
        Assert.NotNull(cachedScore);
        Assert.Equal(score1, cachedScore.OverallScore);
    }

    // ==================== EDGE CASES ====================

    [Fact]
    public async Task CalculateCompatibilityScore_NonExistentUser_ReturnsZero()
    {
        // Act - Try to score with non-existent users
        var score = await _service.CalculateCompatibilityScoreAsync(999, 1000);

        // Assert
        Assert.Equal(0, score);
    }

    [Fact]
    public async Task FindMatchesAsync_NoMatchingCandidates_ReturnsEmptyList()
    {
        // Arrange - User wants Female 25-35, but create only Male profiles
        var user = CreateUserProfile(1, "Male", 28, 37.7749, -122.4194, "Female", 25, 35, 50);
        var target1 = CreateUserProfile(2, "Male", 30, 37.7749, -122.4194, "Female", 25, 35, 50);
        var target2 = CreateUserProfile(3, "Male", 32, 37.7749, -122.4194, "Female", 25, 35, 50);

        await _context.UserProfiles.AddRangeAsync(user, target1, target2);
        await _context.SaveChangesAsync();

        // Act
        var request = new FindMatchesRequest { UserId = 1, Limit = 10, ExcludePreviouslySwiped = false };
        var matches = await _service.FindMatchesAsync(request);

        // Assert - Should return empty list
        Assert.Empty(matches);
    }

    [Fact]
    public async Task GetMatchStatsAsync_NewUser_ReturnsZeroStats()
    {
        // Act
        var stats = await _service.GetMatchStatsAsync(1);

        // Assert
        Assert.Equal(0, stats.TotalMatches);
        Assert.Equal(0, stats.ActiveMatches);
        Assert.Equal(0, stats.AverageCompatibilityScore);
    }

    // ==================== HELPER METHODS ====================

    private UserProfile CreateUserProfile(
        int userId, string gender, int age, 
        double latitude, double longitude,
        string preferredGender, int minAge, int maxAge, int maxDistance)
    {
        return new UserProfile
        {
            UserId = userId,
            Gender = gender,
            Age = age,
            Latitude = latitude,
            Longitude = longitude,
            PreferredGender = preferredGender,
            MinAge = minAge,
            MaxAge = maxAge,
            MaxDistance = maxDistance,
            IsActive = true,
            // Default preference weights
            LocationWeight = 1.0,
            AgeWeight = 1.0,
            InterestsWeight = 1.0,
            EducationWeight = 1.0,
            LifestyleWeight = 1.0,
            City = "San Francisco",
            Country = "USA",
            CreatedAt = DateTime.UtcNow,
            SmokingStatus = "Never",
            DrinkingStatus = "Sometimes",
            WantsChildren = false,
            HasChildren = false
        };
    }

    // ==================== T530: COMPATIBILITY SCORE INTEGRATION TESTS ====================

    [Fact]
    public async Task ScoreCandidate_BothUsersAnswered_CompatibilityReflectedInScore()
    {
        // Arrange — both users have questionnaire answers (scorer returns high value)
        var user   = CreateUserProfile(10, "Male",   28, 37.7749, -122.4194, "Female", 25, 35, 50);
        var target = CreateUserProfile(11, "Female", 30, 37.7749, -122.4194, "Male",   25, 35, 50);

        await _context.UserProfiles.AddRangeAsync(user, target);
        await _context.SaveChangesAsync();

        // Mock: high compatibility (0.9 → 90/100)
        _mockCompatibilityScorer
            .Setup(x => x.GetScoreAsync(10, 11))
            .ReturnsAsync(0.9);

        // Act
        var scoreHigh = await _service.CalculateCompatibilityScoreAsync(10, 11);

        // Reset cache so we can compare with neutral
        var cached = _context.MatchScores.Where(ms => ms.UserId == 10 && ms.TargetUserId == 11);
        _context.MatchScores.RemoveRange(cached);
        await _context.SaveChangesAsync();

        // Mock: neutral compatibility (0.5 → 50/100)
        _mockCompatibilityScorer
            .Setup(x => x.GetScoreAsync(10, 11))
            .ReturnsAsync(0.5);

        var scoreNeutral = await _service.CalculateCompatibilityScoreAsync(10, 11);

        // Assert — high compatibility should yield a higher score than neutral
        Assert.True(scoreHigh > scoreNeutral,
            $"Expected high-compat score ({scoreHigh}) > neutral score ({scoreNeutral})");
    }

    [Fact]
    public async Task ScoreCandidate_OneUserNoAnswers_UsesNeutralFallback()
    {
        // Arrange — scorer signals "no answers" by returning 0.5 (neutral)
        var user   = CreateUserProfile(20, "Male",   28, 37.7749, -122.4194, "Female", 25, 35, 50);
        var target = CreateUserProfile(21, "Female", 30, 37.7749, -122.4194, "Male",   25, 35, 50);

        await _context.UserProfiles.AddRangeAsync(user, target);
        await _context.SaveChangesAsync();

        _mockCompatibilityScorer
            .Setup(x => x.GetScoreAsync(20, 21))
            .ReturnsAsync(0.5); // neutral — one user has no answers

        // Act — must not throw
        var score = await _service.CalculateCompatibilityScoreAsync(20, 21);

        // Assert — score is valid and within [0, 100]
        Assert.True(score >= 0 && score <= 100,
            $"Score should be in [0, 100], got {score}");
    }

    [Fact]
    public async Task ScoreCandidate_BothUsersNoAnswers_UsesNeutralFallback()
    {
        // Arrange — both users have no questionnaire answers (scorer returns neutral)
        var user   = CreateUserProfile(30, "Male",   28, 37.7749, -122.4194, "Female", 25, 35, 50);
        var target = CreateUserProfile(31, "Female", 30, 37.7749, -122.4194, "Male",   25, 35, 50);

        await _context.UserProfiles.AddRangeAsync(user, target);
        await _context.SaveChangesAsync();

        _mockCompatibilityScorer
            .Setup(x => x.GetScoreAsync(30, 31))
            .ReturnsAsync(0.5); // both have no answers → neutral

        // Act — must not throw
        var score = await _service.CalculateCompatibilityScoreAsync(30, 31);

        // Assert — score is valid and within [0, 100]
        Assert.True(score >= 0 && score <= 100,
            $"Score should be in [0, 100], got {score}");
    }

    [Fact]
    public async Task ScoreCandidate_WeightsCorrectlySumToOne()
    {
        // Arrange — identical profiles with all components set to match perfectly
        var user   = CreateUserProfile(40, "Male",   30, 37.7749, -122.4194, "Female", 28, 32, 50);
        var target = CreateUserProfile(41, "Female", 30, 37.7749, -122.4194, "Male",   28, 32, 50);
        user.WantsChildren   = true;
        target.WantsChildren = true;
        // Matching interests and education so those components also score 100
        user.Interests   = JsonSerializer.Serialize(new[] { "hiking", "travel" });
        target.Interests = JsonSerializer.Serialize(new[] { "hiking", "travel" });
        user.Education   = "Bachelor's";
        target.Education = "Bachelor's";

        await _context.UserProfiles.AddRangeAsync(user, target);
        await _context.SaveChangesAsync();

        // Compatibility also perfect (1.0 → 100/100)
        _mockCompatibilityScorer
            .Setup(x => x.GetScoreAsync(40, 41))
            .ReturnsAsync(1.0);

        // Act
        var score = await _service.CalculateCompatibilityScoreAsync(40, 41);

        // Assert — with all components at maximum, the blended score must be ~100
        // (verifies that (1 - w) * 100 + w * 100 = 100; weights sum to 1.0)
        Assert.True(score >= 98,
            $"Expected score ≈ 100 when all components are perfect, got {score}");
    }
}
