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

public class LiveScoringStrategyTests : IDisposable
{
    private readonly MatchmakingDbContext _context;
    private readonly Mock<IAdvancedMatchingService> _matchingServiceMock;
    private readonly Mock<ISwipeServiceClient> _swipeClientMock;
    private readonly Mock<ISafetyServiceClient> _safetyClientMock;
    private readonly Mock<IOptionsMonitor<CandidateOptions>> _optionsMock;
    private readonly Mock<IOptionsMonitor<ScoringConfiguration>> _scoringConfigMock;
    private readonly CandidateFilterPipeline _filterPipeline;

    public LiveScoringStrategyTests()
    {
        var dbOptions = new DbContextOptionsBuilder<MatchmakingDbContext>()
            .UseInMemoryDatabase(databaseName: $"LiveScoringTests_{Guid.NewGuid()}")
            .Options;
        _context = new MatchmakingDbContext(dbOptions);

        _matchingServiceMock = new Mock<IAdvancedMatchingService>();
        _swipeClientMock = new Mock<ISwipeServiceClient>();
        _safetyClientMock = new Mock<ISafetyServiceClient>();

        _optionsMock = new Mock<IOptionsMonitor<CandidateOptions>>();
        _optionsMock.Setup(x => x.CurrentValue).Returns(new CandidateOptions());

        _scoringConfigMock = new Mock<IOptionsMonitor<ScoringConfiguration>>();
        _scoringConfigMock.Setup(x => x.CurrentValue).Returns(new ScoringConfiguration
        {
            MinimumCompatibilityThreshold = 0,
            ActivityScoreHalfLifeDays = 7.0
        });

        // Default mock setups: empty swiped/blocked, full trust
        _swipeClientMock.Setup(x => x.GetSwipedUserIdsAsync(It.IsAny<int>()))
            .ReturnsAsync(new HashSet<int>());
        _safetyClientMock.Setup(x => x.GetBlockedUserIdsAsync(It.IsAny<int>()))
            .ReturnsAsync(new List<int>());
        _swipeClientMock.Setup(x => x.GetBatchTrustScoresAsync(It.IsAny<IEnumerable<int>>()))
            .ReturnsAsync((IEnumerable<int> ids) => ids.ToDictionary(id => id, _ => 100m));

        // Default compat score: 80
        _matchingServiceMock.Setup(x => x.CalculateCompatibilityScoreAsync(It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(80.0);

        // Use no filters in pipeline (test scoring logic in isolation)
        _filterPipeline = new CandidateFilterPipeline(
            new ICandidateFilter[] { new MatchmakingService.Filters.SelfExclusionFilter() },
            NullLogger<CandidateFilterPipeline>.Instance);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    private LiveScoringStrategy CreateStrategy()
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

    private UserProfile SeedUser(int userId, bool isActive = true, double desirability = 50,
        DateTime? lastActive = null)
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
            LastActiveAt = lastActive ?? DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.UserProfiles.Add(user);
        _context.SaveChanges();
        return user;
    }

    // --- Basic behavior tests ---

    [Fact]
    public async Task GetCandidates_UserNotFound_ReturnsEmptyResult()
    {
        var strategy = CreateStrategy();
        var result = await strategy.GetCandidatesAsync(999, new CandidateRequest(10, 0, null, false));

        Assert.Empty(result.Candidates);
        Assert.True(result.QueueExhausted);
        Assert.Equal("Live", result.StrategyUsed);
    }

    [Fact]
    public async Task GetCandidates_InactiveUser_ReturnsEmptyResult()
    {
        SeedUser(1, isActive: false);
        var strategy = CreateStrategy();
        var result = await strategy.GetCandidatesAsync(1, new CandidateRequest(10, 0, null, false));

        Assert.Empty(result.Candidates);
    }

    [Fact]
    public async Task GetCandidates_NoCandidates_ReturnsEmptyResult()
    {
        SeedUser(1); // requesting user exists but no candidates
        var strategy = CreateStrategy();
        var result = await strategy.GetCandidatesAsync(1, new CandidateRequest(10, 0, null, false));

        Assert.Empty(result.Candidates);
        Assert.True(result.QueueExhausted);
    }

    [Fact]
    public async Task GetCandidates_WithCandidates_ReturnsScoredAndSorted()
    {
        SeedUser(1);
        SeedUser(2, desirability: 80);
        SeedUser(3, desirability: 40);

        // Different compat scores for candidates
        _matchingServiceMock.Setup(x => x.CalculateCompatibilityScoreAsync(1, 2)).ReturnsAsync(90.0);
        _matchingServiceMock.Setup(x => x.CalculateCompatibilityScoreAsync(1, 3)).ReturnsAsync(70.0);

        var strategy = CreateStrategy();
        var result = await strategy.GetCandidatesAsync(1, new CandidateRequest(10, 0, null, false));

        Assert.Equal(2, result.Candidates.Count);
        Assert.Equal("Live", result.StrategyUsed);
        // User 2 should rank higher (90*0.7 + activity*0.15 + 80*0.15 > 70*0.7 + ... + 40*0.15)
        Assert.Equal(2, result.Candidates[0].Profile.UserId);
        Assert.Equal(3, result.Candidates[1].Profile.UserId);
    }

    [Fact]
    public async Task GetCandidates_RespectsLimit()
    {
        SeedUser(1);
        for (int i = 2; i <= 15; i++) SeedUser(i);

        var strategy = CreateStrategy();
        var result = await strategy.GetCandidatesAsync(1, new CandidateRequest(5, 0, null, false));

        Assert.Equal(5, result.Candidates.Count);
    }

    // --- MinScore threshold tests ---

    [Fact]
    public async Task GetCandidates_MinScoreFilter_ExcludesLowScores()
    {
        SeedUser(1);
        SeedUser(2);
        SeedUser(3);

        _matchingServiceMock.Setup(x => x.CalculateCompatibilityScoreAsync(1, 2)).ReturnsAsync(90.0);
        _matchingServiceMock.Setup(x => x.CalculateCompatibilityScoreAsync(1, 3)).ReturnsAsync(30.0);

        var strategy = CreateStrategy();
        var result = await strategy.GetCandidatesAsync(1, new CandidateRequest(10, 50, null, false));

        // User 3 with compat 30 should be excluded by minScore 50
        Assert.Single(result.Candidates);
        Assert.Equal(2, result.Candidates[0].Profile.UserId);
    }

    // --- Trust score / shadow-restrict tests (T188) ---

    [Fact]
    public async Task GetCandidates_FullTrust_NoScorePenalty()
    {
        SeedUser(1);
        SeedUser(2, desirability: 50);

        _matchingServiceMock.Setup(x => x.CalculateCompatibilityScoreAsync(1, 2)).ReturnsAsync(80.0);
        _swipeClientMock.Setup(x => x.GetBatchTrustScoresAsync(It.IsAny<IEnumerable<int>>()))
            .ReturnsAsync(new Dictionary<int, decimal> { { 2, 100m } });

        var strategy = CreateStrategy();
        var result = await strategy.GetCandidatesAsync(1, new CandidateRequest(10, 0, null, false));

        Assert.Single(result.Candidates);
        // Trust 100 → multiplier 1.0, so score = (80*0.7 + activity*0.15 + 50*0.15) * 1.0
        var candidate = result.Candidates[0];
        Assert.True(candidate.FinalScore > 0);
    }

    [Fact]
    public async Task GetCandidates_LowTrust_ReducesFinalScore()
    {
        SeedUser(1);
        SeedUser(2, desirability: 50);
        SeedUser(3, desirability: 50);

        _matchingServiceMock.Setup(x => x.CalculateCompatibilityScoreAsync(1, It.IsAny<int>())).ReturnsAsync(80.0);

        // User 2: full trust, User 3: zero trust
        _swipeClientMock.Setup(x => x.GetBatchTrustScoresAsync(It.IsAny<IEnumerable<int>>()))
            .ReturnsAsync(new Dictionary<int, decimal> { { 2, 100m }, { 3, 0m } });

        var strategy = CreateStrategy();
        var result = await strategy.GetCandidatesAsync(1, new CandidateRequest(10, 0, null, false));

        Assert.Equal(2, result.Candidates.Count);
        var highTrust = result.Candidates.First(c => c.Profile.UserId == 2);
        var lowTrust = result.Candidates.First(c => c.Profile.UserId == 3);

        // Trust 0 → multiplier 0.5, so low trust score should be ~half of high trust
        Assert.True(highTrust.FinalScore > lowTrust.FinalScore,
            $"High trust ({highTrust.FinalScore}) should outrank low trust ({lowTrust.FinalScore})");
        // More precisely: lowTrust.Final ≈ highTrust.Final * 0.5
        var ratio = lowTrust.FinalScore / highTrust.FinalScore;
        Assert.InRange(ratio, 0.45, 0.55);
    }

    [Fact]
    public async Task GetCandidates_TrustServiceDown_GracefulDegradation()
    {
        SeedUser(1);
        SeedUser(2);

        _matchingServiceMock.Setup(x => x.CalculateCompatibilityScoreAsync(1, 2)).ReturnsAsync(80.0);
        // Trust service throws
        _swipeClientMock.Setup(x => x.GetBatchTrustScoresAsync(It.IsAny<IEnumerable<int>>()))
            .ThrowsAsync(new HttpRequestException("Service down"));

        var strategy = CreateStrategy();
        var result = await strategy.GetCandidatesAsync(1, new CandidateRequest(10, 0, null, false));

        // Should still return candidates (graceful degradation → assume trust 100)
        Assert.Single(result.Candidates);
    }

    // --- Activity score decay tests ---

    [Fact]
    public async Task GetCandidates_RecentlyActive_HighActivityScore()
    {
        SeedUser(1);
        SeedUser(2, lastActive: DateTime.UtcNow); // just now

        _matchingServiceMock.Setup(x => x.CalculateCompatibilityScoreAsync(1, 2)).ReturnsAsync(80.0);

        var strategy = CreateStrategy();
        var result = await strategy.GetCandidatesAsync(1, new CandidateRequest(10, 0, null, false));

        Assert.Single(result.Candidates);
        // Activity should be close to 100 for recently active user
        Assert.True(result.Candidates[0].ActivityScore >= 95,
            $"Activity score {result.Candidates[0].ActivityScore} should be near 100 for just-active user");
    }

    [Fact]
    public async Task GetCandidates_LongInactive_LowActivityScore()
    {
        SeedUser(1);
        SeedUser(2, lastActive: DateTime.UtcNow.AddDays(-30)); // 30 days ago

        _matchingServiceMock.Setup(x => x.CalculateCompatibilityScoreAsync(1, 2)).ReturnsAsync(80.0);

        var strategy = CreateStrategy();
        var result = await strategy.GetCandidatesAsync(1, new CandidateRequest(10, 0, null, false));

        Assert.Single(result.Candidates);
        // Half-life = 7 days, 30 days ≈ 4+ half-lives → activity < 10
        Assert.True(result.Candidates[0].ActivityScore < 10,
            $"Activity score {result.Candidates[0].ActivityScore} should be low for 30-day-inactive user");
    }

    // --- Scoring formula tests ---

    [Fact]
    public async Task GetCandidates_ScoringWeightsCorrect()
    {
        SeedUser(1);
        SeedUser(2, desirability: 100, lastActive: DateTime.UtcNow);

        _matchingServiceMock.Setup(x => x.CalculateCompatibilityScoreAsync(1, 2)).ReturnsAsync(100.0);

        var strategy = CreateStrategy();
        var result = await strategy.GetCandidatesAsync(1, new CandidateRequest(10, 0, null, false));

        var candidate = result.Candidates[0];
        // With compat=100, activity≈100, desirability=100, trust=100:
        // final ≈ (100*0.7 + 100*0.15 + 100*0.15) * 1.0 = 100
        Assert.InRange(candidate.FinalScore, 95, 100);
        Assert.Equal(100.0, candidate.CompatibilityScore);
        Assert.Equal(100.0, candidate.DesirabilityScore);
    }

    // --- Metadata tests ---

    [Fact]
    public async Task GetCandidates_ReturnsCorrectMetadata()
    {
        SeedUser(1);
        SeedUser(2);
        SeedUser(3);

        var strategy = CreateStrategy();
        var result = await strategy.GetCandidatesAsync(1, new CandidateRequest(10, 0, null, false));

        Assert.Equal("Live", result.StrategyUsed);
        Assert.True(result.ExecutionTime.TotalMilliseconds >= 0);
        Assert.Equal(2, result.TotalFiltered);
        Assert.Equal(2, result.TotalScored);
    }

    [Fact]
    public async Task GetCandidates_QueueExhausted_WhenFewerThanRequested()
    {
        SeedUser(1);
        SeedUser(2);

        var strategy = CreateStrategy();
        var result = await strategy.GetCandidatesAsync(1, new CandidateRequest(10, 0, null, false));

        Assert.True(result.QueueExhausted);
        Assert.Single(result.Candidates);
    }

    [Fact]
    public async Task GetCandidates_NotExhausted_WhenEnoughCandidates()
    {
        SeedUser(1);
        for (int i = 2; i <= 11; i++) SeedUser(i);

        var strategy = CreateStrategy();
        var result = await strategy.GetCandidatesAsync(1, new CandidateRequest(5, 0, null, false));

        Assert.False(result.QueueExhausted);
        Assert.Equal(5, result.Candidates.Count);
    }

    // --- Swiped/blocked exclusion via context ---

    [Fact]
    public async Task GetCandidates_ExcludesSwipedUsers()
    {
        SeedUser(1);
        SeedUser(2);
        SeedUser(3);

        _swipeClientMock.Setup(x => x.GetSwipedUserIdsAsync(1))
            .ReturnsAsync(new HashSet<int> { 2 });

        // Need to include swiped filter in pipeline
        var filters = new List<ICandidateFilter>
        {
            new MatchmakingService.Filters.SelfExclusionFilter(), new MatchmakingService.Filters.ExcludeSwipedFilter()
        };
        var pipeline = new CandidateFilterPipeline(filters, NullLogger<CandidateFilterPipeline>.Instance);
        var strategy = new LiveScoringStrategy(
            _context, pipeline,
            _matchingServiceMock.Object,
            _swipeClientMock.Object,
            _safetyClientMock.Object,
            _optionsMock.Object,
            _scoringConfigMock.Object,
            NullLogger<LiveScoringStrategy>.Instance);

        var result = await strategy.GetCandidatesAsync(1, new CandidateRequest(10, 0, null, false));

        Assert.Single(result.Candidates);
        Assert.Equal(3, result.Candidates[0].Profile.UserId);
    }
}
