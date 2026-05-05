using MatchmakingService.Models;

namespace MatchmakingService.Tests.Services;

/// <summary>
/// T533: Unit tests for the MatchInsight entity — validates property defaults,
/// mutability, and correct FK field wiring. DB-level constraints (unique index,
/// FK cascade) are verified by the EF migration; only in-memory model semantics
/// are tested here.
/// </summary>
public class MatchInsightEntityTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var before = DateTime.UtcNow;
        var insight = new MatchInsight();
        var after = DateTime.UtcNow;

        Assert.Equal(0, insight.Id);
        Assert.Equal(0, insight.MatchId);
        Assert.Equal(string.Empty, insight.ForKeycloakId);
        Assert.Null(insight.ReasonsJson);
        Assert.Null(insight.FrictionJson);
        Assert.Null(insight.GrowthJson);
        Assert.Equal(0.0, insight.OverallScore);
        Assert.InRange(insight.CreatedAt, before, after);
        Assert.Null(insight.Match);
    }

    [Fact]
    public void Properties_CanBeSetAndRead()
    {
        var createdAt = new DateTime(2026, 5, 5, 10, 0, 0, DateTimeKind.Utc);
        var insight = new MatchInsight
        {
            Id = 42,
            MatchId = 7,
            ForKeycloakId = "user-keycloak-abc123",
            ReasonsJson = "[\"shared values\"]",
            FrictionJson = "[\"distance\"]",
            GrowthJson = "[\"communication\"]",
            OverallScore = 87.5,
            CreatedAt = createdAt
        };

        Assert.Equal(42, insight.Id);
        Assert.Equal(7, insight.MatchId);
        Assert.Equal("user-keycloak-abc123", insight.ForKeycloakId);
        Assert.Equal("[\"shared values\"]", insight.ReasonsJson);
        Assert.Equal("[\"distance\"]", insight.FrictionJson);
        Assert.Equal("[\"communication\"]", insight.GrowthJson);
        Assert.Equal(87.5, insight.OverallScore);
        Assert.Equal(createdAt, insight.CreatedAt);
    }

    [Fact]
    public void NullableJsonFields_AcceptNull()
    {
        var insight = new MatchInsight
        {
            MatchId = 1,
            ForKeycloakId = "kc-id",
            ReasonsJson = null,
            FrictionJson = null,
            GrowthJson = null
        };

        Assert.Null(insight.ReasonsJson);
        Assert.Null(insight.FrictionJson);
        Assert.Null(insight.GrowthJson);
    }

    [Fact]
    public void NavigationProperty_CanBeAssigned()
    {
        var match = new Match { Id = 3, User1Id = 1, User2Id = 2 };
        var insight = new MatchInsight
        {
            MatchId = match.Id,
            ForKeycloakId = "kc-id",
            Match = match
        };

        Assert.Same(match, insight.Match);
        Assert.Equal(3, insight.MatchId);
    }

    [Fact]
    public void TwoInsightsForSameMatch_HaveDifferentForKeycloakIds()
    {
        // Reflects the asymmetric-per-user requirement: one match → two insight rows.
        var insight1 = new MatchInsight { MatchId = 10, ForKeycloakId = "user-A" };
        var insight2 = new MatchInsight { MatchId = 10, ForKeycloakId = "user-B" };

        Assert.Equal(insight1.MatchId, insight2.MatchId);
        Assert.NotEqual(insight1.ForKeycloakId, insight2.ForKeycloakId);
    }
}
