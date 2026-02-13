using MatchmakingService.Filters;

namespace MatchmakingService.Tests.Filters;

public class AgeRangeFilterTests : FilterTestBase
{
    private readonly AgeRangeFilter _filter = new();

    [Fact]
    public void InRange_BothDirections_Included()
    {
        var user = CreateUser(userId: 1, age: 28, minAge: 22, maxAge: 35);
        var candidates = CreateCandidates(
            CreateUser(userId: 2, age: 25, minAge: 25, maxAge: 35)); // user 28 is in their range
        var result = _filter.Apply(candidates, CreateContext(user)).ToList();
        Assert.Single(result);
    }

    [Fact]
    public void CandidateTooOld_ForUser_Excluded()
    {
        var user = CreateUser(userId: 1, age: 28, minAge: 22, maxAge: 35);
        var candidates = CreateCandidates(
            CreateUser(userId: 2, age: 40, minAge: 20, maxAge: 45)); // 40 > user's maxAge 35
        var result = _filter.Apply(candidates, CreateContext(user)).ToList();
        Assert.Empty(result);
    }

    [Fact]
    public void UserTooOld_ForCandidate_Excluded()
    {
        var user = CreateUser(userId: 1, age: 50, minAge: 22, maxAge: 55);
        var candidates = CreateCandidates(
            CreateUser(userId: 2, age: 30, minAge: 25, maxAge: 35)); // user 50 > candidate's maxAge 35
        var result = _filter.Apply(candidates, CreateContext(user)).ToList();
        Assert.Empty(result);
    }

    [Fact]
    public void BoundaryExact_Included()
    {
        var user = CreateUser(userId: 1, age: 35, minAge: 22, maxAge: 35);
        var candidates = CreateCandidates(
            CreateUser(userId: 2, age: 35, minAge: 30, maxAge: 40)); // exact boundary
        var result = _filter.Apply(candidates, CreateContext(user)).ToList();
        Assert.Single(result);
    }

    [Fact]
    public void MultipleCandidates_MixedResults()
    {
        var user = CreateUser(userId: 1, age: 28, minAge: 25, maxAge: 32);
        var candidates = CreateCandidates(
            CreateUser(userId: 2, age: 26, minAge: 25, maxAge: 35), // ✓
            CreateUser(userId: 3, age: 20, minAge: 18, maxAge: 30), // ✗ too young for user
            CreateUser(userId: 4, age: 30, minAge: 28, maxAge: 40)); // ✓
        var result = _filter.Apply(candidates, CreateContext(user)).ToList();
        Assert.Equal(2, result.Count);
    }
}
