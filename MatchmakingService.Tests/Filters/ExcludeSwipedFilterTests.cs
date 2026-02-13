using MatchmakingService.Filters;

namespace MatchmakingService.Tests.Filters;

public class ExcludeSwipedFilterTests : FilterTestBase
{
    private readonly ExcludeSwipedFilter _filter = new();

    [Fact]
    public void ExcludesSwipedUsers()
    {
        var user = CreateUser(userId: 1);
        var swipedIds = new HashSet<int> { 2, 4 };
        var candidates = CreateCandidates(
            CreateUser(userId: 2), CreateUser(userId: 3), CreateUser(userId: 4));
        var result = _filter.Apply(candidates, CreateContext(user, swipedIds: swipedIds)).ToList();
        Assert.Single(result);
        Assert.Equal(3, result[0].UserId);
    }

    [Fact]
    public void EmptySwipedSet_KeepsAll()
    {
        var user = CreateUser(userId: 1);
        var candidates = CreateCandidates(
            CreateUser(userId: 2), CreateUser(userId: 3));
        var result = _filter.Apply(candidates, CreateContext(user)).ToList();
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void SwipedUserNotInCandidates_NoEffect()
    {
        var user = CreateUser(userId: 1);
        var swipedIds = new HashSet<int> { 99 };
        var candidates = CreateCandidates(
            CreateUser(userId: 2), CreateUser(userId: 3));
        var result = _filter.Apply(candidates, CreateContext(user, swipedIds: swipedIds)).ToList();
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void AllCandidatesSwiped_ReturnsEmpty()
    {
        var user = CreateUser(userId: 1);
        var swipedIds = new HashSet<int> { 2, 3 };
        var candidates = CreateCandidates(
            CreateUser(userId: 2), CreateUser(userId: 3));
        var result = _filter.Apply(candidates, CreateContext(user, swipedIds: swipedIds)).ToList();
        Assert.Empty(result);
    }
}
