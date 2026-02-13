using MatchmakingService.Filters;

namespace MatchmakingService.Tests.Filters;

public class ExcludeBlockedFilterTests : FilterTestBase
{
    private readonly ExcludeBlockedFilter _filter = new();

    [Fact]
    public void ExcludesBlockedUsers()
    {
        var user = CreateUser(userId: 1);
        var blockedIds = new HashSet<int> { 3 };
        var candidates = CreateCandidates(
            CreateUser(userId: 2), CreateUser(userId: 3), CreateUser(userId: 4));
        var result = _filter.Apply(candidates, CreateContext(user, blockedIds: blockedIds)).ToList();
        Assert.Equal(2, result.Count);
        Assert.DoesNotContain(result, c => c.UserId == 3);
    }

    [Fact]
    public void EmptyBlockedSet_KeepsAll()
    {
        var user = CreateUser(userId: 1);
        var candidates = CreateCandidates(
            CreateUser(userId: 2), CreateUser(userId: 3));
        var result = _filter.Apply(candidates, CreateContext(user)).ToList();
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void AllBlocked_ReturnsEmpty()
    {
        var user = CreateUser(userId: 1);
        var blockedIds = new HashSet<int> { 2, 3 };
        var candidates = CreateCandidates(
            CreateUser(userId: 2), CreateUser(userId: 3));
        var result = _filter.Apply(candidates, CreateContext(user, blockedIds: blockedIds)).ToList();
        Assert.Empty(result);
    }
}
