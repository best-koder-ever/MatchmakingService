using MatchmakingService.Data;
using MatchmakingService.Models;
using MatchmakingService.Strategies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace MatchmakingService.Tests.Strategies;

public class StrategyResolverTests : IDisposable
{
    private readonly MatchmakingDbContext _context;
    private readonly Mock<IServiceProvider> _serviceProviderMock;
    private readonly Mock<IOptionsMonitor<CandidateOptions>> _optionsMock;
    private readonly LiveScoringStrategy _liveStrategy;
    private readonly PreComputedStrategy _preComputedStrategy;

    public StrategyResolverTests()
    {
        // InMemory DB
        var dbOptions = new DbContextOptionsBuilder<MatchmakingDbContext>()
            .UseInMemoryDatabase(databaseName: $"StrategyResolverTests_{Guid.NewGuid()}")
            .Options;
        _context = new MatchmakingDbContext(dbOptions);

        // Default options
        _optionsMock = new Mock<IOptionsMonitor<CandidateOptions>>();
        _optionsMock.Setup(x => x.CurrentValue).Returns(new CandidateOptions());

        // Create real-ish strategy objects via mock service provider
        // We just need them as concrete instances for type resolution
        _liveStrategy = CreateMockLiveStrategy();
        _preComputedStrategy = CreateMockPreComputedStrategy();

        _serviceProviderMock = new Mock<IServiceProvider>();
        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(LiveScoringStrategy)))
            .Returns(_liveStrategy);
        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(PreComputedStrategy)))
            .Returns(_preComputedStrategy);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    private StrategyResolver CreateResolver()
    {
        return new StrategyResolver(
            _serviceProviderMock.Object,
            _optionsMock.Object,
            _context,
            NullLogger<StrategyResolver>.Instance);
    }

    // --- Explicit strategy override tests ---

    [Fact]
    public void Resolve_Live_ReturnsLiveScoringStrategy()
    {
        var resolver = CreateResolver();
        var strategy = resolver.Resolve("live");
        Assert.IsType<LiveScoringStrategy>(strategy);
        Assert.Equal("Live", strategy.Name);
    }

    [Fact]
    public void Resolve_LiveCaseInsensitive_ReturnsLiveScoringStrategy()
    {
        var resolver = CreateResolver();
        var strategy = resolver.Resolve("LIVE");
        Assert.IsType<LiveScoringStrategy>(strategy);
    }

    [Fact]
    public void Resolve_Precomputed_ReturnsPreComputedStrategy()
    {
        var resolver = CreateResolver();
        var strategy = resolver.Resolve("precomputed");
        Assert.IsType<PreComputedStrategy>(strategy);
        Assert.Equal("PreComputed", strategy.Name);
    }

    [Fact]
    public void Resolve_PrecomputedCaseInsensitive_ReturnsPreComputedStrategy()
    {
        var resolver = CreateResolver();
        var strategy = resolver.Resolve("PRECOMPUTED");
        Assert.IsType<PreComputedStrategy>(strategy);
    }

    [Fact]
    public void Resolve_UnknownStrategy_FallsBackToLive()
    {
        var resolver = CreateResolver();
        var strategy = resolver.Resolve("quantum");
        Assert.IsType<LiveScoringStrategy>(strategy);
    }

    // --- Auto strategy tests ---

    [Fact]
    public void Resolve_Auto_FewUsers_ReturnsLive()
    {
        // Fewer users than LiveMaxUsers threshold → Live
        SeedActiveUsers(50);
        var resolver = CreateResolver();
        var strategy = resolver.Resolve("auto");
        Assert.IsType<LiveScoringStrategy>(strategy);
    }

    [Fact]
    public void Resolve_Auto_ManyUsers_ReturnsPreComputed()
    {
        // Set threshold low so our seeded users exceed it
        _optionsMock.Setup(x => x.CurrentValue).Returns(new CandidateOptions
        {
            Strategy = "Auto",
            AutoStrategyThresholds = new MatchmakingService.Models.AutoStrategyThresholdsOptions { LiveMaxUsers = 5 }
        });

        SeedActiveUsers(10);
        var resolver = CreateResolver();
        var strategy = resolver.Resolve("auto");
        Assert.IsType<PreComputedStrategy>(strategy);
    }

    [Fact]
    public void Resolve_Auto_ExactlyAtThreshold_ReturnsLive()
    {
        _optionsMock.Setup(x => x.CurrentValue).Returns(new CandidateOptions
        {
            Strategy = "Auto",
            AutoStrategyThresholds = new MatchmakingService.Models.AutoStrategyThresholdsOptions { LiveMaxUsers = 10 }
        });

        SeedActiveUsers(10);
        var resolver = CreateResolver();
        var strategy = resolver.Resolve("auto");
        Assert.IsType<LiveScoringStrategy>(strategy);
    }

    [Fact]
    public void Resolve_Auto_OneOverThreshold_ReturnsPreComputed()
    {
        _optionsMock.Setup(x => x.CurrentValue).Returns(new CandidateOptions
        {
            Strategy = "Auto",
            AutoStrategyThresholds = new MatchmakingService.Models.AutoStrategyThresholdsOptions { LiveMaxUsers = 10 }
        });

        SeedActiveUsers(11);
        var resolver = CreateResolver();
        var strategy = resolver.Resolve("auto");
        Assert.IsType<PreComputedStrategy>(strategy);
    }

    [Fact]
    public void Resolve_Auto_OnlyCountsActiveUsers()
    {
        _optionsMock.Setup(x => x.CurrentValue).Returns(new CandidateOptions
        {
            Strategy = "Auto",
            AutoStrategyThresholds = new MatchmakingService.Models.AutoStrategyThresholdsOptions { LiveMaxUsers = 5 }
        });

        // Seed 10 users but only 3 active
        for (int i = 1; i <= 10; i++)
        {
            _context.UserProfiles.Add(new UserProfile
            {
                UserId = i,
                IsActive = i <= 3, // only first 3 active
                Gender = "Male",
                Age = 25,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                LastActiveAt = DateTime.UtcNow
            });
        }
        _context.SaveChanges();

        var resolver = CreateResolver();
        var strategy = resolver.Resolve("auto");
        Assert.IsType<LiveScoringStrategy>(strategy); // 3 ≤ 5 → Live
    }

    // --- Config default strategy tests ---

    [Fact]
    public void Resolve_NullOverride_UsesConfigStrategy()
    {
        _optionsMock.Setup(x => x.CurrentValue).Returns(new CandidateOptions
        {
            Strategy = "precomputed"
        });

        var resolver = CreateResolver();
        var strategy = resolver.Resolve(null);
        Assert.IsType<PreComputedStrategy>(strategy);
    }

    [Fact]
    public void Resolve_NullOverride_DefaultConfigIsAuto_ResolvesAuto()
    {
        // Default CandidateOptions.Strategy is "Auto"
        SeedActiveUsers(3);
        var resolver = CreateResolver();
        var strategy = resolver.Resolve(null);
        Assert.IsType<LiveScoringStrategy>(strategy);
    }

    // --- Error handling tests ---

    [Fact]
    public void Resolve_ServiceProviderThrows_FallsBackToLive()
    {
        // Make precomputed resolution throw
        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(PreComputedStrategy)))
            .Throws(new InvalidOperationException("DI error"));

        var resolver = CreateResolver();
        var strategy = resolver.Resolve("precomputed");
        // Should still get LiveScoringStrategy as fallback via catch
        Assert.IsType<LiveScoringStrategy>(strategy);
    }

    // --- Helper methods ---

    private void SeedActiveUsers(int count)
    {
        for (int i = 1; i <= count; i++)
        {
            _context.UserProfiles.Add(new UserProfile
            {
                UserId = 10000 + i,
                IsActive = true,
                Gender = "Male",
                Age = 25,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                LastActiveAt = DateTime.UtcNow
            });
        }
        _context.SaveChanges();
    }

    private LiveScoringStrategy CreateMockLiveStrategy()
    {
        var filterPipeline = new MatchmakingService.Filters.CandidateFilterPipeline(
            Enumerable.Empty<MatchmakingService.Filters.ICandidateFilter>(),
            NullLogger<MatchmakingService.Filters.CandidateFilterPipeline>.Instance);
        var matchingService = new Mock<MatchmakingService.Services.IAdvancedMatchingService>();
        var swipeClient = new Mock<MatchmakingService.Services.ISwipeServiceClient>();
        var safetyClient = new Mock<MatchmakingService.Services.ISafetyServiceClient>();
        var scoringConfig = new Mock<IOptionsMonitor<ScoringConfiguration>>();
        scoringConfig.Setup(x => x.CurrentValue).Returns(new ScoringConfiguration());

        return new LiveScoringStrategy(
            _context,
            filterPipeline,
            matchingService.Object,
            swipeClient.Object,
            safetyClient.Object,
            _optionsMock.Object,
            scoringConfig.Object,
            NullLogger<LiveScoringStrategy>.Instance);
    }

    private PreComputedStrategy CreateMockPreComputedStrategy()
    {
        var filterPipeline = new MatchmakingService.Filters.CandidateFilterPipeline(
            Enumerable.Empty<MatchmakingService.Filters.ICandidateFilter>(),
            NullLogger<MatchmakingService.Filters.CandidateFilterPipeline>.Instance);
        var swipeClient = new Mock<MatchmakingService.Services.ISwipeServiceClient>();
        var safetyClient = new Mock<MatchmakingService.Services.ISafetyServiceClient>();
        var scoringConfig = new Mock<IOptionsMonitor<ScoringConfiguration>>();
        scoringConfig.Setup(x => x.CurrentValue).Returns(new ScoringConfiguration());

        return new PreComputedStrategy(
            _context,
            filterPipeline,
            CreateMockLiveStrategy(),
            swipeClient.Object,
            safetyClient.Object,
            _optionsMock.Object,
            scoringConfig.Object,
            NullLogger<PreComputedStrategy>.Instance);
    }
}
