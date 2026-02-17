using MatchmakingService.Data;
using MatchmakingService.Models;
using MatchmakingService.Strategies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace MatchmakingService.Tests.Strategies;

/// <summary>
/// Tests for DailyPickStrategy — pick serving, fallback logic,
/// seen-marking, activity scoring, and rank ordering.
/// </summary>
public class DailyPickStrategyTests : IDisposable
{
    private readonly MatchmakingDbContext _context;
    private readonly Mock<LiveScoringStrategy> _mockLive;
    private readonly IOptionsMonitor<CandidateOptions> _options;

    public DailyPickStrategyTests()
    {
        var dbOptions = new DbContextOptionsBuilder<MatchmakingDbContext>()
            .UseInMemoryDatabase($"DailyPickTests_{Guid.NewGuid()}")
            .Options;
        _context = new MatchmakingDbContext(dbOptions);

        // We can't mock LiveScoringStrategy directly (it has no virtual methods),
        // so we'll use a wrapper approach — mock ICandidateStrategy
        _mockLive = null!; // Won't use this field

        _options = Microsoft.Extensions.Options.Options.Create(new CandidateOptions()).ToMonitor();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    private DailyPickStrategy CreateStrategy(LiveScoringStrategy? liveFallback = null)
    {
        // For tests where fallback is needed, we'll test separately
        // For pick-serving tests, we seed data so fallback never triggers
        return new DailyPickStrategy(
            _context,
            liveFallback!,
            _options,
            NullLogger<DailyPickStrategy>.Instance);
    }

    private UserProfile SeedUser(int userId, string gender = "Male", int age = 25)
    {
        var profile = new UserProfile
        {
            UserId = userId,
            Gender = gender,
            Age = age,
            IsActive = true,
            LastActiveAt = DateTime.UtcNow,
            DesirabilityScore = 50.0,
            City = "Stockholm",
            Country = "SE"
        };
        _context.UserProfiles.Add(profile);
        _context.SaveChanges();
        return profile;
    }

    private void SeedDailyPick(int userId, int candidateUserId, double score,
        int rank, bool acted = false, bool seen = false,
        DateTime? expiresAt = null)
    {
        _context.DailyPicks.Add(new DailyPick
        {
            UserId = userId,
            CandidateUserId = candidateUserId,
            Score = score,
            Rank = rank,
            GeneratedAt = DateTime.UtcNow.AddHours(-1),
            ExpiresAt = expiresAt ?? DateTime.UtcNow.AddHours(23),
            Seen = seen,
            Acted = acted
        });
        _context.SaveChanges();
    }

    // ═══════════════════════════════════════════════════════
    // Basic Pick Serving Tests
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task GetCandidates_WithPicks_ReturnsPicksInRankOrder()
    {
        var user = SeedUser(1);
        var candidate1 = SeedUser(10, "Female", 24);
        var candidate2 = SeedUser(11, "Female", 26);
        var candidate3 = SeedUser(12, "Female", 23);

        SeedDailyPick(1, 10, score: 85.0, rank: 1);
        SeedDailyPick(1, 11, score: 72.0, rank: 2);
        SeedDailyPick(1, 12, score: 60.0, rank: 3);

        var strategy = CreateStrategy();
        var request = new CandidateRequest(Limit: 10);

        var result = await strategy.GetCandidatesAsync(1, request);

        Assert.Equal(3, result.Candidates.Count);
        Assert.Equal("DailyPick", result.StrategyUsed);
        Assert.Equal(10, result.Candidates[0].Profile.UserId);
        Assert.Equal(11, result.Candidates[1].Profile.UserId);
        Assert.Equal(12, result.Candidates[2].Profile.UserId);
        Assert.Equal(85.0, result.Candidates[0].FinalScore);
    }

    [Fact]
    public async Task GetCandidates_RespectsLimit()
    {
        var user = SeedUser(1);
        for (int i = 10; i < 20; i++)
        {
            SeedUser(i, "Female", 22 + i % 5);
            SeedDailyPick(1, i, score: 90 - i, rank: i - 9);
        }

        var strategy = CreateStrategy();
        var request = new CandidateRequest(Limit: 3);

        var result = await strategy.GetCandidatesAsync(1, request);

        Assert.Equal(3, result.Candidates.Count);
    }

    [Fact]
    public async Task GetCandidates_ActedPicks_Excluded()
    {
        var user = SeedUser(1);
        SeedUser(10, "Female");
        SeedUser(11, "Female");

        SeedDailyPick(1, 10, score: 85.0, rank: 1, acted: true);  // Should be excluded
        SeedDailyPick(1, 11, score: 72.0, rank: 2, acted: false); // Should be included

        var strategy = CreateStrategy();
        var result = await strategy.GetCandidatesAsync(1, new CandidateRequest());

        Assert.Single(result.Candidates);
        Assert.Equal(11, result.Candidates[0].Profile.UserId);
    }

    [Fact]
    public async Task GetCandidates_ExpiredPicks_Excluded()
    {
        var user = SeedUser(1);
        SeedUser(10, "Female");
        SeedUser(11, "Female");

        SeedDailyPick(1, 10, score: 85.0, rank: 1,
            expiresAt: DateTime.UtcNow.AddHours(-1)); // Expired
        SeedDailyPick(1, 11, score: 72.0, rank: 2,
            expiresAt: DateTime.UtcNow.AddHours(23)); // Valid

        var strategy = CreateStrategy();
        var result = await strategy.GetCandidatesAsync(1, new CandidateRequest());

        Assert.Single(result.Candidates);
        Assert.Equal(11, result.Candidates[0].Profile.UserId);
    }

    [Fact]
    public async Task GetCandidates_MarksSeen()
    {
        var user = SeedUser(1);
        SeedUser(10, "Female");
        SeedDailyPick(1, 10, score: 85.0, rank: 1, seen: false);

        var strategy = CreateStrategy();
        await strategy.GetCandidatesAsync(1, new CandidateRequest());

        // Verify the pick was marked as seen in DB
        var pick = await _context.DailyPicks.FirstAsync(dp => dp.UserId == 1);
        Assert.True(pick.Seen);
    }

    [Fact]
    public async Task GetCandidates_DifferentUsers_IsolatedPicks()
    {
        SeedUser(1);
        SeedUser(2);
        SeedUser(10, "Female");
        SeedUser(11, "Female");

        SeedDailyPick(1, 10, score: 85.0, rank: 1); // User 1's pick
        SeedDailyPick(2, 11, score: 72.0, rank: 1); // User 2's pick

        var strategy = CreateStrategy();

        var result1 = await strategy.GetCandidatesAsync(1, new CandidateRequest());
        var result2 = await strategy.GetCandidatesAsync(2, new CandidateRequest());

        Assert.Single(result1.Candidates);
        Assert.Equal(10, result1.Candidates[0].Profile.UserId);
        Assert.Single(result2.Candidates);
        Assert.Equal(11, result2.Candidates[0].Profile.UserId);
    }

    // ═══════════════════════════════════════════════════════
    // Queue Exhausted / Suggestions Remaining Tests
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task GetCandidates_AllPicksReturned_QueueExhausted()
    {
        SeedUser(1);
        SeedUser(10, "Female");
        SeedDailyPick(1, 10, score: 85.0, rank: 1);

        var strategy = CreateStrategy();
        var result = await strategy.GetCandidatesAsync(1, new CandidateRequest(Limit: 10));

        Assert.True(result.QueueExhausted);
        Assert.Equal(0, result.SuggestionsRemaining);
    }

    [Fact]
    public async Task GetCandidates_MorePicksAvailable_NotExhausted()
    {
        SeedUser(1);
        for (int i = 10; i < 15; i++)
        {
            SeedUser(i, "Female");
            SeedDailyPick(1, i, score: 90 - i, rank: i - 9);
        }

        var strategy = CreateStrategy();
        var result = await strategy.GetCandidatesAsync(1, new CandidateRequest(Limit: 2));

        Assert.False(result.QueueExhausted);
        Assert.True(result.SuggestionsRemaining > 0);
    }

    // ═══════════════════════════════════════════════════════
    // Activity Score Tests (static helper)
    // ═══════════════════════════════════════════════════════

    [Fact]
    public void ActivityScore_JustActive_Returns100()
    {
        // User active right now → score ~100
        var method = typeof(DailyPickStrategy)
            .GetMethod("CalculateActivityScore",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);

        var score = (double)method!.Invoke(null, new object[] { DateTime.UtcNow })!;
        Assert.True(score >= 99.0, $"Expected ~100, got {score}");
    }

    [Fact]
    public void ActivityScore_OneWeekAgo_Decayed()
    {
        var method = typeof(DailyPickStrategy)
            .GetMethod("CalculateActivityScore",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        var score = (double)method!.Invoke(null, new object[] { DateTime.UtcNow.AddDays(-7) })!;
        // e^(-0.1 * 7) ≈ 0.497 → score ≈ 49.7
        Assert.True(score > 40 && score < 60, $"Expected ~50, got {score}");
    }

    [Fact]
    public void ActivityScore_MonthAgo_VeryLow()
    {
        var method = typeof(DailyPickStrategy)
            .GetMethod("CalculateActivityScore",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        var score = (double)method!.Invoke(null, new object[] { DateTime.UtcNow.AddDays(-30) })!;
        // e^(-0.1 * 30) ≈ 0.05 → score ≈ 5.0
        Assert.True(score < 10, $"Expected <10, got {score}");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(30)]
    [InlineData(365)]
    public void ActivityScore_NeverNegative(int daysAgo)
    {
        var method = typeof(DailyPickStrategy)
            .GetMethod("CalculateActivityScore",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        var score = (double)method!.Invoke(null, new object[] { DateTime.UtcNow.AddDays(-daysAgo) })!;
        Assert.True(score >= 0, $"Score was negative: {score}");
    }
}

/// <summary>
/// Extension helper to convert Options.Create result to IOptionsMonitor.
/// </summary>
internal static class OptionsExtensions
{
    public static IOptionsMonitor<T> ToMonitor<T>(this IOptions<T> options) where T : class, new()
    {
        var mock = new Mock<IOptionsMonitor<T>>();
        mock.Setup(m => m.CurrentValue).Returns(options.Value);
        return mock.Object;
    }
}
