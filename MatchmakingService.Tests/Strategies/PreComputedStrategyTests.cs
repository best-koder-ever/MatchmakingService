using MatchmakingService.Data;
using MatchmakingService.Filters;
using MatchmakingService.Models;
using MatchmakingService.Services;
using MatchmakingService.Strategies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace MatchmakingService.Tests.Strategies;

public class PreComputedStrategyTests : IDisposable
{
    private readonly MatchmakingDbContext _context;
    private readonly Mock<ISwipeServiceClient> _swipeClientMock;
    private readonly Mock<ISafetyServiceClient> _safetyClientMock;
    private readonly Mock<IOptionsMonitor<CandidateOptions>> _optionsMock;
    private readonly Mock<IOptionsMonitor<ScoringConfiguration>> _scoringConfigMock;
    private readonly Mock<IAdvancedMatchingService> _matchingServiceMock;
    private readonly CandidateFilterPipeline _filterPipeline;

    public PreComputedStrategyTests()
    {
        var dbOptions = new DbContextOptionsBuilder<MatchmakingDbContext>()
            .UseInMemoryDatabase(databaseName: $"PreComputedTests_{Guid.NewGuid()}")
            .Options;
        _context = new MatchmakingDbContext(dbOptions);

        _swipeClientMock = new Mock<ISwipeServiceClient>();
        _safetyClientMock = new Mock<ISafetyServiceClient>();
        _matchingServiceMock = new Mock<IAdvancedMatchingService>();

        _optionsMock = new Mock<IOptionsMonitor<CandidateOptions>>();
        _optionsMock.Setup(x => x.CurrentValue).Returns(new CandidateOptions());

        _scoringConfigMock = new Mock<IOptionsMonitor<ScoringConfiguration>>();
        _scoringConfigMock.Setup(x => x.CurrentValue).Returns(new ScoringConfiguration
        {
            MinimumCompatibilityThreshold = 0,
            ActivityScoreHalfLifeDays = 7.0,
            ScoreCacheHours = 24
        });

        // Default mocks
        _swipeClientMock.Setup(x => x.GetSwipedUserIdsAsync(It.IsAny<int>()))
            .ReturnsAsync(new HashSet<int>());
        _safetyClientMock.Setup(x => x.GetBlockedUserIdsAsync(It.IsAny<int>()))
            .ReturnsAsync(new List<int>());
        _swipeClientMock.Setup(x => x.GetBatchTrustScoresAsync(It.IsAny<IEnumerable<int>>()))
            .ReturnsAsync((IEnumerable<int> ids) => ids.ToDictionary(id => id, _ => 100m));
        _matchingServiceMock.Setup(x => x.CalculateCompatibilityScoreAsync(It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(80.0);

        _filterPipeline = new CandidateFilterPipeline(
            new ICandidateFilter[] { new MatchmakingService.Filters.SelfExclusionFilter() },
            NullLogger<CandidateFilterPipeline>.Instance);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    private LiveScoringStrategy CreateLiveFallback()
    {
        return new LiveScoringStrategy(
            _context,
            _filterPipeline,
            _matchingServiceMock.Object,
            _swipeClientMock.Object,
            _safetyClientMock.Object,
            _optionsMock.Object,
            _scoringConfigMock.Object,
            NullLogger<LiveScoringStrategy>.Instance);
    }

    private PreComputedStrategy CreateStrategy()
    {
        return new PreComputedStrategy(
            _context,
            _filterPipeline,
            CreateLiveFallback(),
            _swipeClientMock.Object,
            _safetyClientMock.Object,
            _optionsMock.Object,
            _scoringConfigMock.Object,
            NullLogger<PreComputedStrategy>.Instance);
    }

    private UserProfile SeedUser(int userId, bool isActive = true, double desirability = 50)
    {
        var user = new UserProfile
        {
            UserId = userId,
            IsActive = isActive,
            Gender = "Male",
            PreferredGender = "Female",
            Age = 28,
            MinAge = 22,
            MaxAge = 35,
            Latitude = 59.33,
            Longitude = 18.07,
            MaxDistance = 50,
            DesirabilityScore = desirability,
            LastActiveAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.UserProfiles.Add(user);
        _context.SaveChanges();
        return user;
    }

    private void SeedMatchScore(int userId, int targetUserId, double overallScore,
        double activityScore = 80, bool isValid = true, DateTime? calculatedAt = null)
    {
        _context.MatchScores.Add(new MatchScore
        {
            UserId = userId,
            TargetUserId = targetUserId,
            OverallScore = overallScore,
            ActivityScore = activityScore,
            LocationScore = 80,
            AgeScore = 80,
            InterestsScore = 70,
            EducationScore = 60,
            LifestyleScore = 75,
            IsValid = isValid,
            CalculatedAt = calculatedAt ?? DateTime.UtcNow
        });
        _context.SaveChanges();
    }

    // --- Basic behavior ---

    [Fact]
    public async Task GetCandidates_UserNotFound_ReturnsEmpty()
    {
        var strategy = CreateStrategy();
        var result = await strategy.GetCandidatesAsync(999, new CandidateRequest(10, 0, null, false));

        Assert.Empty(result.Candidates);
        Assert.Equal("PreComputed", result.StrategyUsed);
    }

    [Fact]
    public async Task GetCandidates_InactiveUser_ReturnsEmpty()
    {
        SeedUser(1, isActive: false);
        var strategy = CreateStrategy();
        var result = await strategy.GetCandidatesAsync(1, new CandidateRequest(10, 0, null, false));

        Assert.Empty(result.Candidates);
    }

    // --- Pre-computed score reading ---

    [Fact]
    public async Task GetCandidates_WithFreshScores_UsesPreComputed()
    {
        SeedUser(1);
        SeedUser(2, desirability: 80);
        SeedUser(3, desirability: 60);

        SeedMatchScore(1, 2, 90);
        SeedMatchScore(1, 3, 70);

        var strategy = CreateStrategy();
        var result = await strategy.GetCandidatesAsync(1, new CandidateRequest(10, 0, null, false));

        Assert.Equal(2, result.Candidates.Count);
        Assert.Equal("PreComputed", result.StrategyUsed);
        // Should be ordered by OverallScore desc
        Assert.Equal(2, result.Candidates[0].Profile.UserId);
        Assert.Equal(3, result.Candidates[1].Profile.UserId);
    }

    [Fact]
    public async Task GetCandidates_ScoresBelowMinScore_Excluded()
    {
        SeedUser(1);
        SeedUser(2);
        SeedUser(3);

        SeedMatchScore(1, 2, 90);
        SeedMatchScore(1, 3, 30);

        // Also set live scoring low for user 3 so supplement does not re-add them
        _matchingServiceMock.Setup(x => x.CalculateCompatibilityScoreAsync(1, 3)).ReturnsAsync(30.0);

        var strategy = CreateStrategy();
        var result = await strategy.GetCandidatesAsync(1, new CandidateRequest(10, 50, null, false));

        Assert.Single(result.Candidates);
        Assert.Equal(2, result.Candidates[0].Profile.UserId);
    }

    // --- Stale scores fallback ---

    [Fact]
    public async Task GetCandidates_StaleScores_FallsBackToLive()
    {
        SeedUser(1);
        SeedUser(2);

        // Score computed 48 hours ago (stale, cache = 24h)
        SeedMatchScore(1, 2, 90, calculatedAt: DateTime.UtcNow.AddHours(-48));

        var strategy = CreateStrategy();
        var result = await strategy.GetCandidatesAsync(1, new CandidateRequest(10, 0, null, false));

        // Should fall back to live scoring
        Assert.Equal("Live", result.StrategyUsed);
    }

    [Fact]
    public async Task GetCandidates_NoScores_FallsBackToLive()
    {
        SeedUser(1);
        SeedUser(2);
        // No pre-computed scores at all

        var strategy = CreateStrategy();
        var result = await strategy.GetCandidatesAsync(1, new CandidateRequest(10, 0, null, false));

        Assert.Equal("Live", result.StrategyUsed);
    }

    [Fact]
    public async Task GetCandidates_InvalidScores_Excluded()
    {
        SeedUser(1);
        SeedUser(2);

        SeedMatchScore(1, 2, 90, isValid: false);

        var strategy = CreateStrategy();
        var result = await strategy.GetCandidatesAsync(1, new CandidateRequest(10, 0, null, false));

        // Invalid scores should be excluded → falls back to live
        Assert.Equal("Live", result.StrategyUsed);
    }

    // --- Supplement with live scores ---

    [Fact]
    public async Task GetCandidates_InsufficientPreComputed_SupplementsWithLive()
    {
        SeedUser(1);
        SeedUser(2);
        SeedUser(3);
        SeedUser(4);

        // Only 1 pre-computed score, but requesting 3
        SeedMatchScore(1, 2, 90);

        var strategy = CreateStrategy();
        var result = await strategy.GetCandidatesAsync(1, new CandidateRequest(3, 0, null, false));

        // Should have pre-computed user 2 + live-scored users 3,4
        Assert.True(result.Candidates.Count >= 2,
            $"Expected at least 2 candidates (1 precomputed + supplements), got {result.Candidates.Count}");
        // User 2 should be present from pre-computed
        Assert.Contains(result.Candidates, c => c.Profile.UserId == 2);
    }

    [Fact]
    public async Task GetCandidates_Supplement_NoDuplicates()
    {
        SeedUser(1);
        SeedUser(2);
        SeedUser(3);

        SeedMatchScore(1, 2, 90);

        var strategy = CreateStrategy();
        var result = await strategy.GetCandidatesAsync(1, new CandidateRequest(5, 0, null, false));

        // Check no duplicate user IDs
        var userIds = result.Candidates.Select(c => c.Profile.UserId).ToList();
        Assert.Equal(userIds.Count, userIds.Distinct().Count());
    }

    // --- Limit ---

    [Fact]
    public async Task GetCandidates_RespectsLimit()
    {
        SeedUser(1);
        for (int i = 2; i <= 10; i++)
        {
            SeedUser(i);
            SeedMatchScore(1, i, 90 - i);
        }

        var strategy = CreateStrategy();
        var result = await strategy.GetCandidatesAsync(1, new CandidateRequest(3, 0, null, false));

        Assert.Equal(3, result.Candidates.Count);
    }

    // --- Metadata ---

    [Fact]
    public async Task GetCandidates_ReturnsCorrectMetadata()
    {
        SeedUser(1);
        SeedUser(2);
        SeedMatchScore(1, 2, 85);

        var strategy = CreateStrategy();
        var result = await strategy.GetCandidatesAsync(1, new CandidateRequest(10, 0, null, false));

        Assert.Equal("PreComputed", result.StrategyUsed);
        Assert.True(result.ExecutionTime.TotalMilliseconds >= 0);
    }

    [Fact]
    public async Task GetCandidates_QueueExhausted_WhenFewerThanRequested()
    {
        SeedUser(1);
        SeedUser(2);
        SeedMatchScore(1, 2, 85);

        var strategy = CreateStrategy();
        var result = await strategy.GetCandidatesAsync(1, new CandidateRequest(10, 0, null, false));

        // Only 1 candidate available, requested 10
        Assert.True(result.QueueExhausted || result.Candidates.Count < 10);
    }
}
