using MatchmakingService.Filters;

namespace MatchmakingService.Tests.Filters;

public class ActiveUserFilterTests : FilterTestBase
{
    private readonly ActiveUserFilter _filter = new();

    [Fact]
    public void ExcludesInactiveUsers()
    {
        var user = CreateUser(userId: 1);
        var candidates = CreateCandidates(
            CreateUser(userId: 2, isActive: true),
            CreateUser(userId: 3, isActive: false),
            CreateUser(userId: 4, isActive: true));
        var result = _filter.Apply(candidates, CreateContext(user)).ToList();
        Assert.Equal(2, result.Count);
        Assert.All(result, c => Assert.True(c.IsActive));
    }

    [Fact]
    public void KeepsAllActiveUsers()
    {
        var user = CreateUser(userId: 1);
        var candidates = CreateCandidates(
            CreateUser(userId: 2, isActive: true),
            CreateUser(userId: 3, isActive: true));
        var result = _filter.Apply(candidates, CreateContext(user)).ToList();
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void AllInactiveReturnsEmpty()
    {
        var user = CreateUser(userId: 1);
        var candidates = CreateCandidates(
            CreateUser(userId: 2, isActive: false),
            CreateUser(userId: 3, isActive: false));
        var result = _filter.Apply(candidates, CreateContext(user)).ToList();
        Assert.Empty(result);
    }
}
