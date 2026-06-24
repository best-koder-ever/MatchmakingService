using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using MatchmakingService.Commands;
using MatchmakingService.Data;
using MatchmakingService.Models;
using MatchmakingService.Services;

namespace MatchmakingService.Tests.Commands;

/// <summary>
/// T624 — Unit tests for SubmitPostDateFeedbackHandler and FeedbackRadarRecalibrator.
/// </summary>
public class PostDateFeedbackTests : IDisposable
{
    private readonly MatchmakingDbContext _context;

    public PostDateFeedbackTests()
    {
        var opts = new DbContextOptionsBuilder<MatchmakingDbContext>()
            .UseInMemoryDatabase($"FeedbackTests_{Guid.NewGuid()}")
            .Options;
        _context = new MatchmakingDbContext(opts);
    }

    public void Dispose() => _context.Dispose();

    private SubmitPostDateFeedbackHandler BuildHandler(IRadarProfileCalculator? radar = null)
        => new(_context, radar ?? Mock.Of<IRadarProfileCalculator>(),
               NullLogger<SubmitPostDateFeedbackHandler>.Instance);

    private static SubmitPostDateFeedbackCommand GoodCommand(
        string keycloakId = "user1",
        string matchId = "match-1",
        int overall = 4,
        int chemistry = 4,
        int conversation = 4,
        bool wouldMeetAgain = true,
        string? freeform = null)
        => new(keycloakId, matchId, overall, chemistry, conversation, wouldMeetAgain, freeform);

    // ── Happy path ──────────────────────────────────────────────────────────

    [Fact]
    public async Task ValidFeedback_SavesRecord()
    {
        var handler = BuildHandler();
        var result = await handler.Handle(GoodCommand(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, await _context.PostDateFeedbacks.CountAsync());
    }

    [Fact]
    public async Task ValidFeedback_ReturnsGeneratedId()
    {
        var handler = BuildHandler();
        var result = await handler.Handle(GoodCommand(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Data!.FeedbackId > 0);
    }

    // ── Duplicate guard ──────────────────────────────────────────────────────

    [Fact]
    public async Task DuplicateFeedback_ReturnsDuplicateError()
    {
        var handler = BuildHandler();
        await handler.Handle(GoodCommand(), CancellationToken.None);

        var result2 = await handler.Handle(GoodCommand(), CancellationToken.None);

        Assert.False(result2.IsSuccess);
        Assert.Equal("DUPLICATE_FEEDBACK", result2.ErrorCode);
    }

    [Fact]
    public async Task SameUserDifferentMatch_Allowed()
    {
        var handler = BuildHandler();
        await handler.Handle(GoodCommand(matchId: "match-A"), CancellationToken.None);
        var result2 = await handler.Handle(GoodCommand(matchId: "match-B"), CancellationToken.None);

        Assert.True(result2.IsSuccess);
        Assert.Equal(2, await _context.PostDateFeedbacks.CountAsync());
    }

    // ── Validation ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0, 3, 3)]
    [InlineData(6, 3, 3)]
    [InlineData(3, 0, 3)]
    [InlineData(3, 6, 3)]
    [InlineData(3, 3, 0)]
    [InlineData(3, 3, 6)]
    public async Task InvalidRating_ReturnsValidationError(int overall, int chemistry, int convo)
    {
        var handler = BuildHandler();
        var cmd = GoodCommand(overall: overall, chemistry: chemistry, conversation: convo);

        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("INVALID_RATING", result.ErrorCode);
    }

    // ── T622 — Radar recalibration logic ─────────────────────────────────────

    [Fact]
    public void LowChemistry_HighConversation_NudgesIntimacyDown_AndOpennessUp()
    {
        var profile = new RadarProfile
        {
            KeycloakId = "u1",
            IntimacyComfort = 0.6,
            Openness = 0.5,
            ConflictStyle = 0.5,
            Confidence = 0.7,
            UpdatedAt = DateTime.UtcNow
        };

        FeedbackRadarRecalibrator.Apply(profile, overallRating: 3, chemistryRating: 2, conversationRating: 5);

        Assert.True(profile.IntimacyComfort < 0.6);  // nudged down
        Assert.True(profile.Openness > 0.5);          // nudged up
    }

    [Fact]
    public void LowOverall_ReducesConfidence()
    {
        var profile = new RadarProfile
        {
            KeycloakId = "u1",
            IntimacyComfort = 0.5,
            Openness = 0.5,
            ConflictStyle = 0.5,
            Confidence = 0.8,
            UpdatedAt = DateTime.UtcNow
        };

        FeedbackRadarRecalibrator.Apply(profile, overallRating: 2, chemistryRating: 3, conversationRating: 3);

        Assert.True(profile.Confidence < 0.8);
    }

    [Fact]
    public void HighOverall_IncreasesConfidence()
    {
        var profile = new RadarProfile
        {
            KeycloakId = "u1",
            IntimacyComfort = 0.5,
            Openness = 0.5,
            ConflictStyle = 0.5,
            Confidence = 0.7,
            UpdatedAt = DateTime.UtcNow
        };

        FeedbackRadarRecalibrator.Apply(profile, overallRating: 5, chemistryRating: 4, conversationRating: 4);

        Assert.True(profile.Confidence > 0.7);
    }

    [Fact]
    public void AllRatingsInRange_ConfidenceClampedToOne()
    {
        var profile = new RadarProfile
        {
            KeycloakId = "u1",
            IntimacyComfort = 0.5,
            Openness = 0.5,
            ConflictStyle = 0.5,
            Confidence = 1.0,   // already at max
            UpdatedAt = DateTime.UtcNow
        };

        FeedbackRadarRecalibrator.Apply(profile, overallRating: 5, chemistryRating: 4, conversationRating: 4);

        Assert.True(profile.Confidence <= 1.0);
    }

    [Fact]
    public void VeryLowOverallRepeated_ConfidenceFloorIsPointOne()
    {
        var profile = new RadarProfile
        {
            KeycloakId = "u1",
            IntimacyComfort = 0.5,
            Openness = 0.5,
            ConflictStyle = 0.5,
            Confidence = 0.11,
            UpdatedAt = DateTime.UtcNow
        };

        // Apply multiple low-overall signals
        for (var i = 0; i < 50; i++)
            FeedbackRadarRecalibrator.Apply(profile, overallRating: 1, chemistryRating: 3, conversationRating: 3);

        Assert.True(profile.Confidence >= 0.1);
    }

    [Fact]
    public async Task NoPriorRadarProfile_HandlerSucceeds()
    {
        // When there's no existing radar profile, the handler should still return success.
        // The fire-and-forget radar recalibration runs independently.
        var radarMock = new Mock<IRadarProfileCalculator>();
        radarMock
            .Setup(r => r.ComputeAndSaveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RadarProfile { KeycloakId = "u1" });

        var handler = BuildHandler(radarMock.Object);
        var result = await handler.Handle(GoodCommand("u1", "m1"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, await _context.PostDateFeedbacks.CountAsync());
    }
}
