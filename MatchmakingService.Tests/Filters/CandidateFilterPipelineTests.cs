using MatchmakingService.Data;
using MatchmakingService.Filters;
using MatchmakingService.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace MatchmakingService.Tests.Filters;

public class CandidateFilterPipelineTests : FilterTestBase, IDisposable
{
    private readonly Mock<ILogger<CandidateFilterPipeline>> _loggerMock = new();
    private readonly MatchmakingDbContext _dbContext;

    public CandidateFilterPipelineTests()
    {
        var options = new DbContextOptionsBuilder<MatchmakingDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new MatchmakingDbContext(options);
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    private CandidateFilterPipeline CreatePipeline(params ICandidateFilter[] filters)
    {
        return new CandidateFilterPipeline(filters, _loggerMock.Object);
    }

    private async Task<IQueryable<UserProfile>> SeedAndQuery(params UserProfile[] users)
    {
        _dbContext.UserProfiles.AddRange(users);
        await _dbContext.SaveChangesAsync();
        return _dbContext.UserProfiles.AsQueryable();
    }

    [Fact]
    public async Task FullPipeline_ProducesCorrectCandidates()
    {
        // Male user in Stockholm, 28yo, seeking female 25-35, 50km radius
        var user = CreateUser(userId: 1, gender: "Male", preferredGender: "Female",
                    age: 28, minAge: 25, maxAge: 35, lat: 59.33, lon: 18.07, maxDistance: 50);

        var query = await SeedAndQuery(
            // ✓ Perfect match: female, 26, nearby, seeks male, active, not swiped
            CreateUser(userId: 2, gender: "Female", preferredGender: "Male",
                age: 26, minAge: 25, maxAge: 35, lat: 59.34, lon: 18.08, isActive: true),
            // ✗ Wrong gender
            CreateUser(userId: 3, gender: "Male", preferredGender: "Female",
                age: 27, minAge: 25, maxAge: 35, lat: 59.34, lon: 18.08),
            // ✗ Inactive
            CreateUser(userId: 4, gender: "Female", preferredGender: "Male",
                age: 27, minAge: 25, maxAge: 35, lat: 59.34, lon: 18.08, isActive: false),
            // ✗ Too far (Malmö)
            CreateUser(userId: 5, gender: "Female", preferredGender: "Male",
                age: 27, minAge: 25, maxAge: 35, lat: 55.60, lon: 13.00),
            // ✗ Already swiped
            CreateUser(userId: 6, gender: "Female", preferredGender: "Male",
                age: 27, minAge: 25, maxAge: 35, lat: 59.34, lon: 18.08),
            // Extra candidate (not a self-match scenario since user isn't in DB)
            CreateUser(userId: 7, gender: "Female", preferredGender: "Male",
                age: 45, minAge: 25, maxAge: 35, lat: 59.34, lon: 18.08)
        );

        var swipedIds = new HashSet<int> { 6 };
        var context = CreateContext(user, swipedIds: swipedIds);

        var pipeline = CreatePipeline(
            new SelfExclusionFilter(),
            new ActiveUserFilter(),
            new GenderFilter(),
            new AgeRangeFilter(),
            new ExcludeSwipedFilter(),
            new ExcludeBlockedFilter(),
            new DistanceFilter());

        var result = await pipeline.ExecuteAsync(query, context, 20);

        Assert.Single(result.Candidates);
        Assert.Equal(2, result.Candidates[0].UserId);
        Assert.Equal(7, result.Metrics.Count);
    }

    [Fact]
    public async Task NoFilters_ReturnsAllCandidates()
    {
        var user = CreateUser(userId: 1);
        var query = await SeedAndQuery(
            CreateUser(userId: 2),
            CreateUser(userId: 3)
        );

        var pipeline = CreatePipeline(); // no filters
        var result = await pipeline.ExecuteAsync(query, CreateContext(user), 20);

        Assert.Equal(2, result.Candidates.Count);
        Assert.Empty(result.Metrics);
    }

    [Fact]
    public async Task LimitRespected()
    {
        var user = CreateUser(userId: 1, preferredGender: "Everyone");
        var users = Enumerable.Range(2, 50)
            .Select(i => CreateUser(userId: i, preferredGender: "Everyone"))
            .ToArray();
        var query = await SeedAndQuery(users);

        var pipeline = CreatePipeline(new SelfExclusionFilter());
        var result = await pipeline.ExecuteAsync(query, CreateContext(user), 5);

        Assert.Equal(5, result.Candidates.Count);
    }

    [Fact]
    public async Task ConflictingFilters_ReturnsEmpty()
    {
        // User wants female, but all candidates are male
        var user = CreateUser(userId: 1, gender: "Male", preferredGender: "Female");
        var query = await SeedAndQuery(
            CreateUser(userId: 2, gender: "Male", preferredGender: "Female"),
            CreateUser(userId: 3, gender: "Male", preferredGender: "Female")
        );

        var pipeline = CreatePipeline(new GenderFilter());
        var result = await pipeline.ExecuteAsync(query, CreateContext(user), 20);

        Assert.Empty(result.Candidates);
    }

    [Fact]
    public async Task EmptyTable_ReturnsEmpty()
    {
        var user = CreateUser(userId: 1);
        var query = _dbContext.UserProfiles.AsQueryable(); // empty table

        var pipeline = CreatePipeline(
            new SelfExclusionFilter(),
            new ActiveUserFilter(),
            new GenderFilter());

        var result = await pipeline.ExecuteAsync(query, CreateContext(user), 20);

        Assert.Empty(result.Candidates);
    }

    [Fact]
    public async Task FiltersExecuteInOrder()
    {
        var user = CreateUser(userId: 1);
        var query = await SeedAndQuery(CreateUser(userId: 2));

        var pipeline = CreatePipeline(
            new DistanceFilter(),      // Order 60 — should be sorted to last
            new SelfExclusionFilter(), // Order 0 — should be sorted to first
            new ActiveUserFilter());   // Order 10 — should be sorted to second

        var result = await pipeline.ExecuteAsync(query, CreateContext(user), 20);

        Assert.Equal("SelfExclusion", result.Metrics[0].FilterName);
        Assert.Equal("ActiveUser", result.Metrics[1].FilterName);
        Assert.Equal("Distance", result.Metrics[2].FilterName);
    }
}
