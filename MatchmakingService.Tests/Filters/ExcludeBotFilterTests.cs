using System.Linq;
using MatchmakingService.Filters;
using MatchmakingService.Models;
using Xunit;

namespace MatchmakingService.Tests.Filters;

/// <summary>
/// Unit tests for the bot-visibility rule (ExcludeBotFilter):
/// - bots only see real users (never other bots),
/// - real users see everyone (bots make the app feel alive),
/// - a bot account listed in DemoProfileIdsAllowedToSeeBots is treated as a real
///   user (demo/dev sign-in) and may see bots.
/// </summary>
public class ExcludeBotFilterTests
{
    private readonly ExcludeBotFilter _filter = new();

    private static UserProfile P(int id, bool isBot) => new() { UserId = id, IsBot = isBot };

    private static IQueryable<UserProfile> Candidates() =>
        new[] { P(10, isBot: true), P(11, isBot: true), P(12, isBot: false) }.AsQueryable();

    private static FilterContext Context(UserProfile requester, int[] demoIds) => new(
        RequestingUser: requester,
        SwipedUserIds: new System.Collections.Generic.HashSet<int>(),
        BlockedUserIds: new System.Collections.Generic.HashSet<int>(),
        Options: new CandidateOptions { DemoProfileIdsAllowedToSeeBots = demoIds });

    [Fact]
    public void BotRequester_ExcludesOtherBots()
    {
        var result = _filter.Apply(Candidates(), Context(P(1, isBot: true), demoIds: System.Array.Empty<int>()))
            .ToList();

        Assert.Single(result);
        Assert.False(result[0].IsBot); // only the real user (12) remains
    }

    [Fact]
    public void RealUserRequester_SeesEveryoneIncludingBots()
    {
        var result = _filter.Apply(Candidates(), Context(P(2, isBot: false), demoIds: System.Array.Empty<int>()))
            .ToList();

        Assert.Equal(3, result.Count); // bots are shown to real users
    }

    [Fact]
    public void DemoBotRequester_Listed_SeesBotsLikeARealUser()
    {
        // demo-user (profile 1) is a bot account but stands in for a human
        var result = _filter.Apply(Candidates(), Context(P(1, isBot: true), demoIds: new[] { 1 }))
            .ToList();

        Assert.Equal(3, result.Count); // allowed to see the other bots too
    }

    [Fact]
    public void Production_EmptyDemoList_BotRequesterStillExcludesBots()
    {
        // production ships with an empty list → behaviour unchanged for bot requesters
        var result = _filter.Apply(Candidates(), Context(P(1, isBot: true), demoIds: System.Array.Empty<int>()))
            .ToList();

        Assert.Single(result);
        Assert.False(result[0].IsBot);
    }
}