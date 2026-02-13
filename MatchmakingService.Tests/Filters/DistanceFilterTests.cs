using MatchmakingService.Filters;

namespace MatchmakingService.Tests.Filters;

public class DistanceFilterTests : FilterTestBase
{
    private readonly DistanceFilter _filter = new();

    [Fact]
    public void WithinRadius_Included()
    {
        // Stockholm user, 50km radius
        var user = CreateUser(userId: 1, lat: 59.33, lon: 18.07, maxDistance: 50);
        var candidates = CreateCandidates(
            CreateUser(userId: 2, lat: 59.35, lon: 18.10)); // ~2km away
        var result = _filter.Apply(candidates, CreateContext(user)).ToList();
        Assert.Single(result);
    }

    [Fact]
    public void OutsideRadius_Excluded()
    {
        // Stockholm user, 50km radius
        var user = CreateUser(userId: 1, lat: 59.33, lon: 18.07, maxDistance: 50);
        var candidates = CreateCandidates(
            CreateUser(userId: 2, lat: 55.60, lon: 13.00)); // Malmö, ~500km away
        var result = _filter.Apply(candidates, CreateContext(user)).ToList();
        Assert.Empty(result);
    }

    [Fact]
    public void SameCity_ZeroDistance_Included()
    {
        var user = CreateUser(userId: 1, lat: 59.33, lon: 18.07, maxDistance: 10);
        var candidates = CreateCandidates(
            CreateUser(userId: 2, lat: 59.33, lon: 18.07)); // exact same location
        var result = _filter.Apply(candidates, CreateContext(user)).ToList();
        Assert.Single(result);
    }

    [Fact]
    public void MaxDistanceZero_ReturnsAll()
    {
        var user = CreateUser(userId: 1, lat: 59.33, lon: 18.07, maxDistance: 0);
        var candidates = CreateCandidates(
            CreateUser(userId: 2, lat: 55.60, lon: 13.00));
        var result = _filter.Apply(candidates, CreateContext(user)).ToList();
        Assert.Single(result); // maxDistance 0 → no filtering
    }

    [Fact]
    public void MixedDistances_FiltersCorrectly()
    {
        var user = CreateUser(userId: 1, lat: 59.33, lon: 18.07, maxDistance: 100);
        var candidates = CreateCandidates(
            CreateUser(userId: 2, lat: 59.85, lon: 17.63), // Uppsala ~60km ✓
            CreateUser(userId: 3, lat: 55.60, lon: 13.00), // Malmö ~500km ✗
            CreateUser(userId: 4, lat: 59.27, lon: 18.03)); // nearby ~7km ✓
        var result = _filter.Apply(candidates, CreateContext(user)).ToList();
        Assert.Equal(2, result.Count);
    }
}
