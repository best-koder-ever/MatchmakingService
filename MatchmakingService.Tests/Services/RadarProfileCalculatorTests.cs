using MatchmakingService.Data;
using MatchmakingService.Models;
using MatchmakingService.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MatchmakingService.Tests.Services;

/// <summary>
/// T593 — Unit tests for RadarProfileCalculator.
/// </summary>
public class RadarProfileCalculatorTests : IDisposable
{
    private readonly MatchmakingDbContext _context;
    private readonly RadarProfileCalculator _calc;

    public RadarProfileCalculatorTests()
    {
        var opts = new DbContextOptionsBuilder<MatchmakingDbContext>()
            .UseInMemoryDatabase($"RadarTests_{Guid.NewGuid()}")
            .Options;
        _context = new MatchmakingDbContext(opts);
        _calc = new RadarProfileCalculator(_context);
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task CalculateAsync_NewUser_ReturnsDefaultProfile()
    {
        var result = await _calc.CalculateAsync("user1");

        Assert.NotNull(result);
        Assert.Equal("user1", result.KeycloakId);
        // All axes should be at least 0.1 (default floor)
        Assert.True(result.EmotionalStability > 0, "EmotionalStability should be > 0");
        Assert.True(result.SocialEnergy > 0, "SocialEnergy should be > 0");
    }

    [Fact]
    public async Task ComputeAndSaveAsync_PersistsToDb()
    {
        var result = await _calc.ComputeAndSaveAsync("user2");

        var fromDb = await _context.RadarProfiles
            .FirstOrDefaultAsync(r => r.KeycloakId == "user2");

        Assert.NotNull(fromDb);
        Assert.Equal(result.EmotionalStability, fromDb!.EmotionalStability);
        Assert.Equal(result.SocialEnergy, fromDb.SocialEnergy);
        Assert.True(fromDb.UpdatedAt > DateTime.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public async Task ComputeAndSaveAsync_Idempotent_UpdatesExisting()
    {
        var first = await _calc.ComputeAndSaveAsync("user3");
        var second = await _calc.ComputeAndSaveAsync("user3");

        var count = await _context.RadarProfiles.CountAsync(r => r.KeycloakId == "user3");
        Assert.Equal(1, count);
        Assert.Equal(first.EmotionalStability, second.EmotionalStability);
    }

    [Fact]
    public async Task CalculateAsync_AllAxesInRange()
    {
        var result = await _calc.CalculateAsync("user4");

        Assert.InRange(result.EmotionalStability, 0.0, 1.0);
        Assert.InRange(result.SocialEnergy, 0.0, 1.0);
        Assert.InRange(result.Openness, 0.0, 1.0);
        Assert.InRange(result.Warmth, 0.0, 1.0);
        Assert.InRange(result.LifeStructure, 0.0, 1.0);
        Assert.InRange(result.IntimacyComfort, 0.0, 1.0);
        Assert.InRange(result.ConflictStyle, 0.0, 1.0);
        Assert.InRange(result.Confidence, 0.0, 1.0);
    }
}
