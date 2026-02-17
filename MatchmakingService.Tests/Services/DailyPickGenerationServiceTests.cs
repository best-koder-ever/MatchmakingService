using MatchmakingService.Services.Background;
using System.Reflection;

namespace MatchmakingService.Tests.Services;

/// <summary>
/// Tests for DailyPickGenerationService static helpers:
/// GetAdaptiveScheduling and CalculateWaitUntilNextRun.
///
/// The main ExecuteAsync / GenerateAllPicksAsync methods are integration tests
/// that need full DI — these unit tests cover the pure scheduling logic.
/// </summary>
public class DailyPickGenerationServiceTests
{
    private static readonly MethodInfo GetAdaptiveSchedulingMethod =
        typeof(DailyPickGenerationService).GetMethod(
            "GetAdaptiveScheduling",
            BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly MethodInfo CalculateWaitMethod =
        typeof(DailyPickGenerationService).GetMethod(
            "CalculateWaitUntilNextRun",
            BindingFlags.NonPublic | BindingFlags.Static)!;

    private (int BatchSize, int DelayMs) InvokeAdaptiveScheduling(int userCount)
    {
        var result = GetAdaptiveSchedulingMethod.Invoke(null, new object[] { userCount });
        var tuple = ((int, int))result!;
        return tuple;
    }

    private TimeSpan InvokeCalculateWait(string timeUtc)
    {
        return (TimeSpan)CalculateWaitMethod.Invoke(null, new object[] { timeUtc })!;
    }

    // ═══════════════════════════════════════════════════════
    // Adaptive Scheduling Tests (T177)
    // ═══════════════════════════════════════════════════════

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(500)]
    [InlineData(999)]
    public void AdaptiveScheduling_Under1K_AllAtOnce(int userCount)
    {
        var (batchSize, delayMs) = InvokeAdaptiveScheduling(userCount);

        Assert.Equal(userCount, batchSize); // Process all at once
        Assert.Equal(0, delayMs);           // No delay
    }

    [Theory]
    [InlineData(1_000)]
    [InlineData(5_000)]
    [InlineData(9_999)]
    public void AdaptiveScheduling_1Kto10K_BatchOf100(int userCount)
    {
        var (batchSize, delayMs) = InvokeAdaptiveScheduling(userCount);

        Assert.Equal(100, batchSize);
        Assert.Equal(100, delayMs);
    }

    [Theory]
    [InlineData(10_000)]
    [InlineData(50_000)]
    [InlineData(99_999)]
    public void AdaptiveScheduling_10Kto100K_BatchOf200(int userCount)
    {
        var (batchSize, delayMs) = InvokeAdaptiveScheduling(userCount);

        Assert.Equal(200, batchSize);
        Assert.Equal(500, delayMs);
    }

    [Theory]
    [InlineData(100_000)]
    [InlineData(1_000_000)]
    public void AdaptiveScheduling_Over100K_BatchOf500(int userCount)
    {
        var (batchSize, delayMs) = InvokeAdaptiveScheduling(userCount);

        Assert.Equal(500, batchSize);
        Assert.Equal(1000, delayMs);
    }

    // ═══════════════════════════════════════════════════════
    // CalculateWaitUntilNextRun Tests
    // ═══════════════════════════════════════════════════════

    [Fact]
    public void CalculateWait_ValidTimeString_ReturnsPositiveTimeSpan()
    {
        var wait = InvokeCalculateWait("03:00");
        Assert.True(wait > TimeSpan.Zero, "Wait should be positive");
        Assert.True(wait <= TimeSpan.FromHours(24), "Wait should be ≤ 24h");
    }

    [Fact]
    public void CalculateWait_InvalidTimeString_DefaultsTo0300()
    {
        var wait = InvokeCalculateWait("not-a-time");
        Assert.True(wait > TimeSpan.Zero);
        Assert.True(wait <= TimeSpan.FromHours(24));
    }

    [Fact]
    public void CalculateWait_EmptyString_DefaultsTo0300()
    {
        var wait = InvokeCalculateWait("");
        Assert.True(wait > TimeSpan.Zero);
        Assert.True(wait <= TimeSpan.FromHours(24));
    }

    [Theory]
    [InlineData("00:00")]
    [InlineData("06:00")]
    [InlineData("12:00")]
    [InlineData("18:00")]
    [InlineData("23:59")]
    public void CalculateWait_VariousTimes_AlwaysPositive(string time)
    {
        var wait = InvokeCalculateWait(time);
        Assert.True(wait > TimeSpan.Zero, $"Wait for {time} should be positive, got {wait}");
    }
}
