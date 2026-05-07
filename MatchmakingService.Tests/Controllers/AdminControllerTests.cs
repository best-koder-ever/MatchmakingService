using MatchmakingService.Controllers;
using MatchmakingService.Data;
using MatchmakingService.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using ModelMatch = MatchmakingService.Models.Match;

namespace MatchmakingService.Tests.Controllers;

public class AdminControllerTests : IDisposable
{
    private readonly MatchmakingDbContext _context;

    public AdminControllerTests()
    {
        var options = new DbContextOptionsBuilder<MatchmakingDbContext>()
            .UseInMemoryDatabase(databaseName: $"AdminReset_{Guid.NewGuid()}")
            .Options;
        _context = new MatchmakingDbContext(options);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    private AdminController BuildController(string envName = "Development")
    {
        var env = new Mock<IWebHostEnvironment>();
        env.SetupGet(e => e.EnvironmentName).Returns(envName);
        var logger = Mock.Of<ILogger<AdminController>>();
        var ctrl = new AdminController(_context, env.Object, logger);
        ctrl.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext()
        };
        return ctrl;
    }

    [Fact]
    public async Task ResetAllMatches_WipesEverythingInDev()
    {
        _context.Matches.Add(new ModelMatch { User1Id = 1, User2Id = 2, IsActive = true, CreatedAt = DateTime.UtcNow });
        _context.MatchScores.Add(new MatchScore { UserId = 1, TargetUserId = 2, OverallScore = 50.0, CalculatedAt = DateTime.UtcNow });
        _context.UserInteractions.Add(new UserInteraction { UserId = 1, TargetUserId = 2, InteractionType = "like", CreatedAt = DateTime.UtcNow });
        await _context.SaveChangesAsync();

        var ctrl = BuildController("Development");
        var result = await ctrl.ResetAllMatches();

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(0, await _context.Matches.CountAsync());
        Assert.Equal(0, await _context.MatchScores.CountAsync());
        Assert.Equal(0, await _context.UserInteractions.CountAsync());
    }

    [Fact]
    public async Task ResetAllMatches_RejectsInProduction()
    {
        var ctrl = BuildController("Production");
        var result = await ctrl.ResetAllMatches();

        var status = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, status.StatusCode);
    }

    [Fact]
    public async Task ResetAllMatches_AllowsStagingAndDemo()
    {
        foreach (var envName in new[] { "Staging", "Demo" })
        {
            var ctrl = BuildController(envName);
            var result = await ctrl.ResetAllMatches();
            Assert.IsType<OkObjectResult>(result);
        }
    }
}
