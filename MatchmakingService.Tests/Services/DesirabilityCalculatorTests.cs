using MatchmakingService.Data;
using MatchmakingService.Models;
using MatchmakingService.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace MatchmakingService.Tests.Services;

/// <summary>
/// T183 acceptance tests for the DesirabilityCalculator.
/// Validates: Bayesian smoothing, ELO adjustments, default scores, minimum data thresholds.
/// </summary>
public class DesirabilityCalculatorTests : IDisposable
{
    private readonly MatchmakingDbContext _dbContext;
    private readonly DesirabilityCalculator _calculator;

    public DesirabilityCalculatorTests()
    {
        var options = new DbContextOptionsBuilder<MatchmakingDbContext>()
            .UseInMemoryDatabase(databaseName: $"DesirabilityTest_{Guid.NewGuid()}")
            .Options;
        _dbContext = new MatchmakingDbContext(options);
        _calculator = new DesirabilityCalculator(
            Mock.Of<ILogger<DesirabilityCalculator>>());
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    // ==================== BAYESIAN SCORING TESTS ====================

    [Fact]
    public async Task HighLikeRate_ReturnsHighScore()
    {
        // User with 80% like rate (80 likes out of 100 swipes received)
        var user = CreateUser(1);
        _dbContext.UserProfiles.Add(user);
        _dbContext.MatchingAlgorithmMetrics.Add(CreateMetric(1, swipesReceived: 100, likesReceived: 80));
        await _dbContext.SaveChangesAsync();

        await _calculator.RecalculateForUsersAsync(_dbContext, new List<UserProfile> { user }, CancellationToken.None);

        // Bayesian: (80 + 10*0.3) / (100 + 10) = 83/110 ≈ 0.7545 → score ≈ 75.5 (before decay)
        Assert.True(user.DesirabilityScore > 70, $"High like rate should give high score, got {user.DesirabilityScore}");
    }

    [Fact]
    public async Task LowLikeRate_ReturnsLowerScore()
    {
        // User with 20% like rate (20 likes out of 100 swipes received)
        var user = CreateUser(2);
        _dbContext.UserProfiles.Add(user);
        _dbContext.MatchingAlgorithmMetrics.Add(CreateMetric(2, swipesReceived: 100, likesReceived: 20));
        await _dbContext.SaveChangesAsync();

        await _calculator.RecalculateForUsersAsync(_dbContext, new List<UserProfile> { user }, CancellationToken.None);

        // Bayesian: (20 + 3) / (100 + 10) = 23/110 ≈ 0.209 → score ≈ 20.9 (before decay)
        Assert.True(user.DesirabilityScore < 40, $"Low like rate should give lower score, got {user.DesirabilityScore}");
    }

    [Fact]
    public async Task NewUser_BelowMinSwipes_ReturnsDefault()
    {
        // User with only 5 swipes received — below minimum threshold of 20
        var user = CreateUser(3);
        _dbContext.UserProfiles.Add(user);
        _dbContext.MatchingAlgorithmMetrics.Add(CreateMetric(3, swipesReceived: 5, likesReceived: 5));
        await _dbContext.SaveChangesAsync();

        await _calculator.RecalculateForUsersAsync(_dbContext, new List<UserProfile> { user }, CancellationToken.None);

        Assert.Equal(50.0, user.DesirabilityScore);
    }

    [Fact]
    public async Task NoMetrics_ReturnsDefault()
    {
        // User with zero metrics entries
        var user = CreateUser(4);
        _dbContext.UserProfiles.Add(user);
        await _dbContext.SaveChangesAsync();

        await _calculator.RecalculateForUsersAsync(_dbContext, new List<UserProfile> { user }, CancellationToken.None);

        Assert.Equal(50.0, user.DesirabilityScore);
    }

    [Fact]
    public async Task BayesianSmoothing_PreventsExtremes_FewSwipes()
    {
        // User with 1 like out of 20 swipes (exactly at threshold)
        // Without Bayesian: 1/20 = 5% → score 5
        // With Bayesian: (1 + 10*0.3) / (20 + 10) = 4/30 ≈ 0.133 → score ≈ 13.3
        var user = CreateUser(5);
        _dbContext.UserProfiles.Add(user);
        _dbContext.MatchingAlgorithmMetrics.Add(CreateMetric(5, swipesReceived: 20, likesReceived: 1));
        await _dbContext.SaveChangesAsync();

        await _calculator.RecalculateForUsersAsync(_dbContext, new List<UserProfile> { user }, CancellationToken.None);

        // Should be pulled up from naive 5% toward prior mean (30%)
        Assert.True(user.DesirabilityScore > 5, "Bayesian smoothing should prevent extreme low scores");
        Assert.True(user.DesirabilityScore < 50, "Score should still be below average");
    }

    [Fact]
    public async Task BayesianSmoothing_PreventsExtremes_PerfectRate()
    {
        // User with 20 likes out of 20 swipes (100% rate, at threshold)
        // Without Bayesian: 20/20 = 100%
        // With Bayesian: (20 + 3) / (20 + 10) = 23/30 ≈ 0.767 → score ≈ 76.7
        var user = CreateUser(6);
        _dbContext.UserProfiles.Add(user);
        _dbContext.MatchingAlgorithmMetrics.Add(CreateMetric(6, swipesReceived: 20, likesReceived: 20));
        await _dbContext.SaveChangesAsync();

        await _calculator.RecalculateForUsersAsync(_dbContext, new List<UserProfile> { user }, CancellationToken.None);

        // Should be pulled down from naive 100% toward prior
        Assert.True(user.DesirabilityScore < 85, "Bayesian smoothing should prevent extreme highs at low data");
        Assert.True(user.DesirabilityScore > 60, "Score should still be well above average");
    }

    // ==================== ELO ADJUSTMENT TESTS ====================

    [Fact]
    public void EloAdjustment_LikeFromHighDesirability_BigBoost()
    {
        // UserA (desirability 70) likes UserB (desirability 40)
        // UserB should get a big boost (liked by someone "above" them)
        var delta = DesirabilityCalculator.CalculateEloAdjustment(
            swiperDesirability: 70, targetDesirability: 40, isLike: true);

        Assert.True(delta > 10, $"Like from high-desirability user should give big boost, got {delta}");
    }

    [Fact]
    public void EloAdjustment_PassFromHighDesirability_SmallPenalty()
    {
        // UserA (desirability 70) passes on UserC (desirability 80)
        // UserC gets a small penalty (passed by someone "below" them — expected)
        var delta = DesirabilityCalculator.CalculateEloAdjustment(
            swiperDesirability: 70, targetDesirability: 80, isLike: false);

        Assert.True(delta < 0, $"Pass should give negative delta, got {delta}");
        Assert.True(delta > -20, $"Penalty should be small since target is higher, got {delta}");
    }

    [Fact]
    public void EloAdjustment_LikeFromEqualDesirability_ModerateBoost()
    {
        // Equal desirability → moderate boost on like
        var delta = DesirabilityCalculator.CalculateEloAdjustment(
            swiperDesirability: 50, targetDesirability: 50, isLike: true);

        Assert.True(delta > 0, "Like should always give positive delta");
        // K-factor * (1 - 0.5) = 32 * 0.5 = 16
        Assert.True(Math.Abs(delta - 16.0) < 0.1, $"Expected ~16 for equal desirability like, got {delta}");
    }

    [Fact]
    public void ApplyAdjustment_ClampsToRange()
    {
        Assert.Equal(100.0, DesirabilityCalculator.ApplyAdjustment(99.0, 5.0));
        Assert.Equal(0.0, DesirabilityCalculator.ApplyAdjustment(1.0, -5.0));
    }

    // ==================== BATCH TESTS ====================

    [Fact]
    public async Task BatchRecalculation_MultipleUsers_AllUpdated()
    {
        var users = new List<UserProfile>();
        for (int i = 10; i <= 12; i++)
        {
            var user = CreateUser(i);
            users.Add(user);
            _dbContext.UserProfiles.Add(user);
            _dbContext.MatchingAlgorithmMetrics.Add(
                CreateMetric(i, swipesReceived: 50, likesReceived: i * 3)); // 30, 33, 36
        }
        await _dbContext.SaveChangesAsync();

        await _calculator.RecalculateForUsersAsync(_dbContext, users, CancellationToken.None);

        // User 10: 30/50 = 60% → high
        // User 12: 36/50 = 72% → higher
        Assert.True(users[2].DesirabilityScore > users[0].DesirabilityScore,
            "Higher like rate should give higher score");
    }

    [Fact]
    public async Task EmptyUserList_DoesNotThrow()
    {
        // Should be a no-op
        await _calculator.RecalculateForUsersAsync(
            _dbContext, new List<UserProfile>(), CancellationToken.None);
    }

    // ==================== HELPERS ====================

    private static UserProfile CreateUser(int userId) => new()
    {
        UserId = userId,
        Gender = "Male",
        Age = 25,
        Latitude = 59.33,
        Longitude = 18.07,
        PreferredGender = "Female",
        MinAge = 20,
        MaxAge = 35,
        MaxDistance = 50,
        IsActive = true,
        LastActiveAt = DateTime.UtcNow,
        DesirabilityScore = 50.0 // Default
    };

    private static MatchingAlgorithmMetric CreateMetric(
        int userId, int swipesReceived, int likesReceived) => new()
    {
        UserId = userId,
        AlgorithmVersion = "v1",
        SwipesReceived = swipesReceived,
        LikesReceived = likesReceived,
        MatchesCreated = 0,
        SuggestionsGenerated = 0,
        SuccessRate = likesReceived / (double)Math.Max(swipesReceived, 1),
        CalculatedAt = DateTime.UtcNow // Fresh
    };
}
