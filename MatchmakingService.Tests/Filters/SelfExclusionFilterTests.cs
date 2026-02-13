using MatchmakingService.Filters;

namespace MatchmakingService.Tests.Filters;

public class SelfExclusionFilterTests : FilterTestBase
{
    private readonly SelfExclusionFilter _filter = new();

    [Fact]
    public void ExcludesOwnProfile()
    {
        var user = CreateUser(userId: 1);
        var candidates = CreateCandidates(
            CreateUser(userId: 1), CreateUser(userId: 2), CreateUser(userId: 3));
        var result = _filter.Apply(candidates, CreateContext(user)).ToList();
        Assert.Equal(2, result.Count);
        Assert.DoesNotContain(result, c => c.UserId == 1);
    }

    [Fact]
    public void KeepsOtherUsers()
    {
        var user = CreateUser(userId: 99);
        var candidates = CreateCandidates(
            CreateUser(userId: 1), CreateUser(userId: 2));
        var result = _filter.Apply(candidates, CreateContext(user)).ToList();
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void EmptyCandidatesReturnsEmpty()
    {
        var user = CreateUser(userId: 1);
        var result = _filter.Apply(CreateCandidates(), CreateContext(user)).ToList();
        Assert.Empty(result);
    }
}
